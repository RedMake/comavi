using COMAVI_SA.Models;
using COMAVI_SA.Utils;
using DocumentFormat.OpenXml.Drawing;
using iText.IO.Font.Constants;
using iText.Kernel.Font;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using System.Security.Cryptography;

namespace COMAVI_SA.Services
{
    public interface IPdfService
    {
        Task<(bool isValid, string errorMessage)> ValidatePdfAsync(IFormFile file);
        Task<(string filePath, string hash)> SavePdfAsync(IFormFile file, string customFileName = null);
        Task<string> ExtractTextFromPdfAsync(IFormFile file);
        bool ContainsRequiredInformation(string pdfText, string documentType);
        Task<bool> GenerarReporteDocumentosPDF(List<COMAVI_SA.Models.Documentos> documentos, string estado, int diasAnticipacion, string filePath);

    }

    public class PdfService : IPdfService
    {
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly ILogger<PdfService> _logger;
        private readonly string _uploadsFolder;

        public PdfService(
            IWebHostEnvironment hostingEnvironment,
            ILogger<PdfService> logger)
        {
            _hostingEnvironment = NotNull.Check(hostingEnvironment);
            _logger = NotNull.Check(logger);
            _uploadsFolder = System.IO.Path.Combine(_hostingEnvironment.WebRootPath, "uploads", "pdfs");

            // Asegurar que exista el directorio para los archivos
            if (!Directory.Exists(_uploadsFolder))
            {
                Directory.CreateDirectory(_uploadsFolder);
            }
        }

        public async Task<(bool isValid, string errorMessage)> ValidatePdfAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return (false, "No se ha proporcionado ningún archivo.");

            if (file.ContentType != "application/pdf")
                return (false, "El archivo no es un PDF válido.");

            if (file.Length > 10 * 1024 * 1024) // 10MB como límite
                return (false, "El archivo excede el tamaño máximo permitido (10MB).");

            try
            {
                using (var stream = file.OpenReadStream())
                {
                    // Intenta abrir como PDF para validar estructura
                    var pdfReader = new PdfReader(stream);
                    var pdfDocument = new PdfDocument(pdfReader);

                    // Verificar que haya al menos una página
                    if (pdfDocument.GetNumberOfPages() < 1)
                        return (false, "El documento PDF no contiene páginas.");

                    pdfDocument.Close();
                }

                return (true, null);
            }
            catch (iText.Kernel.Exceptions.BadPasswordException)
            {
                return (false, "El documento PDF está protegido con contraseña.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al validar archivo PDF");
                return (false, "El documento PDF no es válido o está corrupto.");
            }
        }

