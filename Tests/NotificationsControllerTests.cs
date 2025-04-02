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
        public async Task Index_ReturnsViewWithNotificationsAndPreferences()
        {
            // Arrange
            const int userId = 1;

            var notificaciones = new List<Notificaciones_Usuario>
            {
                new Notificaciones_Usuario { id_notificacion = 1, id_usuario = userId, mensaje = "Test Notification 1" },
                new Notificaciones_Usuario { id_notificacion = 2, id_usuario = userId, mensaje = "Test Notification 2" }
            }.AsQueryable();

            var preferencias = new List<PreferenciasNotificacion>
            {
                new PreferenciasNotificacion
                {
                    id_preferencia = 1,
                    id_usuario = userId,
                    notificar_por_correo = true,
                    dias_anticipacion = 15,
                    notificar_vencimiento_licencia = true,
                    notificar_vencimiento_documentos = true
                }
            }.AsQueryable();

            _mockNotificaciones.As<IQueryable<Notificaciones_Usuario>>().Setup(m => m.Provider).Returns(notificaciones.Provider);
            _mockNotificaciones.As<IQueryable<Notificaciones_Usuario>>().Setup(m => m.Expression).Returns(notificaciones.Expression);
            _mockNotificaciones.As<IQueryable<Notificaciones_Usuario>>().Setup(m => m.ElementType).Returns(notificaciones.ElementType);
            _mockNotificaciones.As<IQueryable<Notificaciones_Usuario>>().Setup(m => m.GetEnumerator()).Returns(notificaciones.GetEnumerator());

            _mockPreferencias.As<IQueryable<PreferenciasNotificacion>>().Setup(m => m.Provider).Returns(preferencias.Provider);
            _mockPreferencias.As<IQueryable<PreferenciasNotificacion>>().Setup(m => m.Expression).Returns(preferencias.Expression);
            _mockPreferencias.As<IQueryable<PreferenciasNotificacion>>().Setup(m => m.ElementType).Returns(preferencias.ElementType);
            _mockPreferencias.As<IQueryable<PreferenciasNotificacion>>().Setup(m => m.GetEnumerator()).Returns(preferencias.GetEnumerator());

            _mockContext.Setup(c => c.NotificacionesUsuario).Returns(_mockNotificaciones.Object);
            _mockContext.Setup(c => c.PreferenciasNotificacion).Returns(_mockPreferencias.Object);

            SetupUserIdClaim(_controller, userId);

            // Act
            var result = await _controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<NotificacionesViewModel>(viewResult.Model);
            Assert.Equal(2, model.Notificaciones.Count);
            Assert.NotNull(model.Preferencias);
            Assert.Equal(15, model.Preferencias.dias_anticipacion);
        }

        [Fact]
        public async Task Index_CreatesDefaultPreferencesWhenNotExist()
        {
            // Arrange
            const int userId = 1;

            var notificaciones = new List<Notificaciones_Usuario>
            {
                new Notificaciones_Usuario { id_notificacion = 1, id_usuario = userId, mensaje = "Test Notification 1" }
            }.AsQueryable();

            var preferencias = new List<PreferenciasNotificacion>().AsQueryable();

            _mockNotificaciones.As<IQueryable<Notificaciones_Usuario>>().Setup(m => m.Provider).Returns(notificaciones.Provider);
            _mockNotificaciones.As<IQueryable<Notificaciones_Usuario>>().Setup(m => m.Expression).Returns(notificaciones.Expression);
            _mockNotificaciones.As<IQueryable<Notificaciones_Usuario>>().Setup(m => m.ElementType).Returns(notificaciones.ElementType);
            _mockNotificaciones.As<IQueryable<Notificaciones_Usuario>>().Setup(m => m.GetEnumerator()).Returns(notificaciones.GetEnumerator());

            _mockPreferencias.As<IQueryable<PreferenciasNotificacion>>().Setup(m => m.Provider).Returns(preferencias.Provider);
            _mockPreferencias.As<IQueryable<PreferenciasNotificacion>>().Setup(m => m.Expression).Returns(preferencias.Expression);
            _mockPreferencias.As<IQueryable<PreferenciasNotificacion>>().Setup(m => m.ElementType).Returns(preferencias.ElementType);
            _mockPreferencias.As<IQueryable<PreferenciasNotificacion>>().Setup(m => m.GetEnumerator()).Returns(preferencias.GetEnumerator());

            _mockContext.Setup(c => c.NotificacionesUsuario).Returns(_mockNotificaciones.Object);
            _mockContext.Setup(c => c.PreferenciasNotificacion).Returns(_mockPreferencias.Object);
            _mockContext.Setup(c => c.PreferenciasNotificacion.Add(It.IsAny<PreferenciasNotificacion>())).Verifiable();
            _mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            SetupUserIdClaim(_controller, userId);

            // Act
            var result = await _controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<NotificacionesViewModel>(viewResult.Model);
            Assert.Equal(1, model.Notificaciones.Count);
            Assert.NotNull(model.Preferencias);
            _mockContext.Verify(c => c.PreferenciasNotificacion.Add(It.IsAny<PreferenciasNotificacion>()), Times.Once);
            _mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

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

        [Fact]
        public async Task ObtenerPagina_ReturnsNotificationsWithPagination()
        {
            // Arrange
            const int userId = 1;
            const int pagina = 1;
            const int elementosPorPagina = 5;

            // Create 10 notifications
            var notificaciones = Enumerable.Range(1, 10)
                .Select(i => new Notificaciones_Usuario
                {
                    id_notificacion = i,
                    id_usuario = userId,
                    mensaje = $"Test Notification {i}",
                    tipo_notificacion = "Test",
                    fecha_hora = DateTime.Now.AddDays(-i),
                    leida = i % 2 == 0 // alternating read/unread
                })
                .AsQueryable();

            _mockNotificaciones.As<IQueryable<Notificaciones_Usuario>>().Setup(m => m.Provider).Returns(notificaciones.Provider);
            _mockNotificaciones.As<IQueryable<Notificaciones_Usuario>>().Setup(m => m.Expression).Returns(notificaciones.Expression);
            _mockNotificaciones.As<IQueryable<Notificaciones_Usuario>>().Setup(m => m.ElementType).Returns(notificaciones.ElementType);
            _mockNotificaciones.As<IQueryable<Notificaciones_Usuario>>().Setup(m => m.GetEnumerator()).Returns(notificaciones.GetEnumerator());

            _mockContext.Setup(c => c.NotificacionesUsuario).Returns(_mockNotificaciones.Object);

            // Mock Count() using extension method
            _mockContext.Setup(c => c.NotificacionesUsuario.CountAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<Notificaciones_Usuario, bool>>>(),
                It.IsAny<CancellationToken>()
            )).ReturnsAsync(10);

            SetupUserIdClaim(_controller, userId);

            // Act
            var result = await _controller.ObtenerPagina(pagina, elementosPorPagina);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            dynamic jsonData = jsonResult.Value;
            Assert.True((bool)jsonData.success);
            Assert.Equal(5, jsonData.notifications.Count);
            Assert.Equal(1, (int)jsonData.currentPage);
            Assert.Equal(2, (int)jsonData.totalPages);
            Assert.Equal(10, (int)jsonData.totalItems);
        }

        #endregion

        #region GuardarPreferencias Tests

        [Fact]
        public async Task GuardarPreferencias_UpdatesExistingPreferences()
        {
            // Arrange
            const int userId = 1;
            var model = new PreferenciasNotificacion
            {
                id_preferencia = 1,
                id_usuario = userId,
                notificar_por_correo = false,
                dias_anticipacion = 7,
                notificar_vencimiento_licencia = true,
                notificar_vencimiento_documentos = false
            };

            var existingPreferences = new PreferenciasNotificacion
            {
                id_preferencia = 1,
                id_usuario = userId,
                notificar_por_correo = true,
                dias_anticipacion = 15,
                notificar_vencimiento_licencia = true,
                notificar_vencimiento_documentos = true
            };

            _mockContext.Setup(c => c.PreferenciasNotificacion.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<PreferenciasNotificacion, bool>>>(),
                It.IsAny<CancellationToken>()
            )).ReturnsAsync(existingPreferences);

            _mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            SetupUserIdClaim(_controller, userId);
            SetupTempData(_controller);

            // Act
            var result = await _controller.GuardarPreferencias(model);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirectResult.ActionName);
            Assert.Equal("Preferencias de notificación guardadas correctamente.", _controller.TempData["SuccessMessage"]);

            // Verify properties were updated
            Assert.Equal(false, existingPreferences.notificar_por_correo);
            Assert.Equal(7, existingPreferences.dias_anticipacion);
            Assert.Equal(false, existingPreferences.notificar_vencimiento_documentos);
        }

        [Fact]
        public async Task GuardarPreferencias_CreatesNewPreferencesWhenNotExist()
        {
            // Arrange
            const int userId = 1;
            var model = new PreferenciasNotificacion
            {
                notificar_por_correo = false,
                dias_anticipacion = 7,
                notificar_vencimiento_licencia = true,
                notificar_vencimiento_documentos = false
            };

            _mockContext.Setup(c => c.PreferenciasNotificacion.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<PreferenciasNotificacion, bool>>>(),
                It.IsAny<CancellationToken>()
            )).ReturnsAsync((PreferenciasNotificacion)null);

            _mockContext.Setup(c => c.PreferenciasNotificacion.Add(It.IsAny<PreferenciasNotificacion>())).Verifiable();
            _mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            SetupUserIdClaim(_controller, userId);
            SetupTempData(_controller);

            // Act
            var result = await _controller.GuardarPreferencias(model);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirectResult.ActionName);
            Assert.Equal("Preferencias de notificación guardadas correctamente.", _controller.TempData["SuccessMessage"]);

            _mockContext.Verify(c => c.PreferenciasNotificacion.Add(It.IsAny<PreferenciasNotificacion>()), Times.Once);
        }

        #endregion

        #region MarcarLeida Tests

        [Fact]
        public async Task MarcarLeida_ReturnsNotFoundWhenNotificationNotExist()
        {
            // Arrange
            const int userId = 1;
            const int notificationId = 99;

            _mockContext.Setup(c => c.NotificacionesUsuario.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<Notificaciones_Usuario, bool>>>(),
                It.IsAny<CancellationToken>()
            )).ReturnsAsync((Notificaciones_Usuario)null);

            SetupUserIdClaim(_controller, userId);

            // Act
            var result = await _controller.MarcarLeida(notificationId);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task MarcarLeida_MarksNotificationAsReadAndReturnSuccess()
        {
            // Arrange
            const int userId = 1;
            const int notificationId = 1;

            var notification = new Notificaciones_Usuario
            {
                id_notificacion = notificationId,
                id_usuario = userId,
                mensaje = "Test Notification",
                leida = false
            };

            _mockContext.Setup(c => c.NotificacionesUsuario.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<Notificaciones_Usuario, bool>>>(),
                It.IsAny<CancellationToken>()
            )).ReturnsAsync(notification);

            _mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            SetupUserIdClaim(_controller, userId);

            // Act
            var result = await _controller.MarcarLeida(notificationId);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            dynamic jsonData = jsonResult.Value;
            Assert.True((bool)jsonData.success);
            Assert.True(notification.leida);
            _mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region EliminarNotificacion Tests

        [Fact]
        public async Task EliminarNotificacion_ReturnsNotFoundWhenNotificationNotExist()
        {
            // Arrange
            const int userId = 1;
            const int notificationId = 99;

            _mockContext.Setup(c => c.NotificacionesUsuario.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<Notificaciones_Usuario, bool>>>(),
                It.IsAny<CancellationToken>()
            )).ReturnsAsync((Notificaciones_Usuario)null);

            SetupUserIdClaim(_controller, userId);

            // Act
            var result = await _controller.EliminarNotificacion(notificationId);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task EliminarNotificacion_RemovesNotificationAndReturnSuccess()
        {
            // Arrange
            const int userId = 1;
            const int notificationId = 1;

            var notification = new Notificaciones_Usuario
            {
                id_notificacion = notificationId,
                id_usuario = userId,
                mensaje = "Test Notification"
            };

            _mockContext.Setup(c => c.NotificacionesUsuario.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<Notificaciones_Usuario, bool>>>(),
                It.IsAny<CancellationToken>()
            )).ReturnsAsync(notification);

            _mockContext.Setup(c => c.NotificacionesUsuario.Remove(It.IsAny<Notificaciones_Usuario>())).Verifiable();
            _mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            SetupUserIdClaim(_controller, userId);

            // Act
            var result = await _controller.EliminarNotificacion(notificationId);

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            dynamic jsonData = jsonResult.Value;
            Assert.True((bool)jsonData.success);
            _mockContext.Verify(c => c.NotificacionesUsuario.Remove(It.IsAny<Notificaciones_Usuario>()), Times.Once);
            _mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region ObtenerNotificacionesNoLeidas Tests

        [Fact]
        public async Task ObtenerNotificacionesNoLeidas_ReturnsErrorJsonWhenUserNotAuthenticated()
        {
            // Arrange
            var controller = new NotificationsController(_mockContext.Object, _mockLogger.Object);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() }
            };

            // Act
            var result = await controller.ObtenerNotificacionesNoLeidas();

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            dynamic jsonData = jsonResult.Value;
            Assert.False((bool)jsonData.success);
            Assert.Equal("Usuario no autenticado.", (string)jsonData.message);
        }

        [Fact]
        public async Task ObtenerNotificacionesNoLeidas_ReturnsUnreadNotifications()
        {
            // Arrange
            const int userId = 1;

            var notificaciones = new List<Notificaciones_Usuario>
            {
                new Notificaciones_Usuario
                {
                    id_notificacion = 1,
                    id_usuario = userId,
                    mensaje = "Unread Notification 1",
                    tipo_notificacion = "Test",
                    fecha_hora = DateTime.Now.AddHours(-1),
                    leida = false
                },
                new Notificaciones_Usuario
                {
                    id_notificacion = 2,
                    id_usuario = userId,
                    mensaje = "Unread Notification 2",
                    tipo_notificacion = "Test",
                    fecha_hora = DateTime.Now.AddHours(-2),
                    leida = null // null should be treated as unread
                },
                new Notificaciones_Usuario
                {
                    id_notificacion = 3,
                    id_usuario = userId,
                    mensaje = "Read Notification",
                    tipo_notificacion = "Test",
                    fecha_hora = DateTime.Now.AddHours(-3),
                    leida = true // this should be excluded
                }
            }.AsQueryable();

            _mockNotificaciones.As<IQueryable<Notificaciones_Usuario>>().Setup(m => m.Provider).Returns(notificaciones.Provider);
            _mockNotificaciones.As<IQueryable<Notificaciones_Usuario>>().Setup(m => m.Expression).Returns(notificaciones.Expression);
            _mockNotificaciones.As<IQueryable<Notificaciones_Usuario>>().Setup(m => m.ElementType).Returns(notificaciones.ElementType);
            _mockNotificaciones.As<IQueryable<Notificaciones_Usuario>>().Setup(m => m.GetEnumerator()).Returns(notificaciones.GetEnumerator());

            _mockContext.Setup(c => c.NotificacionesUsuario).Returns(_mockNotificaciones.Object);

            SetupUserIdClaim(_controller, userId);

            // Act
            var result = await _controller.ObtenerNotificacionesNoLeidas();

            // Assert
            var jsonResult = Assert.IsType<JsonResult>(result);
            dynamic jsonData = jsonResult.Value;
            Assert.True((bool)jsonData.success);
            Assert.Equal(2, (int)jsonData.count);
            Assert.Equal(2, jsonData.notifications.Count);
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