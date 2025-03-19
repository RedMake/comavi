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
    }
}