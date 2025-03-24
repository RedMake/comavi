/**
 * documentos-chofer.js
 * Script para la vista ObtenerDocumentosChofer
 */
document.addEventListener('DOMContentLoaded', function () {
    // Inicializar DataTable (se integrará con la configuración global)
    inicializarTablaDocumentos();
    
    // Configurar modal de actualización de documento
    configurarModalActualizacion();
    
    // Validar formulario de actualización
    configurarValidacionFormulario();
    
    // Manejar vista previa de archivos PDF
    configurarVistaPrevia();
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

/**
 * Configura el modal de actualización de documento
 */
function configurarModalActualizacion() {
    $('#actualizarDocumentoModal').on('show.bs.modal', function (event) {
        const button = $(event.relatedTarget);
        const id = button.data('id');
        const tipo = button.data('tipo');
        const emision = button.data('emision');
        const vencimiento = button.data('vencimiento');
        const estado = button.data('estado');

        const modal = $(this);
        modal.find('#id_documento').val(id);
        modal.find('#tipo_documento').val(tipo);
        modal.find('#fecha_emision').val(emision);
        modal.find('#fecha_vencimiento').val(vencimiento);
        modal.find('#estado_validacion').val(estado);
        
        // Limpiar posibles errores anteriores
        modal.find('.is-invalid').removeClass('is-invalid');
        modal.find('.invalid-feedback').remove();
        
        // Limpiar el input de archivo
        modal.find('#archivo').val('');
    });
}

/**
 * Configura la validación del formulario de actualización
 */
function configurarValidacionFormulario() {
    const form = document.querySelector('form[action*="ActualizarDocumentos"]');
    if (!form) return;
    
    form.addEventListener('submit', function(event) {
        // Obtener campos
        const fechaEmision = form.querySelector('#fecha_emision');
        const fechaVencimiento = form.querySelector('#fecha_vencimiento');
        const archivo = form.querySelector('#archivo');
        
        let isValid = true;
        
        // Validar fecha de emisión
        if (!fechaEmision.value) {
            isValid = false;
            mostrarError(fechaEmision, 'La fecha de emisión es obligatoria');
        } else {
            limpiarError(fechaEmision);
        }
        
        // Validar fecha de vencimiento
        if (!fechaVencimiento.value) {
            isValid = false;
            mostrarError(fechaVencimiento, 'La fecha de vencimiento es obligatoria');
        } else if (new Date(fechaVencimiento.value) < new Date(fechaEmision.value)) {
            isValid = false;
            mostrarError(fechaVencimiento, 'La fecha de vencimiento debe ser posterior a la emisión');
        } else {
            limpiarError(fechaVencimiento);
        }
        
        // Validar archivo si se ha seleccionado
        if (archivo.files.length > 0) {
            const file = archivo.files[0];
            const extension = file.name.split('.').pop().toLowerCase();
            
            if (extension !== 'pdf') {
                isValid = false;
                mostrarError(archivo, 'El archivo debe ser PDF');
            } else if (file.size > 5242880) { // 5MB
                isValid = false;
                mostrarError(archivo, 'El archivo no debe superar 5MB');
            } else {
                limpiarError(archivo);
            }
        }
        
        if (!isValid) {
            event.preventDefault();
        }
    });
}

/**
 * Configura la vista previa de archivos PDF
 */
function configurarVistaPrevia() {
    // Implementar si se requiere la vista previa de PDFs
    // Esta función se puede desarrollar según las necesidades específicas
}

/**
 * Muestra un mensaje de error para un campo específico
 * @param {HTMLElement} field - El campo con error
 * @param {String} message - El mensaje de error
 */
function mostrarError(field, message) {
    field.classList.add('is-invalid');
    
    let feedbackDiv = field.nextElementSibling;
    if (!feedbackDiv || !feedbackDiv.classList.contains('invalid-feedback')) {
        feedbackDiv = document.createElement('div');
        feedbackDiv.classList.add('invalid-feedback');
        field.parentNode.insertBefore(feedbackDiv, field.nextSibling);
    }
    
    feedbackDiv.textContent = message;
}

/**
 * Limpia los errores de un campo
 * @param {HTMLElement} field - El campo a limpiar
 */
function limpiarError(field) {
    field.classList.remove('is-invalid');
    field.classList.add('is-valid');
    
    const feedbackDiv = field.nextElementSibling;
    if (feedbackDiv && feedbackDiv.classList.contains('invalid-feedback')) {
        feedbackDiv.textContent = '';
    }
}