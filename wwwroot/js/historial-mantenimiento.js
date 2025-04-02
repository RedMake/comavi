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

    // Configurar formulario de mantenimiento con cálculos
    configurarFormularioMantenimiento();

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
            {
                targets: [3, 4, 5, 6], render: function (data, type, row) {
                    // Detectar si el valor ya tiene simbolo de moneda
                    if (type === 'display' && data) {
                        if (!data.startsWith('$') && !data.startsWith('₡')) {
                            // Agregar símbolo según la moneda en la columna 7
                            const moneda = row[7] || 'CRC';
                            const simbolo = moneda === 'USD' ? '$' : '₡';
                            return simbolo + parseFloat(data).toLocaleString('es-CR', {
                                minimumFractionDigits: 2,
                                maximumFractionDigits: 2
                            });
                        }
                    }
                    return data;
                }
            }
        ],
        footerCallback: function (row, data, start, end, display) {
            // Calcular y mostrar suma de costos en el footer por moneda
            const api = this.api();

            // Filtrar y sumar por moneda
            const totalCRC = api.column(6, { search: 'applied' })
                .data()
                .filter(function (value, idx) {
                    return api.column(7).data()[idx] === 'CRC' || !api.column(7).data()[idx];
                })
                .reduce(function (a, b) {
                    return parseFloat(a) + parseFloat(b.replace(/[^\d.-]/g, ''));
                }, 0);

            const totalUSD = api.column(6, { search: 'applied' })
                .data()
                .filter(function (value, idx) {
                    return api.column(7).data()[idx] === 'USD';
                })
                .reduce(function (a, b) {
                    return parseFloat(a) + parseFloat(b.replace(/[^\d.-]/g, ''));
                }, 0);

            // Formatear con 2 decimales fijos
            const formatearTotal = (valor) => {
                return valor.toFixed(2);
            };

            // Mostrar totales
            $('#totalCRC').html('₡' + formatearTotal(totalCRC));
            $('#totalUSD').html('$' + formatearTotal(totalUSD));
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
    const modal = $('#detalleMantenimientoModal');

    // Almacenar el elemento que abrió el modal
    let lastFocusedElement;

    modal.on('show.bs.modal', function (event) {
        // Almacenar el elemento que activó el modal
        lastFocusedElement = $(event.relatedTarget);

        const button = $(event.relatedTarget);
        const id = button.data('id');
        const descripcion = button.data('descripcion');
        const fecha = button.data('fecha');
        const costo = button.data('costo');
        const moneda = button.data('moneda') || 'CRC';
        // Obtener el valor directamente del atributo para evitar parsing automático
        const detallesAttr = button.attr('data-detalles');

        const simbolo = moneda === 'USD' ? '$' : '₡';

        const modalEl = $(this);
        modalEl.find('#detalle-id').text(id);
        modalEl.find('#detalle-descripcion').text(descripcion);
        modalEl.find('#detalle-fecha').text(fecha);
        modalEl.find('#detalle-costo').text(`${simbolo}${parseFloat(costo).toFixed(2)}`);

        // Manejar detalles adicionales si existen
        if (detallesAttr && detallesAttr !== 'null' && detallesAttr !== '') {
            try {
                // Intentar parsear el JSON - importante usar el atributo raw
                let detallesObj = JSON.parse(detallesAttr);

                // Verificar si detallesObj tiene las propiedades esperadas
                const tieneProps = detallesObj &&
                    (detallesObj.costo_base !== undefined ||
                        detallesObj.impuesto_iva !== undefined ||
                        detallesObj.otros_costos !== undefined);

                if (tieneProps) {
                    // Mostrar el desglose
                    $('#detalleDesgloseContainer').show();
                    $('#detalleNoDesgloseContainer').hide();

                    // Función segura para formatear valores
                    const formatearDetalle = (valor) => {
                        if (valor === null || valor === undefined || isNaN(parseFloat(valor))) {
                            return '0.00';
                        }
                        return parseFloat(valor).toFixed(2);
                    };

                    // Usar operador de coalescencia nula para manejar valores nulos
                    $('#detalle-costo-base').text(`${simbolo}${formatearDetalle(detallesObj.costo_base ?? 0)}`);
                    $('#detalle-impuesto').text(`${simbolo}${formatearDetalle(detallesObj.impuesto_iva ?? 0)}`);
                    $('#detalle-otros-costos').text(`${simbolo}${formatearDetalle(detallesObj.otros_costos ?? 0)}`);

                    // Tipo de cambio (con valor predeterminado)
                    const tipoCambio = detallesObj.tipo_cambio ?? 625;
                    $('#detalle-tipo-cambio').text(`₡${formatearDetalle(tipoCambio)} por USD`);
                } else {
                    throw new Error('Objeto de detalles no tiene las propiedades esperadas');
                }
            } catch (e) {
                console.error('Error al procesar detalles JSON:', e.message);

                // Mostrar mensaje de error
                $('#detalleDesgloseContainer').hide();
                $('#detalleNoDesgloseContainer').show();
            }
        } else {
            // No hay desglose o es nulo
            $('#detalleDesgloseContainer').hide();
            $('#detalleNoDesgloseContainer').show();
        }
    });

    // Cuando el modal se muestra completamente, establecer el foco en un elemento apropiado
    modal.on('shown.bs.modal', function (event) {
        // Establecer el foco en el botón de cerrar
        $(this).find('.btn-secondary').trigger('focus');
    });

    // Cuando se oculta el modal, devolver el foco al elemento que lo abrió
    modal.on('hidden.bs.modal', function () {
        if (lastFocusedElement && lastFocusedElement.length) {
            lastFocusedElement.focus();
        }
    });
}
/**
 * Configura el formulario de registro de mantenimiento con cálculo de costos
 */
