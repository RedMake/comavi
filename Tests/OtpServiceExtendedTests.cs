using COMAVI_SA.Services;
using Microsoft.Extensions.Configuration;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace COMAVIxUnitTest
{
#nullable disable

    /// <summary>
    /// Pruebas extendidas para OtpService, con énfasis en los códigos de respaldo
    /// </summary>
    public class OtpServiceExtendedTests
    {
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly OtpService _otpService;

        public OtpServiceExtendedTests()
        {
            _mockConfiguration = new Mock<IConfiguration>();

            // Configuración de valores por defecto
            var mockSecretLengthSection = new Mock<IConfigurationSection>();
            mockSecretLengthSection.Setup(s => s.Value).Returns("20");
            _mockConfiguration.Setup(c => c.GetSection("OtpSettings:SecretLength"))
                            .Returns(mockSecretLengthSection.Object);

            var mockStepSection = new Mock<IConfigurationSection>();
            mockStepSection.Setup(s => s.Value).Returns("30");
            _mockConfiguration.Setup(c => c.GetSection("OtpSettings:Step"))
                            .Returns(mockStepSection.Object);

            var mockDigitsSection = new Mock<IConfigurationSection>();
            mockDigitsSection.Setup(s => s.Value).Returns("6");
            _mockConfiguration.Setup(c => c.GetSection("OtpSettings:Digits"))
                            .Returns(mockDigitsSection.Object);

            _otpService = new OtpService(_mockConfiguration.Object);
        }

        [Fact]
        public void GenerateBackupCodes_ShouldReturnCorrectNumberOfCodes()
        {
            // Arrange
            int cantidad = 8; // Valor por defecto

            // Act
            var codes = _otpService.GenerateBackupCodes();

            // Assert
            Assert.Equal(cantidad, codes.Count);
        }

        [Fact]
        public void GenerateBackupCodes_WithCustomCount_ShouldReturnCorrectNumberOfCodes()
        {
            // Arrange
            int cantidad = 5; // Valor personalizado

            // Act
            var codes = _otpService.GenerateBackupCodes(cantidad);

            // Assert
            Assert.Equal(cantidad, codes.Count);
        }

        [Fact]
        public void GenerateBackupCodes_ShouldReturnUniqueValues()
        {
            // Act
            var codes = _otpService.GenerateBackupCodes();

            // Assert
            Assert.Equal(codes.Count, codes.Distinct().Count());
        }

        [Fact]
        public void GenerateBackupCodes_ShouldHaveCorrectFormat()
        {
            // Act
            var codes = _otpService.GenerateBackupCodes();

            // Assert
            foreach (var code in codes)
            {
                // Verificar formato XXXXX-XXXXX
                Assert.Matches(@"^[A-F0-9]{5}-[A-F0-9]{5}$", code);
            }
        }

        [Fact]
        public void GenerateQrCodeUri_ShouldContainCorrectParameters()
        {
            // Arrange
            var secret = "ABCDEFGHIJKLMNOPQRST"; // 20 caracteres en Base32
            var email = "test@example.com";

            // Act
            var uri = _otpService.GenerateQrCodeUri(secret, email);

            // Assert
            Assert.Contains("otpauth://totp/", uri);
            Assert.Contains($"secret={secret}", uri);
            Assert.Contains("algorithm=SHA1", uri);
            Assert.Contains("digits=6", uri);
            Assert.Contains("period=30", uri);
            Assert.Contains("COMAVI_DockTrack", uri);

            // El email debe estar codificado en la URL
            Assert.Contains("test%40example.com", uri);
        }

        [Theory]
        [InlineData("INVALID!", "123456")]  // Secreto inválido
        [InlineData("ABCDEFGHIJKLMNOPQRST", "123ABC")]  // Código no numérico
        [InlineData("ABCDEFGHIJKLMNOPQRST", "1234")]    // Código demasiado corto
        [InlineData("", "123456")]          // Secreto vacío
        [InlineData("ABCDEFGHIJKLMNOPQRST", "")]        // Código vacío
        [InlineData(null, "123456")]        // Secreto null
        [InlineData("ABCDEFGHIJKLMNOPQRST", null)]      // Código null
        public void VerifyOtp_WithInvalidInput_ReturnsFalse(string secret, string code)
        {
            // Act
            var result = _otpService.VerifyOtp(secret, code);

            // Assert
            Assert.False(result);
        }
    }
}