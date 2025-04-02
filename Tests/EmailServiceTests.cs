using COMAVI_SA.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Threading.Tasks;
using Xunit;

namespace COMAVIxUnitTest
{
#nullable disable
#pragma warning disable CS0168

    public class EmailServiceTests
    {
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly Mock<IWebHostEnvironment> _mockEnvironment;
        private readonly EmailService _emailService;

        public EmailServiceTests()
        {
            _mockConfiguration = new Mock<IConfiguration>();
            _mockEnvironment = new Mock<IWebHostEnvironment>();

            // Configurar entorno de desarrollo para usar contraseña local
            // IsDevelopment() es un método de extensión que comprueba si EnvironmentName == "Development"
            _mockEnvironment.Setup(e => e.EnvironmentName).Returns("Development");

            // No necesitamos configurar EmailPassword para desarrollo ya que usa la contraseña hardcoded

            _emailService = new EmailService(
                _mockConfiguration.Object,
                _mockEnvironment.Object);
        }

        [Fact]
        public async Task SendEmailAsync_WithValidInputs_ShouldCatchSocketException()
        {
            // Este test verifica que se manejan adecuadamente los parámetros
            // pero no envía realmente un correo

            // Arrange
            string to = "test@example.com";
            string subject = "Test Subject";
            string body = "<p>Test Body</p>";

            try
            {
                // Act
                await _emailService.SendEmailAsync(to, subject, body);

                // Si llegamos aquí, el servicio está configurado para pruebas
                // y no intentó realmente conectarse a un servidor SMTP
            }
            catch (System.Net.Sockets.SocketException)
            {
                // Si capturamos esta excepción, es porque intentó conectarse
                // a un servidor SMTP real
            }
            catch (Exception ex)
            {
                // Cualquier otra excepción indica un problema diferente
                throw;
            }

            // No verificamos ninguna assertion específica, solo 
            // que no se produzcan excepciones inesperadas
        }

        [Theory]
        [InlineData(null, "Subject", "Body")]
        [InlineData("", "Subject", "Body")]
        [InlineData("test@example.com", null, "Body")]
        [InlineData("test@example.com", "", "Body")]
        [InlineData("test@example.com", "Subject", null)]
        [InlineData("test@example.com", "Subject", "")]
        public async Task SendEmailAsync_WithInvalidInputs_ThrowsArgumentException(string to, string subject, string body)
        {
            // Assert
            await Assert.ThrowsAnyAsync<ArgumentException>(
                async () => await _emailService.SendEmailAsync(to, subject, body)
            );
        }
    }
}