function configurarFormularioMantenimiento() {
    const form = document.getElementById('formRegistrarMantenimiento');
    if (!form) return;

    // Elementos del formulario
    const monedaSelect = document.getElementById('moneda');
    const costoBaseInput = document.getElementById('costo_base');
    const impuestoIvaInput = document.getElementById('impuesto_iva');
    const otrosCostosInput = document.getElementById('otros_costos');
    const tipoCambioInput = document.getElementById('tipo_cambio');
    const totalCRCSpan = document.getElementById('formularioTotalCRC');
    const totalUSDSpan = document.getElementById('formularioTotalUSD');
    const costoInput = document.getElementById('costo');
    const monedaFinalInput = document.getElementById('moneda_final');
    const detallesCostoInput = document.getElementById('detalles_costo');
    const calcularDesgloseCheck = document.getElementById('calcularDesglose');
    const desgloseContainer = document.getElementById('desgloseContainer');
    const fechaInput = document.getElementById('fecha_mantenimiento');

    // Establecer fecha y hora actual
    if (fechaInput) {
        const now = new Date();
        // Formato YYYY-MM-DDThh:mm requerido para datetime-local
        const formattedDate = now.getFullYear() + '-' +
            String(now.getMonth() + 1).padStart(2, '0') + '-' +
            String(now.getDate()).padStart(2, '0') + 'T' +
            String(now.getHours()).padStart(2, '0') + ':' +
            String(now.getMinutes()).padStart(2, '0');
        fechaInput.value = formattedDate;
    }

    // Botones
    const btnEditarImpuesto = document.getElementById('btnEditarImpuesto');
    const btnEditarTC = document.getElementById('btnEditarTC');
    const btnActualizarTC = document.getElementById('btnActualizarTC');

    // Constantes
    const IVA_RATE = 0.13; // 13% IVA en Costa Rica

    // Actualizar símbolos de moneda cuando cambia la selección
    monedaSelect.addEventListener('change', function () {
        const simbolos = document.querySelectorAll('.moneda-simbolo');
        const nuevoSimbolo = this.value === 'USD' ? '$' : '₡';

        simbolos.forEach(span => {
            span.textContent = nuevoSimbolo;
        });

        calcularImpuesto();
        calcularTotales();
    });

    // Toggle desglose de costos
    calcularDesgloseCheck.addEventListener('change', function () {
        if (this.checked) {
            desgloseContainer.style.display = 'block';
            // Distribuir el costo actual en los campos de desglose
            if (costoInput.value > 0) {
                const costoTotal = parseFloat(costoInput.value);
                costoBaseInput.value = (costoTotal / 1.13).toFixed(2);
                calcularImpuesto();
                otrosCostosInput.value = "0.00";
            } else {
                // Inicializar con valores por defecto si no hay costo
                costoBaseInput.value = "0.00";
                impuestoIvaInput.value = "0.00";
                otrosCostosInput.value = "0.00";
            }
        } else {
            desgloseContainer.style.display = 'none';
            // Usar el costo total como costo base
            costoBaseInput.value = costoInput.value || "0.00";
            impuestoIvaInput.value = "0.00";
            otrosCostosInput.value = "0.00";
        }

        calcularTotales();
    });

    // Formatear y desformatear números con separadores de miles
    function formatearNumero(valor) {
        return parseFloat(valor).toLocaleString('es-CR', {
            minimumFractionDigits: 2,
            maximumFractionDigits: 2
        });
    }

    function desformatearNumero(valorFormateado) {
        if (!valorFormateado) return 0;
        return parseFloat(valorFormateado.toString().replace(/[^\d.-]/g, ''));
    }

    // Esta función ha sido simplificada para eliminar el formateo con separadores de miles
    function aplicarFormatoNumerico(input) {
        // No hacemos nada, se utilizan los valores numéricos directos
        return;
    }

    // Event listeners simplificados sin formateo
    [costoBaseInput, impuestoIvaInput, otrosCostosInput, tipoCambioInput].forEach(input => {
        // No aplicamos formateo al inicio

        // Evento input para calcular en tiempo real
        input.addEventListener('input', function () {
            if (calcularDesgloseCheck.checked && this.id === 'costo_base') {
                calcularImpuesto();
            }
            calcularTotales();
        });

        // Evento blur para asegurar cálculos al salir del campo
        input.addEventListener('blur', function () {
            if (calcularDesgloseCheck.checked && this.id === 'costo_base') {
                calcularImpuesto();
            }
            calcularTotales();
        });

        // Evento change para otros cambios
        input.addEventListener('change', function () {
            if (calcularDesgloseCheck.checked && this.id === 'costo_base') {
                calcularImpuesto();
            }
            calcularTotales();
        });
    });

    // Calcular impuesto IVA
    function calcularImpuesto() {
        if (!costoBaseInput.value) return;

        const costoBase = parseFloat(costoBaseInput.value);
        const impuestoCalculado = costoBase * IVA_RATE;

        // Solo actualizar el valor si el campo es de solo lectura
        if (impuestoIvaInput.readOnly) {
            // Asignamos directamente el valor calculado con 2 decimales
            impuestoIvaInput.value = impuestoCalculado.toFixed(2);

            // Verificar si el campo existe y tiene buen formato para evitar NaN
            if (isNaN(parseFloat(impuestoIvaInput.value))) {
                impuestoIvaInput.value = "0.00";
            }
        }
    }

    // Calcular totales
    function calcularTotales() {
        // Obtener valores directamente sin formateo
        const costoBase = parseFloat(costoBaseInput.value) || 0;
        // Verificar si el checkbox está marcado y asegurarse de que el valor sea numérico
        const impuestoIva = calcularDesgloseCheck.checked ? (parseFloat(impuestoIvaInput.value) || 0) : 0;
        const otrosCostos = parseFloat(otrosCostosInput.value) || 0;
        const tipoCambio = parseFloat(tipoCambioInput.value) || 625;
        const monedaActual = monedaSelect.value;


        // Calcular total en la moneda seleccionada
        const totalEnMonedaActual = costoBase + impuestoIva + otrosCostos;

        // Conversión según moneda
        let totalCRC, totalUSD;

        if (monedaActual === 'CRC') {
            totalCRC = totalEnMonedaActual;
            totalUSD = totalEnMonedaActual / tipoCambio;
        } else {
            totalUSD = totalEnMonedaActual;
            totalCRC = totalEnMonedaActual * tipoCambio;
        }

        // Mostrar totales - Asegurándonos de que los elementos existen
        if (totalCRCSpan) {
            totalCRCSpan.textContent = `₡${totalCRC.toFixed(2)}`;
        }

        if (totalUSDSpan) {
            totalUSDSpan.textContent = `$${totalUSD.toFixed(2)}`;
        }

        // Asignar al campo oculto de costo
        costoInput.value = totalEnMonedaActual.toFixed(2);
        monedaFinalInput.value = monedaActual;

        // Guardar detalles como JSON para la BD - asegurar que todos los valores son numéricos válidos
        const detalles = {
            costo_base: Number(costoBase.toFixed(2)),
            impuesto_iva: Number(impuestoIva.toFixed(2)),
            otros_costos: Number(otrosCostos.toFixed(2)),
            tipo_cambio: Number(tipoCambio.toFixed(2))
        };

        // Asegurarse de que no hay NaN
        for (const key in detalles) {
            if (isNaN(detalles[key])) {
                detalles[key] = 0;
            }
        }

        // Convertir a JSON y guardar
        detallesCostoInput.value = JSON.stringify(detalles);
    }

    // Permitir editar impuesto manualmente
    btnEditarImpuesto.addEventListener('click', function () {
        if (impuestoIvaInput.readOnly) {
            impuestoIvaInput.readOnly = false;
            this.classList.add('btn-warning');
            this.title = 'Volver a cálculo automático';

            // Mostrar advertencia
            if (confirm('Al editar manualmente el impuesto, se desactivará el cálculo automático. ¿Desea continuar?')) {
                impuestoIvaInput.focus();
            } else {
                impuestoIvaInput.readOnly = true;
                this.classList.remove('btn-warning');
                this.title = 'Editar impuesto';
            }
        } else {
            impuestoIvaInput.readOnly = true;
            this.classList.remove('btn-warning');
            this.title = 'Editar impuesto';
            calcularImpuesto();
            calcularTotales();
        }
    });

    // Impuesto manual
    impuestoIvaInput.addEventListener('input', calcularTotales);

    // Permitir editar tipo de cambio
    btnEditarTC.addEventListener('click', function () {
        if (tipoCambioInput.readOnly) {
            tipoCambioInput.readOnly = false;
            this.classList.add('btn-warning');
            this.title = 'Volver a tipo de cambio predeterminado';

            // Mostrar advertencia
            if (confirm('Editar el tipo de cambio manualmente puede afectar los cálculos. Esta acción es reversible pero no recomendable. ¿Desea continuar?')) {
                tipoCambioInput.focus();
            } else {
                tipoCambioInput.readOnly = true;
                this.classList.remove('btn-warning');
                this.title = 'Editar tipo de cambio';
            }
        } else {
            tipoCambioInput.readOnly = true;
            this.classList.remove('btn-warning');
            this.title = 'Editar tipo de cambio';
            calcularTotales();
        }
    });

    // Tipo de cambio manual
    tipoCambioInput.addEventListener('input', calcularTotales);

    // Actualizar tipo de cambio con API real
    btnActualizarTC.addEventListener('click', function () {
        this.disabled = true;
        this.innerHTML = '<i class="fas fa-spinner fa-spin"></i>';

        // Usar API pública para obtener tipo de cambio
        fetch('https://open.er-api.com/v6/latest/USD')
            .then(response => response.json())
            .then(data => {
                if (data && data.rates && data.rates.CRC) {
                    const nuevoTC = data.rates.CRC.toFixed(2);
                    tipoCambioInput.value = nuevoTC;
                    document.getElementById('tipoCambioActual').textContent = nuevoTC;
                    calcularTotales();
                    mostrarNotificacion('Tipo de cambio actualizado correctamente: ₡' + nuevoTC + ' por USD', 'success');
                } else {
                    throw new Error('No se pudo obtener el tipo de cambio');
                }
            })
            .catch(error => {
                console.error('Error al obtener tipo de cambio:', error);
                // API alternativa de respaldo
                fetch('https://api.exchangerate-api.com/v4/latest/USD')
                    .then(response => response.json())
                    .then(data => {
                        if (data && data.rates && data.rates.CRC) {
                            const nuevoTC = data.rates.CRC.toFixed(2);
                            tipoCambioInput.value = nuevoTC;
                            document.getElementById('tipoCambioActual').textContent = nuevoTC;
                            calcularTotales();
                            mostrarNotificacion('Tipo de cambio actualizado correctamente: ₡' + nuevoTC + ' por USD', 'success');
                        } else {
                            throw new Error('No se pudo obtener el tipo de cambio');
                        }
                    })
                    .catch(err => {
                        mostrarNotificacion('No se pudo actualizar el tipo de cambio. Intente más tarde.', 'danger');
                    });
            })
            .finally(() => {
                this.disabled = false;
                this.innerHTML = '<i class="fas fa-sync-alt"></i>';
            });
    });

    // Asegurarnos que todos los campos tienen event listeners para actualización inmediata
    [monedaSelect, costoBaseInput, impuestoIvaInput, otrosCostosInput, tipoCambioInput].forEach(elem => {
        // Disparar un evento para asegurar que se inicialicen con valores correctos
        if (elem) {
            const event = new Event('change');
            elem.dispatchEvent(event);
        }
    });

    // Inicializar cálculos
    calcularImpuesto();
    calcularTotales();

    // Comprobar después de un pequeño retraso que los totales se muestran correctamente
    setTimeout(() => {
        calcularTotales();
    }, 500);
}

