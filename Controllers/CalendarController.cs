using COMAVI_SA.Data;
using COMAVI_SA.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace COMAVI_SA.Controllers
{
    [Authorize(Roles = "admin,user")]
    public class CalendarController : Controller
    {
        private readonly ComaviDbContext _context;
        private readonly ILogger<CalendarController> _logger;

        public CalendarController(
            ComaviDbContext context,
            ILogger<CalendarController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            try
            {
                int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

                // Obtener datos del chofer
                var chofer = await _context.Choferes
                    .FirstOrDefaultAsync(c => c.id_usuario == userId);

                if (chofer == null)
                {
                    TempData["Error"] = "Debe completar su perfil primero para acceder al calendario.";
                    return RedirectToAction("Profile", "Login");
                }

                // Obtener eventos de vencimiento
                var eventos = new List<EventoCalendario>();

                // Licencia
                eventos.Add(new EventoCalendario
                {
                    id = "lic-" + chofer.id_chofer,
                    title = "Vencimiento de Licencia",
                    start = chofer.fecha_venc_licencia.ToString("yyyy-MM-dd"),
                    className = GetEventClass(chofer.fecha_venc_licencia),
                    description = $"Licencia N° {chofer.licencia}"
                });

                // Documentos
                var documentos = await _context.Documentos
                    .Where(d => d.id_chofer == chofer.id_chofer)
                    .ToListAsync();

                foreach (var doc in documentos)
                {
                    eventos.Add(new EventoCalendario
                    {
                        id = "doc-" + doc.id_documento,
                        title = $"Vencimiento de {doc.tipo_documento}",
                        start = doc.fecha_vencimiento.ToString("yyyy-MM-dd"),
                        className = GetEventClass(doc.fecha_vencimiento),
                        description = $"Documento emitido el {doc.fecha_emision.ToString("dd/MM/yyyy")}"
                    });
                }

                // Mantenimientos de camión (si tiene asignado)
                var camion = await _context.Camiones
                    .FirstOrDefaultAsync(c => c.chofer_asignado == chofer.id_chofer);

                if (camion != null)
                {
                    var mantenimientos = await _context.Mantenimiento_Camiones
                        .Where(m => m.id_camion == camion.id_camion)
                        .ToListAsync();

                    foreach (var mant in mantenimientos)
                    {
                        eventos.Add(new EventoCalendario
                        {
                            id = "mant-" + mant.id_mantenimiento,
                            title = "Mantenimiento de Vehículo",
                            start = mant.fecha_mantenimiento.ToString("yyyy-MM-dd"),
                            className = "bg-primary",
                            description = mant.descripcion
                        });
                    }
                }

                ViewBag.Eventos = System.Text.Json.JsonSerializer.Serialize(eventos);
                ViewBag.NombreConductor = chofer.nombreCompleto;

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar la página de calendario");
                TempData["Error"] = "Error al cargar el calendario.";
                return RedirectToAction("Profile", "Login");
            }
        }

        [HttpGet]
        public async Task<IActionResult> EventosJson()
        {
            try
            {
                int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

                // Obtener datos del chofer
                var chofer = await _context.Choferes
                    .FirstOrDefaultAsync(c => c.id_usuario == userId);

                if (chofer == null)
                {
                    return Json(new List<EventoCalendario>());
                }

                // Obtener eventos de vencimiento
                var eventos = new List<EventoCalendario>();

                // Licencia
                eventos.Add(new EventoCalendario
                {
                    id = "lic-" + chofer.id_chofer,
                    title = "Vencimiento de Licencia",
                    start = chofer.fecha_venc_licencia.ToString("yyyy-MM-dd"),
                    className = GetEventClass(chofer.fecha_venc_licencia),
                    description = $"Licencia N° {chofer.licencia}"
                });

                // Documentos
                var documentos = await _context.Documentos
                    .Where(d => d.id_chofer == chofer.id_chofer)
                    .ToListAsync();

                foreach (var doc in documentos)
                {
                    eventos.Add(new EventoCalendario
                    {
                        id = "doc-" + doc.id_documento,
                        title = $"Vencimiento de {doc.tipo_documento}",
                        start = doc.fecha_vencimiento.ToString("yyyy-MM-dd"),
                        className = GetEventClass(doc.fecha_vencimiento),
                        description = $"Documento emitido el {doc.fecha_emision.ToString("dd/MM/yyyy")}"
                    });
                }

                // Mantenimientos de camión (si tiene asignado)
                var camion = await _context.Camiones
                    .FirstOrDefaultAsync(c => c.chofer_asignado == chofer.id_chofer);

                if (camion != null)
                {
                    var mantenimientos = await _context.Mantenimiento_Camiones
                        .Where(m => m.id_camion == camion.id_camion)
                        .ToListAsync();

                    foreach (var mant in mantenimientos)
                    {
                        eventos.Add(new EventoCalendario
                        {
                            id = "mant-" + mant.id_mantenimiento,
                            title = "Mantenimiento de Vehículo",
                            start = mant.fecha_mantenimiento.ToString("yyyy-MM-dd"),
                            className = "bg-primary",
                            description = mant.descripcion
                        });
                    }
                }

                return Json(eventos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener eventos del calendario");
                return Json(new { error = "Error al obtener eventos del calendario" });
            }
        }

        private string GetEventClass(DateTime fechaVencimiento)
        {
            int diasParaVencimiento = (int)(fechaVencimiento - DateTime.Now).TotalDays;

            if (diasParaVencimiento <= 0)
                return "bg-danger"; // Vencido
            else if (diasParaVencimiento <= 30)
                return "bg-warning"; // Próximo a vencer
            else
                return "bg-success"; // Vigente
        }
    }
}