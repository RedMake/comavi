document.addEventListener('DOMContentLoaded', function() {
    // Verificación de existencia de canvas antes de intentar usarlo
    function verificarYPrevenirErrorCanvas() {
        // Lista de posibles IDs de canvas utilizados en la aplicación
        const posiblesCanvasIds = ['chartCanvas', 'signatureCanvas', 'graphCanvas', 'auxiliarCanvas'];
        
        // Verificar cada posible canvas
        posiblesCanvasIds.forEach(id => {
            const canvas = document.getElementById(id);
            // Si el canvas no existe, crearlo como respaldo
            if (!canvas) {
                const canvasRespaldo = document.createElement('canvas');
                canvasRespaldo.id = id;
                canvasRespaldo.style.display = 'none';
                document.body.appendChild(canvasRespaldo);
            }
        });
    }
    
    // Ejecutar la función de verificación
    verificarYPrevenirErrorCanvas();
    
    // Validación de fecha de vencimiento (debe ser futura)
    const fechaInput = document.getElementById('Fecha_Venc_Licencia');
    if (fechaInput) {
        // Establecer fecha mínima como hoy
        const today = new Date();
        const minDate = today.toISOString().split('T')[0];
        fechaInput.min = minDate;
        
        // Validación adicional
        fechaInput.addEventListener('change', function() {
            const selectedDate = new Date(this.value);
            if (selectedDate <= today) {
                this.setCustomValidity('La fecha de vencimiento debe ser posterior a hoy');
            } 
            else {
                this.setCustomValidity('');
            }
        });
    }
});