/**
 * Configura la validación del formulario de registro de mantenimiento
 */
function configurarValidacionFormulario() {
    const form = document.querySelector('form[action*="RegistrarMantenimiento"]');
    if (!form) return;

    form.addEventListener('submit', function (event) {
        // Verificar si estamos usando el formulario de desglose
        const usingNewForm = !!document.getElementById('costo_base');

        // Obtener campos comunes
        const fecha = form.querySelector('#fecha_mantenimiento');
        const descripcion = form.querySelector('#descripcion');

        // El costo puede venir del formulario nuevo o del anterior
        const costo = usingNewForm ? form.querySelector('#costo') : form.querySelector('#costo');

        let isValid = true;

        // Validar fecha
        if (!fecha.value) {
            isValid = false;
            mostrarError(fecha, 'La fecha es obligatoria');
        } else {
            const fechaSeleccionada = new Date(fecha.value);
            const ahora = new Date();
            if (fechaSeleccionada < ahora) {
                isValid = false;
                mostrarError(fecha, 'La fecha no puede ser anterior a hoy');
            } else {
                limpiarError(fecha);
            }
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
        if (usingNewForm) {
            const costoBase = form.querySelector('#costo_base');
            if (!costoBase.value) {
                isValid = false;
                mostrarError(costoBase, 'El costo base es obligatorio');
            } else {
                // Comprobar valor numérico directamente
                const valorReal = parseFloat(costoBase.value);
                if (isNaN(valorReal) || valorReal < 0) {
                    isValid = false;
                    mostrarError(costoBase, 'El costo base debe ser un número positivo');
                } else {
                    limpiarError(costoBase);
                }
            }

            // Asegurarnos de que el envío de datos a la BD esté sin formato
            if (isValid) {
                // Ya no es necesario desformatear, los valores se envían tal cual
            }
        } else {
            if (!costo.value) {
                isValid = false;
                mostrarError(costo, 'El costo es obligatorio');
            } else if (isNaN(costo.value) || parseFloat(costo.value) < 0) {
                isValid = false;
                mostrarError(costo, 'El costo debe ser un número positivo');
            } else {
                limpiarError(costo);
            }
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

/**
 * Muestra una notificación tipo toast
 */
function mostrarNotificacion(mensaje, tipo = 'info') {
    // Verificar si existe el contenedor de notificaciones
    let notifContainer = document.getElementById('notificacionesContainer');

    if (!notifContainer) {
        notifContainer = document.createElement('div');
        notifContainer.id = 'notificacionesContainer';
        notifContainer.style.position = 'fixed';
        notifContainer.style.bottom = '20px';
        notifContainer.style.right = '20px';
        notifContainer.style.zIndex = '9999';
        document.body.appendChild(notifContainer);
    }

    // Crear la notificación
    const toast = document.createElement('div');
    toast.className = `toast bg-${tipo}`;
    toast.innerHTML = `
        <div class="toast-header">
            <strong class="mr-auto">Notificación</strong>
            <button type="button" class="ml-2 mb-1 close" data-dismiss="toast" aria-label="Close">
                <span aria-hidden="true">&times;</span>
            </button>
        </div>
        <div class="toast-body text-white">
            ${mensaje}
        </div>
    `;

    notifContainer.appendChild(toast);

    // Inicializar el toast con Bootstrap
    $(toast).toast({
        delay: 3000
    }).toast('show');

    // Remover después de cerrar
    $(toast).on('hidden.bs.toast', function () {
        this.remove();
    });
}

/**
 * Función para formatear números con 2 decimales
 * @param {Number} valor - El valor a formatear
 * @returns {String} Valor formateado con 2 decimales
 */
function formatearNumero(valor) {
    try {
        const numValue = parseFloat(valor);
        return isNaN(numValue) ? '0.00' : numValue.toFixed(2);
    } catch (e) {
        console.warn('Error al convertir a número:', valor);
        return '0.00';
    }
}

/**
 * Función para convertir un valor a número
 * @param {String|Number} valor - El valor a convertir
 * @returns {Number} Valor numérico
 */
function desformatearNumero(valor) {
    if (!valor) return 0;

    try {
        const resultado = parseFloat(valor);
        return isNaN(resultado) ? 0 : resultado;
    } catch (e) {
        console.warn('Error al convertir a número:', valor);
        return 0;
    }
}