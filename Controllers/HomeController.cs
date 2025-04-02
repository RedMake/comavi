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
using System.Security.Claims;

namespace COMAVI_SA.Controllers
{
#nullable disable
    [AllowAnonymous]

    public class HomeController : Controller
    {
        private readonly ComaviDbContext _context;
        private readonly IAuthorizationService _authorizationService;

        public HomeController( ComaviDbContext context, IAuthorizationService authorizationService)
        {
            _context = context;
            _authorizationService = authorizationService;
        }

        public async Task<IActionResult> Index()
        {
            // Si el usuario no est� autenticado, mostrar la p�gina de bienvenida
            if (!User.Identity.IsAuthenticated)
            {
                return View();
            }

            // Si el usuario es admin, solo pasamos a la vista
            if (User.IsInRole("admin"))
            {
                return RedirectToAction("Index", "Admin");
            }

            // Para usuarios con rol "user", cargamos datos relevantes
            if (User.IsInRole("user"))
            {
                // Obtener el ID del usuario
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                // Buscar el chofer relacionado con este usuario
                var chofer = await _context.Choferes
                    .FirstOrDefaultAsync(c => c.id_usuario == int.Parse(userId));


                if (chofer != null)
                {
                    // Informaci�n del cami�n asignado
                    var camion = await _context.Camiones
                        .FirstOrDefaultAsync(c => c.chofer_asignado == chofer.id_chofer);

                    if (camion != null)
                    {
                        ViewBag.InfoCamion = $"{camion.marca} {camion.modelo} - {camion.numero_placa}";
                        ViewBag.TieneCamionAsignado = true;

                        // Obtener el �ltimo mantenimiento y calcular el pr�ximo
                        var ultimoMantenimiento = await _context.Mantenimiento_Camiones
                            .Where(m => m.id_camion == camion.id_camion)
                            .OrderByDescending(m => m.fecha_mantenimiento)
                            .FirstOrDefaultAsync();

                        if (ultimoMantenimiento != null)
                        {
                            // Estimamos el pr�ximo mantenimiento en 3 meses despu�s del �ltimo
                            ViewBag.ProximoMantenimiento = ultimoMantenimiento.fecha_mantenimiento.AddMonths(3);
                        }
                    }
                    else
                    {
                        ViewBag.InfoCamion = "No asignado";
                        ViewBag.TieneCamionAsignado = false;
                    }

                    // Informaci�n de la licencia
                    ViewBag.FechaVencLicencia = chofer.fecha_venc_licencia;
                    ViewBag.DiasParaVencimiento = (chofer.fecha_venc_licencia - DateTime.Now).Days;

                    // Documentos pendientes y pr�ximos a vencer
                    var documentos = await _context.Documentos
                        .Where(d => d.id_chofer == chofer.id_chofer)
                        .ToListAsync();

                    ViewBag.DocumentosPendientes = documentos.Count(d => d.estado_validacion == "pendiente" || d.estado_validacion == "rechazado");

                    // Documentos pr�ximos a vencer (en los pr�ximos 30 d�as)
                    var hoy = DateTime.Now;
                    var proximosVencimientos = documentos
                        .Where(d => d.estado_validacion == "verificado" &&
                                (d.fecha_vencimiento - hoy).TotalDays <= 30 &&
                                d.fecha_vencimiento > hoy)
                        .Select(d => new {
                            d.tipo_documento,
                            d.fecha_vencimiento,
                            dias_restantes = (int)(d.fecha_vencimiento - hoy).TotalDays
                        })
                        .OrderBy(d => d.dias_restantes)
                        .ToList();

                    ViewBag.ProximosVencimientos = proximosVencimientos;

                    // Pr�ximos eventos de la agenda
                    var eventosAgenda = await _context.EventosAgenda
                        .Where(e => e.id_usuario == int.Parse(userId) &&
                               e.fecha_inicio > hoy &&
                               e.fecha_inicio <= hoy.AddDays(30))
                        .OrderBy(e => e.fecha_inicio)
                        .Take(5)
                        .ToListAsync();

                    ViewBag.EventosAgenda = eventosAgenda;
                }
            }

            return View();
        }

        public IActionResult ConsejosAdvertencias()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        // P�gina de informaci�n sobre la empresa
        public IActionResult About()
        {
            return View();
        }


        // Preguntas frecuentes
        public IActionResult FAQ()
        {
            return View();
        }

        // T�rminos y condiciones
        public IActionResult Terms()
        {
            return View();
        }

        public IActionResult SesionExpirada()
        {
            // Limpiar cookies de autenticaci�n
            HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            // Limpiar cookies de sesi�n
            Response.Cookies.Delete("COMAVI.Session");

            return View();
        }
    }

}