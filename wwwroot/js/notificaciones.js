// Script mejorado para manejo de notificaciones y barra de búsqueda
$(document).ready(function () {
    // Número máximo de notificaciones a mostrar en el dropdown
    const MAX_NOTIFICATIONS_DISPLAYED = 3;

    // Función para cargar notificaciones
    function cargarNotificaciones() {
        $.ajax({
            url: '/Notifications/ObtenerNotificacionesNoLeidas',
            type: 'GET',
            dataType: 'json',
            success: function (response) {
                if (response.success) {
                    // Actualizar contador de notificaciones (siempre muestra el total)
                    $('#notificationsCount').text(response.count);

                    // Limpiar el contenedor actual
                    $('#notificationsContainer').empty();

                    // Si no hay notificaciones
                    if (response.count === 0) {
                        $('#notificationsContainer').html(
                            '<a class="dropdown-item d-flex align-items-center" href="/Notifications/Index">' +
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
                        // Mostrar solo un número limitado de notificaciones
                        const notificationsToShow = response.notifications.slice(0, MAX_NOTIFICATIONS_DISPLAYED);

                        // Agregar cada notificación al contenedor
                        $.each(notificationsToShow, function (index, notif) {
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
                                '<a class="dropdown-item d-flex align-items-center" href="/Notifications/Index" data-id="' + notif.id + '">' +
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

                        // Si hay más notificaciones que las mostradas, indicar cuántas más hay
                        if (response.count > MAX_NOTIFICATIONS_DISPLAYED) {
                            const remainingCount = response.count - MAX_NOTIFICATIONS_DISPLAYED;
                            $('#notificationsContainer').append(
                                '<a class="dropdown-item d-flex align-items-center text-center" href="/Notifications/Index">' +
                                '<div class="w-100">' +
                                '<span class="font-weight-bold text-primary">+' + remainingCount + ' notificaciones más</span>' +
                                '</div>' +
                                '</a>'
                            );
                        }
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

    // Configuración para llevar a la página de notificaciones
    $('#notificationsDropdown').on('click', 'a.dropdown-item', function (e) {
        e.preventDefault();
        window.location.href = '/Notifications/Index';
    });

    // Cargar notificaciones al iniciar
    cargarNotificaciones();

    // Actualizar notificaciones cada 60 segundos
    setInterval(cargarNotificaciones, 60000);

    // Términos de búsqueda actualizados según el navbar
    const searchTerms = {
        // Agenda
        'agenda': '/Agenda/Index',
        'mi agenda': '/Agenda/Index',
        'calendario': '/Agenda/Calendar',
        'crear evento': '/Agenda/Create',

        // Perfil
        'perfil': '/Login/Profile',
        'mi perfil': '/Login/Profile',
        'datos personales': '/Login/Profile',

        // Camión (para usuario)
        'mi camión': '/Camion/CamionAsignado',
        'camion': '/Camion/CamionAsignado',
        'vencimientos': '/Calendar/Index',

        // Documentos (para admin)
        'documentos': '/Documentos/PendientesValidacion',
        'pendientes validación': '/Documentos/PendientesValidacion',
        'por vencer': '/Documentos/DocumentosPorVencer',
        'generar reporte': '/Documentos/GenerarReporteDocumentos',

        // Choferes (para admin)
        'choferes': '/Admin/ListarChoferes',
        'registrar chofer': '/Admin/RegistrarChofer',
        'monitorear vencimientos': '/Admin/MonitorearVencimientos',
        'listar choferes': '/Admin/ListarChoferes',

        // Camiones (para admin)
        'camiones': '/Admin/ListarCamiones',
        'listar camiones': '/Admin/ListarCamiones',
        'mantenimientos': '/Admin/NotificacionesMantenimiento',
        'usuarios': '/Admin/ListarUsuarios',
        'dashboard': '/Admin/Dashboard',

        // Configuración
        'contraseña': '/Login/CambiarContrasena',
        'cambiar contraseña': '/Login/CambiarContrasena',
        '2fa': '/Login/ConfigurarMFA',
        'autenticación': '/Login/ConfigurarMFA'
    };

    // Crear el contenedor del dropdown de sugerencias
    $('body').append('<div id="search-suggestions" class="dropdown-menu dropdown-menu-right shadow animated--grow-in" style="display: none; position: absolute; z-index: 1000;"></div>');

    // Función para mostrar sugerencias de búsqueda
    function mostrarSugerencias(query) {
        const $suggestions = $('#search-suggestions');
        $suggestions.empty();

        if (query.length < 2) {
            $suggestions.hide();
            return;
        }

        // Filtrar términos basados en la consulta
        const matches = [];
        $.each(searchTerms, function (term, url) {
            if (term.includes(query.toLowerCase())) {
                matches.push({ term: term, url: url });
            }
        });

        // Mostrar hasta 5 sugerencias
        const maxSuggestions = Math.min(5, matches.length);
        if (maxSuggestions === 0) {
            $suggestions.hide();
            return;
        }

        // Posicionar el dropdown debajo del campo de búsqueda
        const $searchInput = $('.navbar-search input[type="text"]');
        const inputPosition = $searchInput.offset();
        $suggestions.css({
            top: inputPosition.top + $searchInput.outerHeight(),
            left: inputPosition.left,
            width: $searchInput.outerWidth()
        });

        // Agregar sugerencias al dropdown
        for (let i = 0; i < maxSuggestions; i++) {
            $suggestions.append(
                '<a class="dropdown-item search-suggestion" href="' + matches[i].url + '">' +
                matches[i].term.charAt(0).toUpperCase() + matches[i].term.slice(1) +
                '</a>'
            );
        }

        $suggestions.show();
    }

    // Manejar eventos de teclado en el campo de búsqueda
    $('.navbar-search input[type="text"]').on('keyup focus', function () {
        mostrarSugerencias($(this).val());
    });

    // Cerrar sugerencias al hacer clic fuera
    $(document).on('click', function (e) {
        if (!$(e.target).closest('.navbar-search, #search-suggestions').length) {
            $('#search-suggestions').hide();
        }
    });

    // Manejar clics en sugerencias
    $(document).on('click', '.search-suggestion', function (e) {
        e.preventDefault();
        window.location.href = $(this).attr('href');
    });

    // Configuración de la barra de búsqueda
    $('.navbar-search').submit(function (e) {
        e.preventDefault();

        const searchQuery = $(this).find('input[type="text"]').val().trim().toLowerCase();
        if (searchQuery.length < 2) return; // Ignorar búsquedas muy cortas

        // Verificar si la consulta coincide con alguno de los términos
        let foundMatch = false;
        let redirectUrl = '';

        $.each(searchTerms, function (term, url) {
            if (searchQuery.includes(term)) {
                redirectUrl = url;
                foundMatch = true;
                return false; // romper el bucle
            }
        });

        if (foundMatch) {
            window.location.href = redirectUrl;
        } else {
            // Si no hay coincidencia, redirigir a la página de inicio con la consulta
            window.location.href = '/Home/Index?q=' + encodeURIComponent(searchQuery);
        }
    });
});