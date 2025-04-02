/**
 * Funciones JavaScript para la gestión de camiones
 */
document.addEventListener('DOMContentLoaded', function() {
    
    
    // Validar el año del camión
    if (document.getElementById('anio')) {
        $('#anio').change(function() {
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