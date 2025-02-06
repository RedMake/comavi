using Microsoft.AspNetCore.Mvc;

namespace COMAVI_SA.Controllers
{
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
