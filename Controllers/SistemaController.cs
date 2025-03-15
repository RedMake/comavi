using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace COMAVI_SA.Controllers
{
    [Authorize(Policy = "RequireAdminRole")]
    public class SistemaController : Controller
    {
        public IActionResult Notificaciones()
        {
            return View();
        }
        public IActionResult Usuarios()
        {
            {
                return View();
            }
        }
        public IActionResult EditarUsuarios()
        {
            return View();
        }
        public IActionResult EliminarUsuarios()
        {
            {
                return View();
            }
        }
    }
}
