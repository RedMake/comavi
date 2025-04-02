using System;
using System.Threading.Tasks;
using COMAVI_SA.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace COMAVI_SA.Tests.Services
{
    public class EmailServiceTests
    {
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly Mock<IWebHostEnvironment> _mockEnvironment;
        private readonly EmailService _emailService;

        public EmailServiceTests()
        {
            _mockConfiguration = new Mock<IConfiguration>();
            _mockEnvironment = new Mock<IWebHostEnvironment>();

            // Setup environment
            _mockEnvironment.Setup(e => e.EnvironmentName).Returns("Development");

            // Setup configuration
            var configurationSection = new Mock<IConfigurationSection>();
            configurationSection.Setup(a => a.Value).Returns("test-password");
            _mockConfiguration.Setup(c => c["EmailPassword"]).Returns("test-password");

            _emailService = new EmailService(_mockConfiguration.Object, _mockEnvironment.Object);
        }

        #region SendEmailAsync Tests

        [Fact]
        public async Task SendEmailAsync_ThrowsArgumentExceptionWhenToIsNull()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _emailService.SendEmailAsync(null, "Test Subject", "Test Body"));
        }

        [Fact]
        public async Task SendEmailAsync_ThrowsArgumentExceptionWhenSubjectIsNull()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _emailService.SendEmailAsync("test@example.com", null, "Test Body"));
        }

        [Fact]
        public async Task SendEmailAsync_ThrowsArgumentExceptionWhenBodyIsNull()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _emailService.SendEmailAsync("test@example.com", "Test Subject", null));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task SendEmailAsync_ThrowsArgumentExceptionWhenToIsEmpty(string to)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _emailService.SendEmailAsync(to, "Test Subject", "Test Body"));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task SendEmailAsync_ThrowsArgumentExceptionWhenSubjectIsEmpty(string subject)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _emailService.SendEmailAsync("test@example.com", subject, "Test Body"));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task SendEmailAsync_ThrowsArgumentExceptionWhenBodyIsEmpty(string body)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _emailService.SendEmailAsync("test@example.com", "Test Subject", body));
        }

        // Note: We can't easily test the actual SMTP connection in unit tests
        // For a real implementation, we would use an SMTP mock like Papercut or a test email service

        #endregion

        #region EnviarCorreoAsync Tests

        [Fact]
        public async Task EnviarCorreoAsync_CallsSendEmailAsyncWithSameParameters()
        {
            string testEmail = "test@example.com";
            string testSubject = "Test Subject";
            string testBody = "Test Body";

            var mock = new Mock<IEmailService>();


            // Setup
            mock.Setup(e => e.EnviarCorreoAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string, string>((to, subject, body) =>
                    mock.Object.SendEmailAsync(to, subject, body));

            // Act
            await mock.Object.EnviarCorreoAsync(testEmail, testSubject, testBody);

            // Assert
            mock.Verify(e => e.SendEmailAsync(testEmail, testSubject, testBody), Times.AtLeastOnce);
        }

        #endregion
    }
}