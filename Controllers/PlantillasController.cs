using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;

namespace COMAVI_SA.Controllers
{
    [Authorize(Roles = "admin,user")]
    public class PlantillasController : Controller
    {
        private readonly IWebHostEnvironment _webHostEnvironment;

        public PlantillasController(IWebHostEnvironment webHostEnvironment)
        {
            _webHostEnvironment = webHostEnvironment;
        }

        [Authorize(Roles = "admin,user")]
        [HttpGet]
        public IActionResult DescargarPlantilla(string tipoDocumento)
        {
            try
            {
                string fileName;
                string contentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
                string filePath;

                switch (tipoDocumento.ToLowerInvariant())
                {
                    case "licencia":
                        fileName = "Plantilla_Licencia_Conducir.docx";
                        break;
                    case "cedula":
                        fileName = "Plantilla_Cedula_Identidad.docx";
                        break;
                    case "inscripcion":
                        fileName = "Plantilla_Inscripcion_Vehiculo.docx";
                        break;
                    case "mantenimiento":
                        fileName = "Plantilla_Reporte_Mantenimiento.docx";
                        break;
                    default:
                        return NotFound("Tipo de plantilla no disponible");
                }

                // Obtener la ruta completa del archivo en wwwroot/plantillas
                filePath = Path.Combine(_webHostEnvironment.WebRootPath, "plantillas", fileName);

                // Verificar si el archivo existe
                if (!System.IO.File.Exists(filePath))
                {
                    return NotFound($"La plantilla {fileName} no fue encontrada.");
                }

                // Leer el archivo y devolverlo como FileResult
                var fileBytes = System.IO.File.ReadAllBytes(filePath);

                Response.Headers.Append("Content-Disposition", $"attachment; filename=\"{fileName}\"");
                Response.Headers.Append("X-Content-Type-Options", "nosniff");
                return File(fileBytes, contentType, fileName);
            }
            catch (Exception ex)
            {
                // Agregar manejo de excepciones para depuración
                return StatusCode(500, $"Error al acceder a la plantilla: {ex.Message}");
            }
        }
    }
}