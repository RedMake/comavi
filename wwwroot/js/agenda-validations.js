/*
 * Validaciones para formularios de agenda
 */
function initAgendaValidations() {
    // Mostrar/ocultar el campo de días de anticipación basado en si se requiere notificación
    function toggleDiasAnticipacion() {
        if ($("#requiere_notificacion").is(":checked")) {
            $("#dias_anticipacion_notificacion").closest(".form-group").show();
        } else {
            $("#dias_anticipacion_notificacion").closest(".form-group").hide();
        }
    }

    // Configuración inicial
    toggleDiasAnticipacion();

    // Cambiar al hacer clic
    $("#requiere_notificacion").change(function () {
        toggleDiasAnticipacion();
    });

    // Validación de fecha de inicio
    $("#fecha_inicio").change(function () {
        validateStartDate();
    });

    // Validación de fecha de fin
    $("#fecha_fin").change(function () {
        validateEndDate();
    });

    // Función para validar fecha de inicio
    function validateStartDate() {
        var fechaInicio = new Date($("#fecha_inicio").val());
        var hoy = new Date();
        hoy.setHours(0, 0, 0, 0); // Establecer a medianoche para comparar solo la fecha

        if (fechaInicio < hoy) {
            $("#fecha_inicio_error").show();
            $("#fecha_inicio").addClass("is-invalid");
            return false;
        } else {
            $("#fecha_inicio_error").hide();
            $("#fecha_inicio").removeClass("is-invalid");

            // Validar fecha fin si existe
            if ($("#fecha_fin").val()) {
                validateEndDate();
            }
            return true;
        }
    }

    // Función para validar fecha de fin
    function validateEndDate() {
        if ($("#fecha_fin").val()) {
            var fechaInicio = new Date($("#fecha_inicio").val());
            var fechaFin = new Date($("#fecha_fin").val());

            if (fechaFin <= fechaInicio) {
                $("#fecha_fin_error").show();
                $("#fecha_fin").addClass("is-invalid");
                return false;
            } else {
                $("#fecha_fin_error").hide();
                $("#fecha_fin").removeClass("is-invalid");
                return true;
            }
        } else {
            $("#fecha_fin_error").hide();
            $("#fecha_fin").removeClass("is-invalid");
            return true;
        }
    }

    // Validación al enviar el formulario
    $("form").submit(function (e) {
        var isStartDateValid = validateStartDate();
        var isEndDateValid = validateEndDate();

        if (!isStartDateValid || !isEndDateValid) {
            e.preventDefault(); // Detener envío si hay errores
        }
    });
}