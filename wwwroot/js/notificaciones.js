// Script para manejo de notificaciones y barra de búsqueda
$(document).ready(function () {
    // Función para cargar notificaciones
    function cargarNotificaciones() {
        $.ajax({
            url: '/Notifications/ObtenerNotificacionesNoLeidas',
            type: 'GET',
            dataType: 'json',
            success: function (response) {
                if (response.success) {
                    // Actualizar contador de notificaciones
                    $('#notificationsCount').text(response.count);
                    
                    // Limpiar el contenedor actual
                    $('#notificationsContainer').empty();
                    
                    // Si no hay notificaciones
                    if (response.count === 0) {
                        $('#notificationsContainer').html(
                            '<a class="dropdown-item d-flex align-items-center" href="#">' +
                            '<div class="mr-3">' +
                            '<div class="icon-circle bg-primary">' +
                            '<i class="fas fa-info-circle text-white"></i>' +
                            '</div>' +
                            '</div>' +
                            '<div>' +
                            '<div class="small text-gray-500">Ahora</div>' +
                            '<span class="font-weight-bold">No tienes notificaciones nuevas</span>' +
                            '</div>' +
                            '</a>'
                        );
                    } else {
                        // Agregar cada notificación al contenedor
                        $.each(response.notifications, function (index, notif) {
                            // Determinar el ícono según el tipo de notificación
                            let iconClass = 'fa-info-circle';
                            let bgClass = 'bg-primary';
                            
                            if (notif.tipo === 'Vencimiento') {
                                iconClass = 'fa-calendar-alt';
                                bgClass = 'bg-warning';
                            } else if (notif.tipo === 'Alerta') {
                                iconClass = 'fa-exclamation-triangle';
                                bgClass = 'bg-danger';
                            } else if (notif.tipo === 'Documento Nuevo') {
                                iconClass = 'fa-file-alt';
                                bgClass = 'bg-info';
                            }
                            
                            let notifHtml = 
                                '<a class="dropdown-item d-flex align-items-center" href="#" data-id="' + notif.id + '">' +
                                '<div class="mr-3">' +
                                '<div class="icon-circle ' + bgClass + '">' +
                                '<i class="fas ' + iconClass + ' text-white"></i>' +
                                '</div>' +
                                '</div>' +
                                '<div>' +
                                '<div class="small text-gray-500">' + notif.fecha + '</div>' +
                                '<span class="font-weight-bold">' + notif.mensaje + '</span>' +
                                '</div>' +
                                '</a>';
                                
                            $('#notificationsContainer').append(notifHtml);
                        });
                        
                        // Agregar manejador de eventos para marcar como leída
                        $('#notificationsContainer a').click(function (e) {
                            e.preventDefault();
                            const notifId = $(this).data('id');
                            marcarNotificacionLeida(notifId);
                            $(this).fadeOut();
                        });
                    }
                } else {
                    console.error('Error al cargar notificaciones:', response.message);
                }
            },
            error: function (error) {
                console.error('Error en la solicitud AJAX:', error);
            }
        });
    }
    
    // Función para marcar notificación como leída
    function marcarNotificacionLeida(id) {
        $.ajax({
            url: '/Notifications/MarcarLeida',
            type: 'POST',
            data: { id: id },
            headers: {
                'RequestVerificationToken': $('input[name="__RequestVerificationToken"]').val()
            },
            success: function (response) {
                if (response.success) {
                    // Actualizar contador de notificaciones
                    const currentCount = parseInt($('#notificationsCount').text());
                    if (currentCount > 0) {
                        $('#notificationsCount').text(currentCount - 1);
                    }
                }
            }
        });
    }
    
    // Cargar notificaciones al iniciar
    cargarNotificaciones();
    
    // Actualizar notificaciones cada 60 segundos
    setInterval(cargarNotificaciones, 60000);
    
    // Configuración de la barra de búsqueda
    $('.navbar-search').submit(function (e) {
        e.preventDefault();
        
        const searchQuery = $(this).find('input[type="text"]').val().trim().toLowerCase();
        if (searchQuery.length < 2) return; // Ignorar búsquedas muy cortas
        
        // Determinar el rol del usuario para direccionamiento adecuado
        const isAdmin = $('body').hasClass('admin-role') || $('#collapseSistema').length > 0;
        
        // Mapeo de términos de búsqueda a URLs según el rol
        const searchMap = {
            // Términos comunes para ambos roles
            'perfil': '/Login/Profile',
            'cuenta': '/Login/Profile',
            'contraseña': '/Login/CambiarContrasena',
            'password': '/Login/CambiarContrasena',
            'seguridad': '/Login/ConfigurarMFA',
            'mfa': '/Login/ConfigurarMFA',
            'agenda': '/Agenda/Index',
            'eventos': '/Agenda/Index',
            'calendario': '/Agenda/Calendar',
            'notificaciones': '/Notifications/Index',
            'alertas': '/Notifications/Index',
            'salir': '/Login/Logout',
            'cerrar': '/Login/Logout',
            'logout': '/Login/Logout',
            
            // Términos para admin
            'usuarios': '/Sistema/Usuarios',
            'choferes': '/Admin/ObtenerChoferesPaginados',
            'camiones': '/Admin/RegistrarCamion',
            'documentos': '/Documentos/PendientesValidacion',
            'pendientes': '/Documentos/PendientesValidacion',
            'vencer': '/Documentos/DocumentosPorVencer',
            'vencimientos': '/Documentos/DocumentosPorVencer',
            'sistema': '/Sistema/Notificaciones',
            'sesiones': '/Sistema/SesionesActivas',
            'intentos': '/Sistema/IntentosLogin',
            'mantenimiento': '/Admin/HistorialMantenimiento',
            'registrar': '/Admin/RegistrarChofer',
            
            // Términos para usuario normal
            'mi camión': '/Camion/CamionAsignado',
            'camion': '/Camion/CamionAsignado',
            'mi licencia': '/Login/Profile',
            'mi perfil': '/Login/Profile',
            'subir': '/Login/SubirDocumentos',
            'documentos': '/Login/SubirDocumentos',
            'crear evento': '/Agenda/Create'
        };
        
        // Buscar coincidencias
        let foundMatch = false;
        let redirectUrl = '';
        
        $.each(searchMap, function(term, url) {
            if (searchQuery.includes(term)) {
                // Verificar si la URL es específica de admin y el usuario no es admin
                if (!isAdmin && 
                    (url.includes('/Sistema/') || 
                     url.includes('/Admin/') || 
                     url.includes('/Documentos/'))) {
                    return true; // continuar el bucle
                }
                
                redirectUrl = url;
                foundMatch = true;
                return false; // romper el bucle
            }
        });
        
        if (foundMatch) {
            window.location.href = redirectUrl;
        } else {
            // Si no hay coincidencia, redirigir a la página de inicio
            window.location.href = isAdmin ? '/Home/Index?q=' + encodeURIComponent(searchQuery) : '/Home/Index';
        }
    });
    
    // Detectar rol del usuario y añadir clase para identificación
    if ($('#collapseSistema').length > 0) {
        $('body').addClass('admin-role');
    } else {
        $('body').addClass('user-role');
    }
});