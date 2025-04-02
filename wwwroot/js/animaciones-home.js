// animaciones.js - Script para implementar las animaciones avanzadas

document.addEventListener('DOMContentLoaded', function() {
    // Inicializar todas las animaciones
    initScrollIndicator();
    initScrollAnimations();
    initParticles();
    initTiltEffect();
    initCursorFollow();
    initCountUp();
    initRevealSections();
    initSmoothScrolling();
    
    // Inicializar la línea de tiempo (complementa el código original)
    initTimeline();
});

// Función para inicializar la línea de tiempo original
function initTimeline() {
    const timelineEvents = document.querySelectorAll('.timeline-event');
            
    // Activar animación con delay para cada evento
    setTimeout(function() {
        timelineEvents.forEach(function(event, index) {
            setTimeout(function() {
                event.classList.add('active');
            }, index * 200); // 200ms de retraso entre cada evento
        });
    }, 500); // 500ms de retraso inicial
}

// Indicador de scroll en la parte superior
function initScrollIndicator() {
    const scrollIndicator = document.createElement('div');
    scrollIndicator.className = 'scroll-indicator';
    document.body.appendChild(scrollIndicator);

    window.addEventListener('scroll', function() {
        const winScroll = document.body.scrollTop || document.documentElement.scrollTop;
        const height = document.documentElement.scrollHeight - document.documentElement.clientHeight;
        const scrolled = (winScroll / height) * 100;
        scrollIndicator.style.width = scrolled + "%";
    });
}

// Animaciones basadas en scroll
function initScrollAnimations() {
    // Detectar elementos en el viewport
    function isElementInViewport(el) {
        const rect = el.getBoundingClientRect();
        return (
            (rect.top <= 0 && rect.bottom >= 0) ||
            (rect.bottom >= (window.innerHeight || document.documentElement.clientHeight) && 
             rect.top <= (window.innerHeight || document.documentElement.clientHeight)) ||
            (rect.top >= 0 && rect.bottom <= (window.innerHeight || document.documentElement.clientHeight))
        );
    }

    // Función para animar elementos cuando son visibles
    function animateOnScroll() {
        // Valor items
        const valueItems = document.querySelectorAll('.value-item');
        valueItems.forEach(item => {
            if (isElementInViewport(item) && !item.classList.contains('animated')) {
                item.classList.add('animated');
            }
        });

        // Cartas con animaciones de entrada
        const cards = document.querySelectorAll('.card.slide-in-left, .card.slide-in-right, .card.fade-in-up');
        cards.forEach(card => {
            if (isElementInViewport(card) && !card.classList.contains('animated')) {
                card.classList.add('animated');
            }
        });

        // Texto revelado
        const revealTexts = document.querySelectorAll('.reveal-text');
        revealTexts.forEach(text => {
            if (isElementInViewport(text) && !text.classList.contains('animated')) {
                text.classList.add('animated');
            }
        });

        // Contador
        const countUpElements = document.querySelectorAll('.count-up');
        countUpElements.forEach(element => {
            if (isElementInViewport(element) && !element.classList.contains('animated')) {
                element.classList.add('animated');
                const target = parseInt(element.getAttribute('data-target'));
                const duration = parseInt(element.getAttribute('data-duration') || 2000);
                animateValue(element, 0, target, duration);
            }
        });

        // Secciones con efecto de revelación
        const revealSections = document.querySelectorAll('.reveal-section');
        revealSections.forEach(section => {
            if (isElementInViewport(section) && !section.classList.contains('visible')) {
                section.classList.add('visible');
            }
        });
    }

    // Escuchar eventos de scroll
    window.addEventListener('scroll', animateOnScroll);
    
    // Iniciar animaciones visibles al cargar
    setTimeout(animateOnScroll, 100);
}

