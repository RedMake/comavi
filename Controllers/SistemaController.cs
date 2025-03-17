using COMAVI_SA.Data;
using COMAVI_SA.Models;
using COMAVI_SA.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Data;

namespace COMAVI_SA.Controllers
{
    [Authorize(Policy = "RequireAdminRole")]
    public class SistemaController : Controller
    {
        private readonly ComaviDbContext _context;
        private readonly IUserService _userService;
        private readonly IEmailService _emailService;
        private readonly ILogger<SistemaController> _logger;

        public SistemaController(
            ComaviDbContext context,
            IUserService userService,
            IEmailService emailService,
            ILogger<SistemaController> logger)
        {
            _context = context;
            _userService = userService;
            _emailService = emailService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Notificaciones()
        {
            try
            {
                var notificaciones = await _context.NotificacionesUsuario
                    .Include(n => n.Usuario)
                    .OrderByDescending(n => n.fecha_hora)
                    .Take(20)
                    .ToListAsync();

                return View(notificaciones);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar notificaciones");
                TempData["Error"] = "Error al cargar las notificaciones del sistema";
                return View(new List<Notificaciones_Usuario>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> Usuarios()
        {
            try
            {
                var usuarios = await _context.Usuarios
                    .Select(u => new UsuarioAdminViewModel
                    {
                        id_usuario = u.id_usuario,
                        nombre_usuario = u.nombre_usuario,
                        correo_electronico = u.correo_electronico,
                        rol = u.rol,
                        ultimo_ingreso = u.ultimo_ingreso,
                        sesiones_activas = _context.SesionesActivas.Count(s => s.id_usuario == u.id_usuario)
                    })
                    .ToListAsync();

                return View(usuarios);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar usuarios");
                TempData["Error"] = "Error al cargar la lista de usuarios";
                return View(new List<UsuarioAdminViewModel>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> EditarUsuarios(int id)
        {
            try
            {
                var usuario = await _context.Usuarios.FindAsync(id);
                if (usuario == null)
                {
                    TempData["Error"] = "Usuario no encontrado";
                    return RedirectToAction("Usuarios");
                }

                var viewModel = new EditarUsuarioViewModel
                {
                    id_usuario = usuario.id_usuario,
                    nombre_usuario = usuario.nombre_usuario,
                    correo_electronico = usuario.correo_electronico,
                    rol = usuario.rol
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar datos del usuario");
                TempData["Error"] = "Error al cargar los datos del usuario";
                return RedirectToAction("Usuarios");
            }
        }

        [HttpPost]
        public async Task<IActionResult> EditarUsuarios(EditarUsuarioViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            try
            {
                var usuario = await _context.Usuarios.FindAsync(model.id_usuario);
                if (usuario == null)
                {
                    TempData["Error"] = "Usuario no encontrado";
                    return RedirectToAction("Usuarios");
                }

                // Verificar si se está modificando el correo y si ya existe
                if (usuario.correo_electronico != model.correo_electronico)
                {
                    var existeEmail = await _context.Usuarios
                        .AnyAsync(u => u.correo_electronico == model.correo_electronico && u.id_usuario != model.id_usuario);

                    if (existeEmail)
                    {
                        ModelState.AddModelError("correo_electronico", "Este correo electrónico ya está en uso");
                        return View(model);
                    }
                }

                usuario.nombre_usuario = model.nombre_usuario;
                usuario.correo_electronico = model.correo_electronico;
                usuario.rol = model.rol;

                await _context.SaveChangesAsync();

                TempData["Success"] = "Usuario actualizado exitosamente";
                return RedirectToAction("Usuarios");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar usuario");
                TempData["Error"] = "Error al actualizar el usuario";
                return View(model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> EliminarUsuarios(int id)
        {
            try
            {
                var usuario = await _context.Usuarios.FindAsync(id);
                if (usuario == null)
                {
                    TempData["Error"] = "Usuario no encontrado";
                    return RedirectToAction("Usuarios");
                }

                // No permitir eliminar al usuario administrador principal
                if (usuario.id_usuario == 1)
                {
                    TempData["Error"] = "No se puede eliminar al administrador principal";
                    return RedirectToAction("Usuarios");
                }

                return View(usuario);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar datos del usuario para eliminar");
                TempData["Error"] = "Error al cargar los datos del usuario";
                return RedirectToAction("Usuarios");
            }
        }

        [HttpPost]
        public async Task<IActionResult> EliminarUsuarios(int id, bool confirmar)
        {
            if (!confirmar)
                return RedirectToAction("Usuarios");

            try
            {
                var usuario = await _context.Usuarios.FindAsync(id);
                if (usuario == null)
                {
                    TempData["Error"] = "Usuario no encontrado";
                    return RedirectToAction("Usuarios");
                }

                // No permitir eliminar al usuario administrador principal
                if (usuario.id_usuario == 1)
                {
                    TempData["Error"] = "No se puede eliminar al administrador principal";
                    return RedirectToAction("Usuarios");
                }

                // Eliminar registros relacionados
                var intentosLogin = await _context.IntentosLogin.Where(i => i.id_usuario == id).ToListAsync();
                _context.IntentosLogin.RemoveRange(intentosLogin);

                var sesionesActivas = await _context.SesionesActivas.Where(s => s.id_usuario == id).ToListAsync();
                _context.SesionesActivas.RemoveRange(sesionesActivas);

                var notificaciones = await _context.NotificacionesUsuario.Where(n => n.id_usuario == id).ToListAsync();
                _context.NotificacionesUsuario.RemoveRange(notificaciones);

                var resetTokens = await _context.RestablecimientoContrasena.Where(r => r.id_usuario == id).ToListAsync();
                _context.RestablecimientoContrasena.RemoveRange(resetTokens);

                var mfaCodes = await _context.MFA.Where(m => m.id_usuario == id).ToListAsync();
                _context.MFA.RemoveRange(mfaCodes);

                _context.Usuarios.Remove(usuario);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Usuario eliminado exitosamente";
                return RedirectToAction("Usuarios");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar usuario");
                TempData["Error"] = "Error al eliminar el usuario";
                return RedirectToAction("Usuarios");
            }
        }

        [HttpGet]
        public async Task<IActionResult> EnviarNotificacion(int? idUsuario = null)
        {
            try
            {
                var usuarios = await _context.Usuarios.ToListAsync();
                ViewBag.Usuarios = usuarios;
                ViewBag.UsuarioSeleccionado = idUsuario;

                return View(new EnviarNotificacionViewModel());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar datos para enviar notificación");
                TempData["Error"] = "Error al cargar los datos necesarios";
                return RedirectToAction("Notificaciones");
            }
        }

        [HttpPost]
        public async Task<IActionResult> EnviarNotificacion(EnviarNotificacionViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var usuarios = await _context.Usuarios.ToListAsync();
                ViewBag.Usuarios = usuarios;
                return View(model);
            }

            try
            {
                if (model.EnviarATodos)
                {
                    var usuarios = await _context.Usuarios.ToListAsync();
                    foreach (var usuario in usuarios)
                    {
                        var notificacion = new Notificaciones_Usuario
                        {
                            id_usuario = usuario.id_usuario,
                            tipo_notificacion = model.TipoNotificacion,
                            fecha_hora = DateTime.Now,
                            mensaje = model.Mensaje
                        };

                        _context.NotificacionesUsuario.Add(notificacion);

                        // Si está marcado para enviar por correo
                        if (model.EnviarPorCorreo)
                        {
                            await _emailService.SendEmailAsync(
                                usuario.correo_electronico,
                                $"Notificación del Sistema COMAVI: {model.TipoNotificacion}",
                                $"<h2>Notificación del Sistema</h2><p>{model.Mensaje}</p>");
                        }
                    }
                }
                else if (model.UsuarioId.HasValue)
                {
                    var usuario = await _context.Usuarios.FindAsync(model.UsuarioId.Value);
                    if (usuario != null)
                    {
                        var notificacion = new Notificaciones_Usuario
                        {
                            id_usuario = usuario.id_usuario,
                            tipo_notificacion = model.TipoNotificacion,
                            fecha_hora = DateTime.Now,
                            mensaje = model.Mensaje
                        };

                        _context.NotificacionesUsuario.Add(notificacion);

                        // Si está marcado para enviar por correo
                        if (model.EnviarPorCorreo)
                        {
                            await _emailService.SendEmailAsync(
                                usuario.correo_electronico,
                                $"Notificación del Sistema COMAVI: {model.TipoNotificacion}",
                                $"<h2>Notificación del Sistema</h2><p>{model.Mensaje}</p>");
                        }
                    }
                }

                await _context.SaveChangesAsync();

                TempData["Success"] = "Notificación enviada exitosamente";
                return RedirectToAction("Notificaciones");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar notificación");
                TempData["Error"] = "Error al enviar la notificación";
                var usuarios = await _context.Usuarios.ToListAsync();
                ViewBag.Usuarios = usuarios;
                return View(model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> SesionesActivas()
        {
            try
            {
                var sesiones = await _context.SesionesActivas
                    .Include(s => s.Usuario)
                    .OrderByDescending(s => s.fecha_ultima_actividad)
                    .ToListAsync();

                return View(sesiones);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar sesiones activas");
                TempData["Error"] = "Error al cargar las sesiones activas";
                return View(new List<SesionesActivas>());
            }
        }

        [HttpPost]
        public async Task<IActionResult> CerrarSesion(int id)
        {
            try
            {
                var sesion = await _context.SesionesActivas.FindAsync(id);
                if (sesion != null)
                {
                    _context.SesionesActivas.Remove(sesion);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Sesión cerrada exitosamente";
                }
                else
                {
                    TempData["Error"] = "Sesión no encontrada";
                }

                return RedirectToAction("SesionesActivas");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cerrar sesión");
                TempData["Error"] = "Error al cerrar la sesión";
                return RedirectToAction("SesionesActivas");
            }
        }

        [HttpPost]
        public async Task<IActionResult> CerrarTodasSesiones()
        {
            try
            {
                var sesiones = await _context.SesionesActivas.ToListAsync();
                _context.SesionesActivas.RemoveRange(sesiones);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Todas las sesiones han sido cerradas exitosamente";
                return RedirectToAction("SesionesActivas");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cerrar todas las sesiones");
                TempData["Error"] = "Error al cerrar todas las sesiones";
                return RedirectToAction("SesionesActivas");
            }
        }

        [HttpGet]
        public async Task<IActionResult> IntentosLogin()
        {
            try
            {
                var intentos = await _context.IntentosLogin
                    .Include(i => i.Usuario)
                    .OrderByDescending(i => i.fecha_hora)
                    .Take(100)
                    .ToListAsync();

                return View(intentos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar intentos de login");
                TempData["Error"] = "Error al cargar los intentos de login";
                return View(new List<IntentosLogin>());
            }
        }

        [HttpGet]
        public IActionResult ReporteCamiones()
        {
            return View();
        }

        [HttpGet]
        public IActionResult ReporteChoferes()
        {
            return View();
        }

        [HttpGet]
        public IActionResult ConfiguracionSistema()
        {
            try
            {
                var settings = new ConfiguracionSistemaViewModel
                {
                    MaximoIntentosFallidos = 5,
                    TiempoBloqueoMinutos = 15,
                    DuracionSesionMinutos = 30,
                    AnticipoVencimientosDias = 30,
                    NotificacionesEmail = true,
                    RegistroActividadHabilitado = true
                };

                return View(settings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar configuración del sistema");
                TempData["Error"] = "Error al cargar la configuración del sistema";
                return RedirectToAction("Index", "Home");
            }
        }

        [HttpPost]
        public async Task<IActionResult> ConfiguracionSistema(ConfiguracionSistemaViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            try
            {
                // Aquí se guardarían los valores en la base de datos o en appsettings
                // Por ahora solo simulamos que se guardó correctamente

                TempData["Success"] = "Configuración actualizada exitosamente";
                return RedirectToAction("ConfiguracionSistema");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al guardar configuración del sistema");
                TempData["Error"] = "Error al guardar la configuración del sistema";
                return View(model);
            }
        }
    }

    public class UsuarioAdminViewModel
    {
        public int id_usuario { get; set; }
        public string nombre_usuario { get; set; }
        public string correo_electronico { get; set; }
        public string rol { get; set; }
        public DateTime? ultimo_ingreso { get; set; }
        public int sesiones_activas { get; set; }
    }

    public class EditarUsuarioViewModel
    {
        public int id_usuario { get; set; }

        [Required(ErrorMessage = "El nombre de usuario es requerido")]
        [StringLength(50, ErrorMessage = "El nombre de usuario debe tener entre 3 y 50 caracteres", MinimumLength = 3)]
        public string nombre_usuario { get; set; }

        [Required(ErrorMessage = "El correo electrónico es requerido")]
        [EmailAddress(ErrorMessage = "Formato de correo electrónico inválido")]
        [StringLength(100)]
        public string correo_electronico { get; set; }

        [Required(ErrorMessage = "El rol es requerido")]
        public string rol { get; set; }
    }

    public class EnviarNotificacionViewModel
    {
        public int? UsuarioId { get; set; }

        public bool EnviarATodos { get; set; }

        [Required(ErrorMessage = "El tipo de notificación es requerido")]
        [StringLength(50)]
        public string TipoNotificacion { get; set; }

        [Required(ErrorMessage = "El mensaje es requerido")]
        public string Mensaje { get; set; }

        public bool EnviarPorCorreo { get; set; }
    }

    public class ConfiguracionSistemaViewModel
    {
        [Range(1, 10, ErrorMessage = "El valor debe estar entre 1 y 10")]
        public int MaximoIntentosFallidos { get; set; }

        [Range(5, 60, ErrorMessage = "El valor debe estar entre 5 y 60")]
        public int TiempoBloqueoMinutos { get; set; }

        [Range(5, 240, ErrorMessage = "El valor debe estar entre 5 y 240")]
        public int DuracionSesionMinutos { get; set; }

        [Range(1, 90, ErrorMessage = "El valor debe estar entre 1 y 90")]
        public int AnticipoVencimientosDias { get; set; }

        public bool NotificacionesEmail { get; set; }

        public bool RegistroActividadHabilitado { get; set; }
    }
}