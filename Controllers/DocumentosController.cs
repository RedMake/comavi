using COMAVI_SA.Data;
using COMAVI_SA.Models;
using COMAVI_SA.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.IO;

namespace COMAVI_SA.Controllers
{
    [Authorize(Policy = "RequireAdminRole")]
    public class DocumentosController : Controller
    {
        private readonly ComaviDbContext _context;
        private readonly IPdfService _pdfService;
        private readonly IEmailService _emailService;
        private readonly ILogger<DocumentosController> _logger;
        private readonly string _connectionString;

        public DocumentosController(
            ComaviDbContext context,
            IPdfService pdfService,
            IEmailService emailService,
            IConfiguration configuration,
            ILogger<DocumentosController> logger)
        {
            _context = context;
            _pdfService = pdfService;
            _emailService = emailService;
            _logger = logger;
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        private IDbConnection CreateConnection()
        {
            var connection = new SqlConnection(_connectionString);
            connection.Open();
            return connection;
        }

        // Lista de documentos pendientes de validación
        [HttpGet]
        public async Task<IActionResult> PendientesValidacion()
        {
            try
            {
                var documentosPendientes = await _context.Documentos
                    .Include(d => d.Chofer)
                    .Where(d => d.estado_validacion == "pendiente")
                    .OrderByDescending(d => d.fecha_emision)
                    .ToListAsync();

                return View(documentosPendientes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar documentos pendientes de validación");
                TempData["Error"] = "Error al cargar los documentos pendientes de validación";
                return RedirectToAction("Index", "Admin");
            }
        }

        // Ver documento PDF
        [HttpGet]
        public async Task<IActionResult> VerDocumento(int id)
        {
            try
            {
                var documento = await _context.Documentos
                    .Include(d => d.Chofer)
                    .FirstOrDefaultAsync(d => d.id_documento == id);

                if (documento == null)
                {
                    TempData["Error"] = "Documento no encontrado";
                    return RedirectToAction("PendientesValidacion");
                }

                // Verificar si el archivo existe
                if (string.IsNullOrEmpty(documento.ruta_archivo) || !System.IO.File.Exists(documento.ruta_archivo))
                {
                    TempData["Error"] = "El archivo no existe o no se encuentra disponible";
                    return RedirectToAction("PendientesValidacion");
                }

                // Leer el archivo PDF
                var fileBytes = await System.IO.File.ReadAllBytesAsync(documento.ruta_archivo);

                return File(fileBytes, documento.tipo_mime ?? "application/pdf", Path.GetFileName(documento.ruta_archivo));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al ver documento PDF");
                TempData["Error"] = "Error al abrir el documento";
                return RedirectToAction("PendientesValidacion");
            }
        }

        // Validar documento
        [HttpPost]
        public async Task<IActionResult> ValidarDocumento(int id)
        {
            try
            {
                var documento = await _context.Documentos
                    .Include(d => d.Chofer)
                    .FirstOrDefaultAsync(d => d.id_documento == id);

                if (documento == null)
                {
                    TempData["Error"] = "Documento no encontrado";
                    return RedirectToAction("PendientesValidacion");
                }

                // Actualizar estado a verificado
                documento.estado_validacion = "verificado";
                await _context.SaveChangesAsync();

                // Enviar notificación al chofer (obtener usuario asociado a chofer)
                var usuario = await _context.Usuarios
                    .Where(u => u.correo_electronico.StartsWith(documento.Chofer.numero_cedula) && u.rol == "user")
                    .FirstOrDefaultAsync();

                if (usuario != null)
                {
                    // Agregar notificación en el sistema
                    var notificacion = new Notificaciones_Usuario
                    {
                        id_usuario = usuario.id_usuario,
                        tipo_notificacion = "Documento Verificado",
                        fecha_hora = DateTime.Now,
                        mensaje = $"Su documento {documento.tipo_documento} ha sido verificado exitosamente."
                    };

                    _context.NotificacionesUsuario.Add(notificacion);
                    await _context.SaveChangesAsync();

                    // Enviar correo electrónico
                    var emailBody = $@"
                    <h2>Documento Verificado - Sistema COMAVI</h2>
                    <p>Estimado/a {documento.Chofer.nombreCompleto},</p>
                    <p>Nos complace informarle que su documento '{documento.tipo_documento}' ha sido verificado exitosamente.</p>
                    <p>Detalles del documento:</p>
                    <ul>
                        <li><strong>Tipo:</strong> {documento.tipo_documento}</li>
                        <li><strong>Fecha de emisión:</strong> {documento.fecha_emision:dd/MM/yyyy}</li>
                        <li><strong>Fecha de vencimiento:</strong> {documento.fecha_vencimiento:dd/MM/yyyy}</li>
                    </ul>
                    <p>Gracias por mantener actualizada su documentación.</p>
                    <p>Atentamente,<br>Equipo COMAVI</p>";

                    await _emailService.SendEmailAsync(
                        usuario.correo_electronico,
                        "Documento Verificado - Sistema COMAVI",
                        emailBody);
                }

                TempData["Success"] = "Documento verificado exitosamente";
                return RedirectToAction("PendientesValidacion");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al validar documento");
                TempData["Error"] = "Error al validar el documento";
                return RedirectToAction("PendientesValidacion");
            }
        }

        // Rechazar documento
        [HttpPost]
        public async Task<IActionResult> RechazarDocumento(int id, string motivoRechazo)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(motivoRechazo))
                {
                    TempData["Error"] = "Debe proporcionar un motivo de rechazo";
                    return RedirectToAction("PendientesValidacion");
                }

                var documento = await _context.Documentos
                    .Include(d => d.Chofer)
                    .FirstOrDefaultAsync(d => d.id_documento == id);

                if (documento == null)
                {
                    TempData["Error"] = "Documento no encontrado";
                    return RedirectToAction("PendientesValidacion");
                }

                // Actualizar estado a rechazado
                documento.estado_validacion = "rechazado";
                await _context.SaveChangesAsync();

                // Enviar notificación al chofer (obtener usuario asociado a chofer)
                var usuario = await _context.Usuarios
                    .Where(u => u.correo_electronico.StartsWith(documento.Chofer.numero_cedula) && u.rol == "user")
                    .FirstOrDefaultAsync();

                if (usuario != null)
                {
                    // Agregar notificación en el sistema
                    var notificacion = new Notificaciones_Usuario
                    {
                        id_usuario = usuario.id_usuario,
                        tipo_notificacion = "Documento Rechazado",
                        fecha_hora = DateTime.Now,
                        mensaje = $"Su documento {documento.tipo_documento} ha sido rechazado. Motivo: {motivoRechazo}"
                    };

                    _context.NotificacionesUsuario.Add(notificacion);
                    await _context.SaveChangesAsync();

                    // Enviar correo electrónico
                    var emailBody = $@"
                    <h2>Documento Rechazado - Sistema COMAVI</h2>
                    <p>Estimado/a {documento.Chofer.nombreCompleto},</p>
                    <p>Lamentamos informarle que su documento '{documento.tipo_documento}' ha sido rechazado.</p>
                    <p><strong>Motivo del rechazo:</strong> {motivoRechazo}</p>
                    <p>Detalles del documento:</p>
                    <ul>
                        <li><strong>Tipo:</strong> {documento.tipo_documento}</li>
                        <li><strong>Fecha de emisión:</strong> {documento.fecha_emision:dd/MM/yyyy}</li>
                        <li><strong>Fecha de vencimiento:</strong> {documento.fecha_vencimiento:dd/MM/yyyy}</li>
                    </ul>
                    <p>Por favor, suba un nuevo documento que cumpla con los requisitos necesarios.</p>
                    <p>Atentamente,<br>Equipo COMAVI</p>";

                    await _emailService.SendEmailAsync(
                        usuario.correo_electronico,
                        "Documento Rechazado - Sistema COMAVI",
                        emailBody);
                }

                TempData["Success"] = "Documento rechazado exitosamente";
                return RedirectToAction("PendientesValidacion");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al rechazar documento");
                TempData["Error"] = "Error al rechazar el documento";
                return RedirectToAction("PendientesValidacion");
            }
        }

