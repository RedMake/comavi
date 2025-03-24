using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Threading.Tasks;

namespace COMAVI_SA.Filters
{
    public class VerificarAutenticacionAttribute : ActionFilterAttribute
    {
        private readonly List<string> _rutasPublicas = new List<string>
        {
            "/Home/Index",
            "/Login/Index",
            "/Login/VerifyOtp",
            "/Login/Register",
            "/Login/ForgotPassword",
            "/Login/ResetPassword",
            "/Home/SesionExpirada"
        };

        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            // Obtener la ruta actual
            var rutaActual = $"/{context.RouteData.Values["controller"]}/{context.RouteData.Values["action"]}";

            // Verificar si la ruta actual está en la lista de rutas públicas
            bool esRutaPublica = _rutasPublicas.Any(ruta =>
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
                // Si no está autenticado, redirige a la página de login
                var controller = context.Controller as Controller;
                if (controller != null)
                {
                    controller.TempData["Error"] = "Debe iniciar sesión para acceder a esta página.";

                    // Guardar la URL a la que intentaba acceder para redirigir después del login
                    var urlAnterior = context.HttpContext.Request.Path + context.HttpContext.Request.QueryString;
                    controller.TempData["ReturnUrl"] = urlAnterior;

                    context.Result = new RedirectToActionResult("Index", "Login", null);
                    return;
                }
            }

            // Si está autenticado, continúa con la ejecución normal
            await next();
        }
    
    }
}