using COMAVI_SA.Data;
using COMAVI_SA.Models;
using Microsoft.EntityFrameworkCore;

namespace COMAVI_SA.Services
{
    public interface INotificationService
    {
        Task SendExpirationNotificationsAsync();
        Task CreateNotificationAsync(int userId, string type, string message);
    }

    public class NotificationService : INotificationService
    {
        private readonly ComaviDbContext _context;
        private readonly IEmailService _emailService;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(
            ComaviDbContext context,
            IEmailService emailService,
            ILogger<NotificationService> logger)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task SendExpirationNotificationsAsync()
        {
            try
            {
                _logger.LogInformation("Iniciando envío de notificaciones de vencimiento");

                // Obtener todos los choferes activos con sus preferencias
                var choferes = await _context.Choferes
                    .Include(c => c.Usuario)
                    .Where(c => c.estado == "activo" && c.Usuario != null)
                    .ToListAsync();

                foreach (var chofer in choferes)
                {
                    try
                    {
                        if (chofer.Usuario == null)
                            continue;

                        // Obtener preferencias de notificación
                        var preferencias = await _context.PreferenciasNotificacion
                            .FirstOrDefaultAsync(p => p.id_usuario == chofer.Usuario.id_usuario);

                        // Si no hay preferencias, usar valores predeterminados
                        int diasAnticipacion = preferencias?.dias_anticipacion ?? 15;
                        bool notificarPorCorreo = preferencias?.notificar_por_correo ?? true;
                        bool notificarLicencia = preferencias?.notificar_vencimiento_licencia ?? true;
                        bool notificarDocumentos = preferencias?.notificar_vencimiento_documentos ?? true;

                        if (notificarLicencia)
                        {
                            await CheckLicenseExpirationAsync(chofer, diasAnticipacion, notificarPorCorreo);
                        }

                        if (notificarDocumentos)
                        {
                            await CheckDocumentsExpirationAsync(chofer, diasAnticipacion, notificarPorCorreo);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error al procesar notificaciones para el chofer {ChoferId}", chofer.id_chofer);
                    }
                }

                _logger.LogInformation("Finalizado envío de notificaciones de vencimiento");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar notificaciones de vencimiento");
            }
        }

        private async Task CheckLicenseExpirationAsync(Choferes chofer, int diasAnticipacion, bool notificarPorCorreo)
        {
            // Calcular días hasta vencimiento
            int diasParaVencimiento = (int)(chofer.fecha_venc_licencia - DateTime.Now).TotalDays;

            // Si está vencida
            if (diasParaVencimiento <= 0)
            {
                string mensaje = $"Su licencia de conducir está vencida. Licencia: {chofer.licencia}, venció el {chofer.fecha_venc_licencia.ToString("dd/MM/yyyy")}";
                await CreateNotificationAsync(chofer.Usuario.id_usuario, "Licencia Vencida", mensaje);

                if (notificarPorCorreo)
                {
                    await SendExpirationEmailAsync(
                        chofer.Usuario.correo_electronico,
                        chofer.nombreCompleto,
                        "Licencia de Conducir",
                        chofer.fecha_venc_licencia,
                        true);
                }
            }
            // Si está próxima a vencer en los días configurados
            else if (diasParaVencimiento <= diasAnticipacion)
            {
                string mensaje = $"Su licencia de conducir vencerá en {diasParaVencimiento} días. Licencia: {chofer.licencia}, vence el {chofer.fecha_venc_licencia.ToString("dd/MM/yyyy")}";
                await CreateNotificationAsync(chofer.Usuario.id_usuario, "Licencia Próxima a Vencer", mensaje);

                if (notificarPorCorreo)
                {
                    await SendExpirationEmailAsync(
                        chofer.Usuario.correo_electronico,
                        chofer.nombreCompleto,
                        "Licencia de Conducir",
                        chofer.fecha_venc_licencia,
                        false);
                }
            }
        }

        private async Task CheckDocumentsExpirationAsync(Choferes chofer, int diasAnticipacion, bool notificarPorCorreo)
        {
            // Obtener documentos activos
            var documentos = await _context.Documentos
                .Where(d => d.id_chofer == chofer.id_chofer && d.estado_validacion == "verificado")
                .ToListAsync();

            foreach (var documento in documentos)
            {
                // Calcular días hasta vencimiento
                int diasParaVencimiento = (int)(documento.fecha_vencimiento - DateTime.Now).TotalDays;

                // Si está vencido
                if (diasParaVencimiento <= 0)
                {
                    string mensaje = $"Su documento '{documento.tipo_documento}' está vencido. Venció el {documento.fecha_vencimiento.ToString("dd/MM/yyyy")}";
                    await CreateNotificationAsync(chofer.Usuario.id_usuario, "Documento Vencido", mensaje);

                    if (notificarPorCorreo)
                    {
                        await SendExpirationEmailAsync(
                            chofer.Usuario.correo_electronico,
                            chofer.nombreCompleto,
                            documento.tipo_documento,
                            documento.fecha_vencimiento,
                            true);
                    }
                }
                // Si está próximo a vencer en los días configurados
                else if (diasParaVencimiento <= diasAnticipacion)
                {
                    string mensaje = $"Su documento '{documento.tipo_documento}' vencerá en {diasParaVencimiento} días. Vence el {documento.fecha_vencimiento.ToString("dd/MM/yyyy")}";
                    await CreateNotificationAsync(chofer.Usuario.id_usuario, "Documento Próximo a Vencer", mensaje);

                    if (notificarPorCorreo)
                    {
                        await SendExpirationEmailAsync(
                            chofer.Usuario.correo_electronico,
                            chofer.nombreCompleto,
                            documento.tipo_documento,
                            documento.fecha_vencimiento,
                            false);
                    }
                }
            }
        }

        private async Task SendExpirationEmailAsync(string email, string nombre, string tipoDocumento, DateTime fechaVencimiento, bool vencido)
        {
            try
            {
                string asunto = vencido
                    ? $"ALERTA: {tipoDocumento} Vencido - COMAVI S.A."
                    : $"AVISO: {tipoDocumento} Próximo a Vencer - COMAVI S.A.";

                string estado = vencido ? "VENCIDO" : "PRÓXIMO A VENCER";
                string accion = vencido ? "debe renovar inmediatamente" : "debe gestionar la renovación";

                var emailBody = $@"
                <h2>Alerta de Vencimiento - Sistema COMAVI</h2>
                <p>Estimado/a {nombre},</p>
                <p>Le informamos que su <strong>{tipoDocumento}</strong> se encuentra <strong>{estado}</strong>.</p>
                <p>Detalles:</p>
                <ul>
                    <li><strong>Documento:</strong> {tipoDocumento}</li>
                    <li><strong>Fecha de Vencimiento:</strong> {fechaVencimiento.ToString("dd/MM/yyyy")}</li>
                    <li><strong>Estado:</strong> {estado}</li>
                </ul>
                <p>Usted {accion} este documento para continuar operando normalmente.</p>
                <p>Puede acceder al <a href='https://docktrack.lat'>Sistema COMAVI</a> para más detalles y ver el calendario de vencimientos.</p>
                <p>Atentamente,<br>Equipo COMAVI</p>";

                await _emailService.SendEmailAsync(email, asunto, emailBody);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar correo de vencimiento a {Email}", email);
            }
        }

        public async Task CreateNotificationAsync(int userId, string type, string message)
        {
            try
            {
                // Verificar si ya existe una notificación similar no leída
                var existingNotification = await _context.NotificacionesUsuario
                    .FirstOrDefaultAsync(n =>
                        n.id_usuario == userId &&
                        n.tipo_notificacion == type &&
                        n.mensaje == message &&
                        (n.leida == null || n.leida == false));

                if (existingNotification != null)
                {
                    // Actualizar la fecha para que aparezca como reciente
                    existingNotification.fecha_hora = DateTime.Now;
                }
                else
                {
                    // Crear nueva notificación
                    var notificacion = new Notificaciones_Usuario
                    {
                        id_usuario = userId,
                        tipo_notificacion = type,
                        mensaje = message,
                        fecha_hora = DateTime.Now,
                        leida = false
                    };

                    _context.NotificacionesUsuario.Add(notificacion);
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear notificación para el usuario {UserId}", userId);
            }
        }
    }
}