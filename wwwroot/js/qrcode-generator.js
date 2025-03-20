document.addEventListener('DOMContentLoaded', function() {
    // Función para copiar el secreto
    window.copySecret = function() {
        var secretInput = document.getElementById('secretKey');
        var copyAlert = document.getElementById('copyAlert');
        if (secretInput && copyAlert) {
            secretInput.select();
            document.execCommand('copy');
            copyAlert.classList.remove('d-none');
            setTimeout(function() {
                copyAlert.classList.add('d-none');
            }, 3000);
        }
    }

    // Generar código QR si se proporciona la configuración necesaria
    if (typeof qrConfig !== 'undefined' && qrConfig.generateQR) {
        try {
            console.log("Iniciando generación de QR");
            
            // Obtener datos de la configuración
            var email = qrConfig.email;
            var secret = qrConfig.secret;
            var issuer = qrConfig.issuer || 'COMAVI_DockTrack';
            
            // Crear la URL otpauth para aplicaciones de autenticación
            var otpauthUrl = 'otpauth://totp/' +
                encodeURIComponent(issuer) + ':' +
                encodeURIComponent(email) +
                '?secret=' + secret +
                '&issuer=' + encodeURIComponent(issuer) +
                '&algorithm=SHA1&digits=6&period=30';
                
            console.log("URL otpauth generada:", otpauthUrl);
            
            // Obtener el elemento contenedor
            var container = document.getElementById('qrcode-container');
            console.log("Contenedor encontrado:", container != null);
            
            if (container) {
                // Alternativa que no usa canvas
                QRCode.toDataURL(otpauthUrl, function(err, url) {
                    if (err) {
                        console.error("Error generando QR:", err);
                        container.innerHTML = '<div class="alert alert-danger">Error al generar el código QR</div>';
                    } else {
                        console.log("QR generado exitosamente");
                        var img = document.createElement('img');
                        img.src = url;
                        img.alt = "Código QR para autenticación";
                        img.width = 200;
                        img.height = 200;
                        container.appendChild(img);
                    }
                });
            } else {
                console.error("No se encontró el contenedor del QR");
            }
        } catch (error) {
            console.error("Error en generación de QR:", error);
        }
    }
});