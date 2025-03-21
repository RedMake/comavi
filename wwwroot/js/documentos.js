/**
 * Funciones JavaScript para la gestión de documentos
 */
document.addEventListener('DOMContentLoaded', function() {
    // Configuración del modal de recordatorio en DocumentosPorVencer.cshtml
    $('#recordatorioModal').on('show.bs.modal', function (event) {
        var button = $(event.relatedTarget);
        var id = button.data('id');
        var chofer = button.data('chofer');
        var tipo = button.data('tipo');
        var fecha = button.data('fecha');
        
        $('#documentoIdRecordatorio').val(id);
        $('#choferRecordatorio').text(chofer);
        $('#tipoDocumentoRecordatorio').text(tipo);
        $('#fechaVencimientoRecordatorio').text(fecha);
    });

    // Configuración de los modales en GenerarReporteDocumentos.cshtml
    $('.validarBtn').click(function () {
        var id = $(this).data('id');
        $('#validarDocumentoId').val(id);
    });

    $('.rechazarBtn').click(function () {
        var id = $(this).data('id');
        $('#rechazarDocumentoId').val(id);
    });

    $('.recordatorioBtn').click(function () {
        var id = $(this).data('id');
        $('#recordatorioDocumentoId').val(id);
    });

    // Configuración de los modales en PendientesValidacion.cshtml
    $('#validarModal').on('show.bs.modal', function (event) {
        var button = $(event.relatedTarget);
        var id = button.data('id');
        var chofer = button.data('chofer');
        var tipo = button.data('tipo');
        
        $('#documentoIdValidar').val(id);
        $('#choferValidar').text(chofer);
        $('#tipoDocumentoValidar').text(tipo);
    });

    $('#rechazarModal').on('show.bs.modal', function (event) {
        var button = $(event.relatedTarget);
        var id = button.data('id');
        var chofer = button.data('chofer');
        var tipo = button.data('tipo');
        
        $('#documentoIdRechazar').val(id);
        $('#choferRechazar').text(chofer);
        $('#tipoDocumentoRechazar').text(tipo);
    });
});