        // Documentos por vencer
        [HttpGet]
        public async Task<IActionResult> DocumentosPorVencer(int diasAnticipacion = 30)
        {
            try
            {
                var fechaLimite = DateTime.Now.AddDays(diasAnticipacion);
                var documentosPorVencer = await _context.Documentos
                    .Include(d => d.Chofer)
                    .Where(d => d.estado_validacion == "verificado" && d.fecha_vencimiento <= fechaLimite)
                    .OrderBy(d => d.fecha_vencimiento)
                    .ToListAsync();

                ViewBag.DiasAnticipacion = diasAnticipacion;
                return View(documentosPorVencer);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar documentos por vencer");
                TempData["Error"] = "Error al cargar los documentos por vencer";
                return RedirectToAction("Index", "Admin");
            }
        }

        // Enviar recordatorio de vencimiento
        [HttpPost]
        public async Task<IActionResult> EnviarRecordatorio(int id)
        {
            try
            {
                var documento = await _context.Documentos
                    .Include(d => d.Chofer)
                    .FirstOrDefaultAsync(d => d.id_documento == id);

                if (documento == null)
                {
                    TempData["Error"] = "Documento no encontrado";
                    return RedirectToAction("DocumentosPorVencer");
                }

                // Enviar notificación al chofer (obtener usuario asociado a chofer)
                var usuario = await _context.Usuarios
                    .Where(u => u.correo_electronico.StartsWith(documento.Chofer.numero_cedula) && u.rol == "user")
                    .FirstOrDefaultAsync();

                if (usuario != null)
                {
                    // Agregar notificación en el sistema
                    var notificacion = new Notificaciones_Usuario
                    {
                        id_usuario = usuario.id_usuario,
                        tipo_notificacion = "Recordatorio de Vencimiento",
                        fecha_hora = DateTime.Now,
                        mensaje = $"Su documento {documento.tipo_documento} vencerá el {documento.fecha_vencimiento:dd/MM/yyyy}. Por favor, actualícelo antes de la fecha de vencimiento."
                    };

                    _context.NotificacionesUsuario.Add(notificacion);
                    await _context.SaveChangesAsync();

                    // Calcular días restantes
                    var diasRestantes = (documento.fecha_vencimiento - DateTime.Now).Days;

                    // Enviar correo electrónico
                    var emailBody = $@"
                    <h2>Recordatorio de Vencimiento - Sistema COMAVI</h2>
                    <p>Estimado/a {documento.Chofer.nombreCompleto},</p>
                    <p>Le recordamos que su documento '{documento.tipo_documento}' está próximo a vencer.</p>
                    <p><strong>Fecha de vencimiento:</strong> {documento.fecha_vencimiento:dd/MM/yyyy} ({diasRestantes} días restantes)</p>
                    <p>Por favor, actualice este documento antes de la fecha de vencimiento para evitar problemas en sus asignaciones.</p>
                    <p>Puede subir un nuevo documento accediendo a su perfil en el sistema.</p>
                    <p>Atentamente,<br>Equipo COMAVI</p>";

                    await _emailService.SendEmailAsync(
                        usuario.correo_electronico,
                        "Recordatorio de Vencimiento - Sistema COMAVI",
                        emailBody);

                    TempData["Success"] = "Recordatorio enviado exitosamente";
                }
                else
                {
                    TempData["Warning"] = "No se encontró usuario asociado al chofer para enviar el recordatorio";
                }

                return RedirectToAction("DocumentosPorVencer");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar recordatorio de vencimiento");
                TempData["Error"] = "Error al enviar el recordatorio de vencimiento";
                return RedirectToAction("DocumentosPorVencer");
            }
        }

