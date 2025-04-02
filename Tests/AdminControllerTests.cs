using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using COMAVI_SA.Controllers;
using COMAVI_SA.Models;
using COMAVI_SA.Services;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace COMAVI_SA.Tests.Controllers
{
    public class AdminControllerTests
    {
        #region Setup
        private readonly Mock<IAdminService> _mockAdminService;
        private readonly Mock<IAuditService> _mockAuditService;
        private readonly Mock<INotificationService> _mockNotificationService;
        private readonly Mock<IEmailService> _mockEmailService;
        private readonly Mock<IReportService> _mockReportService;
        private readonly Mock<IUserService> _mockUserService;
        private readonly Mock<IMantenimientoService> _mockMantenimientoService;
        private readonly AdminController _controller;

        public AdminControllerTests()
        {
            // Setup mocks
            _mockAdminService = new Mock<IAdminService>();
            _mockAuditService = new Mock<IAuditService>();
            _mockNotificationService = new Mock<INotificationService>();
            _mockEmailService = new Mock<IEmailService>();
            _mockReportService = new Mock<IReportService>();
            _mockUserService = new Mock<IUserService>();
            _mockMantenimientoService = new Mock<IMantenimientoService>();

            // Create controller with mocked dependencies
            _controller = new AdminController(
                _mockAdminService.Object,
                _mockAuditService.Object,
                _mockNotificationService.Object,
                _mockEmailService.Object,
                _mockReportService.Object,
                _mockUserService.Object,
                _mockMantenimientoService.Object
            );

            // Setup TempData para el controlador
            _controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
                new DefaultHttpContext(),
                Mock.Of<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>()
            );

            // Setup HttpContext y User para pruebas que requieren autorización o acceso a Claims
            var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
            {
                new Claim(ClaimTypes.NameIdentifier, "1"),
                new Claim(ClaimTypes.Name, "admin@test.com")
            }, "mock"));

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };
        }
        #endregion

        #region Dashboard Tests
        [Fact]
        public async Task Index_ReturnsViewWithDashboardData_WhenTaskCompletes()
        {
            // Arrange
            var dashboardData = new AdminDashboardViewModel();
            _mockAdminService.Setup(s => s.GetDashboardDataAsync(It.IsAny<bool>()))
                .ReturnsAsync(dashboardData);

            // Act
            var result = await _controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<AdminDashboardViewModel>(viewResult.Model);
            Assert.Same(dashboardData, model);
        }

        [Fact]
        public async Task Index_RedirectsToListarChoferes_WhenExceptionOccurs()
        {
            // Arrange
            _mockAdminService.Setup(s => s.GetDashboardDataAsync(It.IsAny<bool>()))
                .ThrowsAsync(new Exception("Test exception"));

            // Act
            var result = await _controller.Index();

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("ListarChoferes", redirectResult.ActionName);
            Assert.Equal("Error al cargar el dashboard de administración", _controller.TempData["Error"]);
        }

        [Fact]
        public async Task Dashboard_ReturnsViewWithDashboardData_WhenSuccessful()
        {
            // Arrange
            var dashboardData = new DashboardViewModel();
            var mantenimientosPorMes = new List<GraficoDataViewModel>();
            var camionesEstados = new List<GraficoDataViewModel>();
            var documentosEstados = new List<GraficoDataViewModel>();
            var listaActividades = new List<ActividadRecienteViewModel>();

            _mockAdminService.Setup(s => s.GetDashboardIndicadoresAsync())
                .ReturnsAsync(dashboardData);
            _mockAdminService.Setup(s => s.GetMantenimientosPorMesAsync(It.IsAny<int?>()))
                .ReturnsAsync(mantenimientosPorMes);
            _mockAdminService.Setup(s => s.GetEstadosCamionesAsync())
                .ReturnsAsync(camionesEstados);
            _mockAdminService.Setup(s => s.GetEstadosDocumentosAsync())
                .ReturnsAsync(documentosEstados);

            _mockAdminService.Setup(s => s.GetActividadesRecientesAsync(It.IsAny<int>()))
                .ReturnsAsync(listaActividades);

            // Act
            var result = await _controller.Dashboard();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<DashboardViewModel>(viewResult.Model);
            Assert.Same(dashboardData, model);
            Assert.Same(mantenimientosPorMes, viewResult.ViewData["MantenimientosPorMes"]);
            Assert.Same(camionesEstados, viewResult.ViewData["CamionesEstados"]);
            Assert.Same(documentosEstados, viewResult.ViewData["DocumentosEstados"]);
            Assert.Same(listaActividades, viewResult.ViewData["Actividades"]);
        }

        [Fact]
        public async Task Dashboard_ReturnsViewWithEmptyModel_WhenExceptionOccurs()
        {
            // Arrange
            _mockAdminService.Setup(s => s.GetDashboardIndicadoresAsync())
                .ThrowsAsync(new Exception("Test exception"));

            // Act
            var result = await _controller.Dashboard();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<DashboardViewModel>(viewResult.Model);
            Assert.Equal("Error al cargar los datos del dashboard", _controller.TempData["Error"]);
        }
        #endregion

        #region Reportes Tests
        [Fact]
        public void ReportesGenerales_ReturnsViewResult()
        {
            // Act
            var result = _controller.ReportesGenerales();

            // Assert
            Assert.IsType<ViewResult>(result);
            // No retornar nada aquí
        }

        [Fact]
        public async Task GenerarReporteMantenimientos_ReturnsViewWithData_WhenSuccessful()
        {
            // Arrange
            var fechaInicio = DateTime.Today.AddMonths(-1);
            var fechaFin = DateTime.Today;
            var mantenimientos = new List<MantenimientoReporteViewModel>
            {
                new MantenimientoReporteViewModel { costo = 100 },
                new MantenimientoReporteViewModel { costo = 200 }
            };

            _mockAdminService.Setup(s => s.GenerarReporteMantenimientosAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(mantenimientos);

            // Act
            var result = await _controller.GenerarReporteMantenimientos(fechaInicio, fechaFin);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<MantenimientoReporteViewModel>>(viewResult.Model);
            Assert.Equal(mantenimientos, model);
            Assert.Equal(fechaInicio, viewResult.ViewData["FechaInicio"]);
            Assert.Equal(fechaFin, viewResult.ViewData["FechaFin"]);
            Assert.Equal(300m, viewResult.ViewData["TotalCostos"]);
            Assert.NotNull(viewResult.ViewData["FechaGeneracion"]);
        }

        [Fact]
        public async Task ExportarReporteMantenimientosPDF_ReturnsFileResult_WhenSuccessful()
        {
            // Arrange
            var fechaInicio = DateTime.Today.AddMonths(-1);
            var fechaFin = DateTime.Today;
            var mantenimientos = new List<MantenimientoReporteViewModel>();
            var pdfBytes = new byte[] { 1, 2, 3 };

            _mockAdminService.Setup(s => s.GenerarReporteMantenimientosAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(mantenimientos);
            _mockReportService.Setup(s => s.GenerarReporteMantenimientosPdf(
                mantenimientos,
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>()))
                .ReturnsAsync(pdfBytes);

            // Act
            var result = await _controller.ExportarReporteMantenimientosPDF(fechaInicio, fechaFin);

            // Assert
            var fileResult = Assert.IsType<FileContentResult>(result);
            Assert.Equal("application/pdf", fileResult.ContentType);
            Assert.Equal(pdfBytes, fileResult.FileContents);
            Assert.Contains("Reporte_Mantenimientos_", fileResult.FileDownloadName);
        }

        [Fact]
        public async Task ExportarReporteMantenimientosPDF_RedirectsToAction_WhenExceptionOccurs()
        {
            // Arrange
            var fechaInicio = DateTime.Today.AddMonths(-1);
            var fechaFin = DateTime.Today;

            _mockAdminService.Setup(s => s.GenerarReporteMantenimientosAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ThrowsAsync(new Exception("Test exception"));

            // Act
            var result = await _controller.ExportarReporteMantenimientosPDF(fechaInicio, fechaFin);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("GenerarReporteMantenimientos", redirectResult.ActionName);
            Assert.Equal("Error al generar el PDF de mantenimientos", _controller.TempData["Error"]);
        }
        #endregion

        #region Camiones Tests
        [Fact]
        public async Task ListarCamiones_ReturnsViewWithCamiones_WhenSuccessful()
        {
            // Arrange
            var camiones = new List<CamionViewModel>();
            _mockAdminService.Setup(s => s.GetCamionesAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(camiones);

            // Act
            var result = await _controller.ListarCamiones();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<CamionViewModel>>(viewResult.Model);
            Assert.Same(camiones, model);
        }

        [Fact]
        public async Task RegistrarCamion_RedirectsToListarCamiones_WhenSuccessful()
        {
            // Arrange
            var camion = new Camiones();
            _mockAdminService.Setup(s => s.RegistrarCamionAsync(camion))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.RegistrarCamion(camion);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("ListarCamiones", redirectResult.ActionName);
            Assert.Equal("Camión registrado exitosamente", _controller.TempData["Success"]);
        }

        [Fact]
        public async Task RegistrarCamion_RedirectsToListarCamiones_WhenUnsuccessful()
        {
            // Arrange
            var camion = new Camiones();
            _mockAdminService.Setup(s => s.RegistrarCamionAsync(camion))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.RegistrarCamion(camion);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("ListarCamiones", redirectResult.ActionName);
            Assert.Equal("Error al registrar el camión", _controller.TempData["Error"]);
        }

        [Fact]
        public async Task ActualizarCamion_Get_ReturnsViewWithCamion_WhenFound()
        {
            // Arrange
            int camionId = 1;
            var camion = new Camiones { id_camion = camionId };

            _mockAdminService.Setup(s => s.GetCamionByIdAsync(camionId))
                .ReturnsAsync(camion);

            var choferes = new List<ChoferViewModel>();
            _mockAdminService.Setup(s => s.GetChoferesAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(choferes as IEnumerable<ChoferViewModel>);

            // Act
            var result = await _controller.ActualizarCamion(camionId);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<Camiones>(viewResult.Model);
            Assert.Same(camion, model);
            Assert.Same(choferes, viewResult.ViewData["Choferes"]);
        }
        #endregion

        #region Choferes Tests
        [Fact]
        public async Task ListarChoferes_ReturnsViewWithChoferes_WhenSuccessful()
        {
            // Arrange
            var choferes = new List<ChoferViewModel>();
            _mockAdminService.Setup(s => s.GetChoferesAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(choferes);

            // Act
            var result = await _controller.ListarChoferes();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<ChoferViewModel>>(viewResult.Model);
            Assert.Same(choferes, model);
        }

        [Fact]
        public async Task RegistrarChofer_Get_ReturnsView_WithEmptyModel()
        {
            // Arrange
            var usuarios = new List<Usuario>();
            _mockAdminService.Setup(s => s.GetUsuariosSinChoferAsync())
                .ReturnsAsync(usuarios);

            // Act
            var result = await _controller.RegistrarChofer();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.IsType<Choferes>(viewResult.Model);
            Assert.Equal("activo", ((Choferes)viewResult.Model).estado);
            Assert.Same(usuarios, viewResult.ViewData["Usuarios"]);
        }

        [Fact]
        public async Task RegistrarChofer_Post_RedirectsToListarChoferes_WhenSuccessful()
        {
            // Arrange
            var chofer = new Choferes
            {
                nombreCompleto = "Test Driver",
                edad = 30,
                numero_cedula = "123456789",
                licencia = "L123456",
                fecha_venc_licencia = DateTime.Now.AddYears(1),
                estado = "activo",
                genero = "masculino"
            };

            _mockAdminService.Setup(s => s.RegistrarChoferAsync(chofer))
                .ReturnsAsync((true, "Chofer registrado exitosamente"));

            // Act
            var result = await _controller.RegistrarChofer(chofer);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("ListarChoferes", redirectResult.ActionName);
            Assert.Equal("Chofer registrado exitosamente", _controller.TempData["Success"]);
        }
        #endregion

        #region Mantenimiento Tests
        [Fact]
        public async Task SolicitudesMantenimiento_ReturnsViewWithSolicitudes_WhenSuccessful()
        {
            // Arrange
            var solicitudes = new List<SolicitudMantenimientoViewModel>();
            _mockAdminService.Setup(s => s.GetSolicitudesMantenimientoPendientesAsync())
                .ReturnsAsync(solicitudes);

            // Act
            var result = await _controller.SolicitudesMantenimiento();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<SolicitudMantenimientoViewModel>>(viewResult.Model);
            Assert.Same(solicitudes, model);
        }

        [Fact]
        public async Task ProcesarSolicitudMantenimiento_RedirectsToSolicitudes_WhenAprobado()
        {
            // Arrange
            int solicitudId = 1;
            string estado = "aprobado";
            string descripcion = "Mantenimiento preventivo";
            decimal costo = 100.0m;

            _mockAdminService.Setup(s => s.ProcesarSolicitudMantenimientoAsync(
                solicitudId, 1, estado, descripcion, costo, "CRC", null))
                .ReturnsAsync(true);
            // Act
            var result = await _controller.ProcesarSolicitudMantenimiento(
                solicitudId, estado, descripcion, costo, "CRC", null);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("SolicitudesMantenimiento", redirectResult.ActionName);
            Assert.Contains("aprobada correctamente", _controller.TempData["SuccessMessage"].ToString());
        }

        [Fact]
        public async Task ProcesarSolicitudMantenimiento_RedirectsToSolicitudes_WhenRechazado()
        {
            // Arrange
            int solicitudId = 1;
            string estado = "rechazado";

            _mockAdminService.Setup(s => s.ProcesarSolicitudMantenimientoAsync(
                solicitudId, 1, estado, null, null, "CRC", null))
                .ReturnsAsync(true);
            // Act
            var result = await _controller.ProcesarSolicitudMantenimiento(
                solicitudId, estado);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("SolicitudesMantenimiento", redirectResult.ActionName);
            Assert.Contains("rechazada correctamente", _controller.TempData["SuccessMessage"].ToString());
        }
        #endregion
    }
}