// Efecto de partículas para fondos
function initParticles() {
    // Crear contenedor de partículas solo cuando sea necesario
    const timelineSection = document.getElementById('timeline-section');
    if (!timelineSection) return;
    
    // Bandera para controlar si las partículas ya se activaron
    let particlesActivated = false;
    
    // Función para crear el contenedor de partículas bajo demanda
    function createParticlesContainer() {
        if (particlesActivated) return; // Solo ejecutar una vez
        
        // Marcar como activado
        particlesActivated = true;
        
        // Crear el contenedor
        const particlesContainer = document.createElement('div');
        particlesContainer.className = 'particles-container';
        particlesContainer.style.position = 'absolute';
        particlesContainer.style.top = '0';
        particlesContainer.style.left = '0';
        particlesContainer.style.width = '100%';
        particlesContainer.style.height = '100%';
        particlesContainer.style.zIndex = '0';
        particlesContainer.style.pointerEvents = 'none'; // Para que no interfiera con los clicks
        particlesContainer.style.opacity = '0';
        particlesContainer.style.transition = 'opacity 0.5s ease';
        
        // Insertar al principio del contenedor
        timelineSection.insertBefore(particlesContainer, timelineSection.firstChild);
        
        // Hacer visible con un pequeño retraso
        setTimeout(() => {
            particlesContainer.style.opacity = '1';
        }, 50);
        
        // Crear canvas dentro del contenedor
        const canvas = document.createElement('canvas');
        canvas.style.display = 'block';
        particlesContainer.appendChild(canvas);
        const ctx = canvas.getContext('2d');
        
        // Configuración de partículas
        const particlesArray = [];
        const numberOfParticles = 80; // Reducido para mejor rendimiento
        const colors = ['#4e73df', '#36b9cc', '#1cc88a', '#f6c23e', '#e74a3b'];
        
        // Ajustar tamaño del canvas
        canvas.width = particlesContainer.offsetWidth;
        canvas.height = particlesContainer.offsetHeight;
        
        // Clase de partícula
        class Particle {
            constructor() {
                this.x = Math.random() * canvas.width;
                this.y = Math.random() * canvas.height;
                this.size = Math.random() * 4 + 1;
                this.speedX = Math.random() * 2 - 1;
                this.speedY = Math.random() * 2 - 1;
                this.color = colors[Math.floor(Math.random() * colors.length)];
                this.opacity = 1;
                this.fadeOut = false;
            }
            
            update() {
                this.x += this.speedX;
                this.y += this.speedY;
                
                // Manejar rebotes en los bordes
                if (this.x < 0 || this.x > canvas.width) this.speedX *= -1;
                if (this.y < 0 || this.y > canvas.height) this.speedY *= -1;
                
                // Si está en modo fadeOut, reducir opacidad
                if (this.fadeOut) {
                    this.opacity -= 0.01;
                    if (this.opacity <= 0) this.opacity = 0;
                }
            }
            
            draw() {
                ctx.globalAlpha = this.opacity;
                ctx.fillStyle = this.color;
                ctx.beginPath();
                ctx.arc(this.x, this.y, this.size, 0, Math.PI * 2);
                ctx.fill();
                ctx.globalAlpha = 1;
            }
        }
        
        // Inicializar partículas
        function init() {
            for (let i = 0; i < numberOfParticles; i++) {
                particlesArray.push(new Particle());
            }
        }
        
        // Variable para controlar la animación
        let animationId = null;
        let animationActive = true;
        let fadeOutTriggered = false;
        
        // Animar partículas
        function animate() {
            if (!animationActive) return;
            
            ctx.clearRect(0, 0, canvas.width, canvas.height);
            
            let allParticlesFaded = true;
            
            for (let i = 0; i < particlesArray.length; i++) {
                if (fadeOutTriggered) {
                    particlesArray[i].fadeOut = true;
                }
                
                particlesArray[i].update();
                particlesArray[i].draw();
                
                // Comprobar si alguna partícula todavía es visible
                if (particlesArray[i].opacity > 0) {
                    allParticlesFaded = false;
                }
            }
            
            // Si todas las partículas se han desvanecido, detener la animación y eliminar el contenedor
            if (fadeOutTriggered && allParticlesFaded) {
                cancelAnimationFrame(animationId);
                animationActive = false;
                
                // Desvanecer el contenedor antes de eliminarlo
                particlesContainer.style.opacity = '0';
                setTimeout(() => {
                    if (particlesContainer.parentNode) {
                        particlesContainer.parentNode.removeChild(particlesContainer);
                    }
                }, 500);
                
                return;
            }
            
            animationId = requestAnimationFrame(animate);
        }
        
        // Iniciar la animación
        init();
        animate();
        
        // Detener la animación después de 8 segundos
        setTimeout(() => {
            fadeOutTriggered = true;
        }, 8000);
    }
    
    // Evento para activar las partículas al pasar el ratón por la línea de tiempo
    const timeline = timelineSection.querySelector('.horizontal-timeline');
    if (timeline) {
        timeline.addEventListener('mouseenter', createParticlesContainer, { once: true });
    }
    
    // También activar al hacer scroll a la sección como alternativa para móviles
    function checkTimelineVisibility() {
        if (particlesActivated) return;
        
        const rect = timelineSection.getBoundingClientRect();
        const isInViewport = (
            rect.top >= 0 &&
            rect.left >= 0 &&
            rect.bottom <= (window.innerHeight || document.documentElement.clientHeight) &&
            rect.right <= (window.innerWidth || document.documentElement.clientWidth)
        );
        
        if (isInViewport) {
            createParticlesContainer();
            window.removeEventListener('scroll', checkTimelineVisibility);
        }
    }
    
    window.addEventListener('scroll', checkTimelineVisibility);
}
// Efecto 3D para elementos con clase .tilt-effect
function initTiltEffect() {
    const tiltElements = document.querySelectorAll('.tilt-effect');
    
    tiltElements.forEach(element => {
        element.addEventListener('mousemove', function(e) {
            const rect = this.getBoundingClientRect();
            const x = e.clientX - rect.left;
            const y = e.clientY - rect.top;
            
            const xc = rect.width / 2;
            const yc = rect.height / 2;
            
            const dx = x - xc;
            const dy = y - yc;
            
            const tiltX = (dy / yc) * 10;  // Max 10 degrees
            const tiltY = -(dx / xc) * 10; // Max 10 degrees
            
            this.style.transform = `perspective(1000px) rotateX(${tiltX}deg) rotateY(${tiltY}deg) scale3d(1.05, 1.05, 1.05)`;
        });
        
        element.addEventListener('mouseleave', function() {
            this.style.transform = 'perspective(1000px) rotateX(0) rotateY(0) scale3d(1, 1, 1)';
        });
    });
}

