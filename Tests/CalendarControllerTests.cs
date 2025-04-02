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