/**
 * reportes-generales.js
 * Script para la vista ReportesGenerales
 */
document.addEventListener('DOMContentLoaded', function () {
    // Configurar fechas por defecto
    configurarFechasPorDefecto();
    
    // Validar formularios antes de enviar
    const forms = document.querySelectorAll('form');
    forms.forEach(form => {
        form.addEventListener('submit', validarFormulario);
    });
});

/**
 * Configura fechas por defecto en los campos de fecha
 */
function configurarFechasPorDefecto() {
    // Formato de fecha YYYY-MM-DD para campos date HTML
    const hoy = new Date();
    const unMesAtras = new Date();
    unMesAtras.setMonth(hoy.getMonth() - 1);
    
    const formatoFecha = fecha => {
        return fecha.toISOString().split('T')[0];
    };
    
    // Establecer fechas por defecto para el formulario de mantenimientos
    const fechaInicio = document.getElementById('fechaInicio');
    const fechaFin = document.getElementById('fechaFin');
    
    if (fechaInicio && !fechaInicio.value) {
        fechaInicio.value = formatoFecha(unMesAtras);
    }
    
    if (fechaFin && !fechaFin.value) {
        fechaFin.value = formatoFecha(hoy);
    }
}

/**
 * Valida el formulario antes del envío
 * @param {Event} event - Evento de envío del formulario
 */
function validarFormulario(event) {
    // Prevenir envío si hay campos fecha y están en el futuro
    const form = event.target;
    const fechaInicio = form.querySelector('[name="fechaInicio"]');
    const fechaFin = form.querySelector('[name="fechaFin"]');
    
    if (fechaInicio && fechaFin) {
        const hoy = new Date();
        const inicio = new Date(fechaInicio.value);
        const fin = new Date(fechaFin.value);
        
        if (inicio > fin) {
            alert('La fecha de inicio no puede ser posterior a la fecha fin');
            event.preventDefault();
            return false;
        }
        
        if (fin > hoy) {
            alert('La fecha fin no puede ser posterior a la fecha actual');
            event.preventDefault();
            return false;
        }
    }
    
    return true;
}