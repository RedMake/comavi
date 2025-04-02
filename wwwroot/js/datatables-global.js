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

        if ($.fn.DataTable.isDataTable(selector)) {
            return $(selector).DataTable().destroy();
        }
        // Configuración por defecto
        const defaultOptions = {
            language: {
                url: "//cdn.datatables.net/plug-ins/1.10.25/i18n/Spanish.json"
            },
            responsive: true,
            pageLength: 10,
            lengthMenu: [[5, 10, 25, 50, -1], [5, 10, 25, 50, "Todos"]],
            dom: '<"row"<"col-sm-6"l><"col-sm-6"f>><"row"<"col-sm-12"tr>><"row"<"col-sm-5"i><"col-sm-7"p>><"row"<"col-sm-12"B>>',
            buttons: [
                {
                    extend: 'copy',
                    text: '<i class="fas fa-copy"></i> Copiar',
                    className: 'btn btn-primary btn-sm'
                },
                {
                    extend: 'excel',
                    text: '<i class="fas fa-file-excel"></i> Excel',
                    className: 'btn btn-success btn-sm',
                    exportOptions: {
                        columns: ':visible'
                    }
                },
                {
                    extend: 'pdf',
                    text: '<i class="fas fa-file-pdf"></i> PDF',
                    className: 'btn btn-danger btn-sm',
                    orientation: 'landscape',
                    pageSize: 'LEGAL',
                    exportOptions: {
                        columns: ':visible'
                    }
                },
                {
                    extend: 'print',
                    text: '<i class="fas fa-print"></i> Imprimir',
                    className: 'btn btn-info btn-sm',
                    exportOptions: {
                        columns: ':visible'
                    }
                },
                {
                    extend: 'colvis',
                    text: '<i class="fas fa-columns"></i> Columnas',
                    className: 'btn btn-secondary btn-sm'
                }
            ],
            // Búsqueda y filtrado mejorados
            search: {
                smart: true    // Búsqueda inteligente
            },
            searchDelay: 500,  // Retraso en milisegundos para búsquedas

            // Rendimiento para tablas grandes
            deferRender: true,
            scroller: true,    // Activar scroll virtual para tablas grandes
            scrollY: '50vh',   // Altura del área de scroll
            scrollCollapse: true,

            // Características visuales adicionales
            stateSave: true,   // Guardar el estado de la tabla (ordenación, filtros, etc.)
            autoWidth: false,  // Mejor rendimiento

            // Integración con Bootstrap
            processing: true,
            rowCallback: function (row, data) {
                // Puedes personalizar las filas aquí
                // Por ejemplo, agregar clases condicionales
                if (data[4] === 'activo') {  // Suponiendo que el estado está en la columna 4
                    $(row).addClass('table-success');
                }
            },

            // Mejoras de accesibilidad
            initComplete: function (settings, json) {
                // Código que se ejecuta cuando la tabla está completamente inicializada
                $('.dataTables_filter input').attr('aria-label', 'Buscar');
                $('.dataTables_length select').attr('aria-label', 'Número de registros por página');
            },

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
            order: [[3, 'asc']], // Ordenar por días restantes
            pageLength: 5
        },
        '#pendientesTable': {
            order: [[4, 'desc']], // Ordenar por fecha de subida
            pageLength: 10
        },
        '#porVencerTable': {
            order: [[5, 'asc']], // Ordenar por días restantes
            columnDefs: [
                { type: 'num', targets: 5 }
            ],
            pageLength: 10
        },
        '#historialTable': { 
            order: [[2, 'desc']], // Ordenar por fecha de emisión
            pageLength: 10
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
        '#solicitudesTable': {
            pageLength: 10
        },
        '#licenciasTable': {
            order: [[4, 'asc']],
            columnDefs: [
                { type: 'num', targets: 4 }
            ],
            pageLength: 10
        },
        '#dataTableDocumentsAdmin': {
            order: [[5, 'asc']],
            pageLength: 10
        },
        '#dataTableLicenciasAdmin': {
            order: [[4, 'asc']],
            pageLength: 10
        },
        '#dataTableRecentAdminMaintenanceReport': {
            order: [[4, 'desc']],
            pageLength: 10
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
    $('#diasAnticipacion').on('change', function (event) {
        $(this).closest('form').trigger('submit');
    });
});