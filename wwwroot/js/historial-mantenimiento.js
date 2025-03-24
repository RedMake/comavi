/**
 * historial-mantenimiento.js
 * Script para la vista HistorialMantenimiento
 */
document.addEventListener('DOMContentLoaded', function () {
    // Inicializar DataTable - Se integrará con la configuración global
    // pero puede tener configuraciones específicas
    inicializarTablaMantenimientos();
    
    // Configurar modal de detalle
    configurarModalDetalle();
    
    // Validar formulario de registro
    configurarValidacionFormulario();
});

/**
 * Inicializa la tabla de mantenimientos con configuraciones específicas
 */
function inicializarTablaMantenimientos() {
    // Verificar si la tabla existe en la página actual
    const tabla = document.getElementById('mantenimientosTable');
    if (!tabla) return;
    
    // Si datatables-global.js ya inicializó la tabla, podemos acceder a ella
    if (window.appTables && window.appTables.mantenimientosTable) {
        return;
    }
    
    const customOptions = {
        order: [[1, 'desc']], // Ordenar por fecha (columna 1) descendente
        columnDefs: [
            { targets: 3, render: $.fn.dataTable.render.number(',', '.', 2, '$') } // Formato de moneda
        ],
        footerCallback: function(row, data, start, end, display) {
            // Calcular y mostrar suma de costos en el footer
            const api = this.api();
            const total = api
                .column(3, { search: 'applied' })
                .data()
                .reduce(function(a, b) {
                    return parseFloat(a) + parseFloat(b);
                }, 0);
                
            $(api.column(3).footer()).html('$' + total.toFixed(2));
        }
    };
    
    // Inicializar a través de la función global si está disponible
    if (typeof initializeDataTable === 'function') {
        initializeDataTable('#mantenimientosTable', customOptions);
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
 * Configura el modal de detalle de mantenimiento
 */
function configurarModalDetalle() {
    $('#detalleMantenimientoModal').on('show.bs.modal', function (event) {
        const button = $(event.relatedTarget);
        const id = button.data('id');
        const descripcion = button.data('descripcion');
        const fecha = button.data('fecha');
        const costo = button.data('costo');

        const modal = $(this);
        modal.find('#detalle-id').text(id);
        modal.find('#detalle-descripcion').text(descripcion);
        modal.find('#detalle-fecha').text(fecha);
        modal.find('#detalle-costo').text('$' + parseFloat(costo).toFixed(2));
    });
}

/**
 * Configura la validación del formulario de registro de mantenimiento
 */
function configurarValidacionFormulario() {
    const form = document.querySelector('form[action*="RegistrarMantenimiento"]');
    if (!form) return;
    
    form.addEventListener('submit', function(event) {
        // Obtener campos
        const fecha = form.querySelector('#fecha_mantenimiento');
        const descripcion = form.querySelector('#descripcion');
        const costo = form.querySelector('#costo');
        
        let isValid = true;
        
        // Validar fecha
        if (!fecha.value) {
            isValid = false;
            mostrarError(fecha, 'La fecha es obligatoria');
        } else if (new Date(fecha.value) > new Date()) {
            isValid = false;
            mostrarError(fecha, 'La fecha no puede ser futura');
        } else {
            limpiarError(fecha);
        }
        
        // Validar descripción
        if (!descripcion.value.trim()) {
            isValid = false;
            mostrarError(descripcion, 'La descripción es obligatoria');
        } else if (descripcion.value.length > 500) {
            isValid = false;
            mostrarError(descripcion, 'La descripción no puede exceder 500 caracteres');
        } else {
            limpiarError(descripcion);
        }
        
        // Validar costo
        if (!costo.value) {
            isValid = false;
            mostrarError(costo, 'El costo es obligatorio');
        } else if (isNaN(costo.value) || parseFloat(costo.value) < 0) {
            isValid = false;
            mostrarError(costo, 'El costo debe ser un número positivo');
        } else {
            limpiarError(costo);
        }
        
        if (!isValid) {
            event.preventDefault();
        }
    });
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