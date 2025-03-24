using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace COMAVI_SA.Controllers
{
    [AllowAnonymous]
    public class MaintenanceController : Controller
    {
        public IActionResult Index()
        {
            Response.StatusCode = 503; // Service Unavailable
            Response.Headers.Add("Retry-After", "300"); // 5 minutos

            return View();
        }
    }
}