$(document).ready(function () {

    // Modal Validar Documento
    $('#validarModal').on('show.bs.modal', function (event) {
        var button = $(event.relatedTarget);
        var id = button.data('id');
        var chofer = button.data('chofer');
        var tipo = button.data('tipo');
        
        var modal = $(this);
        modal.find('#documentoIdValidar').val(id);
        modal.find('#choferValidar').text(chofer);
        modal.find('#tipoDocumentoValidar').text(tipo);
    });
    
    // Modal Rechazar Documento
    $('#rechazarModal').on('show.bs.modal', function (event) {
        var button = $(event.relatedTarget);
        var id = button.data('id');
        var chofer = button.data('chofer');
        var tipo = button.data('tipo');
        
        var modal = $(this);
        modal.find('#documentoIdRechazar').val(id);
        modal.find('#choferRechazar').text(chofer);
        modal.find('#tipoDocumentoRechazar').text(tipo);
        modal.find('#motivoRechazo').val('');
    });
});