using COMAVI_SA.Data;
using COMAVI_SA.Models;
using COMAVI_SA.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace COMAVIxUnitTest
{
    public class ReportServiceTests
    {
        private readonly Mock<ILogger<ReportService>> _mockLogger;
        private readonly ComaviDbContext _dbContext;
        private readonly ReportService _reportService;

        public ReportServiceTests()
        {
            // Configurar DbContext en memoria
            var options = new DbContextOptionsBuilder<ComaviDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _dbContext = new ComaviDbContext(options);
            _mockLogger = new Mock<ILogger<ReportService>>();
            _reportService = new ReportService(_dbContext, _mockLogger.Object);

            // Configurar datos de prueba
            SetupTestData();
        }

        private void SetupTestData()
        {
            // Usuario de prueba
            var user = new Usuario
            {
                id_usuario = 1,
                nombre_usuario = "Test User",
                correo_electronico = "test@example.com",
                contrasena = "hashedpassword"
            };
            _dbContext.Usuarios.Add(user);

            // Chofer
            var chofer = new Choferes
            {
                id_chofer = 1,
                id_usuario = 1,
                nombreCompleto = "John Doe",
                numero_cedula = "1234567890",
                edad = 35,
                genero = "masculino",
                licencia = "ABC123",
                fecha_venc_licencia = DateTime.Now.AddMonths(2),
                estado = "activo",
                Usuario = user
            };
            _dbContext.Choferes.Add(chofer);

            // Camión asignado
            var camion = new Camiones
            {
                id_camion = 1,
                numero_placa = "XYZ-789",
                marca = "Volvo",
                modelo = "FH16",
                anio = 2020,
                estado = "activo",
                chofer_asignado = 1
            };
            _dbContext.Camiones.Add(camion);

            // Documentos
            var documentos = new List<Documentos>
            {
                new Documentos
                {
                    id_documento = 1,
                    id_chofer = 1,
                    tipo_documento = "Seguro",
                    fecha_emision = DateTime.Now.AddMonths(-6),
                    fecha_vencimiento = DateTime.Now.AddMonths(6),
                    estado_validacion = "verificado"
                },
                new Documentos
                {
                    id_documento = 2,
                    id_chofer = 1,
                    tipo_documento = "Certificado",
                    fecha_emision = DateTime.Now.AddMonths(-3),
                    fecha_vencimiento = DateTime.Now.AddMonths(9),
                    estado_validacion = "verificado"
                }
            };
            _dbContext.Documentos.AddRange(documentos);

            _dbContext.SaveChanges();
        }

        [Fact]
        public async Task GenerateDriverReportAsync_WithValidUser_ReturnsNonEmptyPdf()
        {
            // Arrange
            int userId = 1;

            // Act
            var pdfBytes = await _reportService.GenerateDriverReportAsync(userId);

            // Assert
            Assert.NotNull(pdfBytes);
            Assert.True(pdfBytes.Length > 0);
            Assert.True(IsPdfBytes(pdfBytes));
        }

        [Fact]
        public async Task GenerateDriverReportAsync_WithInvalidUser_ThrowsException()
        {
            // Arrange
            int invalidUserId = 999;

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() =>
                _reportService.GenerateExpirationReportAsync(invalidUserId));

            await Assert.ThrowsAsync<Exception>(() =>
                _reportService.GenerateDriverReportAsync(invalidUserId));
        }

        [Fact]
        public async Task GenerateExpirationReportAsync_WithValidUser_ReturnsNonEmptyPdf()
        {
            // Arrange
            int userId = 1;

            // Act
            var pdfBytes = await _reportService.GenerateExpirationReportAsync(userId);

            // Assert
            Assert.NotNull(pdfBytes);
            Assert.True(pdfBytes.Length > 0);
            Assert.True(IsPdfBytes(pdfBytes));
        }

        [Fact]
        public async Task GenerateExpirationReportAsync_WithInvalidUser_ThrowsException()
        {
            // Arrange
            int invalidUserId = 999;

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() =>
                _reportService.GenerateExpirationReportAsync(invalidUserId));
        }

        private bool IsPdfBytes(byte[] bytes)
        {
            // Verificar la firma del archivo PDF
            // Los archivos PDF comienzan con la firma "%PDF-"
            if (bytes.Length < 5)
                return false;

            return bytes[0] == 0x25 && // %
                   bytes[1] == 0x50 && // P
                   bytes[2] == 0x44 && // D
                   bytes[3] == 0x46 && // F
                   bytes[4] == 0x2D;   // -
        }
    }
}