/**
 * solicitudes-mantenimiento.js
 * Maneja la funcionalidad para el formulario de procesamiento de solicitudes de mantenimiento
 */
document.addEventListener('DOMContentLoaded', function () {

    // Configurar modal de observaciones
    $('.ver-observaciones').click(function () {
        var observaciones = $(this).data('observaciones');
        $('#observacionesTexto').text(observaciones);
    });

    // Configurar modal procesar
    $('.procesar-solicitud').click(function () {
        var id = $(this).data('id');
        var info = $(this).data('info');
        $('#idSolicitud').val(id);
        $('#infoSolicitud').text(info);

        // Reiniciar formulario
        $('#descripcion').val('');
        $('#costo').val('');
        $('#moneda').val('CRC');
        $('#detallesCosto').val('');

        // Mostrar campos de mantenimiento por defecto
        $('#seccionMantenimiento').show();
        $('#estadoAprobado').prop('checked', true);

        // Inicializar campos de costo
        actualizarTipoCambio();
    });

    // Mostrar/ocultar sección de mantenimiento según decisión
    $('input[name="estado"]').change(function () {
        if ($(this).val() === 'aprobado') {
            $('#seccionMantenimiento').show();
            $('#descripcion, #costo').prop('required', true);
        } else {
            $('#seccionMantenimiento').hide();
            $('#descripcion, #costo').prop('required', false);
        }
    });

    // Agregar el botón de tipo de cambio y el contenedor
    if (!$('#tipoCambioContainer').length) {
        // Crear contenedor para mostrar el tipo de cambio
        const cambioContainer = document.createElement('div');
        cambioContainer.id = 'tipoCambioContainer';
        cambioContainer.className = 'mb-3';
        cambioContainer.innerHTML = `
            <div class="d-flex align-items-center">
                <span>Tipo de cambio actual: <strong id="tipoCambioActual">625.00</strong> ₡/USD</span>
                <button type="button" id="btnActualizarTC" class="btn btn-sm btn-outline-info ml-2" title="Actualizar tipo de cambio">
                    <i class="fas fa-sync-alt"></i>
                </button>
            </div>
        `;

        // Insertar después de la selección de moneda
        $('#moneda').closest('.form-group').after(cambioContainer);

        // Asignar evento al botón
        document.getElementById('btnActualizarTC').addEventListener('click', actualizarTipoCambio);
    }

    // Preparar formulario antes de enviar
    $('form[action*="ProcesarSolicitudMantenimiento"]').on('submit', function (e) {
        if ($('#estadoAprobado').is(':checked')) {
            // Solo cuando se aprueba, preparar detalles de costo
            prepararDetallesCosto();
        }
    });

    // Funciones para manejar tipo de cambio y detalles

    function actualizarTipoCambio() {
        const btnTipoCambio = document.getElementById('btnActualizarTC');
        if (!btnTipoCambio) return;

        btnTipoCambio.disabled = true;
        btnTipoCambio.innerHTML = '<i class="fas fa-spinner fa-spin"></i>';

        // Usar API pública para obtener tipo de cambio
        fetch('https://open.er-api.com/v6/latest/USD')
            .then(response => response.json())
            .then(data => {
                if (data && data.rates && data.rates.CRC) {
                    const nuevoTC = data.rates.CRC.toFixed(2);
                    document.getElementById('tipoCambioActual').textContent = nuevoTC;
                    mostrarNotificacion('Tipo de cambio actualizado: ₡' + nuevoTC + ' por USD', 'success');
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
                            document.getElementById('tipoCambioActual').textContent = nuevoTC;
                            mostrarNotificacion('Tipo de cambio actualizado: ₡' + nuevoTC + ' por USD', 'success');
                        } else {
                            throw new Error('No se pudo obtener el tipo de cambio');
                        }
                    })
                    .catch(err => {
                        mostrarNotificacion('No se pudo actualizar el tipo de cambio.', 'danger');
                    });
            })
            .finally(() => {
                btnTipoCambio.disabled = false;
                btnTipoCambio.innerHTML = '<i class="fas fa-sync-alt"></i>';
            });
    }

    function prepararDetallesCosto() {
        // Obtener valores
        const costo = parseFloat($('#costo').val()) || 0;
        const moneda = $('#moneda').val();
        const tipoCambio = parseFloat($('#tipoCambioActual').text()) || 625;

        // Calcular impuesto (13%)
        const costoBase = costo / 1.13;
        const impuesto = costo - costoBase;

        // Crear objeto de detalles
        const detalles = {
            costo_base: parseFloat(costoBase.toFixed(2)),
            impuesto_iva: parseFloat(impuesto.toFixed(2)),
            otros_costos: 0,
            tipo_cambio: tipoCambio
        };

        // Convertir a JSON y asignar al campo
        $('#detallesCosto').val(JSON.stringify(detalles));

    }

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
});