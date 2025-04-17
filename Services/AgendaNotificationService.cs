using COMAVI_SA.Data;
using COMAVI_SA.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Reflection;

namespace COMAVI_SA.Services
{
#pragma warning disable CS0168
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

    public class AgendaNotificationService
    {
        private readonly ComaviDbContext _context;
        private readonly IEmailService _emailService;
        private readonly IEmailTemplatingService _emailTemplatingService;

        public AgendaNotificationService(
            ComaviDbContext context,
            IEmailService emailService,
            IEmailTemplatingService emailTemplatingService)
        {
            _context = context;
            _emailService = emailService;
            _emailTemplatingService = emailTemplatingService;

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
                throw;
            }
        }

        public async Task EnviarCorreoNotificacionAsync(EventoAgenda evento)
        {
            try
            {
                string diasFaltantes = (evento.fecha_inicio.Date - DateTime.Now.Date).Days.ToString();
                if (int.TryParse(diasFaltantes, out int diasInt) && diasInt < 0)
                {
                    diasFaltantes = "0";
                }

                string asunto = $"Recordatorio: {evento.titulo} - Faltan {diasFaltantes} día(s)";
                string templateFileName = "RecordatorioComaviProfesional.html";

                var data = new Dictionary<string, string>
                {
                    { "NombreUsuario", evento.Usuario?.nombre_usuario ?? "Usuario" },
                    { "DiasFaltantes", diasFaltantes },
                    { "TituloEvento", evento.titulo },
                    { "FechaEvento", evento.fecha_inicio.ToString("dd/MM/yyyy HH:mm") },
                    { "TipoEvento", evento.tipo_evento },
                    { "DescripcionEvento", System.Net.WebUtility.HtmlEncode(evento.descripcion ?? "") }
                };

                var emailBody = await _emailTemplatingService.LoadAndPopulateTemplateAsync(templateFileName, data);

                if (emailBody.StartsWith("Error:"))
                {
                    return;
                }

                await _emailService.SendEmailAsync(
                    evento.Usuario?.correo_electronico ?? "Usuario",
                    asunto,
                    emailBody
                );
            }
            catch (Exception)
            {
                throw;
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
                throw;
            }
        }
    }
}
