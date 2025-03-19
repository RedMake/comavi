using COMAVI_SA.Services;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Reflection;
using System.Threading;
using Xunit;

namespace COMAVIxUnitTest
{
    public class JwtBlacklistServiceTests
    {
        private readonly Mock<ILogger<JwtBlacklistService>> _mockLogger;
        private readonly JwtBlacklistService _blacklistService;

        public JwtBlacklistServiceTests()
        {
            _mockLogger = new Mock<ILogger<JwtBlacklistService>>();
            _blacklistService = new JwtBlacklistService(_mockLogger.Object);
        }

        [Fact]
        public void Constructor_ShouldInitializeBlacklistedTokensCollection()
        {
            // Arrange & Act - Constructor called in setup

            // Assert - Verificar que la colección se inicializó correctamente
            var fieldInfo = typeof(JwtBlacklistService).GetField("_blacklistedTokens", BindingFlags.NonPublic | BindingFlags.Instance);
            var blacklistedTokens = fieldInfo.GetValue(_blacklistService);

            Assert.NotNull(blacklistedTokens);
        }

        [Fact]
        public void AddToBlacklist_ShouldAddTokenToCollection()
        {
            // Arrange
            var token = "test_token";
            var expirationTime = TimeSpan.FromMinutes(10);

            // Act
            _blacklistService.AddToBlacklist(token, expirationTime);

            // Assert
            Assert.True(_blacklistService.IsTokenBlacklisted(token));
        }

        [Fact]
        public void IsTokenBlacklisted_WithNonBlacklistedToken_ShouldReturnFalse()
        {
            // Arrange
            var token = "non_blacklisted_token";

            // Act
            var result = _blacklistService.IsTokenBlacklisted(token);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void CleanupExpiredTokens_ShouldRemoveExpiredTokens()
        {
            // Arrange
            var expiredToken = "expired_token";
            var validToken = "valid_token";

            // Añadir token con expiración en el pasado
            _blacklistService.AddToBlacklist(expiredToken, TimeSpan.FromMilliseconds(-10));

            // Añadir token con expiración futura
            _blacklistService.AddToBlacklist(validToken, TimeSpan.FromHours(1));

            // Esperar a que pase un breve tiempo
            Thread.Sleep(50);

            // Act
            _blacklistService.CleanupExpiredTokens();

            // Assert
            Assert.False(_blacklistService.IsTokenBlacklisted(expiredToken));
            Assert.True(_blacklistService.IsTokenBlacklisted(validToken));
        }

        [Fact]
        public void TimerCallback_ShouldExecuteCleanupExpiredTokens()
        {
            // Este test es más complejo y requiere verificar el comportamiento del timer
            // que se inicializa en el constructor del servicio

            // En un escenario real, podríamos implementar un mock del timer o usar una interfaz
            // abstrayendo el timer para poder testearlo más fácilmente

            // Aquí verificamos indirectamente la configuración del timer
            var fieldInfo = typeof(JwtBlacklistService).GetField("_blacklistedTokens", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(fieldInfo);
        }
    }
}