// Efecto de seguimiento del cursor
function initCursorFollow() {
    const cursorFollowElements = document.querySelectorAll('.cursor-follow');
    
    document.addEventListener('mousemove', function(e) {
        cursorFollowElements.forEach(element => {
            const rect = element.getBoundingClientRect();
            const centerX = rect.left + rect.width / 2;
            const centerY = rect.top + rect.height / 2;
            
            const moveX = (e.clientX - centerX) / 20; // Reduce el movimiento dividiendo
            const moveY = (e.clientY - centerY) / 20;
            
            element.style.transform = `translate(${moveX}px, ${moveY}px)`;
        });
    });
}

// Animación de contador para números
function initCountUp() {
    // Función para animar el contador
    function animateValue(element, start, end, duration) {
        let startTimestamp = null;
        const step = (timestamp) => {
            if (!startTimestamp) startTimestamp = timestamp;
            const progress = Math.min((timestamp - startTimestamp) / duration, 1);
            const value = Math.floor(progress * (end - start) + start);
            element.innerHTML = value.toLocaleString();
            if (progress < 1) {
                window.requestAnimationFrame(step);
            }
        };
        window.requestAnimationFrame(step);
    }

    // Preparar elementos pero no iniciar la animación (se hará en scroll)
    const countUpElements = document.querySelectorAll('.count-up');
    countUpElements.forEach(element => {
        const target = parseInt(element.getAttribute('data-target'));
        element.innerHTML = "0";
    });

    // La animación se inicia en la función de scroll
    window.animateValue = animateValue;
}

// Efecto de revelado para secciones
function initRevealSections() {
    // Añadir la clase reveal-section a elementos que necesitan esta animación
    const sections = document.querySelectorAll('.card.shadow');
    sections.forEach(section => {
        if (!section.classList.contains('reveal-section')) {
            section.classList.add('reveal-section');
        }
    });
    
    // La animación se controla en la función de scroll
}

// Scroll suave para enlaces de anclaje
function initSmoothScrolling() {
    document.querySelectorAll('a[href^="#"]').forEach(anchor => {
        anchor.addEventListener('click', function(e) {
            e.preventDefault();
            const targetId = this.getAttribute('href');
            if (targetId === '#') return;
            
            const targetElement = document.querySelector(targetId);
            if (targetElement) {
                targetElement.scrollIntoView({
                    behavior: 'smooth',
                    block: 'start'
                });
            }
        });
    });
}

// Función para preparar la página después de cargar
window.addEventListener('load', function() {
    // Retraso para que todas las animaciones empiecen después de cargar la página
    setTimeout(function() {
        // Añadir clases específicas a elementos existentes
        document.querySelectorAll('.card-header h6').forEach((element, index) => {
            element.classList.add('gradient-text');
        });
        
        // Añadir atributos data-text a elementos de texto para efecto de revelado
        document.querySelectorAll('h2.h4').forEach(element => {
            element.classList.add('reveal-text');
            element.setAttribute('data-text', element.textContent);
        });
        
        // Añadir efecto de brillo a elementos destacados
        document.querySelectorAll('.card-header').forEach(element => {
            element.classList.add('shimmer-effect');
        });
        
        // Añadir efectos tilt a tarjetas seleccionadas
        document.querySelectorAll('.info-card, .card.border-left-primary').forEach(element => {
            element.classList.add('tilt-effect');
        });
        
        // Añadir números con animación
        const dataElements = document.querySelectorAll('.card-body p strong');
        dataElements.forEach(element => {
            const text = element.textContent;
            if (!isNaN(parseInt(text))) {
                element.classList.add('count-up');
                element.setAttribute('data-target', text);
                element.setAttribute('data-duration', '1500');
            }
        });
        
        // Añadir clases para animaciones de entrada
        const cards = document.querySelectorAll('.card.shadow.mb-4');
        cards.forEach((card, index) => {
            if (index % 2 === 0) {
                card.classList.add('slide-in-left');
            } else {
                card.classList.add('slide-in-right');
            }
        });
        
        // Hacer que los iconos circulares tengan un efecto cursor-follow
        document.querySelectorAll('.icon-circle').forEach(icon => {
            icon.classList.add('cursor-follow');
        });
        
        // Añadir efecto de zoom a imágenes
        document.querySelectorAll('.card img').forEach(img => {
            const parent = img.parentElement;
            if (!parent.classList.contains('zoom-container')) {
                const container = document.createElement('div');
                container.className = 'zoom-container';
                parent.insertBefore(container, img);
                container.appendChild(img);
            }
        });
        
        // Iniciar animaciones visibles
        document.querySelector('body').classList.add('loaded');
        initScrollAnimations();
    }, 300);
});