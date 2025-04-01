using COMAVI_SA.Data;
using COMAVI_SA.Models;
using Dapper;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Diagnostics;

namespace COMAVI_SA.Controllers
{
    [AllowAnonymous]

    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ComaviDbContext _context;
        private readonly string _connectionString;

        public HomeController(ILogger<HomeController> logger, ComaviDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            return View();
        }

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        public IActionResult SesionExpirada()
        {
            // Limpiar cookies de autenticación
            HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            // Limpiar cookies de sesión
            Response.Cookies.Delete("COMAVI.Session");

            return View();
        }
    }

}