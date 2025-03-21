/**
 * Funciones JavaScript para la gestión de choferes
 */
document.addEventListener('DOMContentLoaded', function() {
    // Validaciones para el formulario de registro de chofer
    if (document.getElementById('fecha_venc_licencia')) {
        // Validar que la fecha de vencimiento sea mayor a la fecha actual
        $('#fecha_venc_licencia').change(function() {
            var selectedDate = new Date($(this).val());
            var currentDate = new Date();
            
            if (selectedDate <= currentDate) {
                alert('La fecha de vencimiento debe ser posterior a la fecha actual.');
                $(this).val('');
            }
        });
    }
    
    if (document.getElementById('edad')) {
        // Validar que la edad sea mayor de 18
        $('#edad').change(function() {
            var age = parseInt($(this).val());
            
            if (age < 18) {
                alert('El chofer debe ser mayor de edad (18 años o más).');
                $(this).val('');
            }
        });
    }
    
    // Configuración de dataTables
    if ($.fn.dataTable && document.getElementById('dataTable')) {
        $('#dataTable').DataTable({
            language: {
                url: '//cdn.datatables.net/plug-ins/1.10.25/i18n/Spanish.json'
            },
            responsive: true
        });
    }
});