        // Histórico de documentos por chofer
        [HttpGet]
        public async Task<IActionResult> HistoricoDocumentos(int idChofer)
        {
            try
            {
                var chofer = await _context.Choferes.FindAsync(idChofer);
                if (chofer == null)
                {
                    TempData["Error"] = "Chofer no encontrado";
                    return RedirectToAction("Index", "Admin");
                }

                var documentos = await _context.Documentos
                    .Where(d => d.id_chofer == idChofer)
                    .OrderByDescending(d => d.fecha_emision)
                    .ToListAsync();

                ViewBag.Chofer = chofer;
                return View(documentos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar histórico de documentos");
                TempData["Error"] = "Error al cargar el histórico de documentos";
                return RedirectToAction("Index", "Admin");
            }
        }

        // Generar reporte de documentos
        [HttpGet]
        public async Task<IActionResult> GenerarReporteDocumentos(string estado = "todos", int diasAnticipacion = 30)
        {
            try
            {
                var fechaLimite = DateTime.Now.AddDays(diasAnticipacion);
                var query = _context.Documentos.Include(d => d.Chofer).AsQueryable();

                // Filtrar por estado
                if (estado != "todos")
                {
                    query = query.Where(d => d.estado_validacion == estado);
                }

                // Si queremos documentos por vencer
                if (estado == "porVencer")
                {
                    query = query.Where(d => d.estado_validacion == "verificado" && d.fecha_vencimiento <= fechaLimite);
                }

                var documentos = await query.OrderBy(d => d.fecha_vencimiento).ToListAsync();

                ViewBag.Estado = estado;
                ViewBag.DiasAnticipacion = diasAnticipacion;
                return View(documentos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar reporte de documentos");
                TempData["Error"] = "Error al generar el reporte de documentos";
                return RedirectToAction("Index", "Admin");
            }
        }
    }
}