using COMAVI_SA.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using COMAVI_SA.Services;

namespace COMAVI_SA.Middleware
{
#nullable disable
#pragma warning disable CS0168

    public class SessionValidationMiddleware : IMiddleware
    {
        private readonly ComaviDbContext _context;

        public SessionValidationMiddleware(
            ComaviDbContext context)
        {
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
                    context.Items["SesionExpirada"] = true;
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
                    return false;
                }

                var userId = int.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier));

                var sesionActiva = await _context.SesionesActivas
                    .AnyAsync(s => s.id_usuario == userId);

                if (!sesionActiva)
                {
                    return false;
                }

                if (context.Session.TryGetValue("JwtToken", out var jwtBytes))
                {
                    var jwt = Encoding.UTF8.GetString(jwtBytes);

                    // Obtener servicio de blacklist
                    var blacklistService = context.RequestServices.GetService<IJwtBlacklistService>();
                    if (blacklistService != null && blacklistService.IsTokenBlacklisted(jwt))
                    {
                        return false;
                    }
                }

                var currentUserAgent = context.Request.Headers["User-Agent"].ToString();
                var sesionDb = await _context.SesionesActivas
                    .FirstOrDefaultAsync(s => s.id_usuario == userId);

                if (sesionDb != null && sesionDb.dispositivo != currentUserAgent)
                {
                    return false;
                }

                var user = await _context.Usuarios.FindAsync(userId);
                if (user != null && user.mfa_habilitado && !context.User.HasClaim(c => c.Type == "MfaCompleted" && c.Value == "true"))
                {
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
                return false; // Ante cualquier error, invalidar la sesión
            }
        }
    }
}
