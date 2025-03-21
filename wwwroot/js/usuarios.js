/**
 * Funciones JavaScript para la gestión de usuarios
 */
document.addEventListener('DOMContentLoaded', function() {
    // Inicializar DataTables si existe el elemento
    if ($.fn.dataTable && document.getElementById('dataTable')) {
        $('#dataTable').DataTable({
            language: {
                url: '//cdn.datatables.net/plug-ins/1.10.25/i18n/Spanish.json'
            },
            responsive: true,
            order: [[0, 'asc']]
        });
    }
    
    // Confirmación para restablecer contraseña
    $('form[action*="ResetearContrasena"]').submit(function(e) {
        if (!confirm('¿Está seguro de resetear la contraseña del usuario? Se enviará un correo con una nueva contraseña.')) {
            e.preventDefault();
        }
    });
    
    // Confirmación para cambiar estado de usuario
    $('form[action*="CambiarEstadoUsuario"]').submit(function(e) {
        var estado = $(this).find('input[name="estado"]').val();
        var mensaje = estado === 'activo' ? 
            '¿Está seguro de activar este usuario?' : 
            '¿Está seguro de desactivar este usuario? No podrá acceder al sistema.';
            
        if (!confirm(mensaje)) {
            e.preventDefault();
        }
    });
});