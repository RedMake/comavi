using System;
using System.Security.Claims;
using System.Threading.Tasks;
using COMAVI_SA.Controllers;
using COMAVI_SA.Data;
using COMAVI_SA.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace COMAVI_SA.Tests.Controllers
{
    public class ReportsControllerTests
    {
        private readonly Mock<IReportService> _mockReportService;
        private readonly Mock<ComaviDbContext> _mockContext;
        private readonly ReportsController _controller;

        public ReportsControllerTests()
        {
            _mockReportService = new Mock<IReportService>();
            _mockContext = new Mock<ComaviDbContext>(new DbContextOptions<ComaviDbContext>());

            _controller = new ReportsController(_mockReportService.Object, _mockContext.Object);
            SetupAuthenticatedUser(_controller);
            SetupTempData(_controller);
        }

        #region DriverReport Tests

        [Fact]
        public async Task DriverReport_ReturnsFileResultWithPdf()
        {
            // Arrange
            const int userId = 1;
            byte[] pdfBytes = new byte[] { 1, 2, 3, 4, 5 }; // Sample PDF content

            _mockReportService.Setup(r => r.GenerateDriverReportAsync(userId))
                .ReturnsAsync(pdfBytes);

            SetupUserIdClaim(_controller, userId);

            // Act
            var result = await _controller.DriverReport();

            // Assert
            var fileResult = Assert.IsType<FileContentResult>(result);
            Assert.Equal("application/pdf", fileResult.ContentType);
            Assert.Contains("ReporteConductor_", fileResult.FileDownloadName);
            Assert.Equal(pdfBytes, fileResult.FileContents);
        }

        [Fact]
        public async Task DriverReport_HandlesExceptionCorrectly()
        {
            // Arrange
            const int userId = 1;

            _mockReportService.Setup(r => r.GenerateDriverReportAsync(userId))
                .ThrowsAsync(new Exception("Error generating report"));

            SetupUserIdClaim(_controller, userId);

            // Act
            var result = await _controller.DriverReport();

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Profile", redirectResult.ActionName);
            Assert.Equal("Login", redirectResult.ControllerName);
            Assert.Contains("Error al generar el reporte", _controller.TempData["Error"].ToString());
        }

        #endregion

        #region ExpirationReport Tests

        [Fact]
        public async Task ExpirationReport_ReturnsFileResultWithPdf()
        {
            // Arrange
            const int userId = 1;
            byte[] pdfBytes = new byte[] { 5, 4, 3, 2, 1 }; // Sample PDF content

            _mockReportService.Setup(r => r.GenerateExpirationReportAsync(userId))
                .ReturnsAsync(pdfBytes);

            SetupUserIdClaim(_controller, userId);

            // Act
            var result = await _controller.ExpirationReport();

            // Assert
            var fileResult = Assert.IsType<FileContentResult>(result);
            Assert.Equal("application/pdf", fileResult.ContentType);
            Assert.Contains("ReporteVencimientos_", fileResult.FileDownloadName);
            Assert.Equal(pdfBytes, fileResult.FileContents);
        }

        [Fact]
        public async Task ExpirationReport_HandlesExceptionCorrectly()
        {
            // Arrange
            const int userId = 1;

            _mockReportService.Setup(r => r.GenerateExpirationReportAsync(userId))
                .ThrowsAsync(new Exception("Error generating report"));

            SetupUserIdClaim(_controller, userId);

            // Act
            var result = await _controller.ExpirationReport();

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Profile", redirectResult.ActionName);
            Assert.Equal("Login", redirectResult.ControllerName);
            Assert.Contains("Error al generar el reporte", _controller.TempData["Error"].ToString());
        }

        #endregion

        #region Helper Methods

        private void SetupAuthenticatedUser(ReportsController controller)
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

        private void SetupUserIdClaim(ReportsController controller, int userId)
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

        private void SetupTempData(ReportsController controller)
        {
            controller.TempData = new TempDataDictionary(
                new DefaultHttpContext(),
                Mock.Of<ITempDataProvider>());
        }

        #endregion
    }
}