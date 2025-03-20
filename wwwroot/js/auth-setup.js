document.addEventListener('DOMContentLoaded', function() {
    // Toggle para mostrar/ocultar el formulario de código de respaldo
    const toggleBackupCodeBtn = document.getElementById('toggleBackupCodeOption');
    const backupCodeForm = document.getElementById('backupCodeForm');
    
    if (toggleBackupCodeBtn && backupCodeForm) {
        toggleBackupCodeBtn.addEventListener('click', function(e) {
            e.preventDefault();
            if (backupCodeForm.style.display === 'none') {
                backupCodeForm.style.display = 'block';
                toggleBackupCodeBtn.textContent = 'Usar código OTP';
            } else {
                backupCodeForm.style.display = 'none';
                toggleBackupCodeBtn.textContent = 'Usar código de respaldo';
            }
        });
    }
    
    // Generación de QR para configuración inicial
    if (typeof qrSetupConfig !== 'undefined' && qrSetupConfig.isFirstTimeSetup && qrSetupConfig.secret) {
        try {
            console.log("Iniciando generación de QR");
            
            // Obtener datos de la configuración
            var email = qrSetupConfig.email;
            var secret = qrSetupConfig.secret;
            var issuer = qrSetupConfig.issuer || 'COMAVI_DockTrack';
            
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