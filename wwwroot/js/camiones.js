/**
 * Funciones JavaScript para la gestión de camiones
 */
document.addEventListener('DOMContentLoaded', function() {
    // Inicializar DataTables si existe el elemento
    if ($.fn.dataTable && document.getElementById('dataTable')) {
        $('#dataTable').DataTable({
            language: {
                url: '//cdn.datatables.net/plug-ins/1.10.25/i18n/Spanish.json'
            },
            responsive: true
        });
    }
    
    // Validar el año del camión
    if (document.getElementById('año')) {
        $('#año').change(function() {
            var year = parseInt($(this).val());
            var currentYear = new Date().getFullYear();
            
            if (year < 1990 || year > currentYear) {
                alert('El año debe estar entre 1990 y ' + currentYear);
                $(this).val('');
            }
        });
    }
    
    // Confirmación para eliminar camión
    $('form[action*="EliminarCamion"]').submit(function(e) {
        if (!confirm('¿Está seguro de eliminar este camión? Esta acción no se puede deshacer.')) {
            e.preventDefault();
        }
    });
});