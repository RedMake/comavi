/**
 * Validaciones específicas para Costa Rica - Sistema COMAVI
 * Este archivo contiene funciones de validación para los formularios del sistema,
 * considerando particularidades y requisitos de Costa Rica.
 */

document.addEventListener('DOMContentLoaded', function() {
    // Aplicar validaciones según los formularios presentes en la página
    inicializarValidaciones();
});

/**
 * Inicializa todas las validaciones necesarias según los formularios presentes
 */
function inicializarValidaciones() {
    // Validar cédula costarricense
    const cedulaInputs = document.querySelectorAll('input[name="numero_cedula"], input[id="Numero_Cedula"]');
    cedulaInputs.forEach(input => {
        input.addEventListener('blur', validarCedulaCostarricense);
        input.addEventListener('input', function() {
            this.value = this.value.replace(/[^0-9]/g, '').substring(0, 9);
        });
    });

    // Validar placa de vehículo
    const placaInputs = document.querySelectorAll('input[name="numero_placa"]');
    placaInputs.forEach(input => {
        input.addEventListener('blur', validarPlacaCostarricense);
        input.addEventListener('input', function() {
            this.value = this.value.toUpperCase().replace(/[^A-Z0-9]/g, '').substring(0, 8);
        });
    });

    // Validar fechas de vencimiento de licencia
    const licenciaFechaInputs = document.querySelectorAll('input[name="fecha_venc_licencia"], input[id="Fecha_Venc_Licencia"]');
    licenciaFechaInputs.forEach(input => {
        input.addEventListener('change', validarFechaVencimientoLicencia);
    });

    // Validar fechas de emisión/vencimiento de documentos
    const fechaEmisionInputs = document.querySelectorAll('input[name="fecha_emision"], input[id="FechaEmision"]');
    fechaEmisionInputs.forEach(input => {
        input.addEventListener('change', validarFechaEmision);
    });

    const fechaVencimientoInputs = document.querySelectorAll('input[name="fecha_vencimiento"], input[id="FechaVencimiento"]');
    fechaVencimientoInputs.forEach(input => {
        input.addEventListener('change', validarFechaVencimiento);
    });

    // Validar años de vehículos
    const anioInputs = document.querySelectorAll('input[name="anio"]');
    anioInputs.forEach(input => {
        input.addEventListener('blur', validarAnioVehiculo);
        input.min = "1990";
        input.max = new Date().getFullYear().toString();
    });

    // Validar códigos OTP
    const otpInputs = document.querySelectorAll('input[id="OtpCode"]');
    otpInputs.forEach(input => {
        if (input.placeholder.includes('código de 6 dígitos')) {
            input.addEventListener('input', function() {
                this.value = this.value.replace(/[^0-9]/g, '').substring(0, 6);
            });
        }
    });

    // Validar contraseñas
    const passwordInputs = document.querySelectorAll('input[id="Password"], input[id="NuevaPassword"]');
    passwordInputs.forEach(input => {
        input.addEventListener('blur', validarSeguridad);
    });

    // Establecer lista de tipos de documentos
    const tipoDocumentoSelects = document.querySelectorAll('select[id="TipoDocumento"]');
    tipoDocumentoSelects.forEach(select => {
        if (select.options.length === 1) { // Solo si no tiene opciones ya
            const documentosTipos = ["licencia", "cedula", "inscripcion", "marchamo", "riteve"];
            documentosTipos.forEach(tipo => {
                const option = document.createElement('option');
                option.value = tipo;
                option.textContent = tipo.charAt(0).toUpperCase() + tipo.slice(1);
                select.appendChild(option);
            });
        }
    });

    // Validar montos en colones
    const montoInputs = document.querySelectorAll('input[name="costo_base"], input[name="impuesto_iva"], input[name="otros_costos"]');
    montoInputs.forEach(input => {
        input.addEventListener('blur', validarMontoCRC);
    });
}

/**
 * Valida que la cédula tenga el formato correcto de Costa Rica
 */
function validarCedulaCostarricense() {
    const cedula = this.value.trim();
    const regex = /^\d{9}$/;
    
    if (!regex.test(cedula)) {
        this.setCustomValidity('La cédula debe contener exactamente 9 dígitos numéricos');
    } else {
        // Validación adicional: verificar que sea un número de cédula válido
        if (parseInt(cedula.substring(0, 1)) > 9) {
            this.setCustomValidity('El primer dígito de la cédula no es válido');
        } else {
            this.setCustomValidity('');
        }
    }
    
    this.reportValidity();
}

/**
 * Valida que la placa tenga el formato correcto para vehículos de Costa Rica
 */
