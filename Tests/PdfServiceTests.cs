using COMAVI_SA.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace COMAVIxUnitTest
{
    public class PdfServiceTests
    {
        private readonly Mock<IWebHostEnvironment> _mockEnvironment;
        private readonly Mock<ILogger<PdfService>> _mockLogger;
        private readonly PdfService _pdfService;
        private readonly string _testFolderPath;

        public PdfServiceTests()
        {
            _mockEnvironment = new Mock<IWebHostEnvironment>();
            _mockLogger = new Mock<ILogger<PdfService>>();

            // Configurar ruta de prueba
            _testFolderPath = Path.Combine(Path.GetTempPath(), "COMAVI_Tests", "uploads", "pdfs");
            Directory.CreateDirectory(_testFolderPath);

            _mockEnvironment.Setup(m => m.WebRootPath).Returns(Path.Combine(Path.GetTempPath(), "COMAVI_Tests"));

            _pdfService = new PdfService(_mockEnvironment.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task ValidatePdfAsync_NullFile_ReturnsFalse()
        {
            // Act
            var (isValid, errorMessage) = await _pdfService.ValidatePdfAsync(null);

            // Assert
            Assert.False(isValid);
            Assert.Equal("No se ha proporcionado ningún archivo.", errorMessage);
        }

        [Fact]
        public async Task ValidatePdfAsync_EmptyFile_ReturnsFalse()
        {
            // Arrange
            var mockFile = CreateMockFile("test.pdf", 0, "application/pdf");

            // Act
            var (isValid, errorMessage) = await _pdfService.ValidatePdfAsync(mockFile.Object);

            // Assert
            Assert.False(isValid);
            Assert.Equal("No se ha proporcionado ningún archivo.", errorMessage);
        }

        [Fact]
        public async Task ValidatePdfAsync_WrongContentType_ReturnsFalse()
        {
            // Arrange
            var mockFile = CreateMockFile("test.txt", 100, "text/plain");

            // Act
            var (isValid, errorMessage) = await _pdfService.ValidatePdfAsync(mockFile.Object);

            // Assert
            Assert.False(isValid);
            Assert.Equal("El archivo no es un PDF válido.", errorMessage);
        }

        // Método helper para crear mock de IFormFile
        private Mock<IFormFile> CreateMockFile(string fileName, long length, string contentType)
        {
            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.FileName).Returns(fileName);
            mockFile.Setup(f => f.Length).Returns(length);
            mockFile.Setup(f => f.ContentType).Returns(contentType);

            if (length > 0)
            {
                var content = Encoding.UTF8.GetBytes("Fake file content");
                var stream = new MemoryStream(content);
                mockFile.Setup(f => f.OpenReadStream()).Returns(stream);
            }

            return mockFile;
        }

        // Limpieza después de las pruebas
        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testFolderPath))
                {
                    Directory.Delete(_testFolderPath, true);
                }
            }
            catch
            {
                // Ignora errores de limpieza
            }
        }
    }
}