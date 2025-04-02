using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using COMAVI_SA.Data;
using COMAVI_SA.Models;
using COMAVI_SA.Services;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace COMAVI_SA.Tests.Services
{
    public class NotificationServiceTests
    {
        private readonly Mock<ComaviDbContext> _mockContext;
        private readonly Mock<IEmailService> _mockEmailService;
        private readonly NotificationService _notificationService;
        private readonly Mock<DbSet<Choferes>> _mockChoferes;
        private readonly Mock<DbSet<PreferenciasNotificacion>> _mockPreferencias;
        private readonly Mock<DbSet<Documentos>> _mockDocumentos;
        private readonly Mock<DbSet<Notificaciones_Usuario>> _mockNotificaciones;

        public NotificationServiceTests()
        {
            _mockContext = new Mock<ComaviDbContext>(new DbContextOptions<ComaviDbContext>());
            _mockEmailService = new Mock<IEmailService>();

            _mockChoferes = new Mock<DbSet<Choferes>>();
            _mockPreferencias = new Mock<DbSet<PreferenciasNotificacion>>();
            _mockDocumentos = new Mock<DbSet<Documentos>>();
            _mockNotificaciones = new Mock<DbSet<Notificaciones_Usuario>>();

            _mockContext.Setup(c => c.Choferes).Returns(_mockChoferes.Object);
            _mockContext.Setup(c => c.PreferenciasNotificacion).Returns(_mockPreferencias.Object);
            _mockContext.Setup(c => c.Documentos).Returns(_mockDocumentos.Object);
            _mockContext.Setup(c => c.NotificacionesUsuario).Returns(_mockNotificaciones.Object);

            _notificationService = new NotificationService(_mockContext.Object, _mockEmailService.Object);
        }

        #region SendExpirationNotificationsAsync Tests

        [Fact]
        public async Task SendExpirationNotificationsAsync_ProcessesNoChoferesWhenNoneExist()
        {
            // Arrange
            var choferes = new List<Choferes>().AsQueryable();

            _mockChoferes.As<IQueryable<Choferes>>().Setup(m => m.Provider).Returns(choferes.Provider);
            _mockChoferes.As<IQueryable<Choferes>>().Setup(m => m.Expression).Returns(choferes.Expression);
            _mockChoferes.As<IQueryable<Choferes>>().Setup(m => m.ElementType).Returns(choferes.ElementType);
            _mockChoferes.As<IQueryable<Choferes>>().Setup(m => m.GetEnumerator()).Returns(choferes.GetEnumerator());

            // Act
            await _notificationService.SendExpirationNotificationsAsync();

            // Assert - No exceptions should be thrown
            // We're just verifying the method completes without errors when no choferes exist
        }

        [Fact]
        public async Task SendExpirationNotificationsAsync_ChecksLicenseExpirationForActiveChoferes()
        {
            // Arrange
            const int userId = 1;
            var today = DateTime.Now;

            // Create chofer with license expiring in 10 days
            var chofer = new Choferes
            {
                id_chofer = 1,
                id_usuario = userId,
                estado = "activo",
                nombreCompleto = "Test Driver",
                licencia = "ABC123",
                fecha_venc_licencia = today.AddDays(10),
                Usuario = new Usuario
                {
                    id_usuario = userId,
                    nombre_usuario = "Test User",
                    correo_electronico = "test@example.com"
                }
            };

            var choferes = new List<Choferes> { chofer }.AsQueryable();

            _mockChoferes.As<IQueryable<Choferes>>().Setup(m => m.Provider).Returns(choferes.Provider);
            _mockChoferes.As<IQueryable<Choferes>>().Setup(m => m.Expression).Returns(choferes.Expression);
            _mockChoferes.As<IQueryable<Choferes>>().Setup(m => m.ElementType).Returns(choferes.ElementType);
            _mockChoferes.As<IQueryable<Choferes>>().Setup(m => m.GetEnumerator()).Returns(choferes.GetEnumerator());

            // User preferences - 15 days notification window
            var preferencias = new PreferenciasNotificacion
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
            )).ReturnsAsync(preferencias);

            // Setup system notification
            _mockContext.Setup(c => c.NotificacionesUsuario.Add(It.IsAny<Notificaciones_Usuario>())).Verifiable();
            _mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            // Setup email notification
            _mockEmailService.Setup(e => e.SendEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()
            )).Returns(Task.CompletedTask);

            // Act
            await _notificationService.SendExpirationNotificationsAsync();

            // Assert
            _mockContext.Verify(c => c.NotificacionesUsuario.Add(It.Is<Notificaciones_Usuario>(n =>
                n.id_usuario == userId &&
                n.tipo_notificacion == "Licencia Próxima a Vencer")),
                Times.Once);

            _mockEmailService.Verify(e => e.SendEmailAsync(
                It.Is<string>(email => email == "test@example.com"),
                It.IsAny<string>(),
                It.IsAny<string>()),
                Times.Once);

            _mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task SendExpirationNotificationsAsync_ChecksDocumentExpirationForActiveChoferes()
        {
            // Arrange
            const int userId = 1;
            const int choferId = 1;
            var today = DateTime.Now;

            // Create chofer with unexpired license
            var chofer = new Choferes
            {
                id_chofer = choferId,
                id_usuario = userId,
                estado = "activo",
                nombreCompleto = "Test Driver",
                licencia = "ABC123",
                fecha_venc_licencia = today.AddDays(40), // Not expiring soon
                Usuario = new Usuario
                {
                    id_usuario = userId,
                    nombre_usuario = "Test User",
                    correo_electronico = "test@example.com"
                }
            };

            var choferes = new List<Choferes> { chofer }.AsQueryable();

            _mockChoferes.As<IQueryable<Choferes>>().Setup(m => m.Provider).Returns(choferes.Provider);
            _mockChoferes.As<IQueryable<Choferes>>().Setup(m => m.Expression).Returns(choferes.Expression);
            _mockChoferes.As<IQueryable<Choferes>>().Setup(m => m.ElementType).Returns(choferes.ElementType);
            _mockChoferes.As<IQueryable<Choferes>>().Setup(m => m.GetEnumerator()).Returns(choferes.GetEnumerator());

            // User preferences - 15 days notification window
            var preferencias = new PreferenciasNotificacion
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
            )).ReturnsAsync(preferencias);

            // Document expiring in 5 days
            var documento = new Documentos
            {
                id_documento = 1,
                id_chofer = choferId,
                tipo_documento = "Cédula",
                fecha_emision = today.AddDays(-360),
                fecha_vencimiento = today.AddDays(5),
                estado_validacion = "verificado"
            };

            var documentos = new List<Documentos> { documento }.AsQueryable();

            _mockDocumentos.As<IQueryable<Documentos>>().Setup(m => m.Provider).Returns(documentos.Provider);
            _mockDocumentos.As<IQueryable<Documentos>>().Setup(m => m.Expression).Returns(documentos.Expression);
            _mockDocumentos.As<IQueryable<Documentos>>().Setup(m => m.ElementType).Returns(documentos.ElementType);
            _mockDocumentos.As<IQueryable<Documentos>>().Setup(m => m.GetEnumerator()).Returns(documentos.GetEnumerator());

            // Setup system notification
            _mockContext.Setup(c => c.NotificacionesUsuario.Add(It.IsAny<Notificaciones_Usuario>())).Verifiable();
            _mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            // Setup email notification
            _mockEmailService.Setup(e => e.SendEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()
            )).Returns(Task.CompletedTask);

            // Act
            await _notificationService.SendExpirationNotificationsAsync();

            // Assert
            _mockContext.Verify(c => c.NotificacionesUsuario.Add(It.Is<Notificaciones_Usuario>(n =>
                n.id_usuario == userId &&
                n.tipo_notificacion == "Documento Próximo a Vencer")),
                Times.Once);

            _mockEmailService.Verify(e => e.SendEmailAsync(
                It.Is<string>(email => email == "test@example.com"),
                It.IsAny<string>(),
                It.IsAny<string>()),
                Times.Once);

            _mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task SendExpirationNotificationsAsync_RespectsUserNotificationPreferences()
        {
            // Arrange
            const int userId = 1;
            const int choferId = 1;
            var today = DateTime.Now;

            // Create chofer with license expiring in 5 days
            var chofer = new Choferes
            {
                id_chofer = choferId,
                id_usuario = userId,
                estado = "activo",
                nombreCompleto = "Test Driver",
                licencia = "ABC123",
                fecha_venc_licencia = today.AddDays(5),
                Usuario = new Usuario
                {
                    id_usuario = userId,
                    nombre_usuario = "Test User",
                    correo_electronico = "test@example.com"
                }
            };

            var choferes = new List<Choferes> { chofer }.AsQueryable();

            _mockChoferes.As<IQueryable<Choferes>>().Setup(m => m.Provider).Returns(choferes.Provider);
            _mockChoferes.As<IQueryable<Choferes>>().Setup(m => m.Expression).Returns(choferes.Expression);
            _mockChoferes.As<IQueryable<Choferes>>().Setup(m => m.ElementType).Returns(choferes.ElementType);
            _mockChoferes.As<IQueryable<Choferes>>().Setup(m => m.GetEnumerator()).Returns(choferes.GetEnumerator());

            // User preferences - License notifications disabled, no email notifications
            var preferencias = new PreferenciasNotificacion
            {
                id_preferencia = 1,
                id_usuario = userId,
                notificar_por_correo = false, // No email
                dias_anticipacion = 15,
                notificar_vencimiento_licencia = false, // No license notifications
                notificar_vencimiento_documentos = true
            };

            _mockContext.Setup(c => c.PreferenciasNotificacion.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<PreferenciasNotificacion, bool>>>(),
                It.IsAny<CancellationToken>()
            )).ReturnsAsync(preferencias);

            // Document expiring in 5 days
            var documento = new Documentos
            {
                id_documento = 1,
                id_chofer = choferId,
                tipo_documento = "Cédula",
                fecha_emision = today.AddDays(-360),
                fecha_vencimiento = today.AddDays(5),
                estado_validacion = "verificado"
            };

            var documentos = new List<Documentos> { documento }.AsQueryable();

            _mockDocumentos.As<IQueryable<Documentos>>().Setup(m => m.Provider).Returns(documentos.Provider);
            _mockDocumentos.As<IQueryable<Documentos>>().Setup(m => m.Expression).Returns(documentos.Expression);
            _mockDocumentos.As<IQueryable<Documentos>>().Setup(m => m.ElementType).Returns(documentos.ElementType);
            _mockDocumentos.As<IQueryable<Documentos>>().Setup(m => m.GetEnumerator()).Returns(documentos.GetEnumerator());

            // Setup system notification
            _mockContext.Setup(c => c.NotificacionesUsuario.Add(It.IsAny<Notificaciones_Usuario>())).Verifiable();
            _mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            // Act
            await _notificationService.SendExpirationNotificationsAsync();

            // Assert

            // Should NOT get license notification (disabled)
            _mockContext.Verify(c => c.NotificacionesUsuario.Add(It.Is<Notificaciones_Usuario>(n =>
                n.id_usuario == userId &&
                n.tipo_notificacion.Contains("Licencia"))),
                Times.Never);

            // Should get document notification (enabled)
            _mockContext.Verify(c => c.NotificacionesUsuario.Add(It.Is<Notificaciones_Usuario>(n =>
                n.id_usuario == userId &&
                n.tipo_notificacion.Contains("Documento"))),
                Times.Once);

            // Should NOT get any email (disabled)
            _mockEmailService.Verify(e => e.SendEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()),
                Times.Never);

            _mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }

        #endregion

        #region CreateNotificationAsync Tests

        [Fact]
        public async Task CreateNotificationAsync_CreatesNewNotification()
        {
            // Arrange
            const int userId = 1;
            const string type = "Test Notification";
            const string message = "This is a test notification";

            _mockContext.Setup(c => c.NotificacionesUsuario.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<Notificaciones_Usuario, bool>>>(),
                It.IsAny<CancellationToken>()
            )).ReturnsAsync((Notificaciones_Usuario)null);

            _mockContext.Setup(c => c.NotificacionesUsuario.Add(It.IsAny<Notificaciones_Usuario>())).Verifiable();
            _mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            // Act
            await _notificationService.CreateNotificationAsync(userId, type, message);

            // Assert
            _mockContext.Verify(c => c.NotificacionesUsuario.Add(It.Is<Notificaciones_Usuario>(n =>
                n.id_usuario == userId &&
                n.tipo_notificacion == type &&
                n.mensaje == message &&
                n.leida == false)),
                Times.Once);

            _mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CreateNotificationAsync_UpdatesExistingUnreadNotification()
        {
            // Arrange
            const int userId = 1;
            const string type = "Test Notification";
            const string message = "This is a test notification";

            var existingNotification = new Notificaciones_Usuario
            {
                id_notificacion = 1,
                id_usuario = userId,
                tipo_notificacion = type,
                mensaje = message,
                fecha_hora = DateTime.Now.AddHours(-1),
                leida = false
            };

            _mockContext.Setup(c => c.NotificacionesUsuario.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<Notificaciones_Usuario, bool>>>(),
                It.IsAny<CancellationToken>()
            )).ReturnsAsync(existingNotification);

            _mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            // Act
            await _notificationService.CreateNotificationAsync(userId, type, message);

            // Assert
            // Should not add a new notification
            _mockContext.Verify(c => c.NotificacionesUsuario.Add(It.IsAny<Notificaciones_Usuario>()), Times.Never);

            // Should update the timestamp of existing notification
            Assert.True(existingNotification.fecha_hora > DateTime.Now.AddMinutes(-1));

            _mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CreateNotificationAsync_HandlesExceptionGracefully()
        {
            // Arrange
            const int userId = 1;
            const string type = "Test Notification";
            const string message = "This is a test notification";

            _mockContext.Setup(c => c.NotificacionesUsuario.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<Notificaciones_Usuario, bool>>>(),
                It.IsAny<CancellationToken>()
            )).ThrowsAsync(new Exception("Database error"));

            // Act & Assert
            // Should not throw exception
            await _notificationService.CreateNotificationAsync(userId, type, message);
        }

        #endregion
    }
}