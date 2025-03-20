using COMAVI_SA.Data;
using COMAVI_SA.Models;
using iText.IO.Font.Constants;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Draw;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace COMAVI_SA.Services
{
    public interface IReportService
    {
        Task<byte[]> GenerateDriverReportAsync(int userId);
        Task<byte[]> GenerateExpirationReportAsync(int userId);

        Task<byte[]> GenerarReporteCamionesPdf(List<CamionViewModel> camiones, string estado);
        Task<byte[]> GenerarReporteChoferesPdf(List<ChoferViewModel> choferes, string estado);
        Task<byte[]> GenerarReporteUsuariosPdf(List<UsuarioAdminViewModel> usuarios, string rol);
        Task<byte[]> GenerarReporteMantenimientosPdf(List<MantenimientoReporteViewModel> mantenimientos, DateTime fechaInicio, DateTime fechaFin);
        Task<byte[]> GenerarReporteDocumentosVencidosPdf(List<DocumentoVencimientoViewModel> documentos, List<ChoferViewModel> licencias, int diasPrevios);
    }


    public class ReportService : IReportService
    {
        private readonly ComaviDbContext _context;
        private readonly ILogger<ReportService> _logger;

        public ReportService(ComaviDbContext context, ILogger<ReportService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<byte[]> GenerateDriverReportAsync(int userId)
        {
            try
            {
                // Obtener datos del chofer
                var chofer = await _context.Choferes
                    .Include(c => c.Usuario)
                    .FirstOrDefaultAsync(c => c.id_usuario == userId);

                if (chofer == null)
                {
                    throw new Exception("No se encontró información del conductor");
                }

                // Obtener camión asignado
                var camion = await _context.Camiones
                    .FirstOrDefaultAsync(c => c.chofer_asignado == chofer.id_chofer);

                // Obtener documentos
                var documentos = await _context.Documentos
                    .Where(d => d.id_chofer == chofer.id_chofer)
                    .ToListAsync();

                // Generar el PDF
                using (MemoryStream stream = new MemoryStream())
                {
                    PdfWriter writer = new PdfWriter(stream);
                    PdfDocument pdf = new PdfDocument(writer);
                    Document document = new Document(pdf);

                    // Título
                    document.Add(new Paragraph("REPORTE DE CONDUCTOR")
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetFontSize(20)
                        .SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));

                    // Separador
                    LineSeparator ls = new LineSeparator(new SolidLine());
                    document.Add(ls);

                    // Fecha del reporte
                    document.Add(new Paragraph($"Fecha de generación: {DateTime.Now.ToString("dd/MM/yyyy HH:mm")}")
                        .SetTextAlignment(TextAlignment.RIGHT)
                        .SetFontSize(10)
                        .SetFont(PdfFontFactory.CreateFont(StandardFonts.TIMES_ITALIC)));

                    // Información personal
                    document.Add(new Paragraph("INFORMACIÓN PERSONAL")
                        .SetFontSize(14)
                        .SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD))
                        .SetBackgroundColor(ColorConstants.LIGHT_GRAY));

                    Table infoTable = new Table(2).UseAllAvailableWidth();
                    infoTable.AddCell(new Cell().Add(new Paragraph("Nombre completo:")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                    infoTable.AddCell(new Cell().Add(new Paragraph(chofer.nombreCompleto)));
                    infoTable.AddCell(new Cell().Add(new Paragraph("Correo electrónico:")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                    infoTable.AddCell(new Cell().Add(new Paragraph(chofer.Usuario?.correo_electronico ?? "No disponible")));
                    infoTable.AddCell(new Cell().Add(new Paragraph("Número de cédula:")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                    infoTable.AddCell(new Cell().Add(new Paragraph(chofer.numero_cedula)));
                    infoTable.AddCell(new Cell().Add(new Paragraph("Edad:")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                    infoTable.AddCell(new Cell().Add(new Paragraph(chofer.edad.ToString())));
                    infoTable.AddCell(new Cell().Add(new Paragraph("Género:")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                    infoTable.AddCell(new Cell().Add(new Paragraph(chofer.genero == "masculino" ? "Masculino" : "Femenino")));
                    infoTable.AddCell(new Cell().Add(new Paragraph("Estado:")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                    infoTable.AddCell(new Cell().Add(new Paragraph(chofer.estado)));
                    document.Add(infoTable);

                    document.Add(new Paragraph("\n"));

                    // Información de licencia
                    document.Add(new Paragraph("INFORMACIÓN DE LICENCIA")
                        .SetFontSize(14)
                        .SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD))
                        .SetBackgroundColor(ColorConstants.LIGHT_GRAY));

                    Table licenseTable = new Table(2).UseAllAvailableWidth();
                    licenseTable.AddCell(new Cell().Add(new Paragraph("Número de licencia:")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                    licenseTable.AddCell(new Cell().Add(new Paragraph(chofer.licencia)));
                    licenseTable.AddCell(new Cell().Add(new Paragraph("Fecha de vencimiento:")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                    licenseTable.AddCell(new Cell().Add(new Paragraph(chofer.fecha_venc_licencia.ToString("dd/MM/yyyy"))));

                    // Calcular días para vencimiento
                    int diasParaVencimiento = (int)(chofer.fecha_venc_licencia - DateTime.Now).TotalDays;
                    string estadoLicencia = diasParaVencimiento <= 0 ? "VENCIDA" :
                                           diasParaVencimiento <= 30 ? "PRÓXIMA A VENCER" : "VIGENTE";

                    licenseTable.AddCell(new Cell().Add(new Paragraph("Estado de licencia:")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                    Cell estadoCell = new Cell().Add(new Paragraph(estadoLicencia));
                    if (estadoLicencia == "VENCIDA")
                        estadoCell.SetBackgroundColor(ColorConstants.RED);
                    else if (estadoLicencia == "PRÓXIMA A VENCER")
                        estadoCell.SetBackgroundColor(ColorConstants.ORANGE);
                    else
                        estadoCell.SetBackgroundColor(ColorConstants.GREEN);

                    licenseTable.AddCell(estadoCell);

                    licenseTable.AddCell(new Cell().Add(new Paragraph("Días para vencimiento:")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                    licenseTable.AddCell(new Cell().Add(new Paragraph(diasParaVencimiento > 0 ? diasParaVencimiento.ToString() : "0 (VENCIDA)")));
                    document.Add(licenseTable);

                    document.Add(new Paragraph("\n"));

                    // Información del vehículo asignado
                    document.Add(new Paragraph("VEHÍCULO ASIGNADO")
                        .SetFontSize(14)
                        .SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD))
                        .SetBackgroundColor(ColorConstants.LIGHT_GRAY));

                    if (camion != null)
                    {
                        Table vehicleTable = new Table(2).UseAllAvailableWidth();
                        vehicleTable.AddCell(new Cell().Add(new Paragraph("Número de placa:")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                        vehicleTable.AddCell(new Cell().Add(new Paragraph(camion.numero_placa)));
                        vehicleTable.AddCell(new Cell().Add(new Paragraph("Marca:")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                        vehicleTable.AddCell(new Cell().Add(new Paragraph(camion.marca)));
                        vehicleTable.AddCell(new Cell().Add(new Paragraph("Modelo:")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                        vehicleTable.AddCell(new Cell().Add(new Paragraph(camion.modelo)));
                        vehicleTable.AddCell(new Cell().Add(new Paragraph("Año:")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                        vehicleTable.AddCell(new Cell().Add(new Paragraph(camion.anio.ToString())));
                        vehicleTable.AddCell(new Cell().Add(new Paragraph("Estado:")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                        vehicleTable.AddCell(new Cell().Add(new Paragraph(camion.estado)));
                        document.Add(vehicleTable);
                    }
                    else
                    {
                        document.Add(new Paragraph("No hay vehículo asignado actualmente.")
                            .SetFont(PdfFontFactory.CreateFont(StandardFonts.TIMES_ITALIC)));
                    }

                    document.Add(new Paragraph("\n"));

                    // Documentos
                    document.Add(new Paragraph("DOCUMENTOS REGISTRADOS")
                        .SetFontSize(14)
                        .SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD))
                        .SetBackgroundColor(ColorConstants.LIGHT_GRAY));

                    if (documentos.Count > 0)
                    {
                        Table docsTable = new Table(4).UseAllAvailableWidth();
                        docsTable.AddHeaderCell(new Cell().Add(new Paragraph("Tipo")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                        docsTable.AddHeaderCell(new Cell().Add(new Paragraph("Fecha Emisión")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                        docsTable.AddHeaderCell(new Cell().Add(new Paragraph("Fecha Vencimiento")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                        docsTable.AddHeaderCell(new Cell().Add(new Paragraph("Estado")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));

                        foreach (var doc in documentos)
                        {
                            docsTable.AddCell(new Cell().Add(new Paragraph(doc.tipo_documento)));
                            docsTable.AddCell(new Cell().Add(new Paragraph(doc.fecha_emision.ToString("dd/MM/yyyy"))));
                            docsTable.AddCell(new Cell().Add(new Paragraph(doc.fecha_vencimiento.ToString("dd/MM/yyyy"))));

                            Cell estadoDocCell = new Cell().Add(new Paragraph(doc.estado_validacion.ToUpper()));
                            if (doc.estado_validacion.ToLower() == "rechazado")
                                estadoDocCell.SetBackgroundColor(ColorConstants.RED);
                            else if (doc.estado_validacion.ToLower() == "pendiente")
                                estadoDocCell.SetBackgroundColor(ColorConstants.ORANGE);
                            else if (doc.estado_validacion.ToLower() == "verificado")
                                estadoDocCell.SetBackgroundColor(ColorConstants.GREEN);

                            docsTable.AddCell(estadoDocCell);
                        }
                        document.Add(docsTable);
                    }
                    else
                    {
                        document.Add(new Paragraph("No hay documentos registrados.")
                            .SetFont(PdfFontFactory.CreateFont(StandardFonts.TIMES_ITALIC)));
                    }

                    // Pie de página
                    document.Add(new Paragraph("\n\n"));
                    document.Add(new Paragraph("Este reporte ha sido generado automáticamente por el sistema COMAVI S.A.")
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetFontSize(8)
                        .SetFont(PdfFontFactory.CreateFont(StandardFonts.TIMES_ITALIC)));

                    document.Close();
                    return stream.ToArray();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar reporte de conductor");
                throw;
            }
        }

        public async Task<byte[]> GenerateExpirationReportAsync(int userId)
        {
            try
            {
                // Obtener datos del chofer
                var chofer = await _context.Choferes
                    .Include(c => c.Usuario)
                    .FirstOrDefaultAsync(c => c.id_usuario == userId);

                if (chofer == null)
                {
                    throw new Exception("No se encontró información del conductor");
                }

                // Obtener documentos
                var documentos = await _context.Documentos
                    .Where(d => d.id_chofer == chofer.id_chofer)
                    .OrderBy(d => d.fecha_vencimiento)
                    .ToListAsync();

                // Generar el PDF
                using (MemoryStream stream = new MemoryStream())
                {
                    PdfWriter writer = new PdfWriter(stream);
                    PdfDocument pdf = new PdfDocument(writer);
                    Document document = new Document(pdf);

                    // Título
                    document.Add(new Paragraph("REPORTE DE VENCIMIENTOS")
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetFontSize(20)
                        .SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));

                    // Separador
                    LineSeparator ls = new LineSeparator(new SolidLine());
                    document.Add(ls);

                    // Fecha del reporte
                    document.Add(new Paragraph($"Fecha de generación: {DateTime.Now.ToString("dd/MM/yyyy HH:mm")}")
                        .SetTextAlignment(TextAlignment.RIGHT)
                        .SetFontSize(10)
                        .SetFont(PdfFontFactory.CreateFont(StandardFonts.TIMES_ITALIC)));

                    // Información del conductor
                    document.Add(new Paragraph($"Conductor: {chofer.nombreCompleto}")
                        .SetFontSize(14)
                        .SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));

                    document.Add(new Paragraph($"Cédula: {chofer.numero_cedula}")
                        .SetFontSize(12));

                    document.Add(new Paragraph("\n"));

                    // Licencia de conducir
                    document.Add(new Paragraph("VENCIMIENTO DE LICENCIA")
                        .SetFontSize(14)
                        .SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD))
                        .SetBackgroundColor(ColorConstants.LIGHT_GRAY));

                    // Calcular días para vencimiento de licencia
                    int diasParaVencimientoLicencia = (int)(chofer.fecha_venc_licencia - DateTime.Now).TotalDays;
                    string estadoLicencia = diasParaVencimientoLicencia <= 0 ? "VENCIDA" :
                                          diasParaVencimientoLicencia <= 30 ? "PRÓXIMA A VENCER" : "VIGENTE";

                    Table licenseTable = new Table(2).UseAllAvailableWidth();
                    licenseTable.AddCell(new Cell().Add(new Paragraph("Número de licencia:")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                    licenseTable.AddCell(new Cell().Add(new Paragraph(chofer.licencia)));
                    licenseTable.AddCell(new Cell().Add(new Paragraph("Fecha de vencimiento:")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                    licenseTable.AddCell(new Cell().Add(new Paragraph(chofer.fecha_venc_licencia.ToString("dd/MM/yyyy"))));
                    licenseTable.AddCell(new Cell().Add(new Paragraph("Estado:")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));

                    Cell estadoLicCell = new Cell().Add(new Paragraph(estadoLicencia));
                    if (estadoLicencia == "VENCIDA")
                        estadoLicCell.SetBackgroundColor(ColorConstants.RED);
                    else if (estadoLicencia == "PRÓXIMA A VENCER")
                        estadoLicCell.SetBackgroundColor(ColorConstants.ORANGE);
                    else
                        estadoLicCell.SetBackgroundColor(ColorConstants.GREEN);

                    licenseTable.AddCell(estadoLicCell);

                    licenseTable.AddCell(new Cell().Add(new Paragraph("Días para vencimiento:")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                    licenseTable.AddCell(new Cell().Add(new Paragraph(diasParaVencimientoLicencia > 0 ? diasParaVencimientoLicencia.ToString() : "0 (VENCIDA)")));
                    document.Add(licenseTable);

                    document.Add(new Paragraph("\n"));

                    // Documentos
                    document.Add(new Paragraph("VENCIMIENTOS DE DOCUMENTOS")
                        .SetFontSize(14)
                        .SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD))
                        .SetBackgroundColor(ColorConstants.LIGHT_GRAY));

                    if (documentos.Count > 0)
                    {
                        Table docsTable = new Table(4).UseAllAvailableWidth();
                        docsTable.AddHeaderCell(new Cell().Add(new Paragraph("Tipo")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                        docsTable.AddHeaderCell(new Cell().Add(new Paragraph("Vencimiento")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                        docsTable.AddHeaderCell(new Cell().Add(new Paragraph("Días restantes")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                        docsTable.AddHeaderCell(new Cell().Add(new Paragraph("Estado")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));

                        foreach (var doc in documentos)
                        {
                            int diasParaVencimiento = (int)(doc.fecha_vencimiento - DateTime.Now).TotalDays;
                            string estadoDoc = diasParaVencimiento <= 0 ? "VENCIDO" :
                                              diasParaVencimiento <= 30 ? "PRÓXIMO A VENCER" : "VIGENTE";

                            docsTable.AddCell(new Cell().Add(new Paragraph(doc.tipo_documento)));
                            docsTable.AddCell(new Cell().Add(new Paragraph(doc.fecha_vencimiento.ToString("dd/MM/yyyy"))));
                            docsTable.AddCell(new Cell().Add(new Paragraph(diasParaVencimiento > 0 ? diasParaVencimiento.ToString() : "0 (VENCIDO)")));

                            Cell estadoDocCell = new Cell().Add(new Paragraph(estadoDoc));
                            if (estadoDoc == "VENCIDO")
                                estadoDocCell.SetBackgroundColor(ColorConstants.RED);
                            else if (estadoDoc == "PRÓXIMO A VENCER")
                                estadoDocCell.SetBackgroundColor(ColorConstants.ORANGE);
                            else
                                estadoDocCell.SetBackgroundColor(ColorConstants.GREEN);

                            docsTable.AddCell(estadoDocCell);
                        }
                        document.Add(docsTable);
                    }
                    else
                    {
                        document.Add(new Paragraph("No hay documentos registrados.")
                            .SetFont(PdfFontFactory.CreateFont(StandardFonts.TIMES_ITALIC)));
                    }

                    // Resumen de vencimientos
                    document.Add(new Paragraph("\n"));
                    document.Add(new Paragraph("RESUMEN DE VENCIMIENTOS")
                        .SetFontSize(14)
                        .SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD))
                        .SetBackgroundColor(ColorConstants.LIGHT_GRAY));

                    int docsVencidos = documentos.Count(d => (d.fecha_vencimiento - DateTime.Now).TotalDays <= 0);
                    int docsProximosVencer = documentos.Count(d => (d.fecha_vencimiento - DateTime.Now).TotalDays > 0 && (d.fecha_vencimiento - DateTime.Now).TotalDays <= 30);
                    int docsVigentes = documentos.Count(d => (d.fecha_vencimiento - DateTime.Now).TotalDays > 30);

                    Table summaryTable = new Table(2).UseAllAvailableWidth();
                    summaryTable.AddCell(new Cell().Add(new Paragraph("Documentos vencidos:")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                    summaryTable.AddCell(new Cell().Add(new Paragraph(docsVencidos.ToString())));
                    summaryTable.AddCell(new Cell().Add(new Paragraph("Documentos próximos a vencer:")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                    summaryTable.AddCell(new Cell().Add(new Paragraph(docsProximosVencer.ToString())));
                    summaryTable.AddCell(new Cell().Add(new Paragraph("Documentos vigentes:")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                    summaryTable.AddCell(new Cell().Add(new Paragraph(docsVigentes.ToString())));

                    // Estado general
                    string estadoGeneral;
                    if (docsVencidos > 0 || diasParaVencimientoLicencia <= 0)
                        estadoGeneral = "REQUIERE ATENCIÓN INMEDIATA";
                    else if (docsProximosVencer > 0 || diasParaVencimientoLicencia <= 30)
                        estadoGeneral = "REQUIERE REVISIÓN PRÓXIMA";
                    else
                        estadoGeneral = "DOCUMENTACIÓN EN REGLA";

                    summaryTable.AddCell(new Cell().Add(new Paragraph("Estado general:")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                    Cell estadoGeneralCell = new Cell().Add(new Paragraph(estadoGeneral));
                    if (estadoGeneral == "REQUIERE ATENCIÓN INMEDIATA")
                        estadoGeneralCell.SetBackgroundColor(ColorConstants.RED);
                    else if (estadoGeneral == "REQUIERE REVISIÓN PRÓXIMA")
                        estadoGeneralCell.SetBackgroundColor(ColorConstants.ORANGE);
                    else
                        estadoGeneralCell.SetBackgroundColor(ColorConstants.GREEN);

                    summaryTable.AddCell(estadoGeneralCell);

                    document.Add(summaryTable);

                    // Pie de página
                    document.Add(new Paragraph("\n\n"));
                    document.Add(new Paragraph("Este reporte ha sido generado automáticamente por el sistema COMAVI S.A.")
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetFontSize(8)
                        .SetFont(PdfFontFactory.CreateFont(StandardFonts.TIMES_ITALIC)));

                    document.Close();
                    return stream.ToArray();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar reporte de vencimientos");
                throw;
            }
        }

        public async Task<byte[]> GenerarReporteCamionesPdf(List<CamionViewModel> camiones, string estado)
        {
            try
            {
                using (MemoryStream stream = new MemoryStream())
                {
                    PdfWriter writer = new PdfWriter(stream);
                    PdfDocument pdf = new PdfDocument(writer);
                    Document document = new Document(pdf);

                    // Título
                    document.Add(new Paragraph("REPORTE DE CAMIONES")
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetFontSize(20)
                        .SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));

                    // Separador
                    LineSeparator ls = new LineSeparator(new SolidLine());
                    document.Add(ls);

                    // Fecha del reporte
                    document.Add(new Paragraph($"Fecha de generación: {DateTime.Now.ToString("dd/MM/yyyy HH:mm")}")
                        .SetTextAlignment(TextAlignment.RIGHT)
                        .SetFontSize(10)
                        .SetFont(PdfFontFactory.CreateFont(StandardFonts.TIMES_ITALIC)));

                    // Filtro aplicado
                    string estadoTexto = string.IsNullOrEmpty(estado) ? "Todos" : estado.ToUpper();
                    document.Add(new Paragraph($"Filtro por estado: {estadoTexto}")
                        .SetTextAlignment(TextAlignment.LEFT)
                        .SetFontSize(12));

                    document.Add(new Paragraph("\n"));

                    // Tabla de camiones
                    Table table = new Table(6).UseAllAvailableWidth();
                    table.AddHeaderCell(new Cell().Add(new Paragraph("Placa")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                    table.AddHeaderCell(new Cell().Add(new Paragraph("Marca")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                    table.AddHeaderCell(new Cell().Add(new Paragraph("Modelo")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                    table.AddHeaderCell(new Cell().Add(new Paragraph("Año")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                    table.AddHeaderCell(new Cell().Add(new Paragraph("Estado")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                    table.AddHeaderCell(new Cell().Add(new Paragraph("Chofer Asignado")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));

                    foreach (var camion in camiones)
                    {
                        table.AddCell(new Cell().Add(new Paragraph(camion.numero_placa)));
                        table.AddCell(new Cell().Add(new Paragraph(camion.marca)));
                        table.AddCell(new Cell().Add(new Paragraph(camion.modelo)));
                        table.AddCell(new Cell().Add(new Paragraph(camion.anio.ToString())));

                        Cell estadoCell = new Cell().Add(new Paragraph(camion.estado.ToUpper()));
                        if (camion.estado.ToLower() == "mantenimiento")
                            estadoCell.SetBackgroundColor(ColorConstants.ORANGE);
                        else if (camion.estado.ToLower() == "inactivo")
                            estadoCell.SetBackgroundColor(ColorConstants.LIGHT_GRAY);
                        else if (camion.estado.ToLower() == "activo")
                            estadoCell.SetBackgroundColor(ColorConstants.GREEN);

                        table.AddCell(estadoCell);
                        table.AddCell(new Cell().Add(new Paragraph(camion.NombreChofer ?? "No asignado")));
                    }

                    document.Add(table);

                    // Resumen
                    document.Add(new Paragraph("\n"));
                    document.Add(new Paragraph("RESUMEN")
                        .SetFontSize(14)
                        .SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD))
                        .SetBackgroundColor(ColorConstants.LIGHT_GRAY));

                    Table summaryTable = new Table(2).UseAllAvailableWidth();
                    summaryTable.AddCell(new Cell().Add(new Paragraph("Total de camiones:")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                    summaryTable.AddCell(new Cell().Add(new Paragraph(camiones.Count.ToString())));

                    // Contar por estado
                    var activosCount = camiones.Count(c => c.estado.ToLower() == "activo");
                    var mantenimientoCount = camiones.Count(c => c.estado.ToLower() == "mantenimiento");
                    var inactivosCount = camiones.Count(c => c.estado.ToLower() == "inactivo");

                    summaryTable.AddCell(new Cell().Add(new Paragraph("Camiones activos:")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                    summaryTable.AddCell(new Cell().Add(new Paragraph(activosCount.ToString())));
                    summaryTable.AddCell(new Cell().Add(new Paragraph("Camiones en mantenimiento:")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                    summaryTable.AddCell(new Cell().Add(new Paragraph(mantenimientoCount.ToString())));
                    summaryTable.AddCell(new Cell().Add(new Paragraph("Camiones inactivos:")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                    summaryTable.AddCell(new Cell().Add(new Paragraph(inactivosCount.ToString())));

                    document.Add(summaryTable);

                    // Pie de página
                    document.Add(new Paragraph("\n\n"));
                    document.Add(new Paragraph("Este reporte ha sido generado automáticamente por el sistema COMAVI S.A.")
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetFontSize(8)
                        .SetFont(PdfFontFactory.CreateFont(StandardFonts.TIMES_ITALIC)));

                    document.Close();
                    return stream.ToArray();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar reporte de camiones en PDF");
                throw;
            }
        }

        public async Task<byte[]> GenerarReporteChoferesPdf(List<ChoferViewModel> choferes, string estado)
        {
            try
            {
                using (MemoryStream stream = new MemoryStream())
                {
                    PdfWriter writer = new PdfWriter(stream);
                    PdfDocument pdf = new PdfDocument(writer);
                    Document document = new Document(pdf);

                    // Título
                    document.Add(new Paragraph("REPORTE DE CHOFERES")
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetFontSize(20)
                        .SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));

                    // Separador
                    LineSeparator ls = new LineSeparator(new SolidLine());
                    document.Add(ls);

                    // Fecha del reporte
                    document.Add(new Paragraph($"Fecha de generación: {DateTime.Now.ToString("dd/MM/yyyy HH:mm")}")
                        .SetTextAlignment(TextAlignment.RIGHT)
                        .SetFontSize(10)
                        .SetFont(PdfFontFactory.CreateFont(StandardFonts.TIMES_ITALIC)));

                    // Filtro aplicado
                    string estadoTexto = string.IsNullOrEmpty(estado) ? "Todos" : estado.ToUpper();
                    document.Add(new Paragraph($"Filtro por estado: {estadoTexto}")
                        .SetTextAlignment(TextAlignment.LEFT)
                        .SetFontSize(12));

                    document.Add(new Paragraph("\n"));

                    // Tabla de choferes
                    Table table = new Table(7).UseAllAvailableWidth();
                    table.AddHeaderCell(new Cell().Add(new Paragraph("Nombre")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                    table.AddHeaderCell(new Cell().Add(new Paragraph("Cédula")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                    table.AddHeaderCell(new Cell().Add(new Paragraph("Licencia")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                    table.AddHeaderCell(new Cell().Add(new Paragraph("Estado Licencia")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                    table.AddHeaderCell(new Cell().Add(new Paragraph("Vencimiento")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                    table.AddHeaderCell(new Cell().Add(new Paragraph("Estado")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                    table.AddHeaderCell(new Cell().Add(new Paragraph("Camión Asignado")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));

                    foreach (var chofer in choferes)
                    {
                        table.AddCell(new Cell().Add(new Paragraph(chofer.nombreCompleto)));
                        table.AddCell(new Cell().Add(new Paragraph(chofer.numero_cedula)));
                        table.AddCell(new Cell().Add(new Paragraph(chofer.licencia)));

                        Cell estadoLicCell = new Cell().Add(new Paragraph(chofer.estado_licencia.ToUpper()));
                        if (chofer.estado_licencia.ToLower() == "vencida")
                            estadoLicCell.SetBackgroundColor(ColorConstants.RED);
                        else if (chofer.estado_licencia.ToLower() == "por vencer")
                            estadoLicCell.SetBackgroundColor(ColorConstants.ORANGE);
                        else
                            estadoLicCell.SetBackgroundColor(ColorConstants.GREEN);

                        table.AddCell(estadoLicCell);
                        table.AddCell(new Cell().Add(new Paragraph(chofer.fecha_venc_licencia.ToString("dd/MM/yyyy"))));

                        Cell estadoCell = new Cell().Add(new Paragraph(chofer.estado.ToUpper()));
                        if (chofer.estado.ToLower() == "inactivo")
                            estadoCell.SetBackgroundColor(ColorConstants.LIGHT_GRAY);
                        else if (chofer.estado.ToLower() == "activo")
                            estadoCell.SetBackgroundColor(ColorConstants.GREEN);

                        table.AddCell(estadoCell);
                        table.AddCell(new Cell().Add(new Paragraph(chofer.camion_asignado ?? "No asignado")));
                    }

                    document.Add(table);

                    // Resumen
                    document.Add(new Paragraph("\n"));
                    document.Add(new Paragraph("RESUMEN")
                        .SetFontSize(14)
                        .SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD))
                        .SetBackgroundColor(ColorConstants.LIGHT_GRAY));

                    Table summaryTable = new Table(2).UseAllAvailableWidth();
                    summaryTable.AddCell(new Cell().Add(new Paragraph("Total de choferes:")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                    summaryTable.AddCell(new Cell().Add(new Paragraph(choferes.Count.ToString())));

                    // Contar por estado
                    var activosCount = choferes.Count(c => c.estado.ToLower() == "activo");
                    var inactivosCount = choferes.Count(c => c.estado.ToLower() == "inactivo");

                    // Contar por estado de licencia
                    var licenciasVencidas = choferes.Count(c => c.estado_licencia.ToLower() == "vencida");
                    var licenciasPorVencer = choferes.Count(c => c.estado_licencia.ToLower() == "por vencer");
                    var licenciasVigentes = choferes.Count(c => c.estado_licencia.ToLower() == "vigente");

                    summaryTable.AddCell(new Cell().Add(new Paragraph("Choferes activos:")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                    summaryTable.AddCell(new Cell().Add(new Paragraph(activosCount.ToString())));
                    summaryTable.AddCell(new Cell().Add(new Paragraph("Choferes inactivos:")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                    summaryTable.AddCell(new Cell().Add(new Paragraph(inactivosCount.ToString())));
                    summaryTable.AddCell(new Cell().Add(new Paragraph("Licencias vencidas:")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                    summaryTable.AddCell(new Cell().Add(new Paragraph(licenciasVencidas.ToString())));
                    summaryTable.AddCell(new Cell().Add(new Paragraph("Licencias por vencer:")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                    summaryTable.AddCell(new Cell().Add(new Paragraph(licenciasPorVencer.ToString())));
                    summaryTable.AddCell(new Cell().Add(new Paragraph("Licencias vigentes:")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                    summaryTable.AddCell(new Cell().Add(new Paragraph(licenciasVigentes.ToString())));

                    document.Add(summaryTable);

                    // Pie de página
                    document.Add(new Paragraph("\n\n"));
                    document.Add(new Paragraph("Este reporte ha sido generado automáticamente por el sistema COMAVI S.A.")
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetFontSize(8)
                        .SetFont(PdfFontFactory.CreateFont(StandardFonts.TIMES_ITALIC)));

                    document.Close();
                    return stream.ToArray();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar reporte de choferes en PDF");
                throw;
            }
        }

        public async Task<byte[]> GenerarReporteUsuariosPdf(List<UsuarioAdminViewModel> usuarios, string rol)
        {
            try
            {
                using (MemoryStream stream = new MemoryStream())
                {
                    PdfWriter writer = new PdfWriter(stream);
                    PdfDocument pdf = new PdfDocument(writer);
                    Document document = new Document(pdf);

                    // Título
                    document.Add(new Paragraph("REPORTE DE USUARIOS")
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetFontSize(20)
                        .SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));

                    // Separador
                    LineSeparator ls = new LineSeparator(new SolidLine());
                    document.Add(ls);

                    // Fecha del reporte
                    document.Add(new Paragraph($"Fecha de generación: {DateTime.Now.ToString("dd/MM/yyyy HH:mm")}")
                        .SetTextAlignment(TextAlignment.RIGHT)
                        .SetFontSize(10)
                        .SetFont(PdfFontFactory.CreateFont(StandardFonts.TIMES_ITALIC)));

                    // Filtro aplicado
                    string rolTexto = string.IsNullOrEmpty(rol) ? "Todos" : rol.ToUpper();
                    document.Add(new Paragraph($"Filtro por rol: {rolTexto}")
                        .SetTextAlignment(TextAlignment.LEFT)
                        .SetFontSize(12));

                    document.Add(new Paragraph("\n"));

                    // Tabla de usuarios
                    Table table = new Table(5).UseAllAvailableWidth();
                    table.AddHeaderCell(new Cell().Add(new Paragraph("ID")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                    table.AddHeaderCell(new Cell().Add(new Paragraph("Nombre")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                    table.AddHeaderCell(new Cell().Add(new Paragraph("Email")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                    table.AddHeaderCell(new Cell().Add(new Paragraph("Rol")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                    table.AddHeaderCell(new Cell().Add(new Paragraph("Estado")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));

                    foreach (var usuario in usuarios)
                    {
                        table.AddCell(new Cell().Add(new Paragraph(usuario.id_usuario.ToString())));
                        table.AddCell(new Cell().Add(new Paragraph(usuario.nombre_usuario)));
                        table.AddCell(new Cell().Add(new Paragraph(usuario.correo_electronico)));
                        table.AddCell(new Cell().Add(new Paragraph(usuario.rol)));

                        Cell estadoCell = new Cell().Add(new Paragraph(usuario.estado_verificacion.ToUpper()));
                        if (usuario.estado_verificacion.ToLower() == "pendiente")
                            estadoCell.SetBackgroundColor(ColorConstants.ORANGE);
                        else if (usuario.estado_verificacion.ToLower() == "rechazado")
                            estadoCell.SetBackgroundColor(ColorConstants.RED);
                        else if (usuario.estado_verificacion.ToLower() == "verificado")
                            estadoCell.SetBackgroundColor(ColorConstants.GREEN);

                        table.AddCell(estadoCell);
                    }

                    document.Add(table);

                    // Resumen
                    document.Add(new Paragraph("\n"));
                    document.Add(new Paragraph("RESUMEN")
                        .SetFontSize(14)
                        .SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD))
                        .SetBackgroundColor(ColorConstants.LIGHT_GRAY));

                    Table summaryTable = new Table(2).UseAllAvailableWidth();
                    summaryTable.AddCell(new Cell().Add(new Paragraph("Total de usuarios:")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                    summaryTable.AddCell(new Cell().Add(new Paragraph(usuarios.Count.ToString())));

                    // Contar por rol
                    var rolesCount = usuarios.GroupBy(u => u.rol)
                                             .Select(g => new { Rol = g.Key, Count = g.Count() });

                    foreach (var rolCount in rolesCount)
                    {
                        summaryTable.AddCell(new Cell().Add(new Paragraph($"Usuarios con rol {rolCount.Rol}:")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                        summaryTable.AddCell(new Cell().Add(new Paragraph(rolCount.Count.ToString())));
                    }

                    // Contar por estado
                    var verificadosCount = usuarios.Count(u => u.estado_verificacion.ToLower() == "verificado");
                    var pendientesCount = usuarios.Count(u => u.estado_verificacion.ToLower() == "pendiente");
                    var rechazadosCount = usuarios.Count(u => u.estado_verificacion.ToLower() == "rechazado");

                    summaryTable.AddCell(new Cell().Add(new Paragraph("Usuarios verificados:")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                    summaryTable.AddCell(new Cell().Add(new Paragraph(verificadosCount.ToString())));
                    summaryTable.AddCell(new Cell().Add(new Paragraph("Usuarios pendientes:")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                    summaryTable.AddCell(new Cell().Add(new Paragraph(pendientesCount.ToString())));
                    summaryTable.AddCell(new Cell().Add(new Paragraph("Usuarios rechazados:")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                    summaryTable.AddCell(new Cell().Add(new Paragraph(rechazadosCount.ToString())));

                    document.Add(summaryTable);

                    // Pie de página
                    document.Add(new Paragraph("\n\n"));
                    document.Add(new Paragraph("Este reporte ha sido generado automáticamente por el sistema COMAVI S.A.")
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetFontSize(8)
                        .SetFont(PdfFontFactory.CreateFont(StandardFonts.TIMES_ITALIC)));

                    document.Close();
                    return stream.ToArray();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar reporte de usuarios en PDF");
                throw;
            }
        }

        public async Task<byte[]> GenerarReporteMantenimientosPdf(List<MantenimientoReporteViewModel> mantenimientos, DateTime fechaInicio, DateTime fechaFin)
        {
            try
            {
                using (MemoryStream stream = new MemoryStream())
                {
                    PdfWriter writer = new PdfWriter(stream);
                    PdfDocument pdf = new PdfDocument(writer);
                    Document document = new Document(pdf);

                    // Título
                    document.Add(new Paragraph("REPORTE DE MANTENIMIENTOS")
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetFontSize(20)
                        .SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));

                    // Separador
                    LineSeparator ls = new LineSeparator(new SolidLine());
                    document.Add(ls);

                    // Fecha del reporte
                    document.Add(new Paragraph($"Fecha de generación: {DateTime.Now.ToString("dd/MM/yyyy HH:mm")}")
                        .SetTextAlignment(TextAlignment.RIGHT)
                        .SetFontSize(10)
                        .SetFont(PdfFontFactory.CreateFont(StandardFonts.TIMES_ITALIC)));

                    // Período del reporte
                    document.Add(new Paragraph($"Período: {fechaInicio.ToString("dd/MM/yyyy")} - {fechaFin.ToString("dd/MM/yyyy")}")
                        .SetTextAlignment(TextAlignment.LEFT)
                        .SetFontSize(12));

                    document.Add(new Paragraph("\n"));

                    // Tabla de mantenimientos
                    Table table = new Table(5).UseAllAvailableWidth();
                    table.AddHeaderCell(new Cell().Add(new Paragraph("Camión")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                    table.AddHeaderCell(new Cell().Add(new Paragraph("Fecha")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                    table.AddHeaderCell(new Cell().Add(new Paragraph("Descripción")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                    table.AddHeaderCell(new Cell().Add(new Paragraph("Costo")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                    table.AddHeaderCell(new Cell().Add(new Paragraph("Estado")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));

                    foreach (var mantenimiento in mantenimientos)
                    {
                        table.AddCell(new Cell().Add(new Paragraph(mantenimiento.marca +" - "+ mantenimiento.modelo)));
                        table.AddCell(new Cell().Add(new Paragraph(mantenimiento.fecha_mantenimiento.ToString("dd/MM/yyyy"))));
                        table.AddCell(new Cell().Add(new Paragraph(mantenimiento.descripcion)));
                        table.AddCell(new Cell().Add(new Paragraph($"${mantenimiento.costo:N2}")));

                        // Determinar estado según la fecha
                        string estado = DateTime.Now.Date > mantenimiento.fecha_mantenimiento.Date
                            ? "COMPLETADO"
                            : "PROGRAMADO";

                        Cell estadoCell = new Cell().Add(new Paragraph(estado));
                        if (estado == "COMPLETADO")
                            estadoCell.SetBackgroundColor(ColorConstants.GREEN);
                        else
                            estadoCell.SetBackgroundColor(ColorConstants.ORANGE);

                        table.AddCell(estadoCell);
                    }

                    document.Add(table);

                    // Resumen financiero
                    document.Add(new Paragraph("\n"));
                    document.Add(new Paragraph("RESUMEN FINANCIERO")
                        .SetFontSize(14)
                        .SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD))
                        .SetBackgroundColor(ColorConstants.LIGHT_GRAY));

                    Table summaryTable = new Table(2).UseAllAvailableWidth();

                    // Calcular costos totales
                    decimal costoTotal = mantenimientos.Sum(m => m.costo);
                    var costosPorMes = mantenimientos
                        .GroupBy(m => new { m.fecha_mantenimiento.Year, m.fecha_mantenimiento.Month })
                        .Select(g => new {
                            Mes = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMMM yyyy", new CultureInfo("es-ES")),
                            Total = g.Sum(m => m.costo)
                        });

                    summaryTable.AddCell(new Cell().Add(new Paragraph("Costo total:")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                    summaryTable.AddCell(new Cell().Add(new Paragraph($"${costoTotal:N2}")));

                    foreach (var costoMes in costosPorMes)
                    {
                        summaryTable.AddCell(new Cell().Add(new Paragraph($"Costo {costoMes.Mes}:")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                        summaryTable.AddCell(new Cell().Add(new Paragraph($"${costoMes.Total:N2}")));
                    }

                    // Costo promedio por mantenimiento
                    if (mantenimientos.Count > 0)
                    {
                        decimal costoPromedio = costoTotal / mantenimientos.Count;
                        summaryTable.AddCell(new Cell().Add(new Paragraph("Costo promedio por mantenimiento:")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                        summaryTable.AddCell(new Cell().Add(new Paragraph($"${costoPromedio:N2}")));
                    }

                    document.Add(summaryTable);

                    // Pie de página
                    document.Add(new Paragraph("\n\n"));
                    document.Add(new Paragraph("Este reporte ha sido generado automáticamente por el sistema COMAVI S.A.")
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetFontSize(8)
                        .SetFont(PdfFontFactory.CreateFont(StandardFonts.TIMES_ITALIC)));

                    document.Close();
                    return stream.ToArray();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar reporte de mantenimientos en PDF");
                throw;
            }
        }

        public async Task<byte[]> GenerarReporteDocumentosVencidosPdf(List<DocumentoVencimientoViewModel> documentos, List<ChoferViewModel> licencias, int diasPrevios)
        {
            try
            {
                using (MemoryStream stream = new MemoryStream())
                {
                    PdfWriter writer = new PdfWriter(stream);
                    PdfDocument pdf = new PdfDocument(writer);
                    Document document = new Document(pdf);

                    // Título
                    document.Add(new Paragraph("REPORTE DE DOCUMENTOS PRÓXIMOS A VENCER")
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetFontSize(20)
                        .SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));

                    // Separador
                    LineSeparator ls = new LineSeparator(new SolidLine());
                    document.Add(ls);

                    // Fecha del reporte
                    document.Add(new Paragraph($"Fecha de generación: {DateTime.Now.ToString("dd/MM/yyyy HH:mm")}")
                        .SetTextAlignment(TextAlignment.RIGHT)
                        .SetFontSize(10)
                        .SetFont(PdfFontFactory.CreateFont(StandardFonts.TIMES_ITALIC)));

                    // Criterio de vencimiento
                    document.Add(new Paragraph($"Criterio: Documentos a vencer en los próximos {diasPrevios} días")
                        .SetTextAlignment(TextAlignment.LEFT)
                        .SetFontSize(12));

                    document.Add(new Paragraph("\n"));

                    // Licencias de conducir por vencer
                    document.Add(new Paragraph("LICENCIAS DE CONDUCIR POR VENCER")
                        .SetFontSize(14)
                        .SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD))
                        .SetBackgroundColor(ColorConstants.LIGHT_GRAY));

                    if (licencias.Count > 0)
                    {
                        Table licTable = new Table(5).UseAllAvailableWidth();
                        licTable.AddHeaderCell(new Cell().Add(new Paragraph("Chofer")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                        licTable.AddHeaderCell(new Cell().Add(new Paragraph("Licencia")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                        licTable.AddHeaderCell(new Cell().Add(new Paragraph("Fecha Vencimiento")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                        licTable.AddHeaderCell(new Cell().Add(new Paragraph("Días Restantes")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                        licTable.AddHeaderCell(new Cell().Add(new Paragraph("Estado")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));

                        foreach (var lic in licencias)
                        {
                            int diasParaVencimiento = (int)(lic.fecha_venc_licencia - DateTime.Now).TotalDays;

                            licTable.AddCell(new Cell().Add(new Paragraph(lic.nombreCompleto)));
                            licTable.AddCell(new Cell().Add(new Paragraph(lic.licencia)));
                            licTable.AddCell(new Cell().Add(new Paragraph(lic.fecha_venc_licencia.ToString("dd/MM/yyyy"))));
                            licTable.AddCell(new Cell().Add(new Paragraph(diasParaVencimiento > 0 ? diasParaVencimiento.ToString() : "0 (VENCIDA)")));

                            Cell estadoCell = new Cell().Add(new Paragraph(lic.estado_licencia.ToUpper()));
                            if (lic.estado_licencia.ToLower() == "vencida")
                                estadoCell.SetBackgroundColor(ColorConstants.RED);
                            else if (lic.estado_licencia.ToLower() == "por vencer")
                                estadoCell.SetBackgroundColor(ColorConstants.ORANGE);
                            else
                                estadoCell.SetBackgroundColor(ColorConstants.GREEN);

                            licTable.AddCell(estadoCell);
                        }

                        document.Add(licTable);
                    }
                    else
                    {
                        document.Add(new Paragraph("No hay licencias próximas a vencer en el período especificado.")
                            .SetFont(PdfFontFactory.CreateFont(StandardFonts.TIMES_ITALIC)));
                    }

                    document.Add(new Paragraph("\n"));

                    // Documentos por vencer
                    document.Add(new Paragraph("DOCUMENTOS POR VENCER")
                        .SetFontSize(14)
                        .SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD))
                        .SetBackgroundColor(ColorConstants.LIGHT_GRAY));

                    if (documentos.Count > 0)
                    {
                        Table docsTable = new Table(5).UseAllAvailableWidth();
                        docsTable.AddHeaderCell(new Cell().Add(new Paragraph("Chofer")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                        docsTable.AddHeaderCell(new Cell().Add(new Paragraph("Documento")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                        docsTable.AddHeaderCell(new Cell().Add(new Paragraph("Fecha Vencimiento")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                        docsTable.AddHeaderCell(new Cell().Add(new Paragraph("Días Restantes")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                        docsTable.AddHeaderCell(new Cell().Add(new Paragraph("Estado")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));

                        foreach (var doc in documentos)
                        {
                            docsTable.AddCell(new Cell().Add(new Paragraph(doc.nombreChofer)));
                            docsTable.AddCell(new Cell().Add(new Paragraph(doc.tipo_documento)));
                            docsTable.AddCell(new Cell().Add(new Paragraph(doc.fecha_vencimiento.ToString("dd/MM/yyyy"))));
                            docsTable.AddCell(new Cell().Add(new Paragraph(doc.diasParaVencimiento > 0 ? doc.diasParaVencimiento.ToString() : "0 (VENCIDO)")));

                            Cell estadoCell = new Cell().Add(new Paragraph(doc.estadoDocumento.ToUpper()));
                            if (doc.estadoDocumento.ToLower() == "vencido")
                                estadoCell.SetBackgroundColor(ColorConstants.RED);
                            else if (doc.estadoDocumento.ToLower() == "por vencer")
                                estadoCell.SetBackgroundColor(ColorConstants.ORANGE);
                            else
                                estadoCell.SetBackgroundColor(ColorConstants.GREEN);

                            docsTable.AddCell(estadoCell);
                        }

                        document.Add(docsTable);
                    }
                    else
                    {
                        document.Add(new Paragraph("No hay documentos próximos a vencer en el período especificado.")
                            .SetFont(PdfFontFactory.CreateFont(StandardFonts.TIMES_ITALIC)));
                    }

                    document.Add(new Paragraph("\n"));

                    // Resumen
                    document.Add(new Paragraph("RESUMEN GENERAL")
                        .SetFontSize(14)
                        .SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD))
                        .SetBackgroundColor(ColorConstants.LIGHT_GRAY));

                    Table summaryTable = new Table(2).UseAllAvailableWidth();

                    // Licencias
                    int licenciasVencidas = licencias.Count(l => l.estado_licencia.ToLower() == "vencida");
                    int licenciasPorVencer = licencias.Count(l => l.estado_licencia.ToLower() == "por vencer");

                    summaryTable.AddCell(new Cell().Add(new Paragraph("Total de licencias a revisar:")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                    summaryTable.AddCell(new Cell().Add(new Paragraph(licencias.Count.ToString())));
                    summaryTable.AddCell(new Cell().Add(new Paragraph("Licencias vencidas:")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                    summaryTable.AddCell(new Cell().Add(new Paragraph(licenciasVencidas.ToString())));
                    summaryTable.AddCell(new Cell().Add(new Paragraph("Licencias por vencer:")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                    summaryTable.AddCell(new Cell().Add(new Paragraph(licenciasPorVencer.ToString())));

                    // Documentos
                    int docsVencidos = documentos.Count(d => d.estadoDocumento.ToLower() == "vencido");
                    int docsPorVencer = documentos.Count(d => d.estadoDocumento.ToLower() == "por vencer");

                    summaryTable.AddCell(new Cell().Add(new Paragraph("Total de documentos a revisar:")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                    summaryTable.AddCell(new Cell().Add(new Paragraph(documentos.Count.ToString())));
                    summaryTable.AddCell(new Cell().Add(new Paragraph("Documentos vencidos:")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                    summaryTable.AddCell(new Cell().Add(new Paragraph(docsVencidos.ToString())));
                    summaryTable.AddCell(new Cell().Add(new Paragraph("Documentos por vencer:")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                    summaryTable.AddCell(new Cell().Add(new Paragraph(docsPorVencer.ToString())));

                    // Total documentos a revisar
                    summaryTable.AddCell(new Cell().Add(new Paragraph("TOTAL DOCUMENTOS CRÍTICOS:")).SetFont(PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)));
                    summaryTable.AddCell(new Cell().Add(new Paragraph((licenciasVencidas + licenciasPorVencer + docsVencidos + docsPorVencer).ToString())));

                    document.Add(summaryTable);

                    // Pie de página
                    document.Add(new Paragraph("\n\n"));
                    document.Add(new Paragraph("Este reporte ha sido generado automáticamente por el sistema COMAVI S.A.")
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetFontSize(8)
                        .SetFont(PdfFontFactory.CreateFont(StandardFonts.TIMES_ITALIC)));

                    document.Close();
                    return stream.ToArray();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar reporte de documentos vencidos en PDF");
                throw;
            }
        }
    }
}   