        public async Task<(string filePath, string hash)> SavePdfAsync(IFormFile file, string customFileName = null)
        {
            var fileName = customFileName ?? $"{Guid.NewGuid()}.pdf";
            var filePath = System.IO.Path.Combine(_uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Calcular hash SHA-256 del archivo
            string fileHash;
            using (var stream = new FileStream(filePath, FileMode.Open))
            {
                using (var sha256 = SHA256.Create())
                {
                    byte[] hashBytes = sha256.ComputeHash(stream);
                    fileHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                }
            }

            return (filePath, fileHash);
        }

        public async Task<string> ExtractTextFromPdfAsync(IFormFile file)
        {
            try
            {
                using (var stream = file.OpenReadStream())
                {
                    var pdfReader = new PdfReader(stream);
                    var pdfDocument = new PdfDocument(pdfReader);
                    var textBuilder = new System.Text.StringBuilder();

                    for (int pageNum = 1; pageNum <= pdfDocument.GetNumberOfPages(); pageNum++)
                    {
                        var page = pdfDocument.GetPage(pageNum);
                        var strategy = new SimpleTextExtractionStrategy();
                        string pageText = PdfTextExtractor.GetTextFromPage(page, strategy);
                        textBuilder.AppendLine(pageText);
                    }

                    pdfDocument.Close();
                    return textBuilder.ToString();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al extraer texto del PDF");
                return string.Empty;
            }
        }

        public bool ContainsRequiredInformation(string pdfText, string documentType)
        {
            // Normalizar texto para búsqueda (minúsculas, sin acentos)
            string normalizedText = pdfText.ToLowerInvariant()
                .Replace("á", "a").Replace("é", "e").Replace("í", "i")
                .Replace("ó", "o").Replace("ú", "u").Replace("ñ", "n");

            switch (documentType.ToLowerInvariant())
            {
                case "licencia":
                    // Verificar si contiene información típica de una licencia de conducir
                    return normalizedText.Contains("licencia de conducir") &&
                           normalizedText.Contains("nombre") &&
                           normalizedText.Contains("cedula") &&
                           (normalizedText.Contains("fecha") &&
                            (normalizedText.Contains("emision") || normalizedText.Contains("expedicion"))) &&
                           (normalizedText.Contains("fecha") && normalizedText.Contains("vencimiento")) &&
                           (normalizedText.Contains("clase") || normalizedText.Contains("categoria")) &&
                           ContainsDatePattern(normalizedText) &&
                           ContainsIdNumberPattern(normalizedText);

                case "cedula":
                    // Verificar si contiene información típica de una cédula
                    return (normalizedText.Contains("cedula") || normalizedText.Contains("documento")) &&
                           normalizedText.Contains("identidad") &&
                           normalizedText.Contains("nombre") &&
                           (normalizedText.Contains("fecha") && normalizedText.Contains("nacimiento")) &&
                           (normalizedText.Contains("nacionalidad") || normalizedText.Contains("pais")) &&
                           ContainsDatePattern(normalizedText) &&
                           ContainsIdNumberPattern(normalizedText);

                case "inscripcion":
                    // Verificar si contiene información de inscripción de vehículo/camión
                    return (normalizedText.Contains("inscripcion") || normalizedText.Contains("registro")) &&
                           normalizedText.Contains("vehiculo") &&
                           (normalizedText.Contains("placa") || normalizedText.Contains("patente")) &&
                           normalizedText.Contains("marca") &&
                           normalizedText.Contains("modelo") &&
                           (normalizedText.Contains("chasis") || normalizedText.Contains("vin")) &&
                           normalizedText.Contains("motor") &&
                           (normalizedText.Contains("propietario") || normalizedText.Contains("dueño")) &&
                           ContainsIdNumberPattern(normalizedText);

                case "mantenimiento":
                    // Verificar si contiene información de un reporte de mantenimiento
                    return normalizedText.Contains("mantenimiento") &&
                           normalizedText.Contains("vehiculo") &&
                           normalizedText.Contains("servicio") &&
                           (normalizedText.Contains("placa") || normalizedText.Contains("patente")) &&
                           (normalizedText.Contains("fecha") &&
                            (normalizedText.Contains("servicio") || normalizedText.Contains("mantenimiento"))) &&
                           (normalizedText.Contains("tecnico") || normalizedText.Contains("mecanico")) &&
                           ContainsDatePattern(normalizedText);

                default:
                    return false;
            }
        }

        public async Task<bool> GenerarReporteDocumentosPDF(List<Documentos> documentos, string estado, int diasAnticipacion, string filePath)
        {
            try
            {
                // Crear documento PDF
                using (var writer = new PdfWriter(filePath))
                using (var pdf = new PdfDocument(writer))
                using (var document = new iText.Layout.Document(pdf))
                {
                    // Título y encabezado
                    string estadoTexto = estado == "todos" ? "Todos" :
                        estado == "pendiente" ? "Pendientes" :
                        estado == "verificado" ? "Verificados" :
                        estado == "rechazado" ? "Rechazados" :
                        estado == "porVencer" ? "Por Vencer" : estado;

                    var boldFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);

                    // Añadir título
                    document.Add(new iText.Layout.Element.Paragraph("Reporte de Documentos")
                        .SetFontSize(16)
                        .SetFont(boldFont)
                        .SetMarginBottom(10));

                    document.Add(new iText.Layout.Element.Paragraph($"Estado: {estadoTexto}")
                        .SetFontSize(12)
                        .SetMarginBottom(5));

                    if (estado == "porVencer")
                    {
                        document.Add(new iText.Layout.Element.Paragraph($"Días de Anticipación: {diasAnticipacion}")
                            .SetFontSize(12)
                            .SetMarginBottom(5));
                    }

                    document.Add(new iText.Layout.Element.Paragraph($"Fecha de Generación: {DateTime.Now:dd/MM/yyyy HH:mm}")
                        .SetFontSize(10)
                        .SetMarginBottom(5));

                    document.Add(new iText.Layout.Element.Paragraph($"Total de Documentos: {documentos.Count}")
                        .SetFontSize(10)
                        .SetMarginBottom(15));

                    // Crear tabla
                    var table = new iText.Layout.Element.Table(7)
                        .SetWidth(iText.Layout.Properties.UnitValue.CreatePercentValue(100));

                    // Encabezados
                    table.AddHeaderCell(new iText.Layout.Element.Cell().Add(new iText.Layout.Element.Paragraph("ID")).SetFont(boldFont));
                    table.AddHeaderCell(new iText.Layout.Element.Cell().Add(new iText.Layout.Element.Paragraph("Chofer")).SetFont(boldFont));
                    table.AddHeaderCell(new iText.Layout.Element.Cell().Add(new iText.Layout.Element.Paragraph("Tipo Documento")).SetFont(boldFont));
                    table.AddHeaderCell(new iText.Layout.Element.Cell().Add(new iText.Layout.Element.Paragraph("Emisión")).SetFont(boldFont));
                    table.AddHeaderCell(new iText.Layout.Element.Cell().Add(new iText.Layout.Element.Paragraph("Vencimiento")).SetFont(boldFont));
                    table.AddHeaderCell(new iText.Layout.Element.Cell().Add(new iText.Layout.Element.Paragraph("Estado")).SetFont(boldFont));
                    table.AddHeaderCell(new iText.Layout.Element.Cell().Add(new iText.Layout.Element.Paragraph("Días Restantes")).SetFont(boldFont));

                    // Datos
                    foreach (var documento in documentos)
                    {
                        var diasRestantes = (documento.fecha_vencimiento - DateTime.Now).Days;
                        var estadoValidacion = documento.estado_validacion == "pendiente" ? "Pendiente" :
                                              documento.estado_validacion == "verificado" ? "Verificado" :
                                              documento.estado_validacion == "rechazado" ? "Rechazado" : documento.estado_validacion;

                        table.AddCell(documento.id_documento.ToString());
                        table.AddCell(documento.Chofer.nombreCompleto);
                        table.AddCell(documento.tipo_documento);
                        table.AddCell(documento.fecha_emision.ToString("dd/MM/yyyy"));

                        // Celda con color según vencimiento
                        var vencimientoCell = new iText.Layout.Element.Cell()
                            .Add(new iText.Layout.Element.Paragraph(documento.fecha_vencimiento.ToString("dd/MM/yyyy")));

                        if (diasRestantes < 0)
                        {
                            vencimientoCell.SetBackgroundColor(new iText.Kernel.Colors.DeviceRgb(255, 200, 200));
                        }
                        else if (diasRestantes <= 15)
                        {
                            vencimientoCell.SetBackgroundColor(new iText.Kernel.Colors.DeviceRgb(255, 235, 156));
                        }

                        table.AddCell(vencimientoCell);
                        table.AddCell(estadoValidacion);

                        // Días restantes con formato según vencimiento
                        var diasRestantesCell = new iText.Layout.Element.Cell();
                        if (diasRestantes < 0)
                        {
                            diasRestantesCell
                                .Add(new iText.Layout.Element.Paragraph($"Vencido ({Math.Abs(diasRestantes)} días)"))
                                .SetBackgroundColor(new iText.Kernel.Colors.DeviceRgb(255, 200, 200));
                        }
                        else
                        {
                            diasRestantesCell.Add(new iText.Layout.Element.Paragraph($"{diasRestantes} días"));

                            if (diasRestantes <= 15)
                            {
                                diasRestantesCell.SetBackgroundColor(new iText.Kernel.Colors.DeviceRgb(255, 235, 156));
                            }
                            else if (diasRestantes <= 30)
                            {
                                diasRestantesCell.SetBackgroundColor(new iText.Kernel.Colors.DeviceRgb(200, 230, 255));
                            }
                        }

                        table.AddCell(diasRestantesCell);
                    }

                    document.Add(table);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar PDF de documentos");
                return false;
            }
        }


        private bool ContainsDatePattern(string text)
        {
            // Expresión regular para detectar fechas en formatos comunes (DD/MM/YYYY o DD-MM-YYYY)
            var dateRegex = new System.Text.RegularExpressions.Regex(@"\b\d{1,2}[/\-\.]\d{1,2}[/\-\.]\d{2,4}\b");
            return dateRegex.IsMatch(text);
        }

        private bool ContainsIdNumberPattern(string text)
        {
            // Expresión regular para números de identificación (secuencia de 6-15 dígitos)
            var idRegex = new System.Text.RegularExpressions.Regex(@"\b\d{6,15}\b");
            return idRegex.IsMatch(text);
        }
    }
}