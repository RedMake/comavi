using COMAVI_SA.Services;
using Microsoft.Extensions.Configuration;
using Moq;
using OtpNet;
using System;
using Xunit;

namespace COMAVIxUnitTest
{
    public class OtpServiceTests
    {
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly OtpService _otpService;

        public OtpServiceTests()
        {
            _mockConfiguration = new Mock<IConfiguration>();
            var mockConfigSection = new Mock<IConfigurationSection>();

            // Configurar valores de prueba para la configuración OTP
            // En lugar de mockear el método de extensión GetValue, configuramos
            // un IConfigurationSection por cada configuración
            var mockOtpConfigSection = new Mock<IConfigurationSection>();

            // SecretLength
            mockOtpConfigSection.Setup(s => s.Value).Returns("20");
            _mockConfiguration.Setup(c => c.GetSection("OtpSettings:SecretLength"))
                            .Returns(mockOtpConfigSection.Object);

            // Step
            var mockStepConfigSection = new Mock<IConfigurationSection>();
            mockStepConfigSection.Setup(s => s.Value).Returns("30");
            _mockConfiguration.Setup(c => c.GetSection("OtpSettings:Step"))
                            .Returns(mockStepConfigSection.Object);

            // Digits
            var mockDigitsConfigSection = new Mock<IConfigurationSection>();
            mockDigitsConfigSection.Setup(s => s.Value).Returns("6");
            _mockConfiguration.Setup(c => c.GetSection("OtpSettings:Digits"))
                            .Returns(mockDigitsConfigSection.Object);

            _otpService = new OtpService(_mockConfiguration.Object);
        }

        [Fact]
        public void GenerateSecret_ReturnsNonEmptyString()
        {
            // Act
            var secret = _otpService.GenerateSecret();

            // Assert
            Assert.NotNull(secret);
            Assert.NotEmpty(secret);
            // Verificar que el secreto esté en formato Base32
            Assert.True(IsValidBase32(secret));
        }

        [Fact]
        public void GenerateQrCodeUri_ReturnsValidUri()
        {
            // Arrange
            var secret = _otpService.GenerateSecret();
            var email = "test@example.com";

            // Act
            var uri = _otpService.GenerateQrCodeUri(secret, email);

            // Assert
            Assert.NotNull(uri);
            Assert.NotEmpty(uri);
            Assert.StartsWith("otpauth://totp/", uri);
            Assert.Contains(secret, uri);
            Assert.Contains("test%40example.com", uri); // El email está codificado en la URL
        }

        [Fact]
        public void VerifyOtp_WithValidCode_ReturnsTrue()
        {
            // Arrange
            var secret = _otpService.GenerateSecret();
            var totp = new Totp(Base32Encoding.ToBytes(secret));
            var validCode = totp.ComputeTotp(); // Genera un código válido en el tiempo actual

            // Act
            var result = _otpService.VerifyOtp(secret, validCode);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void VerifyOtp_WithInvalidCode_ReturnsFalse()
        {
            // Arrange
            var secret = _otpService.GenerateSecret();
            var invalidCode = "123456"; // Código arbitrario que probablemente no sea válido

            // Act
            var result = _otpService.VerifyOtp(secret, invalidCode);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void VerifyOtp_WithNullOrEmptyInputs_ReturnsFalse()
        {
            // Arrange
            var secret = _otpService.GenerateSecret();

            // Act & Assert
            Assert.False(_otpService.VerifyOtp(null, "123456"));
            Assert.False(_otpService.VerifyOtp("", "123456"));
            Assert.False(_otpService.VerifyOtp(secret, null));
            Assert.False(_otpService.VerifyOtp(secret, ""));
            Assert.False(_otpService.VerifyOtp(secret, "abc123")); // No numérico
        }

        // Función auxiliar para verificar que una cadena sea Base32 válida
        private bool IsValidBase32(string input)
        {
            // Base32 solo usa A-Z y 2-7, más el carácter de relleno '='
            foreach (char c in input)
            {
                if (!((c >= 'A' && c <= 'Z') || (c >= '2' && c <= '7') || c == '='))
                {
                    return false;
                }
            }
            return true;
        }
    }
}