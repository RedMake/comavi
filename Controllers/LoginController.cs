using Microsoft.AspNetCore.Mvc;

namespace COMAVI_SA.Controllers
{
    public class LoginController : Controller
    {
        public IActionResult Login()
        {
            return View();
        }
        public IActionResult Register()
        {
            return View();
        }

        public IActionResult RestablecerContrasena()
        {
            return View();
        }

        public IActionResult MFA()
        {
            return View();
        }

        public IActionResult CerrarSesiones()
        {
            return View();
        }

        public IActionResult PerfilConfiguracion()
        {
            return View();
        }
        
    }
}
