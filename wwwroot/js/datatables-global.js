/**
 * datatables-global.js
 * Configuración centralizada para inicialización de DataTables en todo el sistema
 */
document.addEventListener('DOMContentLoaded', function () {
    // Función para inicializar DataTables de manera segura
    function initializeDataTable(selector, options = {}) {
        const table = $(selector);

        // Verificar si la tabla existe en el DOM
        if (table.length === 0) return null;

        // Configuración por defecto
        const defaultOptions = {
            language: {
                url: "//cdn.datatables.net/plug-ins/1.10.25/i18n/Spanish.json"
            },
            responsive: true,
            pageLength: 10,
            lengthMenu: [[5, 10, 25, 50, -1], [5, 10, 25, 50, "Todos"]],
            dom: 'Bfrtip',
            buttons: [
                'copy', 'excel', 'pdf', 'print'
            ],
            // Evitar problemas de reinicialización
            destroy: true
        };

        // Combinar opciones por defecto con las proporcionadas
        const finalOptions = { ...defaultOptions, ...options };

        // Inicializar DataTable con las opciones finales
        try {
            return table.DataTable(finalOptions);
        } catch (error) {
            console.error(`Error al inicializar DataTable ${selector}:`, error);
            return null;
        }
    }

    // Configuraciones de tablas específicas
    const tableConfigs = {
        '#dataTable': {
            order: [[3, 'asc']] // Ordenar por días restantes
        },
        '#pendientesTable': {
            order: [[4, 'desc']] // Ordenar por fecha de subida
        },
        '#porVencerTable': {
            order: [[5, 'asc']], // Ordenar por días restantes
            columnDefs: [
                { type: 'num', targets: 5 }
            ]
        },
        '#historialTable': {
            order: [[2, 'desc']] // Ordenar por fecha de emisión
        },
        '#choferesTable': {
            pageLength: 25
        },
        '#camionesTable': {
            pageLength: 25
        },
        '#usuariosTable': {
            pageLength: 25
        },
        '#documentsTable': {
            pageLength: 5
        },
        '#licenciasTable': {
            order: [[4, 'asc']],
            columnDefs: [
                { type: 'num', targets: 4 }
            ]
        },
        '#dataTableDocumentsAdmin': {
            order: [[5, 'asc']]
        },
        '#dataTableLicenciasAdmin': {
            order: [[4, 'asc']]
        },
        '#dataTableRecentAdminMaintenanceReport': {
            order: [[4, 'desc']]
        }
    };

    // Objeto para almacenar las instancias de tablas inicializadas
    const tables = {};

    // Inicializar solo las tablas que existen en la página actual
    Object.entries(tableConfigs).forEach(([selector, options]) => {
        if ($(selector).length > 0) {
            tables[selector.replace('#', '')] = initializeDataTable(selector, options);
        }
    });

    // Exponer el objeto de tablas globalmente para acceso desde otros scripts
    window.appTables = tables;

    // Configuración de eventos para modales (unificados desde diversos archivos)

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
    $('#diasAnticipacion').change(function () {
        $(this).closest('form').submit();
    });
});