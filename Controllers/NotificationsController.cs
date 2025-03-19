using COMAVI_SA.Data;
using COMAVI_SA.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace COMAVI_SA.Controllers
{
    [Authorize(Roles = "admin,user")]
    public class NotificationsController : Controller
    {
        private readonly ComaviDbContext _context;
        private readonly ILogger<NotificationsController> _logger;

        public NotificationsController(
            ComaviDbContext context,
            ILogger<NotificationsController> logger)
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

                // Obtener notificaciones del usuario
                var notificaciones = await _context.NotificacionesUsuario
                    .Where(n => n.id_usuario == userId)
                    .OrderByDescending(n => n.fecha_hora)
                    .ToListAsync();

                // Obtener configuración actual
                var preferencias = await _context.PreferenciasNotificacion
                    .FirstOrDefaultAsync(p => p.id_usuario == userId);

                if (preferencias == null)
                {
                    // Crear preferencias por defecto si no existen
                    preferencias = new PreferenciasNotificacion
                    {
                        id_usuario = userId,
                        notificar_por_correo = true,
                        dias_anticipacion = 15,
                        notificar_vencimiento_licencia = true,
                        notificar_vencimiento_documentos = true
                    };

                    _context.PreferenciasNotificacion.Add(preferencias);
                    await _context.SaveChangesAsync();
                }

                var model = new NotificacionesViewModel
                {
                    Notificaciones = notificaciones,
                    Preferencias = preferencias
                };

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar la página de notificaciones");
                TempData["Error"] = "Error al cargar las notificaciones.";
                return RedirectToAction("Profile", "Login");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GuardarPreferencias(PreferenciasNotificacion model)
        {
            try
            {
                int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

                // Obtener preferencias existentes
                var preferencias = await _context.PreferenciasNotificacion
                    .FirstOrDefaultAsync(p => p.id_usuario == userId);

                if (preferencias == null)
                {
                    // Crear nuevas preferencias
                    preferencias = new PreferenciasNotificacion
                    {
                        id_usuario = userId,
                        notificar_por_correo = model.notificar_por_correo,
                        dias_anticipacion = model.dias_anticipacion,
                        notificar_vencimiento_licencia = model.notificar_vencimiento_licencia,
                        notificar_vencimiento_documentos = model.notificar_vencimiento_documentos
                    };

                    _context.PreferenciasNotificacion.Add(preferencias);
                }
                else
                {
                    // Actualizar preferencias existentes
                    preferencias.notificar_por_correo = model.notificar_por_correo;
                    preferencias.dias_anticipacion = model.dias_anticipacion;
                    preferencias.notificar_vencimiento_licencia = model.notificar_vencimiento_licencia;
                    preferencias.notificar_vencimiento_documentos = model.notificar_vencimiento_documentos;
                }

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Preferencias de notificación guardadas correctamente.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al guardar preferencias de notificación");
                TempData["Error"] = "Error al guardar las preferencias de notificación.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarcarLeida(int id)
        {
            try
            {
                int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

                // Obtener la notificación
                var notificacion = await _context.NotificacionesUsuario
                    .FirstOrDefaultAsync(n => n.id_notificacion == id && n.id_usuario == userId);

                if (notificacion == null)
                {
                    return NotFound();
                }

                // Marcar como leída
                notificacion.leida = true;
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al marcar notificación como leída");
                return Json(new { success = false, message = "Error al marcar la notificación como leída." });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarNotificacion(int id)
        {
            try
            {
                int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

                // Obtener la notificación
                var notificacion = await _context.NotificacionesUsuario
                    .FirstOrDefaultAsync(n => n.id_notificacion == id && n.id_usuario == userId);

                if (notificacion == null)
                {
                    return NotFound();
                }

                // Eliminar la notificación
                _context.NotificacionesUsuario.Remove(notificacion);
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar notificación");
                return Json(new { success = false, message = "Error al eliminar la notificación." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerNotificacionesNoLeidas()
        {
            try
            {
                int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

                // Obtener notificaciones no leídas, teniendo en cuenta posibles valores nulos
                var notificacionesNoLeidas = await _context.NotificacionesUsuario
                    .Where(n => n.id_usuario == userId && (n.leida == null || n.leida == false))
                    .OrderByDescending(n => n.fecha_hora)
                    .Select(n => new {
                        id = n.id_notificacion,
                        mensaje = n.mensaje,
                        tipo = n.tipo_notificacion,
                        fecha = n.fecha_hora.ToString("dd/MM/yyyy HH:mm")
                    })
                    .ToListAsync();

                return Json(new
                {
                    success = true,
                    count = notificacionesNoLeidas.Count,
                    notifications = notificacionesNoLeidas
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener notificaciones no leídas");
                return Json(new { success = false, message = "Error al obtener notificaciones no leídas." });
            }
        }
    }
}