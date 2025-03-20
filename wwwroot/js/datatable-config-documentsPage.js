$(document).ready(function () {
    
    
    // Modal Enviar Recordatorio
    $('#recordatorioModal').on('show.bs.modal', function (event) {
        var button = $(event.relatedTarget);
        var id = button.data('id');
        var chofer = button.data('chofer');
        var tipo = button.data('tipo');
        var fecha = button.data('fecha');
        
        var modal = $(this);
        modal.find('#documentoIdRecordatorio').val(id);
        modal.find('#choferRecordatorio').text(chofer);
        modal.find('#tipoDocumentoRecordatorio').text(tipo);
        modal.find('#fechaVencimientoRecordatorio').text(fecha);
    });
    
    // Cambiar filtro de días automáticamente
    $('#diasAnticipacion').change(function() {
        $(this).closest('form').submit();
    });
});