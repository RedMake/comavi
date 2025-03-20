using COMAVI_SA.Data;
using COMAVI_SA.Models;
using COMAVI_SA.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace COMAVI_SA.Controllers
{
    [Authorize(Roles = "admin,user")]
    public class CamionController : Controller
    {
        private readonly ComaviDbContext _context;
        private readonly ILogger<CamionController> _logger;
        private readonly IPdfService _pdfService;
        private readonly IEmailService _emailService;

        public CamionController(
            ComaviDbContext context,
            ILogger<CamionController> logger,
            IPdfService pdfService,
            IEmailService emailService)
        {
            _context = context;
            _logger = logger;
            _pdfService = pdfService;
            _emailService = emailService;
        }

        // Mostrar perfil del camión asignado al chofer
        [HttpGet]
        public async Task<IActionResult> CamionAsignado()
        {
            try
            {
                int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

                // Obtener información del chofer
                var chofer = await _context.Choferes
                    .FirstOrDefaultAsync(c => c.id_usuario == userId);

                if (chofer == null)
                {
                    TempData["Error"] = "Debe completar su perfil primero.";
                    return RedirectToAction("Profile", "Login");
                }

                // Obtener camión asignado
                var camion = await _context.Camiones
                    .FirstOrDefaultAsync(c => c.chofer_asignado == chofer.id_chofer);

                if (camion == null)
                {
                    TempData["Info"] = "No tiene un camión asignado actualmente.";
                    return View(null);
                }

                // Obtener historial de mantenimiento
                var historialMantenimiento = await _context.Mantenimiento_Camiones
                    .Where(m => m.id_camion == camion.id_camion)
                    .OrderByDescending(m => m.fecha_mantenimiento)
                    .ToListAsync();

                ViewBag.HistorialMantenimiento = historialMantenimiento;

                return View(camion);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener información del camión asignado");
                TempData["Error"] = "Error al cargar la información del camión.";
                return RedirectToAction("Profile", "Login");
            }
        }
        
    }
}