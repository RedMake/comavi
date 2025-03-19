using COMAVI_SA.Data;
using COMAVI_SA.Models;
using COMAVI_SA.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace COMAVIxUnitTest
{
    public class NotificationServiceTests
    {
        private readonly Mock<IEmailService> _mockEmailService;
        private readonly Mock<ILogger<NotificationService>> _mockLogger;
        private readonly ComaviDbContext _dbContext;
        private readonly NotificationService _notificationService;

        public NotificationServiceTests()
        {
            // Configurar DbContext en memoria
            var options = new DbContextOptionsBuilder<ComaviDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _dbContext = new ComaviDbContext(options);
            _mockEmailService = new Mock<IEmailService>();
            _mockLogger = new Mock<ILogger<NotificationService>>();
            _notificationService = new NotificationService(_dbContext, _mockEmailService.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task CreateNotificationAsync_ShouldCreateNewNotification()
        {
            // Arrange
            int userId = 1;
            string type = "Test Notification";
            string message = "This is a test notification";

            // Act
            await _notificationService.CreateNotificationAsync(userId, type, message);

            // Assert
            var notification = await _dbContext.NotificacionesUsuario.FirstOrDefaultAsync();
            Assert.NotNull(notification);
            Assert.Equal(userId, notification.id_usuario);
            Assert.Equal(type, notification.tipo_notificacion);
            Assert.Equal(message, notification.mensaje);
            Assert.False(notification.leida);
        }

        [Fact]
        public async Task CreateNotificationAsync_WithExistingNotification_ShouldUpdateTimestamp()
        {
            // Arrange
            int userId = 1;
            string type = "Test Notification";
            string message = "This is a test notification";
            DateTime oldDate = DateTime.Now.AddDays(-1);

            // Crear notificación existente
            var existingNotification = new Notificaciones_Usuario
            {
                id_usuario = userId,
                tipo_notificacion = type,
                mensaje = message,
                fecha_hora = oldDate,
                leida = false
            };

            _dbContext.NotificacionesUsuario.Add(existingNotification);
            await _dbContext.SaveChangesAsync();

            // Limpiar tracking
            _dbContext.ChangeTracker.Clear();

            // Act
            await _notificationService.CreateNotificationAsync(userId, type, message);

            // Assert
            var notifications = await _dbContext.NotificacionesUsuario.ToListAsync();
            Assert.Single(notifications); // Solo debe haber una notificación
            Assert.True(notifications[0].fecha_hora > oldDate); // La fecha debe ser más reciente
        }

        [Fact]
        public async Task SendExpirationNotificationsAsync_WithLicenseAboutToExpire_ShouldCreateNotification()
        {
            // Arrange
            var tomorrow = DateTime.Now.AddDays(1);

            // Crear un usuario
            var user = new Usuario
            {
                id_usuario = 1,
                nombre_usuario = "Test User",
                correo_electronico = "test@example.com",
                contrasena = "hashedpassword"
            };
            _dbContext.Usuarios.Add(user);

            // Crear un chofer con licencia próxima a vencer
            var chofer = new Choferes
            {
                id_chofer = 1,
                id_usuario = 1,
                nombreCompleto = "Test Driver",
                numero_cedula = "1234567890",
                licencia = "ABC123",
                fecha_venc_licencia = tomorrow,
                estado = "activo",
                Usuario = user,
                genero = "masculino"
            };
            _dbContext.Choferes.Add(chofer);

            // Preferencias de notificación (para simplificar, usamos los valores por defecto)
            await _dbContext.SaveChangesAsync();

            // Act
            await _notificationService.SendExpirationNotificationsAsync();

            // Assert
            // Verificar que se creó una notificación
            var notification = await _dbContext.NotificacionesUsuario.FirstOrDefaultAsync();
            Assert.NotNull(notification);
            Assert.Equal(user.id_usuario, notification.id_usuario);
            Assert.Contains("Licencia", notification.tipo_notificacion);

            // Verificar que se envió un correo
            _mockEmailService.Verify(
                e => e.SendEmailAsync(
                    It.Is<string>(to => to == user.correo_electronico),
                    It.IsAny<string>(),
                    It.IsAny<string>()),
                Times.Once);
        }

        [Fact]
        public async Task SendExpirationNotificationsAsync_WithDocumentAboutToExpire_ShouldCreateNotification()
        {
            // Arrange
            var tomorrow = DateTime.Now.AddDays(1);

            // Crear un usuario
            var user = new Usuario
            {
                id_usuario = 1,
                nombre_usuario = "Test User",
                correo_electronico = "test@example.com",
                contrasena = "hashedpassword"
            };
            _dbContext.Usuarios.Add(user);

            // Crear un chofer
            var chofer = new Choferes
            {
                id_chofer = 1,
                id_usuario = 1,
                nombreCompleto = "Test Driver",
                numero_cedula = "1234567890",
                licencia = "ABC123",
                fecha_venc_licencia = DateTime.Now.AddMonths(6), // Licencia no próxima a vencer
                estado = "activo",
                Usuario = user,
                genero = "masculino"
            };
            _dbContext.Choferes.Add(chofer);

            // Documento próximo a vencer
            var document = new Documentos
            {
                id_documento = 1,
                id_chofer = 1,
                tipo_documento = "Seguro",
                fecha_emision = DateTime.Now.AddMonths(-11),
                fecha_vencimiento = tomorrow,
                estado_validacion = "verificado"
            };
            _dbContext.Documentos.Add(document);

            await _dbContext.SaveChangesAsync();

            // Act
            await _notificationService.SendExpirationNotificationsAsync();

            // Assert
            // Verificar que se creó una notificación
            var notifications = await _dbContext.NotificacionesUsuario.ToListAsync();
            Assert.NotEmpty(notifications);
            var docNotification = notifications.FirstOrDefault(n => n.tipo_notificacion.Contains("Documento"));
            Assert.NotNull(docNotification);

            // Verificar que se envió un correo
            _mockEmailService.Verify(
                e => e.SendEmailAsync(
                    It.Is<string>(to => to == user.correo_electronico),
                    It.IsAny<string>(),
                    It.IsAny<string>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task SendExpirationNotificationsAsync_WithUserPreferences_ShouldRespectPreferences()
        {
            // Arrange
            var soon = DateTime.Now.AddDays(5);

            // Crear un usuario
            var user = new Usuario
            {
                id_usuario = 1,
                nombre_usuario = "Test User",
                correo_electronico = "test@example.com",
                contrasena = "hashedpassword"
            };
            _dbContext.Usuarios.Add(user);

            // Crear un chofer
            var chofer = new Choferes
            {
                id_chofer = 1,
                id_usuario = 1,
                nombreCompleto = "Test Driver",
                numero_cedula = "1234567890",
                licencia = "ABC123",
                fecha_venc_licencia = soon,
                estado = "activo",
                Usuario = user,
                genero = "masculino"
            };
            _dbContext.Choferes.Add(chofer);

            // Preferencias personalizadas (no quiere notificaciones por correo)
            var prefs = new PreferenciasNotificacion
            {
                id_preferencia = 1,
                id_usuario = 1,
                notificar_por_correo = false,
                notificar_vencimiento_licencia = true,
                notificar_vencimiento_documentos = true,
                dias_anticipacion = 10 // Notificar con 10 días de anticipación
            };
            _dbContext.PreferenciasNotificacion.Add(prefs);

            await _dbContext.SaveChangesAsync();

            // Act
            await _notificationService.SendExpirationNotificationsAsync();

            // Assert
            // Verificar que se creó una notificación en el sistema
            var notification = await _dbContext.NotificacionesUsuario.FirstOrDefaultAsync();
            Assert.NotNull(notification);

            // Verificar que NO se envió un correo (según preferencias)
            _mockEmailService.Verify(
                e => e.SendEmailAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()),
                Times.Never);
        }
    }
}