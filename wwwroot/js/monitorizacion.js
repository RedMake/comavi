/**
 * Funciones JavaScript para el monitoreo de vencimientos
 */
document.addEventListener('DOMContentLoaded', function() {
    
    // Aplicar filtro al cambiar los días previos
    $('#diasPrevios').change(function() {
        $(this).closest('form').submit();
    });
});