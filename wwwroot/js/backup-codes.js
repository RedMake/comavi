document.addEventListener('DOMContentLoaded', function() {
    // Botón para copiar todos los códigos
    const btnCopiar = document.getElementById('btnCopiarCodigos');
    const alertCopied = document.getElementById('alertCopied');
    
    if (btnCopiar) {
        btnCopiar.addEventListener('click', function() {
            // Obtener todos los códigos
            const codigos = Array.from(document.querySelectorAll('.backup-code'))
                .map(el => el.textContent.trim())
                .join('\n');
                
            // Copiar al portapapeles
            const textarea = document.createElement('textarea');
            textarea.value = codigos;
            document.body.appendChild(textarea);
            textarea.select();
            document.execCommand('copy');
            document.body.removeChild(textarea);
            
            // Mostrar alerta
            alertCopied.classList.remove('d-none');
            setTimeout(function() {
                alertCopied.classList.add('d-none');
            }, 3000);
        });
    }
    
    // Botón para descargar como PDF
    const btnDescargar = document.getElementById('btnDescargarCodigos');
    
    if (btnDescargar && typeof window.jspdf !== 'undefined') {
        btnDescargar.addEventListener('click', function() {
            const { jsPDF } = window.jspdf;
            const doc = new jsPDF();
            
            // Título
            doc.setFontSize(18);
            doc.text('Códigos de respaldo - COMAVI', 20, 20);
            
            // Información
            doc.setFontSize(12);
            doc.text('Estos códigos le permitirán acceder a su cuenta si pierde acceso a su', 20, 35);
            doc.text('aplicación de autenticación. Cada código puede usarse una sola vez.', 20, 42);
            
            // Fecha
            const fecha = new Date().toLocaleDateString();
            doc.text(`Generados el: ${fecha}`, 20, 55);
            
            // Códigos
            doc.setFontSize(14);
            doc.text('Sus códigos de respaldo:', 20, 70);
            const codigos = Array.from(document.querySelectorAll('.backup-code'))
                .map(el => el.textContent.trim());
                
            let yPos = 85;
            codigos.forEach(codigo => {
                doc.text(codigo, 30, yPos);
                yPos += 10;
            });
            
            // Nota final
            yPos += 10;
            doc.setFontSize(12);
            doc.text('Importante: Guarde estos códigos en un lugar seguro.', 20, yPos);
            
            // Descargar PDF
            doc.save('codigos-respaldo-comavi.pdf');
        });
    }
});