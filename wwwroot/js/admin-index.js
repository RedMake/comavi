/**
 * admin-index.js
 * Script para la vista Index del panel de administración
 */
document.addEventListener('DOMContentLoaded', function () {
    // Funcionalidad para animar las tarjetas en hover
    const cards = document.querySelectorAll('.card');
    cards.forEach(card => {
        card.addEventListener('mouseenter', function() {
            this.classList.add('shadow-lg');
        });
        card.addEventListener('mouseleave', function() {
            this.classList.remove('shadow-lg');
        });
    });

    // Inicializar tablas si están presentes en el DOM
    // Las tablas se inicializarán a través de datatables-global.js
    // No es necesario código adicional aquí
});