function validarPlacaCostarricense() {
    const placa = this.value.trim().toUpperCase();
    
    // Formatos válidos :
    // - Placas particulares: XXX000 (SJM123, etc.)
    // - Placas de carga liviana: CL000000
    // - Placas de carga pesada: C000000
    // - Placas de equipo especial: EE000000
    // - Placas de transporte público: AB000000
    
    const regexParticular = /^[A-Z]{3}\d{3}$/;
    const regexCargaLiviana = /^CL\d{6}$/;
    const regexCargaPesada = /^C\d{6}$/;
    const regexEquipoEspecial = /^EE\d{6}$/;
    const regexTransportePublico = /^[A-Z]{2}\d{6}$/;
    
    if (regexParticular.test(placa) || 
        regexCargaLiviana.test(placa) || 
        regexCargaPesada.test(placa) || 
        regexEquipoEspecial.test(placa) || 
        regexTransportePublico.test(placa)) {
        this.setCustomValidity('');
    } else {
        this.setCustomValidity('El formato de placa no es válido para Costa Rica. Ejemplos válidos: SJM123, CL123456, C123456');
    }
    
    this.reportValidity();
}

/**
 * Valida que la fecha de vencimiento de licencia sea futura y razonable
 */
function validarFechaVencimientoLicencia() {
    const fechaVencimiento = new Date(this.value);
    const fechaActual = new Date();
    
    // Fecha máxima de vencimiento (6 años desde hoy, que es lo máximo en CR para algunas licencias)
    const fechaMaxima = new Date();
    fechaMaxima.setFullYear(fechaMaxima.getFullYear() + 6);
    
    if (fechaVencimiento <= fechaActual) {
        this.setCustomValidity('La fecha de vencimiento debe ser posterior a la fecha actual');
    } else if (fechaVencimiento > fechaMaxima) {
        this.setCustomValidity('La fecha de vencimiento no puede ser mayor a 6 años desde hoy (máximo permitido en Costa Rica)');
    } else {
        this.setCustomValidity('');
    }
    
    this.reportValidity();
}

/**
 * Valida que la fecha de emisión sea razonable
 */
function validarFechaEmision() {
    const fechaEmision = new Date(this.value);
    const fechaActual = new Date();
    
    // La fecha de emisión no debe ser futura
    if (fechaEmision > fechaActual) {
        this.setCustomValidity('La fecha de emisión no puede ser futura');
    } 
    // La fecha de emisión no debe ser más antigua que 10 años
    else {
        const diezAnosAtras = new Date();
        diezAnosAtras.setFullYear(diezAnosAtras.getFullYear() - 10);
        
        if (fechaEmision < diezAnosAtras) {
            this.setCustomValidity('La fecha de emisión no puede ser mayor a 10 años atrás');
        } else {
            this.setCustomValidity('');
        }
    }
    
    this.reportValidity();
    
    // Si hay un campo de fecha de vencimiento relacionado, validarlo también
    const formGroup = this.closest('.form-group').parentElement;
    const fechaVencInput = formGroup.querySelector('input[name="fecha_vencimiento"], input[id="FechaVencimiento"]');
    if (fechaVencInput && fechaVencInput.value) {
        // Disparar evento para validar
        const event = new Event('change');
        fechaVencInput.dispatchEvent(event);
    }
}

/**
 * Valida que la fecha de vencimiento sea posterior a la emisión y razonable
 */
function validarFechaVencimiento() {
    const fechaVencimiento = new Date(this.value);
    const fechaActual = new Date();
    
    // Buscar la fecha de emisión relacionada
    const formGroup = this.closest('.form-group').parentElement;
    const fechaEmisionInput = formGroup.querySelector('input[name="fecha_emision"], input[id="FechaEmision"]');
    
    if (fechaEmisionInput && fechaEmisionInput.value) {
        const fechaEmision = new Date(fechaEmisionInput.value);
        
        if (fechaVencimiento <= fechaEmision) {
            this.setCustomValidity('La fecha de vencimiento debe ser posterior a la fecha de emisión');
            this.reportValidity();
            return;
        }
    }
    
    // Validar que la fecha no sea futura más de 6 años (licencias) 
    // o 1 año (marchamos) dependiendo del tipo de documento
    const tipoDocumentoSelect = document.querySelector('select[id="TipoDocumento"]');
    let maxAnios = 6; // Por defecto, 6 años (licencias)
    
    if (tipoDocumentoSelect) {
        const tipoDoc = tipoDocumentoSelect.value.toLowerCase();
        if (tipoDoc === 'marchamo' || tipoDoc === 'riteve') {
            maxAnios = 1; // Marchamo o RTV es anual
        } else if (tipoDoc === 'inscripcion') {
            maxAnios = 99; // La inscripción no vence normalmente
        }
    }
    
    const fechaMaxima = new Date();
    fechaMaxima.setFullYear(fechaMaxima.getFullYear() + maxAnios);
    
    if (fechaVencimiento > fechaMaxima) {
        this.setCustomValidity(`La fecha de vencimiento no puede ser mayor a ${maxAnios} años desde hoy`);
    } else {
        this.setCustomValidity('');
    }
    
    this.reportValidity();
}

