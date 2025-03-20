document.addEventListener('DOMContentLoaded', function () {
    const fechaEmisionInput = document.getElementById('FechaEmision');
    const fechaVencimientoInput = document.getElementById('FechaVencimiento');
    const tipoDocumentoSelect = document.getElementById('TipoDocumento');

    // Funciones para manejar fechas
    if (fechaEmisionInput && fechaVencimientoInput) {
        // Obtener la fecha actual en formato YYYY-MM-DD para campos de tipo date
        const today = new Date();
        const currentDate = today.toISOString().split('T')[0];

        // Establecer fecha máxima para emisión (no puede ser futura)
        fechaEmisionInput.max = currentDate;

        // Verificar fecha de emisión
        // Si es 01/01/0001 (valor mínimo de DateTime) o está vacía, establecer fecha actual
        function isInvalidDate(dateStr) {
            if (!dateStr) return true;

            const date = new Date(dateStr);
            // Verificar si es una fecha válida y si es 01/01/0001 o cercana
            return isNaN(date.getTime()) || date.getFullYear() <= 1901;
        }

        if (isInvalidDate(fechaEmisionInput.value)) {
            fechaEmisionInput.value = currentDate;
        }

        // Calcular una fecha de vencimiento predeterminada (1 año después de la emisión)
        function calculateExpiryDate(emissionDate) {
            const expiry = new Date(emissionDate);
            expiry.setFullYear(expiry.getFullYear() + 1);
            return expiry.toISOString().split('T')[0];
        }

        // Verificar si la fecha de vencimiento necesita un valor predeterminado
        if (isInvalidDate(fechaVencimientoInput.value)) {
            fechaVencimientoInput.value = calculateExpiryDate(fechaEmisionInput.value);
        }

        // Asegurar que la fecha de vencimiento sea posterior a la de emisión
        fechaEmisionInput.addEventListener('change', function () {
            // Actualizar la fecha mínima de vencimiento
            fechaVencimientoInput.min = this.value;

            // Si la fecha de vencimiento es anterior a la nueva fecha de emisión,
            // actualizarla automáticamente a un año después
            if (new Date(fechaVencimientoInput.value) <= new Date(this.value)) {
                fechaVencimientoInput.value = calculateExpiryDate(this.value);
            }
        });

        // Actualizar la restricción inicial
        fechaVencimientoInput.min = fechaEmisionInput.value;

        // Validación para fecha de vencimiento
        fechaVencimientoInput.addEventListener('change', function () {
            if (new Date(this.value) <= new Date(fechaEmisionInput.value)) {
                this.setCustomValidity('La fecha de vencimiento debe ser posterior a la fecha de emisión');
            } else {
                this.setCustomValidity('');
            }
        });
    }

    // Validación de tamaño de archivo
    const archivoInput = document.querySelector('input[type="file"]');
    if (archivoInput) {
        archivoInput.addEventListener('change', function () {
            if (this.files.length > 0) {
                const fileSize = this.files[0].size / 1024 / 1024; // tamaño en MB
                if (fileSize > 10) {
                    this.setCustomValidity('El archivo no debe exceder 10MB');
                } else {
                    this.setCustomValidity('');
                }
            }
        });
    }
});