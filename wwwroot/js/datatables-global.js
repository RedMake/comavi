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

        // Verificar si ya está inicializada como DataTable
        if ($.fn.dataTable.isDataTable(selector)) {
            // Si ya existe, obtener la instancia y destruirla para reinicializarla
            table.DataTable().destroy();
        }

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
            ]
        };

        // Combinar opciones por defecto con las proporcionadas
        const finalOptions = { ...defaultOptions, ...options };

        // Inicializar DataTable con las opciones finales
        return table.DataTable(finalOptions);
    }

    // Inicializar tablas comunes
    const tables = {
        // Tabla principal de documentos
        dataTable: initializeDataTable('#dataTable', {
            order: [[3, 'asc']] // Ordenar por días restantes (o la columna que corresponda)
        }),

        // Tabla de documentos pendientes de validación
        pendientesTable: initializeDataTable('#pendientesTable', {
            order: [[4, 'desc']] // Ordenar por fecha de subida
        }),

        // Tabla de documentos por vencer
        porVencerTable: initializeDataTable('#porVencerTable', {
            order: [[3, 'asc']] // Ordenar por días restantes
        }),

        // Tabla de historial de documentos
        historialTable: initializeDataTable('#historialTable', {
            order: [[2, 'desc']] // Ordenar por fecha de emisión
        }),

        // Tabla de choferes
        choferesTable: initializeDataTable('#choferesTable', {
            pageLength: 25
        }),

        // Tabla de camiones
        camionesTable: initializeDataTable('#camionesTable', {
            pageLength: 25
        }),

        // Tabla de usuarios
        usuariosTable: initializeDataTable('#usuariosTable', {
            pageLength: 25
        }),

        // Tabla de documentos específica
        documentsTable: initializeDataTable('#documentsTable', {
            pageLength: 5
        }),

        // Tabla de licencias por vencer
        licenciasTable: initializeDataTable('#licenciasTable', {
            order: [[4, 'asc']] // Ordenar por días restantes
        })
    };

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