/**
 * Funciones JavaScript para la gestión de choferes
 */
document.addEventListener('DOMContentLoaded', function() {
    // Validaciones para el formulario de registro de chofer
    if (document.getElementById('fecha_venc_licencia')) {
        // Establecer la fecha mínima como mañana
        var tomorrow = new Date();
        tomorrow.setDate(tomorrow.getDate() + 1);
        
        // Formatear la fecha como YYYY-MM-DD para el atributo min
        var year = tomorrow.getFullYear();
        var month = String(tomorrow.getMonth() + 1).padStart(2, '0');
        var day = String(tomorrow.getDate()).padStart(2, '0');
        var tomorrowFormatted = year + '-' + month + '-' + day;
        
        // Establecer el atributo min en el campo de fecha
        $('#fecha_venc_licencia').attr('min', tomorrowFormatted);
        
        // Validar que la fecha de vencimiento sea al menos mañana
        $('#fecha_venc_licencia').change(function() {
            var selectedDate = new Date($(this).val());
            var tomorrow = new Date();
            tomorrow.setDate(tomorrow.getDate() + 1);
            tomorrow.setHours(0, 0, 0, 0); // Resetear horas para comparación correcta
            
            if (selectedDate < tomorrow) {
                alert('La fecha de vencimiento debe ser como mínimo el día de mañana.');
                $(this).val('');
            }
        });
    }

    if (document.getElementById('edad')) {
        // Validar que la edad sea mayor de 18
        $('#edad').change(function() {
            var age = parseInt($(this).val());
            
            if (age < 18) {
                alert('El chofer debe ser mayor de edad (18 años o más).');
                $(this).val('');
            }
        });
    }
    if (document.getElementById('licencia')) {
        // Validación cuando se envía el formulario
        $('form').submit(function (event) {
            if ($('#licencia').val() === '') {
                $('#licencia').addClass('is-invalid');
                $('[asp-validation-for="licencia"]').text('La licencia es requerida.');
                event.preventDefault();
            } else {
                $('#licencia').removeClass('is-invalid').addClass('is-valid');
                $('[asp-validation-for="licencia"]').text('');
            }
        });

        $('#licencia').change(function () {
            if ($(this).val() !== '') {
                $(this).removeClass('is-invalid').addClass('is-valid');
                $('[asp-validation-for="licencia"]').text('');
            }
        });
    }

    const cedulaInput = document.querySelector('input[name="numero_cedula"]');
    if (cedulaInput) {
        cedulaInput.addEventListener('blur', function () {
            const cedula = this.value.trim();
            if (cedula.length !== 9 || isNaN(Number(cedula))) {
                this.setCustomValidity('El número de cédula debe tener 9 dígitos numéricos');
            } else {
                this.setCustomValidity('');
            }
        });
    }

});