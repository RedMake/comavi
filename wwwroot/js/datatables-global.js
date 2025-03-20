/**
 * datatables-global.js
 * Archivo centralizado para inicialización y configuración de DataTables
 */
document.addEventListener('DOMContentLoaded', function() {
    // Función para inicializar DataTables de manera segura
    function initializeDataTable(selector, options = {}) {
        const table = $(selector);
        
        // Verificar si la tabla existe en el DOM
        if (table.length === 0) return;
        
        // Verificar si ya está inicializada como DataTable
        if ($.fn.dataTable.isDataTable(selector)) {
            // Si ya existe, obtener la instancia y destruirla para reinicializarla con nuevas opciones
            table.DataTable().destroy();
        }
        
        // Configuración por defecto
        const defaultOptions = {
            language: {
                url: "//cdn.datatables.net/plug-ins/1.10.25/i18n/Spanish.json"
            }
        };
        
        // Combinar opciones por defecto con las proporcionadas
        const finalOptions = {...defaultOptions, ...options};
        
        // Inicializar DataTable con las opciones finales
        return table.DataTable(finalOptions);
    }
    
    // Inicializar tablas comunes
    const tables = {
        // Tabla principal de documentos
        dataTable: initializeDataTable('#dataTable', {
            order: [[3, 'asc']] // Ordenar por días restantes (o la columna que corresponda)
        }),
        
        // Tabla de documentos específica
        documentsTable: initializeDataTable('#documentsTable', {
            pageLength: 5
        })
        
        // Agrega aquí otras tablas si es necesario
    };
    
    // Exponer el objeto de tablas globalmente para acceso desde otros scripts si es necesario
    window.appTables = tables;
    
    // Configuración de eventos para modales (del archivo document-validation.js)
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
    
    // Configuración del modal de recordatorio (del archivo datatable-config-documentsPage.js)
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