using COMAVI_SA.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using COMAVI_SA.Services;

namespace COMAVI_SA.Middleware
{
    public class SessionValidationMiddleware : IMiddleware
    {
        private readonly ILogger<SessionValidationMiddleware> _logger;
        private readonly ComaviDbContext _context;

        public SessionValidationMiddleware(
            ILogger<SessionValidationMiddleware> logger,
            ComaviDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            // Ignorar rutas específicas que no requieren validación
            var path = context.Request.Path.Value?.ToLower();
            bool isAuthPath = !(path?.Contains("/login") == true || path?.Contains("/logout") == true);

            // Permitir siempre acceso a la ruta VerifyOtp
            bool isVerifyOtpPath = path?.Contains("/login/verifyotp") == true;

            if (context.User.Identity?.IsAuthenticated == true && isAuthPath && !isVerifyOtpPath)
            {
                _logger.LogDebug("Validando sesión para ruta {Path}", context.Request.Path);

                // Verificar si el usuario ha completado MFA
                bool mfaRedirect = context.User.HasClaim(c => c.Type == "MfaCompleted" && c.Value == "true");

                if (!mfaRedirect)
                {

                    // Guardar URL actual para redireccionar después de completar MFA
                    context.Session.SetString("ReturnUrl", context.Request.Path);

                    // VerifyOtp es especial porque acepta usuario sin MFA, despues de todo el MFA es opcional
                    context.Response.Redirect("/Login/VerifyOtp");
                    return;
                }

                if (!await IsValidSession(context))
                {
                    _logger.LogWarning("Sesión inválida detectada. Forzando logout");

                    // Limpiar sesión
                    context.Session.Clear();

                    // Desconectar usuario
                    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

                    // Eliminar cookies
                    foreach (var cookie in context.Request.Cookies.Keys)
                    {
                        context.Response.Cookies.Delete(cookie);
                    }

                    // Redireccionar al login
                    context.Response.Redirect("/Login/Index?expired=true");
                    return;
                }
            }

            await next(context);
        }

        private async Task<bool> IsValidSession(HttpContext context)
        {
            try
            {
                if (!context.User.HasClaim(c => c.Type == ClaimTypes.NameIdentifier))
                {
                    _logger.LogWarning("Usuario sin ID en claims");
                    return false;
                }

                var userId = int.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier));

                var sesionActiva = await _context.SesionesActivas
                    .AnyAsync(s => s.id_usuario == userId);

                if (!sesionActiva)
                {
                    _logger.LogWarning("No se encontró sesión activa en BD para usuario {UserId}", userId);
                    return false;
                }

                if (context.Session.TryGetValue("JwtToken", out var jwtBytes))
                {
                    var jwt = Encoding.UTF8.GetString(jwtBytes);

                    // Obtener servicio de blacklist
                    var blacklistService = context.RequestServices.GetService<IJwtBlacklistService>();
                    if (blacklistService != null && blacklistService.IsTokenBlacklisted(jwt))
                    {
                        _logger.LogWarning("Token JWT en lista negra");
                        return false;
                    }
                }

                var currentUserAgent = context.Request.Headers["User-Agent"].ToString();
                var sesionDb = await _context.SesionesActivas
                    .FirstOrDefaultAsync(s => s.id_usuario == userId);

                if (sesionDb != null && sesionDb.dispositivo != currentUserAgent)
                {
                    _logger.LogWarning("Cambio de User-Agent detectado para usuario {UserId}", userId);
                    return false;
                }

                var user = await _context.Usuarios.FindAsync(userId);
                if (user != null && user.mfa_habilitado && !context.User.HasClaim(c => c.Type == "MfaCompleted" && c.Value == "true"))
                {
                    _logger.LogWarning("Usuario con MFA habilitado pero sin verificación completa {UserId}", userId);
                    return false;
                }

                // Actualizar tiempo de última actividad
                if (sesionDb != null)
                {
                    sesionDb.fecha_ultima_actividad = DateTime.Now;
                    await _context.SaveChangesAsync();
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validando sesión");
                return false; // Ante cualquier error, invalidar la sesión
            }
        }
    }
}
