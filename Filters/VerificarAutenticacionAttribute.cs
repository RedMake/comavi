using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace COMAVI_SA.Filters
{
#nullable disable

    public class VerificarAutenticacionAttribute : ActionFilterAttribute
    {
        private readonly List<string> rutasPublicas = new List<string> {
            "/Home/Index",
            "/Login/Index",
            "/Login/VerifyOtp",
            "/Login/Register",
            "/Login/ForgotPassword",
            "/Login/ResetPassword",
            "/Login/Verificar",
            "/Login/InstruccionesVerificacion",
            "/Home/SesionExpirada",
            "/Home/About",
            "/Home/FAQ",
            "/Home/Terms",
            "/Home/Privacy",
            "/Home/ConsejosAdvertencias",
            "/Maintenance/Index"
        };

        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            // Obtener la ruta actual
            var rutaActual = $"/{context.RouteData.Values["controller"]}/{context.RouteData.Values["action"]}";

            // Verificar si la ruta actual está en la lista de rutas públicas
            bool esRutaPublica = rutasPublicas.Any(ruta =>
                string.Equals(ruta, rutaActual, System.StringComparison.OrdinalIgnoreCase));

            // Si es una ruta pública, permitir el acceso sin verificar autenticación
            if (esRutaPublica)
            {
                await next();
                return;
            }


            // Verificar si el usuario está autenticado
            if (!context.HttpContext.User.Identity.IsAuthenticated)
            {
                var controller = context.Controller as Controller;

                if (controller != null)
                {
                    // Determinar si viene de una expiración de sesión

                    // Y en el filtro
                    bool sesionExpirada = context.HttpContext.Items.ContainsKey("SesionExpirada") ||
                                          (context.HttpContext.Request.Cookies.ContainsKey("COMAVI.Auth") &&
                                          !context.HttpContext.User.Identity.IsAuthenticated);

                    if (sesionExpirada)
                    {
                        // Limpiar la cookie explícitamente
                        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

                        // Redirigir a la página de sesión expirada
                        context.Result = new RedirectToActionResult("SesionExpirada", "Home", null);
                    }
                    else
                    {
                        // Guardar la URL a la que intentaba acceder para redirigir después del login
                        var urlAnterior = context.HttpContext.Request.Path + context.HttpContext.Request.QueryString;
                        controller.TempData["ReturnUrl"] = urlAnterior;

                        context.Result = new RedirectToActionResult("Index", "Login", null);
                    }
                    return;
                }
            }
            
            await next();
        }
    }
}