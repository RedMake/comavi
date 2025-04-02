using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using COMAVI_SA.Models;
using COMAVI_SA.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using Moq;
using Xunit;

namespace COMAVI_SA.Tests.Services
{
    public class PdfServiceTests
    {
        private readonly Mock<IWebHostEnvironment> _mockHostingEnvironment;
        private readonly string _tempDirectory;
        private readonly PdfService _pdfService;

        public PdfServiceTests()
        {
            _mockHostingEnvironment = new Mock<IWebHostEnvironment>();
            _tempDirectory = Path.Combine(Path.GetTempPath(), "COMAVI_Tests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDirectory);

            _mockHostingEnvironment.Setup(h => h.WebRootPath).Returns(_tempDirectory);

            // Create uploads/pdfs directory
            var uploadsDir = Path.Combine(_tempDirectory, "uploads", "pdfs");
            Directory.CreateDirectory(uploadsDir);

            _pdfService = new PdfService(_mockHostingEnvironment.Object);
        }

        // Cleanup after tests
        ~PdfServiceTests()
        {
            try
            {
                if (Directory.Exists(_tempDirectory))
                {
                    Directory.Delete(_tempDirectory, true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        #region ValidatePdfAsync Tests

        [Fact]
        public async Task ValidatePdfAsync_ReturnsFalseWhenFileIsNull()
        {
            // Act
            var result = await _pdfService.ValidatePdfAsync(null);

            // Assert
            Assert.False(result.isValid);
            Assert.Equal("No se ha proporcionado ningún archivo.", result.errorMessage);
        }

        [Fact]
        public async Task ValidatePdfAsync_ReturnsFalseWhenFileIsEmpty()
        {
            // Arrange
            var formFile = new FormFile(new MemoryStream(), 0, 0, "file", "test.pdf");

            // Act
            var result = await _pdfService.ValidatePdfAsync(formFile);

            // Assert
            Assert.False(result.isValid);
            Assert.Equal("No se ha proporcionado ningún archivo.", result.errorMessage);
        }

        [Fact]
        public async Task ValidatePdfAsync_ReturnsFalseWhenFileIsNotPdf()
        {
            // Arrange
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            writer.Write("This is not a PDF file");
            writer.Flush();
            stream.Position = 0;

            var formFile = new FormFile(stream, 0, stream.Length, "file", "test.txt")
            {
                Headers = new HeaderDictionary(),
                ContentType = "text/plain"
            };

            // Act
            var result = await _pdfService.ValidatePdfAsync(formFile);

            // Assert
            Assert.False(result.isValid);
            Assert.Equal("El archivo no es un PDF válido.", result.errorMessage);
        }

        [Fact]
        public async Task ValidatePdfAsync_ReturnsFalseWhenFileIsTooLarge()
        {
            // Arrange - Create a large mock file
            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(11 * 1024 * 1024); // 11MB
            mockFile.Setup(f => f.ContentType).Returns("application/pdf");

            // Act
            var result = await _pdfService.ValidatePdfAsync(mockFile.Object);

            // Assert
            Assert.False(result.isValid);
            Assert.Equal("El archivo excede el tamaño máximo permitido (10MB).", result.errorMessage);
        }


        #endregion

        #region ContainsRequiredInformation Tests

        [Theory]
        [InlineData("licencia", "Este documento contiene una licencia de conducir con nombre, cedula, fecha de emision y FECHA de vencimiento para la clase B. 24/05/2023", true)]
        [InlineData("licencia", "Este documento no contiene la información necesaria", false)]
        [InlineData("cedula", "Este es un documento de identidad con nombre, fecha de nacimiento y nacionalidad. 123456789", true)]
        [InlineData("cedula", "Documento incompleto", false)]
        [InlineData("inscripcion", "Inscripción de vehículo placa ABC-123, marca Toyota, modelo Corolla, chasis 123456, motor 654321, propietario Juan Pérez", true)]
        [InlineData("inscripcion", "Documento sin datos completos", false)]
        [InlineData("mantenimiento", "Reporte de mantenimiento del vehículo con placa ABC-123, servicio realizado el 15/06/2023 por el técnico José Rodríguez", true)]
        [InlineData("mantenimiento", "Documento incompleto", false)]
        [InlineData("otro", "Cualquier texto", false)]
        public void ContainsRequiredInformation_ValidatesDocumentTypeCorrectly(string documentType, string pdfText, bool expected)
        {
            // Act
            var result = _pdfService.ContainsRequiredInformation(pdfText, documentType);

            // Assert
            Assert.Equal(expected, result);
        }

        #endregion

        #region GenerarReporteDocumentosPDF Tests

        [Fact]
        public async Task GenerarReporteDocumentosPDF_CreatesFileWithCorrectContent()
        {
            // Arrange
            var documentos = new List<Documentos>
            {
                new Documentos
                {
                    id_documento = 1,
                    id_chofer = 1,
                    tipo_documento = "Licencia",
                    fecha_emision = DateTime.Now.AddDays(-90),
                    fecha_vencimiento = DateTime.Now.AddDays(30),
                    estado_validacion = "verificado",
                    Chofer = new Choferes { nombreCompleto = "Test Driver 1" }
                },
                new Documentos
                {
                    id_documento = 2,
                    id_chofer = 2,
                    tipo_documento = "Cédula",
                    fecha_emision = DateTime.Now.AddDays(-180),
                    fecha_vencimiento = DateTime.Now.AddDays(-5),
                    estado_validacion = "verificado",
                    Chofer = new Choferes { nombreCompleto = "Test Driver 2" }
                }
            };

            string filePath = Path.Combine(_tempDirectory, "test_report.pdf");

            // Act
            var result = await _pdfService.GenerarReporteDocumentosPDF(documentos, "todos", 30, filePath);

            // Assert
            Assert.True(result);
            Assert.True(File.Exists(filePath));

            // Check file size is greater than 0
            var fileInfo = new FileInfo(filePath);
            Assert.True(fileInfo.Length > 0);

            // Cleanup
            File.Delete(filePath);
        }

        [Fact]
        public async Task GenerarReporteDocumentosPDF_ReturnsFalseOnException()
        {
            // Arrange
            var documentos = new List<Documentos>
            {
                new Documentos
                {
                    id_documento = 1,
                    id_chofer = 1,
                    tipo_documento = "Licencia",
                    fecha_emision = DateTime.Now.AddDays(-90),
                    fecha_vencimiento = DateTime.Now.AddDays(30),
                    estado_validacion = "verificado",
                    // Missing Chofer - will cause NullReferenceException
                }
            };

            // Use an invalid path to generate an exception
            string filePath = Path.Combine(_tempDirectory, "invalid/path/test_report.pdf");

            // Act
            var result = await _pdfService.GenerarReporteDocumentosPDF(documentos, "todos", 30, filePath);

            // Assert
            Assert.False(result);
        }

        #endregion

        #region SavePdfAsync Tests

        [Fact]
        public async Task SavePdfAsync_SavesFileAndReturnsPathAndHash()
        {
            // Arrange
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            writer.Write("%PDF-1.5\nThis is a mock PDF file");
            writer.Flush();
            stream.Position = 0;

            var formFile = new FormFile(stream, 0, stream.Length, "file", "test.pdf")
            {
                Headers = new HeaderDictionary(),
                ContentType = "application/pdf"
            };

            // Act
            var result = await _pdfService.SavePdfAsync(formFile);

            // Assert
            Assert.NotNull(result.filePath);
            Assert.NotNull(result.hash);
            Assert.True(File.Exists(result.filePath));

            // Check file size is greater than 0
            var fileInfo = new FileInfo(result.filePath);
            Assert.True(fileInfo.Length > 0);

            // Cleanup
            File.Delete(result.filePath);
        }

        [Fact]
        public async Task SavePdfAsync_UsesCustomFilenameWhenProvided()
        {
            // Arrange
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            writer.Write("%PDF-1.5\nThis is a mock PDF file");
            writer.Flush();
            stream.Position = 0;

            var formFile = new FormFile(stream, 0, stream.Length, "file", "test.pdf")
            {
                Headers = new HeaderDictionary(),
                ContentType = "application/pdf"
            };

            string customFilename = "custom_file.pdf";

            // Act
            var result = await _pdfService.SavePdfAsync(formFile, customFilename);

            // Assert
            Assert.NotNull(result.filePath);
            Assert.Contains(customFilename, result.filePath);
            Assert.True(File.Exists(result.filePath));

            // Cleanup
            File.Delete(result.filePath);
        }

        #endregion
    }
}