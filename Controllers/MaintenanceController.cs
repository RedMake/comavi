using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
namespace COMAVI_SA.Controllers
{
    [AllowAnonymous]
    public class MaintenanceController : Controller
    {
        public IActionResult Index(int errorCode = 0, string errorMessage = "")
        {
            Response.StatusCode = 503; // Service Unavailable
            Response.Headers.Append("Retry-After", "300"); // 5 minutos

            // Pasar el código de error y mensaje a la vista
            ViewBag.ErrorCode = errorCode > 0 ? errorCode : 40613; // Valor por defecto
            ViewBag.ErrorMessage = !string.IsNullOrEmpty(errorMessage)
                ? errorMessage
                : "Base de datos temporalmente no disponible";

            return View();
        }
    }
}