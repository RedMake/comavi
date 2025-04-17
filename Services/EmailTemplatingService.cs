// --- INTERFACE ---
using COMAVI_SA.Models;
using Microsoft.Extensions.Options;
using System.Reflection;
using System.Text;

using Microsoft.Extensions.Options; 
using System.IO;
using System.Reflection;
using System.Text; 
using System.Net; 
public interface IEmailTemplatingService
{
    /// <summary>
    /// Carga una plantilla HTML, reemplaza los placeholders y devuelve el contenido HTML final.
    /// </summary>
    /// <param name="templateName">Nombre del archivo de plantilla (ej. "MiPlantilla.html")</param>
    /// <param name="data">Diccionario con los placeholders (sin llaves) y sus valores.</param>
    /// <returns>El cuerpo del correo en HTML listo para enviar, o un mensaje de error.</returns>
    Task<string> LoadAndPopulateTemplateAsync(string templateName, Dictionary<string, string> data);
}

public class EmailTemplatingService : IEmailTemplatingService
{
    private readonly string _baseUrl;
    private readonly string _templateFolderPath;

    // Inyecta IOptions<AppSettings> para obtener la BaseUrl
    public EmailTemplatingService(IOptions<AppSettings> appSettings)
    {
        _baseUrl = appSettings.Value.BaseUrl; // Ya lo tienes configurado

        // Determina la ruta de la carpeta de plantillas (ajusta si es necesario)
        var assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        _templateFolderPath = Path.Combine(assemblyPath, "EmailTemplates"); // Asume que están en EmailTemplates
    }

    public async Task<string> LoadAndPopulateTemplateAsync(string templateName, Dictionary<string, string> data)
    {
        var templatePath = Path.Combine(_templateFolderPath, templateName);

        if (!File.Exists(templatePath))
        {
            // Log.Error($"Plantilla de correo no encontrada: {templatePath}");
            return $"Error: Plantilla {templateName} no encontrada.";
        }

        try
        {
            var templateContent = await File.ReadAllTextAsync(templatePath, Encoding.UTF8); // Especifica Encoding por si acaso

            // Usa StringBuilder para eficiencia en múltiples reemplazos
            var emailBodyBuilder = new StringBuilder(templateContent);

            // Reemplazar placeholders del diccionario de datos
            foreach (var kvp in data)
            {
                // Sanitizar valores que podrían contener HTML (opcional pero recomendado para datos como 'motivoRechazo')
                // Decide si quieres escapar HTML o no basado en la naturaleza del placeholder.
                // Por ejemplo, Nombres o Fechas usualmente no necesitan escape, pero texto libre sí.
                // bool requiresHtmlEncoding = kvp.Key.Equals("MotivoRechazo", StringComparison.OrdinalIgnoreCase); // Ejemplo
                // string valueToInsert = requiresHtmlEncoding ? WebUtility.HtmlEncode(kvp.Value) : kvp.Value;

                // Para simplificar, por ahora no escaparemos, asumiendo que los datos son seguros
                // o ya están formateados. ¡Cuidado con XSS si los datos vienen del usuario!
                string valueToInsert = kvp.Value ?? string.Empty; // Asegura que no sea null

                emailBodyBuilder.Replace($"{{{{{kvp.Key}}}}}", valueToInsert); // Reemplaza {{Placeholder}}
            }

            // Reemplazar placeholders comunes/globales
            emailBodyBuilder.Replace("{{UrlBase}}", _baseUrl);
            emailBodyBuilder.Replace("{{AnioActual}}", DateTime.Now.Year.ToString());

            return emailBodyBuilder.ToString();
        }
        catch (Exception ex)
        {
            // Log.Error(ex, $"Error al cargar o poblar la plantilla '{templateName}'.");
            return $"Error: Fallo al procesar la plantilla {templateName}.";
        }
    }
}