/**
 * Valida que el año del vehículo sea razonable
 */
function validarAnioVehiculo() {
    const anio = parseInt(this.value);
    const anioActual = new Date().getFullYear();
    
    if (isNaN(anio)) {
        this.setCustomValidity('Debe ingresar un año válido');
    } else if (anio < 1990) {
        this.setCustomValidity('El año no puede ser anterior a 1990 (limitación de Riteve)');
    } else if (anio > anioActual) {
        this.setCustomValidity('El año no puede ser futuro');
    } else {
        this.setCustomValidity('');
    }
    
    this.reportValidity();
}

/**
 * Valida que el monto en colones sea razonable
 */
function validarMontoCRC() {
    const monto = parseFloat(this.value);
    
    if (isNaN(monto)) {
        this.setCustomValidity('Debe ingresar un monto válido');
    } else if (monto < 0) {
        this.setCustomValidity('El monto no puede ser negativo');
    } else if (monto > 10000000) { // 10 millones de colones como límite razonable para mantenimientos
        this.setCustomValidity('El monto parece excesivamente alto. Verifique la cantidad');
    } else {
        this.setCustomValidity('');
    }
    
    this.reportValidity();
}

/**
 * Valida que la contraseña cumpla con requisitos de seguridad
 */
function validarSeguridad() {
    const password = this.value;
    
    if (password.length < 8) {
        this.setCustomValidity('La contraseña debe tener al menos 8 caracteres');
    } else if (!/[A-Z]/.test(password)) {
        this.setCustomValidity('La contraseña debe incluir al menos una letra mayúscula');
    } else if (!/[a-z]/.test(password)) {
        this.setCustomValidity('La contraseña debe incluir al menos una letra minúscula');
    } else if (!/[0-9]/.test(password)) {
        this.setCustomValidity('La contraseña debe incluir al menos un número');
    } else if (!/[^A-Za-z0-9]/.test(password)) {
        this.setCustomValidity('La contraseña debe incluir al menos un carácter especial');
    } else {
        this.setCustomValidity('');
    }
    
    this.reportValidity();
}

/**
 * Formatea un número como moneda en colones
 * @param {number} amount - Monto a formatear
 * @returns {string} - Monto formateado en colones
 */
function formatColones(amount) {
    return new Intl.NumberFormat('es-CR', {
        style: 'currency',
        currency: 'CRC',
        minimumFractionDigits: 0,
        maximumFractionDigits: 0
    }).format(amount);
}

/**
 * Formatea un número como moneda en dólares
 * @param {number} amount - Monto a formatear
 * @returns {string} - Monto formateado en dólares
 */
function formatDollars(amount) {
    return new Intl.NumberFormat('es-CR', {
        style: 'currency',
        currency: 'USD',
        minimumFractionDigits: 2,
        maximumFractionDigits: 2
    }).format(amount);
}

/**
 * Calcula la edad a partir de una fecha de nacimiento
 * @param {Date} fechaNacimiento - Fecha de nacimiento
 * @returns {number} - Edad en años
 */
function calcularEdad(fechaNacimiento) {
    const hoy = new Date();
    let edad = hoy.getFullYear() - fechaNacimiento.getFullYear();
    const mes = hoy.getMonth() - fechaNacimiento.getMonth();
    
    if (mes < 0 || (mes === 0 && hoy.getDate() < fechaNacimiento.getDate())) {
        edad--;
    }
    
    return edad;
}

/**
 * Calcula el impuesto al valor agregado (IVA) en Costa Rica
 * @param {number} montoBase - Monto base sin impuestos
 * @returns {number} - Monto del IVA
 */
function calcularIVA(montoBase) {
    // IVA en Costa Rica es del 13%
    return montoBase * 0.13;
}

/**
 * Obtiene el tipo de cambio actual del dólar
 * Nota: Esta función simula una consulta al BCCR, en producción
 * debería usar una API real para obtener el tipo de cambio actual
 * @returns {Promise<number>} - Tipo de cambio actual
 */
async function obtenerTipoCambio() {
    // En un entorno real, aquí se haría una llamada a la API del BCCR
    // Para este ejemplo, usamos un valor aproximado
    return new Promise(resolve => {
        setTimeout(() => {
            // Tipo de cambio aproximado abril 2023
            resolve(535.50);
        }, 300);
    });
}