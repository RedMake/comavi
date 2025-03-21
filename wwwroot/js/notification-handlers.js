$(document).ready(function () {
    $.ajaxSetup({
        beforeSend: function (xhr, settings) {
            // Solo incluir token en solicitudes POST, PUT, DELETE (no en GET)
            if (settings.type !== 'GET') {
                // Obtener el token CSRF del formulario
                var token = $('input[name="__RequestVerificationToken"]').val();

                // Si existe el token, añadirlo a los headers
                if (token) {
                    xhr.setRequestHeader('RequestVerificationToken', token);
                }
            }
        }
    });

    function getAntiForgeryToken() {
        return $('input[name="__RequestVerificationToken"]').val();
    }

    // Configuración de paginación
    const notificacionesPorPagina = 5; // Ajusta según tus necesidades
    let paginaActual = 1;
    let totalNotificaciones = 0;
    let totalPaginas = 0;

    // Cargar la primera página al iniciar
    cargarNotificacionesServidor(1, notificacionesPorPagina);

    // Función principal para cargar notificaciones desde el servidor
    function cargarNotificacionesServidor(pagina, elementosPorPagina) {
        // Mostrar indicador de carga
        $('.notifications-list').html(
            '<div class="text-center py-4">' +
            '<i class="fas fa-spinner fa-spin fa-2x text-primary mb-3"></i>' +
            '<p>Cargando notificaciones...</p>' +
            '</div>'
        );
        console.log("Solicitando notificaciones para página:", pagina);

        $.ajax({
            url: '/Notifications/ObtenerPagina',
            type: 'GET',
            data: {
                pagina: pagina,
                elementosPorPagina: elementosPorPagina

            },
            success: function (response) {
                if (response.success) {
                    console.log("Respuesta recibida:", response);


                    // Actualizar variables de paginación
                    paginaActual = response.currentPage;
                    totalPaginas = response.totalPages;
                    totalNotificaciones = response.totalItems;

                    // Limpiar la lista actual
                    $('.notifications-list').empty();

                    // Si no hay notificaciones
                    if (response.notifications.length === 0) {
                        $('.notifications-list').html(
                            '<div class="text-center py-4">' +
                            '<i class="fas fa-bell-slash fa-3x text-muted mb-3"></i>' +
                            '<p>No hay notificaciones recientes.</p>' +
                            '</div>'
                        );

                        // Eliminar controles de paginación
                        $('.pagination-controls').remove();
                    } else {
                        // Agregar las notificaciones recibidas
                        $.each(response.notifications, function (index, notif) {
                            let notifHtml =
                                '<div class="list-group-item list-group-item-action ' + (notif.leida ? 'bg-light' : '') + '" data-id="' + notif.id + '">' +
                                '<div class="d-flex w-100 justify-content-between">' +
                                '<h5 class="mb-1">' + notif.tipo + '</h5>' +
                                '<small class="text-muted">' + notif.fecha + '</small>' +
                                '</div>' +
                                '<p class="mb-1">' + notif.mensaje + '</p>' +
                                '<div class="d-flex justify-content-end">';

                            if (!notif.leida) {
                                notifHtml += '<button class="btn btn-sm btn-outline-success mr-2 btn-mark-read">' +
                                    '<i class="fas fa-check"></i> Marcar como leída' +
                                    '</button>';
                            }

                            notifHtml += '<button class="btn btn-sm btn-outline-danger btn-delete">' +
                                '<i class="fas fa-trash"></i> Eliminar' +
                                '</button>' +
                                '</div>' +
                                '</div>';

                            $('.notifications-list').append(notifHtml);
                        });

                        // Actualizar o crear controles de paginación
                        actualizarControlesPaginacion();

                        // Reactivar manejadores para botones de acción
                        activarManejadoresEventos();
                    }
                } else {
                    console.error('Error al cargar notificaciones:', response.message);
                    $('.notifications-list').html(
                        '<div class="text-center py-4">' +
                        '<i class="fas fa-exclamation-circle fa-3x text-danger mb-3"></i>' +
                        '<p>Error al cargar notificaciones. Intente de nuevo.</p>' +
                        '</div>'
                    );
                }
            },
            error: function () {
                console.error('Error en la solicitud AJAX');
                $('.notifications-list').html(
                    '<div class="text-center py-4">' +
                    '<i class="fas fa-exclamation-circle fa-3x text-danger mb-3"></i>' +
                    '<p>Error de conexión. Intente de nuevo más tarde.</p>' +
                    '</div>'
                );
            }
        });
    }

    // Función para actualizar o crear controles de paginación
    function actualizarControlesPaginacion() {
        // Si hay suficientes notificaciones para paginar
        if (totalNotificaciones > notificacionesPorPagina) {
            // Crear controles de paginación si no existen
            if ($('.pagination-controls').length === 0) {
                let paginacionHTML = `
                    <div class="d-flex justify-content-between align-items-center mt-3 pagination-controls">
                        <div>
                            <span class="pagination-info">Mostrando ${((paginaActual - 1) * notificacionesPorPagina) + 1}-${Math.min(paginaActual * notificacionesPorPagina, totalNotificaciones)} de ${totalNotificaciones}</span>
                        </div>
                        <div class="btn-group">
                            <button class="btn btn-sm btn-outline-primary pagination-prev" ${paginaActual === 1 ? 'disabled' : ''}>
                                <i class="fas fa-chevron-left"></i>
                            </button>
                            <span class="btn btn-sm btn-outline-secondary pagination-current">${paginaActual}/${totalPaginas}</span>
                            <button class="btn btn-sm btn-outline-primary pagination-next" ${paginaActual === totalPaginas ? 'disabled' : ''}>
                                <i class="fas fa-chevron-right"></i>
                            </button>
                        </div>
                    </div>
                `;
                $('.notifications-list').after(paginacionHTML);

                // Añadir eventos a los botones de paginación
                $('.pagination-prev').click(function () {
                    if (paginaActual > 1) {
                        cargarNotificacionesServidor(paginaActual - 1, notificacionesPorPagina);
                    }
                });

                $('.pagination-next').click(function () {
                    if (paginaActual < totalPaginas) {
                        cargarNotificacionesServidor(paginaActual + 1, notificacionesPorPagina);
                    }
                });
            } else {
                // Actualizar controles existentes
                $('.pagination-info').text(`Mostrando ${((paginaActual - 1) * notificacionesPorPagina) + 1}-${Math.min(paginaActual * notificacionesPorPagina, totalNotificaciones)} de ${totalNotificaciones}`);
                $('.pagination-current').text(`${paginaActual}/${totalPaginas}`);
                $('.pagination-prev').prop('disabled', paginaActual === 1);
                $('.pagination-next').prop('disabled', paginaActual === totalPaginas);
            }
        } else {
            // Si hay pocas notificaciones, eliminar controles de paginación
            $('.pagination-controls').remove();
        }
    }

    // Función para activar manejadores de eventos en los botones
    function activarManejadoresEventos() {
        // Marcar como leída
        $('.btn-mark-read').click(function (e) {
            e.preventDefault();
            var btn = $(this);
            var notificationId = btn.closest('.list-group-item').data('id');

            $.ajax({
                url: markReadUrl,
                type: 'POST',
                data: {
                    id: notificationId,
                    __RequestVerificationToken: getAntiForgeryToken()
                },
                success: function (result) {
                    if (result.success) {
                        btn.closest('.list-group-item').addClass('bg-light');
                        btn.fadeOut(300, function () {
                            $(this).remove();
                        });
                    } else {
                        alert('Error al marcar como leída: ' + result.message);
                    }
                },
                error: function () {
                    alert('Error al marcar como leída');
                }
            });
        });

        // Eliminar notificación
        $('.btn-delete').click(function (e) {
            e.preventDefault();
            var btn = $(this);
            var notificationId = btn.closest('.list-group-item').data('id');

            if (confirm('¿Está seguro de eliminar esta notificación?')) {
                $.ajax({
                    url: deleteUrl,
                    type: 'POST',
                    data: {
                        id: notificationId,
                        __RequestVerificationToken: getAntiForgeryToken()
                    },
                    success: function (result) {
                        if (result.success) {
                            btn.closest('.list-group-item').fadeOut(300, function () {
                                $(this).remove();

                                // Si se eliminó la última notificación en la página actual
                                if ($('.list-group-item:visible').length === 0) {
                                    // Si hay más páginas anteriores
                                    if (paginaActual > 1) {
                                        cargarNotificacionesServidor(paginaActual - 1, notificacionesPorPagina);
                                    } else {
                                        // Recargar la página actual para mostrar el mensaje de "no hay notificaciones"
                                        cargarNotificacionesServidor(1, notificacionesPorPagina);
                                    }
                                } else {
                                    // Recargar la página actual para actualizar el contador
                                    cargarNotificacionesServidor(paginaActual, notificacionesPorPagina);
                                }
                            });
                        } else {
                            alert('Error al eliminar: ' + result.message);
                        }
                    },
                    error: function () {
                        alert('Error al eliminar notificación');
                    }
                });
            }
        });
    }
});