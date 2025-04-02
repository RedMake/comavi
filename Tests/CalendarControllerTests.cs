using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using COMAVI_SA.Controllers;
using COMAVI_SA.Data;
using COMAVI_SA.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace COMAVI_SA.Tests.Controllers
{
    public class CalendarControllerTests
    {
        private readonly Mock<ComaviDbContext> _mockContext;
        private readonly CalendarController _controller;
        private readonly Mock<DbSet<Choferes>> _mockChoferes;
        private readonly Mock<DbSet<Documentos>> _mockDocumentos;
        private readonly Mock<DbSet<Camiones>> _mockCamiones;
        private readonly Mock<DbSet<Mantenimiento_Camiones>> _mockMantenimientos;

        public CalendarControllerTests()
        {
            // Setup mock context and DbSets
            _mockContext = new Mock<ComaviDbContext>(new DbContextOptions<ComaviDbContext>());
            _mockChoferes = new Mock<DbSet<Choferes>>();
            _mockDocumentos = new Mock<DbSet<Documentos>>();
            _mockCamiones = new Mock<DbSet<Camiones>>();
            _mockMantenimientos = new Mock<DbSet<Mantenimiento_Camiones>>();

            // Setup mock context returns
            _mockContext.Setup(c => c.Choferes).Returns(_mockChoferes.Object);
            _mockContext.Setup(c => c.Documentos).Returns(_mockDocumentos.Object);
            _mockContext.Setup(c => c.Camiones).Returns(_mockCamiones.Object);
            _mockContext.Setup(c => c.Mantenimiento_Camiones).Returns(_mockMantenimientos.Object);

            // Setup controller with authenticated user
            _controller = new CalendarController(_mockContext.Object);
            SetupAuthenticatedUser(_controller);
            SetupTempData(_controller);
        }

        #region Index Tests

        [Fact]
        public async Task Index_ReturnsNotFoundWhenChoferNotFound()
        {
            // Arrange
            const int userId = 1;
            var choferes = new List<Choferes>().AsQueryable();

            _mockChoferes.As<IQueryable<Choferes>>().Setup(m => m.Provider).Returns(choferes.Provider);
            _mockChoferes.As<IQueryable<Choferes>>().Setup(m => m.Expression).Returns(choferes.Expression);
            _mockChoferes.As<IQueryable<Choferes>>().Setup(m => m.ElementType).Returns(choferes.ElementType);
            _mockChoferes.As<IQueryable<Choferes>>().Setup(m => m.GetEnumerator()).Returns(choferes.GetEnumerator());

            SetupUserIdClaim(_controller, userId);

            // Act
            var result = await _controller.Index();

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Profile", redirectResult.ActionName);
            Assert.Equal("Login", redirectResult.ControllerName);
            Assert.Equal("Debe completar su perfil primero para acceder al calendario.", _controller.TempData["Error"]);
        }

        [Fact]
        public async Task Index_ReturnsViewWithEventsWhenChoferExists()
        {
            // Arrange
            const int userId = 1;
            const int choferId = 1;
            var today = DateTime.Now;

            // Create chofer
            var chofer = new Choferes
            {
                id_chofer = choferId,
                id_usuario = userId,
                nombreCompleto = "Test Driver",
                licencia = "ABC123",
                fecha_venc_licencia = today.AddDays(30)
            };

            var choferes = new List<Choferes> { chofer }.AsQueryable();

            _mockChoferes.As<IQueryable<Choferes>>().Setup(m => m.Provider).Returns(choferes.Provider);
            _mockChoferes.As<IQueryable<Choferes>>().Setup(m => m.Expression).Returns(choferes.Expression);
            _mockChoferes.As<IQueryable<Choferes>>().Setup(m => m.ElementType).Returns(choferes.ElementType);
            _mockChoferes.As<IQueryable<Choferes>>().Setup(m => m.GetEnumerator()).Returns(choferes.GetEnumerator());

            // Setup documents
            var documento = new Documentos
            {
                id_documento = 1,
                id_chofer = choferId,
                tipo_documento = "Cédula",
                fecha_emision = today.AddDays(-180),
                fecha_vencimiento = today.AddDays(60)
            };

            var documentos = new List<Documentos> { documento }.AsQueryable();

            _mockDocumentos.As<IQueryable<Documentos>>().Setup(m => m.Provider).Returns(documentos.Provider);
            _mockDocumentos.As<IQueryable<Documentos>>().Setup(m => m.Expression).Returns(documentos.Expression);
            _mockDocumentos.As<IQueryable<Documentos>>().Setup(m => m.ElementType).Returns(documentos.ElementType);
            _mockDocumentos.As<IQueryable<Documentos>>().Setup(m => m.GetEnumerator()).Returns(documentos.GetEnumerator());

            // Setup assigned truck
            var camion = new Camiones
            {
                id_camion = 1,
                marca = "Test",
                modelo = "Truck",
                chofer_asignado = choferId
            };

            var camiones = new List<Camiones> { camion }.AsQueryable();

            _mockCamiones.As<IQueryable<Camiones>>().Setup(m => m.Provider).Returns(camiones.Provider);
            _mockCamiones.As<IQueryable<Camiones>>().Setup(m => m.Expression).Returns(camiones.Expression);
            _mockCamiones.As<IQueryable<Camiones>>().Setup(m => m.ElementType).Returns(camiones.ElementType);
            _mockCamiones.As<IQueryable<Camiones>>().Setup(m => m.GetEnumerator()).Returns(camiones.GetEnumerator());

            // Setup maintenance
            var mantenimiento = new Mantenimiento_Camiones
            {
                id_mantenimiento = 1,
                id_camion = 1,
                descripcion = "Test Maintenance",
                fecha_mantenimiento = today.AddDays(15)
            };

            var mantenimientos = new List<Mantenimiento_Camiones> { mantenimiento }.AsQueryable();

            _mockMantenimientos.As<IQueryable<Mantenimiento_Camiones>>().Setup(m => m.Provider).Returns(mantenimientos.Provider);
            _mockMantenimientos.As<IQueryable<Mantenimiento_Camiones>>().Setup(m => m.Expression).Returns(mantenimientos.Expression);
            _mockMantenimientos.As<IQueryable<Mantenimiento_Camiones>>().Setup(m => m.ElementType).Returns(mantenimientos.ElementType);
            _mockMantenimientos.As<IQueryable<Mantenimiento_Camiones>>().Setup(m => m.GetEnumerator()).Returns(mantenimientos.GetEnumerator());

            SetupUserIdClaim(_controller, userId);

            // Act
            var result = await _controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.NotNull(viewResult.ViewData["Eventos"]);
            Assert.Equal("Test Driver", viewResult.ViewData["NombreConductor"]);
        }

        [Fact]
        public async Task Index_HandlesExceptionCorrectly()
        {
            // Arrange
            _mockContext.Setup(c => c.Choferes).Throws(new Exception("Database error"));
            SetupTempData(_controller);

            // Act
            var result = await _controller.Index();

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Profile", redirectResult.ActionName);
            Assert.Equal("Login", redirectResult.ControllerName);
            Assert.Equal("Error al cargar el calendario.", _controller.TempData["Error"]);
        }

        #endregion

        #region EventosJson Tests

        [Fact]
        public async Task EventosJson_ReturnsEmptyListWhenChoferNotFound()
        {
            // Arrange
            const int userId = 1;
            var choferes = new List<Choferes>().AsQueryable();

            _mockChoferes.As<IQueryable<Choferes>>().Setup(m => m.Provider).Returns(choferes.Provider);
            _mockChoferes.As<IQueryable<Choferes>>().Setup(m => m.Expression).Returns(choferes.Expression);
            _mockChoferes.As<IQueryable<Choferes>>().Setup(m => m.ElementType).Returns(choferes.ElementType);
            _mockChoferes.As<IQueryable<Choferes>>().Setup(m => m.GetEnumerator()).Returns(choferes.GetEnumerator());

            SetupUserIdClaim(_controller, userId);

            // Act
            var result = await _controller.EventosJson();

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            var eventos = Assert.IsAssignableFrom<List<EventoCalendario>>(jsonResult.Value);
            Assert.Empty(eventos);
        }

        [Fact]
        public async Task EventosJson_ReturnsEventsWhenChoferExists()
        {
            // Arrange
            const int userId = 1;
            const int choferId = 1;
            var today = DateTime.Now;

            // Create chofer
            var chofer = new Choferes
            {
                id_chofer = choferId,
                id_usuario = userId,
                nombreCompleto = "Test Driver",
                licencia = "ABC123",
                fecha_venc_licencia = today.AddDays(30)
            };

            var choferes = new List<Choferes> { chofer }.AsQueryable();

            _mockChoferes.As<IQueryable<Choferes>>().Setup(m => m.Provider).Returns(choferes.Provider);
            _mockChoferes.As<IQueryable<Choferes>>().Setup(m => m.Expression).Returns(choferes.Expression);
            _mockChoferes.As<IQueryable<Choferes>>().Setup(m => m.ElementType).Returns(choferes.ElementType);
            _mockChoferes.As<IQueryable<Choferes>>().Setup(m => m.GetEnumerator()).Returns(choferes.GetEnumerator());

            // Setup documents
            var documento = new Documentos
            {
                id_documento = 1,
                id_chofer = choferId,
                tipo_documento = "Cédula",
                fecha_emision = today.AddDays(-180),
                fecha_vencimiento = today.AddDays(60)
            };

            var documentos = new List<Documentos> { documento }.AsQueryable();

            _mockDocumentos.As<IQueryable<Documentos>>().Setup(m => m.Provider).Returns(documentos.Provider);
            _mockDocumentos.As<IQueryable<Documentos>>().Setup(m => m.Expression).Returns(documentos.Expression);
            _mockDocumentos.As<IQueryable<Documentos>>().Setup(m => m.ElementType).Returns(documentos.ElementType);
            _mockDocumentos.As<IQueryable<Documentos>>().Setup(m => m.GetEnumerator()).Returns(documentos.GetEnumerator());

            // Setup assigned truck without maintenance
            var camion = new Camiones
            {
                id_camion = 1,
                marca = "Test",
                modelo = "Truck",
                chofer_asignado = choferId
            };

            var camiones = new List<Camiones> { camion }.AsQueryable();

            _mockCamiones.As<IQueryable<Camiones>>().Setup(m => m.Provider).Returns(camiones.Provider);
            _mockCamiones.As<IQueryable<Camiones>>().Setup(m => m.Expression).Returns(camiones.Expression);
            _mockCamiones.As<IQueryable<Camiones>>().Setup(m => m.ElementType).Returns(camiones.ElementType);
            _mockCamiones.As<IQueryable<Camiones>>().Setup(m => m.GetEnumerator()).Returns(camiones.GetEnumerator());

            // Setup empty maintenance
            var mantenimientos = new List<Mantenimiento_Camiones>().AsQueryable();

            _mockMantenimientos.As<IQueryable<Mantenimiento_Camiones>>().Setup(m => m.Provider).Returns(mantenimientos.Provider);
            _mockMantenimientos.As<IQueryable<Mantenimiento_Camiones>>().Setup(m => m.Expression).Returns(mantenimientos.Expression);
            _mockMantenimientos.As<IQueryable<Mantenimiento_Camiones>>().Setup(m => m.ElementType).Returns(mantenimientos.ElementType);
            _mockMantenimientos.As<IQueryable<Mantenimiento_Camiones>>().Setup(m => m.GetEnumerator()).Returns(mantenimientos.GetEnumerator());

            SetupUserIdClaim(_controller, userId);

            // Act
            var result = await _controller.EventosJson();

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            var eventos = Assert.IsAssignableFrom<List<EventoCalendario>>(jsonResult.Value);

            // Should have 2 events: 1 for license and 1 for document
            Assert.Equal(2, eventos.Count);

            // Verify license event
            var licenseEvent = eventos.FirstOrDefault(e => e.id.StartsWith("lic-"));
            Assert.NotNull(licenseEvent);
            Assert.Contains("Licencia", licenseEvent.title);

            // Verify document event
            var docEvent = eventos.FirstOrDefault(e => e.id.StartsWith("doc-"));
            Assert.NotNull(docEvent);
            Assert.Contains("Cédula", docEvent.title);
        }

        [Fact]
        public async Task EventosJson_HandlesExceptionCorrectly()
        {
            // Arrange
            _mockContext.Setup(c => c.Choferes).Throws(new Exception("Database error"));

            // Act
            var result = await _controller.EventosJson();

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            Assert.Contains("error", jsonResult.Value.ToString());
        }

        #endregion

        #region GetEventClass Tests

        [Fact]
        public void GetEventClass_ReturnsCorrectClassBasedOnDate()
        {
            // Using reflection to test private method
            var methodInfo = typeof(CalendarController).GetMethod("GetEventClass",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Today
            var today = DateTime.Now;

            // Test expired (negative days)
            var expired = today.AddDays(-10);
            var expiredResult = methodInfo.Invoke(_controller, new object[] { expired });
            Assert.Equal("bg-danger", expiredResult);

            // Test expiring soon (15 days)
            var expiringSoon = today.AddDays(15);
            var expiringSoonResult = methodInfo.Invoke(_controller, new object[] { expiringSoon });
            Assert.Equal("bg-warning", expiringSoonResult);

            // Test valid (60 days)
            var valid = today.AddDays(60);
            var validResult = methodInfo.Invoke(_controller, new object[] { valid });
            Assert.Equal("bg-success", validResult);
        }

        #endregion

        #region Helper Methods

        private void SetupAuthenticatedUser(CalendarController controller)
        {
            var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
            {
                new Claim(ClaimTypes.NameIdentifier, "1"),
                new Claim(ClaimTypes.Name, "testuser@example.com"),
                new Claim(ClaimTypes.Role, "user")
            }, "mock"));

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };
        }

        private void SetupUserIdClaim(CalendarController controller, int userId)
        {
            var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Name, "testuser@example.com"),
                new Claim(ClaimTypes.Role, "user")
            }, "mock"));

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };
        }

        private void SetupTempData(CalendarController controller)
        {
            controller.TempData = new TempDataDictionary(
                new DefaultHttpContext(),
                Mock.Of<ITempDataProvider>());
        }

        #endregion
    }
}