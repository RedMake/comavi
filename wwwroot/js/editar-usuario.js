/**
 * editar-usuario.js
 * Script para la vista EditarUsuario
 */
document.addEventListener('DOMContentLoaded', function () {
    // Aplicar validación del lado del cliente
    const form = document.querySelector('form');
    if (form) {
        inicializarValidacion(form);
    }
    
    // Configurar confirmaciones para acciones críticas
    const resetPasswordBtn = document.querySelector('button[type="submit"][form*="resetear"]');
    if (resetPasswordBtn) {
        resetPasswordBtn.addEventListener('click', confirmarReseteoPassword);
    }
});

/**
 * Inicializa la validación del formulario
 * @param {HTMLFormElement} form - El formulario a validar
 */
function inicializarValidacion(form) {
    // Reglas de validación
    const validationRules = {
        'nombre_usuario': {
            required: true,
            minLength: 3,
            maxLength: 50
        },
        'correo_electronico': {
            required: true,
            email: true,
            maxLength: 100
        },
        'rol': {
            required: true
        }
    };
    
    // Mensajes personalizados
    const validationMessages = {
        'nombre_usuario': {
            required: 'El nombre de usuario es obligatorio',
            minLength: 'El nombre debe tener al menos 3 caracteres',
            maxLength: 'El nombre no puede exceder 50 caracteres'
        },
        'correo_electronico': {
            required: 'El correo es obligatorio',
            email: 'Introduce un correo válido',
            maxLength: 'El correo no puede exceder 100 caracteres'
        },
        'rol': {
            required: 'Selecciona un rol'
        }
    };
    
    // Validar en tiempo real
    const inputs = form.querySelectorAll('input, select');
    inputs.forEach(input => {
        input.addEventListener('blur', function() {
            validarCampo(this, validationRules, validationMessages);
        });
        
        input.addEventListener('input', function() {
            // Eliminar mensaje de error si el usuario corrige
            const feedbackElement = this.nextElementSibling;
            if (feedbackElement && feedbackElement.classList.contains('text-danger')) {
                feedbackElement.textContent = '';
            }
        });
    });
    
    // Validar al enviar
    form.addEventListener('submit', function(event) {
        let formValido = true;
        
        inputs.forEach(input => {
            if (!validarCampo(input, validationRules, validationMessages)) {
                formValido = false;
            }
        });
        
        if (!formValido) {
            event.preventDefault();
        }
    });
}

/**
 * Valida un campo individual según las reglas definidas
 * @param {HTMLElement} field - El campo a validar
 * @param {Object} rules - Las reglas de validación
 * @param {Object} messages - Los mensajes de error
 * @returns {Boolean} - Si el campo es válido
 */
function validarCampo(field, rules, messages) {
    const fieldName = field.name;
    const fieldValue = field.value.trim();
    const fieldRules = rules[fieldName];
    
    if (!fieldRules) return true;
    
    let isValid = true;
    let errorMessage = '';
    
    // Verificar si es requerido
    if (fieldRules.required && fieldValue === '') {
        isValid = false;
        errorMessage = messages[fieldName].required;
    }
    // Verificar longitud mínima
    else if (fieldRules.minLength && fieldValue.length < fieldRules.minLength) {
        isValid = false;
        errorMessage = messages[fieldName].minLength;
    }
    // Verificar longitud máxima
    else if (fieldRules.maxLength && fieldValue.length > fieldRules.maxLength) {
        isValid = false;
        errorMessage = messages[fieldName].maxLength;
    }
    // Verificar formato de email
    else if (fieldRules.email && !validarEmail(fieldValue)) {
        isValid = false;
        errorMessage = messages[fieldName].email;
    }
    
    // Mostrar mensaje de error
    let feedbackElement = field.nextElementSibling;
    if (!feedbackElement || !feedbackElement.classList.contains('text-danger')) {
        feedbackElement = document.createElement('div');
        feedbackElement.classList.add('text-danger');
        field.parentNode.insertBefore(feedbackElement, field.nextSibling);
    }
    
    feedbackElement.textContent = errorMessage;
    
    // Aplicar clases visuales
    field.classList.toggle('is-invalid', !isValid);
    field.classList.toggle('is-valid', isValid && fieldValue !== '');
    
    return isValid;
}

/**
 * Valida el formato de un email
 * @param {String} email - El email a validar
 * @returns {Boolean} - Si el email tiene formato válido
 */
function validarEmail(email) {
    const re = /^(([^<>()\[\]\\.,;:\s@"]+(\.[^<>()\[\]\\.,;:\s@"]+)*)|(".+"))@((\[[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}])|(([a-zA-Z\-0-9]+\.)+[a-zA-Z]{2,}))$/;
    return re.test(email.toLowerCase());
}

/**
 * Muestra confirmación antes de resetear password
 * @param {Event} event - El evento de click
 */
function confirmarReseteoPassword(event) {
    if (!confirm('¿Está seguro de resetear la contraseña? Se enviará una nueva contraseña al correo del usuario.')) {
        event.preventDefault();
    }
}