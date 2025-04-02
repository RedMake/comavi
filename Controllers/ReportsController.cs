using COMAVI_SA.Data;
using COMAVI_SA.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace COMAVI_SA.Controllers
{
#nullable disable

    [Authorize(Roles = "admin,user")]
    public class ReportsController : Controller
    {
        private readonly IReportService _reportService;
        private readonly ComaviDbContext _context;

        public ReportsController(
            IReportService reportService,
            ComaviDbContext context)
        {
            _reportService = reportService;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> DriverReport()
        {
            try
            {
                int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var pdfBytes = await _reportService.GenerateDriverReportAsync(userId);

                return File(pdfBytes, "application/pdf", $"ReporteConductor_{DateTime.Now:yyyyMMdd}.pdf");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al generar el reporte: " + ex.Message;
                return RedirectToAction("Profile", "Login");
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExpirationReport()
        {
            try
            {
                int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var pdfBytes = await _reportService.GenerateExpirationReportAsync(userId);

                return File(pdfBytes, "application/pdf", $"ReporteVencimientos_{DateTime.Now:yyyyMMdd}.pdf");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al generar el reporte: " + ex.Message;
                return RedirectToAction("Profile", "Login");
            }
        }
    }
#nullable enable

}