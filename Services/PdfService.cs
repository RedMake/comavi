using COMAVI_SA.Utils;
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
            _uploadsFolder = Path.Combine(_hostingEnvironment.WebRootPath, "uploads", "pdfs");

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
            var filePath = Path.Combine(_uploadsFolder, fileName);

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
                .Replace("ó", "o").Replace("ú", "u");

            switch (documentType.ToLowerInvariant())
            {
                case "licencia":
                    // Verificar si contiene información típica de una licencia de conducir
                    return normalizedText.Contains("licencia") &&
                           normalizedText.Contains("conducir") &&
                           (normalizedText.Contains("vencimiento") || normalizedText.Contains("expiracion")) &&
                           (normalizedText.Contains("categoria") || normalizedText.Contains("clase")) &&
                           ContainsDatePattern(normalizedText);

                case "cedula":
                    // Verificar si contiene información típica de una cédula
                    return normalizedText.Contains("documento") &&
                           normalizedText.Contains("identidad") &&
                           ContainsIdNumberPattern(normalizedText);

                case "inscripcion":
                    // Verificar si contiene información de inscripción de vehículo/camión
                    return normalizedText.Contains("vehiculo") &&
                           (normalizedText.Contains("placa") || normalizedText.Contains("patente")) &&
                           (normalizedText.Contains("registro") || normalizedText.Contains("inscripcion"));

                case "mantenimiento":
                    // Verificar si contiene información de un reporte de mantenimiento
                    return normalizedText.Contains("mantenimiento") &&
                           (normalizedText.Contains("reparacion") || normalizedText.Contains("servicio")) &&
                           ContainsDatePattern(normalizedText);

                default:
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