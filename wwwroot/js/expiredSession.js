document.addEventListener('DOMContentLoaded', function() {
    let countdown = 10;
    const countdownElement = document.getElementById('countdown');
    const loginBtn = document.getElementById('loginBtn');
    
    // Establecer el atributo href para el botón de inicio de sesión
    // En Razor Pages, podemos usar rutas relativas o una variable global definida en el cshtml
    loginBtn.setAttribute('href', loginUrl); // loginUrl se define en el cshtml
    
    const timer = setInterval(() => {
        countdown--;
        countdownElement.textContent = countdown;
        
        // Añadir efecto de urgencia cuando queda poco tiempo
        if (countdown <= 3) {
            countdownElement.style.color = '#dc3545';
            countdownElement.style.fontSize = '1.8rem';
        }
        
        if (countdown <= 0) {
            clearInterval(timer);
            window.location.href = loginUrl;
        }
    }, 1000);
});