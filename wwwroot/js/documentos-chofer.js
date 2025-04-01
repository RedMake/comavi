/**
 * documentos-chofer.js
 * Script para la vista ObtenerDocumentosChofer
 */
document.addEventListener('DOMContentLoaded', function () {
    // Inicializar DataTable (se integrará con la configuración global)
    inicializarTablaDocumentos();
    
});

/**
 * Inicializa la tabla de documentos con configuraciones específicas
 */
function inicializarTablaDocumentos() {
    // Verificar si la tabla existe en la página actual
    const tabla = document.getElementById('documentosTable');
    if (!tabla) return;
    
    // Si datatables-global.js ya inicializó la tabla, podemos acceder a ella
    if (window.appTables && window.appTables.documentosTable) {
        return;
    }
    
    // Si no fue inicializada por datatables-global.js, inicializarla aquí
    const customOptions = {
        order: [[5, 'asc']], // Ordenar por días restantes ascendente
        columnDefs: [
            { targets: 5, type: 'num' }, // Columna de días restantes es numérica
            { 
                targets: 6, 
                orderable: false, 
                searchable: false 
            }, // Columna de archivo no ordenable
            { 
                targets: 7, 
                orderable: false, 
                searchable: false 
            }  // Columna de acciones no ordenable
        ],
        createdRow: function(row, data, dataIndex) {
            // Aplicar clases según estado del documento
            const diasCol = $(row).find('td:eq(5)');
            const texto = diasCol.text().trim();
            
            if (texto.includes('Vencido')) {
                $(row).addClass('table-danger');
            } else if (texto.match(/\d+/) && parseInt(texto.match(/\d+/)[0]) <= 15) {
                $(row).addClass('table-warning');
            }
        }
    };
    
    // Inicializar a través de la función global si está disponible
    if (typeof initializeDataTable === 'function') {
        initializeDataTable('#documentosTable', customOptions);
    } else {
        // Inicialización alternativa directa
        $(tabla).DataTable({
            language: {
                url: "//cdn.datatables.net/plug-ins/1.10.25/i18n/Spanish.json"
            },
            responsive: true,
            ...customOptions
        });
    }
}
