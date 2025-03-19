using COMAVI_SA.Data;
using COMAVI_SA.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace COMAVI_SA.Services
{
    public class AgendaNotificationService
    {
        private readonly ComaviDbContext _context;
        private readonly IEmailService _emailService;
        private readonly ILogger<AgendaNotificationService> _logger;

        public AgendaNotificationService(
            ComaviDbContext context,
            IEmailService emailService,
            ILogger<AgendaNotificationService> logger)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task EnviarNotificacionesAgendaAsync()
        {
            try
            {
                // Obtener fecha actual
                var hoy = DateTime.Now.Date;

                // Buscar eventos que requieren notificación
                var eventosParaNotificar = await _context.EventosAgenda
                    .Include(e => e.Usuario)
                    .Where(e => e.requiere_notificacion
                              && !e.notificacion_enviada
                              && e.estado != "Cancelado"
                              && e.fecha_inicio.Date >= hoy)
                    .ToListAsync();

                foreach (var evento in eventosParaNotificar)
                {
                    int diasAnticipacion = evento.dias_anticipacion_notificacion ?? 3;
                    var fechaNotificacion = evento.fecha_inicio.AddDays(-diasAnticipacion).Date;

                    // Si hoy es el día para notificar
                    if (hoy >= fechaNotificacion)
                    {
                        // Enviar correo
                        if (evento.Usuario != null && !string.IsNullOrEmpty(evento.Usuario.correo_electronico))
                        {
                            await EnviarCorreoNotificacionAsync(evento);
                        }

                        // Crear notificación en sistema
                        await CrearNotificacionSistemaAsync(evento);

                        // Marcar como notificado
                        evento.notificacion_enviada = true;
                    }
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar notificaciones de agenda");
            }
        }

        private async Task EnviarCorreoNotificacionAsync(EventoAgenda evento)
        {
            try
            {
                string diasFaltantes = (evento.fecha_inicio.Date - DateTime.Now.Date).Days.ToString();
                string asunto = $"Recordatorio: {evento.titulo} - {diasFaltantes} días";

                var emailBody = $@"
            <h2>Recordatorio de Agenda - Sistema COMAVI</h2>
            <p>Estimado/a {evento.Usuario.nombre_usuario},</p>
            <p>Le recordamos que tiene un evento programado en {diasFaltantes} días:</p>
            <p><strong>Título:</strong> {evento.titulo}</p>
            <p><strong>Fecha:</strong> {evento.fecha_inicio.ToString("dd/MM/yyyy HH:mm")}</p>
            <p><strong>Tipo:</strong> {evento.tipo_evento}</p>
            <p><strong>Descripción:</strong> {evento.descripcion}</p>
            <p>Puede acceder al sistema para ver más detalles o actualizar este evento.</p>
            <p>Atentamente,<br>Sistema COMAVI</p>";

                await _emailService.SendEmailAsync(
                    evento.Usuario.correo_electronico,
                    asunto,
                    emailBody);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar correo de notificación de agenda");
            }
        }

        private async Task CrearNotificacionSistemaAsync(EventoAgenda evento)
        {
            try
            {
                var diasFaltantes = (evento.fecha_inicio.Date - DateTime.Now.Date).Days;
                string mensaje = $"Recordatorio: '{evento.titulo}' programado para {evento.fecha_inicio.ToString("dd/MM/yyyy HH:mm")} ({diasFaltantes} días)";

                var notificacion = new Notificaciones_Usuario
                {
                    id_usuario = evento.id_usuario,
                    tipo_notificacion = "Recordatorio Agenda",
                    fecha_hora = DateTime.Now,
                    mensaje = mensaje,
                    leida = false
                };

                _context.NotificacionesUsuario.Add(notificacion);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear notificación de sistema para agenda");
            }
        }
    }
}
