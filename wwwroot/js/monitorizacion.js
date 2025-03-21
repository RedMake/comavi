/**
 * Funciones JavaScript para el monitoreo de vencimientos
 */
document.addEventListener('DOMContentLoaded', function() {
    // Inicializar DataTables para las tablas de monitoreo
    if ($.fn.dataTable) {
        // Tabla de documentos por vencer
        if (document.getElementById('porVencerTable')) {
            $('#porVencerTable').DataTable({
                language: {
                    url: '//cdn.datatables.net/plug-ins/1.10.25/i18n/Spanish.json'
                },
                responsive: true,
                order: [[5, 'asc']], // Ordenar por días restantes
                columnDefs: [
                    { type: 'num', targets: 5 }
                ]
            });
        }
        
        // Tabla de licencias por vencer
        if (document.getElementById('licenciasTable')) {
            $('#licenciasTable').DataTable({
                language: {
                    url: '//cdn.datatables.net/plug-ins/1.10.25/i18n/Spanish.json'
                },
                responsive: true,
                order: [[4, 'asc']], // Ordenar por días restantes
                columnDefs: [
                    { type: 'num', targets: 4 }
                ]
            });
        }
    }
    
    // Aplicar filtro al cambiar los días previos
    $('#diasPrevios').change(function() {
        $(this).closest('form').submit();
    });
});