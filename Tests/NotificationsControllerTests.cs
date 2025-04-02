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
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace COMAVI_SA.Tests.Controllers
{
    public class NotificationsControllerTests
    {
        private readonly Mock<ComaviDbContext> _mockContext;
        private readonly Mock<ILogger<NotificationsController>> _mockLogger;
        private readonly NotificationsController _controller;
        private readonly Mock<DbSet<Notificaciones_Usuario>> _mockNotificaciones;
        private readonly Mock<DbSet<PreferenciasNotificacion>> _mockPreferencias;

        public NotificationsControllerTests()
        {
            // Setup mock context and DbSets
            _mockContext = new Mock<ComaviDbContext>(new DbContextOptions<ComaviDbContext>());
            _mockLogger = new Mock<ILogger<NotificationsController>>();
            _mockNotificaciones = new Mock<DbSet<Notificaciones_Usuario>>();
            _mockPreferencias = new Mock<DbSet<PreferenciasNotificacion>>();

            // Setup controller with authenticated user
            _controller = new NotificationsController(_mockContext.Object, _mockLogger.Object);
            SetupAuthenticatedUser(_controller);
            SetupTempData(_controller);
        }

        #region Index Tests

       

        [Fact]
        public async Task Index_HandlesExceptionCorrectly()
        {
            // Arrange
            _mockContext.Setup(c => c.NotificacionesUsuario).Throws(new Exception("Database error"));
            SetupTempData(_controller);

            // Act
            var result = await _controller.Index();

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Profile", redirectResult.ActionName);
            Assert.Equal("Login", redirectResult.ControllerName);
            Assert.Equal("Error al cargar las notificaciones.", _controller.TempData["Error"]);
        }

        #endregion

        #region ObtenerPagina Tests

        [Fact]
        public async Task ObtenerPagina_ReturnsErrorJsonWhenUserNotAuthenticated()
        {
            // Arrange
            var controller = new NotificationsController(_mockContext.Object, _mockLogger.Object);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() }
            };

            // Act
            var result = await controller.ObtenerPagina();

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            dynamic jsonData = jsonResult.Value;
            Assert.False((bool)jsonData.success);
            Assert.Equal("Error al cargar notificaciones.", (string)jsonData.message);

        } 

        #endregion
 

        #region Helper Methods

        private void SetupAuthenticatedUser(NotificationsController controller)
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

        private void SetupUserIdClaim(NotificationsController controller, int userId)
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

        private void SetupTempData(NotificationsController controller)
        {
            controller.TempData = new TempDataDictionary(
                new DefaultHttpContext(),
                Mock.Of<ITempDataProvider>());
        }

        #endregion
    }
}