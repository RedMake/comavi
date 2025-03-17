using COMAVI_SA.Models;
using COMAVI_SA.Services;
using Microsoft.Extensions.Configuration;
using Moq;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using Xunit;

namespace COMAVIxUnitTest
{
    public class JwtServiceTests
    {
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly JwtService _jwtService;
        private readonly Usuario _testUser;

        public JwtServiceTests()
        {
            _mockConfiguration = new Mock<IConfiguration>();
            var mockConfigSection = new Mock<IConfigurationSection>();

            // Configurar valores de prueba para la configuración JWT
            mockConfigSection.Setup(s => s.Value).Returns("TestSecretKeyWithAtLeast32Characters12345");
            _mockConfiguration.Setup(c => c["JwtSettings:Secret"]).Returns("TestSecretKeyWithAtLeast32Characters12345");
            _mockConfiguration.Setup(c => c["JwtSettings:ExpirationInMinutes"]).Returns("60");
            _mockConfiguration.Setup(c => c["JwtSettings:Issuer"]).Returns("TestIssuer");
            _mockConfiguration.Setup(c => c["JwtSettings:Audience"]).Returns("TestAudience");

            _jwtService = new JwtService(_mockConfiguration.Object);

            // Usuario de prueba
            _testUser = new Usuario
            {
                id_usuario = 1,
                nombre_usuario = "TestUser",
                correo_electronico = "test@example.com",
                rol = "admin"
            };
        }

        [Fact]
        public void GenerateJwtToken_ReturnsValidToken()
        {
            // Act
            var token = _jwtService.GenerateJwtToken(_testUser);

            // Assert
            Assert.NotNull(token);
            Assert.NotEmpty(token);
        }

        [Fact]
        public void ValidateToken_WithValidToken_ReturnsPrincipal()
        {
            // Arrange
            var token = _jwtService.GenerateJwtToken(_testUser);

            // Act
            var principal = _jwtService.ValidateToken(token);

            // Assert
            Assert.NotNull(principal);
            Assert.Equal(_testUser.id_usuario.ToString(), principal.FindFirstValue(ClaimTypes.NameIdentifier));
            Assert.Equal(_testUser.nombre_usuario, principal.FindFirstValue(ClaimTypes.Name));
            Assert.Equal(_testUser.correo_electronico, principal.FindFirstValue(ClaimTypes.Email));
            Assert.Equal(_testUser.rol, principal.FindFirstValue(ClaimTypes.Role));
        }

        [Fact]
        public void ValidateToken_WithInvalidToken_ReturnsNull()
        {
            // Arrange
            var invalidToken = "invalid_token";

            // Act
            var principal = _jwtService.ValidateToken(invalidToken);

            // Assert
            Assert.Null(principal);
        }
    }
}