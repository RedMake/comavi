using ClosedXML.Excel;
using COMAVI_SA.Models;
using System.IO;

namespace COMAVI_SA.Services
{
#nullable disable
#pragma warning disable CS0168
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

    public interface IExcelService
    {
        Task<MemoryStream> GenerarReporteDocumentosExcel(List<COMAVI_SA.Models.Documentos> documentos, string estado, int diasAnticipacion);
    }

    public class ExcelService : IExcelService
    {

        public async Task<MemoryStream> GenerarReporteDocumentosExcel(List<COMAVI_SA.Models.Documentos> documentos, string estado, int diasAnticipacion)
        {
            try
            {
                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("Documentos");

                    // Aplicar encabezados
                    AplicarEncabezados(worksheet);

                    // Llenar datos
                    LlenarDatosDocumentos(worksheet, documentos);

                    // Aplicar formato
                    AplicarFormatoWorksheet(worksheet);

                    // Crear y devolver memoria
                    return GenerarStreamExcel(workbook);
                }
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        #region Métodos Helper

        private void AplicarEncabezados(IXLWorksheet worksheet)
        {
            string[] encabezados = new string[] {
                "ID", "Chofer", "Tipo Documento", "Fecha Emisión",
                "Fecha Vencimiento", "Estado", "Días Restantes"
            };

            for (int i = 0; i < encabezados.Length; i++)
            {
                worksheet.Cell(1, i + 1).Value = encabezados[i];
            }

            // Estilo de encabezados
            var headerRange = worksheet.Range(1, 1, 1, encabezados.Length);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
        }

        private void LlenarDatosDocumentos(IXLWorksheet worksheet, List<COMAVI_SA.Models.Documentos> documentos)
        {
            int row = 2;
            foreach (var documento in documentos)
            {
                var diasRestantes = (documento.fecha_vencimiento - DateTime.Now).Days;

                // Llenar datos básicos
                worksheet.Cell(row, 1).Value = documento.id_documento;
                worksheet.Cell(row, 2).Value = documento.Chofer.nombreCompleto;
                worksheet.Cell(row, 3).Value = documento.tipo_documento;

                // Formatear fechas
                AplicarFormatoFecha(worksheet, row, 4, documento.fecha_emision);
                AplicarFormatoFecha(worksheet, row, 5, documento.fecha_vencimiento);

                // Estado y días restantes
                worksheet.Cell(row, 6).Value = ObtenerTextoEstado(documento.estado_validacion);
                worksheet.Cell(row, 7).Value = diasRestantes;

                // Aplicar formato a días restantes
                AplicarFormatoDiasRestantes(worksheet, row, 7, diasRestantes);

                row++;
            }
        }

        private void AplicarFormatoFecha(IXLWorksheet worksheet, int row, int col, DateTime fecha)
        {
            worksheet.Cell(row, col).Value = fecha;
            worksheet.Cell(row, col).Style.DateFormat.Format = "dd/MM/yyyy";
        }

        private string ObtenerTextoEstado(string estadoValidacion)
        {
            return estadoValidacion switch
            {
                "pendiente" => "Pendiente",
                "verificado" => "Verificado",
                "rechazado" => "Rechazado",
                _ => estadoValidacion
            };
        }

        private void AplicarFormatoDiasRestantes(IXLWorksheet worksheet, int row, int col, int diasRestantes)
        {
            if (diasRestantes < 0)
            {
                worksheet.Cell(row, col).Style.Fill.BackgroundColor = XLColor.Red;
                worksheet.Cell(row, col).Style.Font.FontColor = XLColor.White;
            }
            else if (diasRestantes <= 15)
            {
                worksheet.Cell(row, col).Style.Fill.BackgroundColor = XLColor.Yellow;
            }
            else if (diasRestantes <= 30)
            {
                worksheet.Cell(row, col).Style.Fill.BackgroundColor = XLColor.LightBlue;
            }
        }

        private void AplicarFormatoWorksheet(IXLWorksheet worksheet)
        {
            // Ajustar columnas
            worksheet.Columns().AdjustToContents();

            // Aplicar bordes a todas las celdas con datos
            var rango = worksheet.RangeUsed();
            rango.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            rango.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        }

        private MemoryStream GenerarStreamExcel(XLWorkbook workbook)
        {
            var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;
            return stream;
        }

        #endregion
    }
}