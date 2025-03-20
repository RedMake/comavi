$(document).ready(function() {
    // Marcar como leída
    $('.btn-mark-read').click(function(e) {
        e.preventDefault();
        var btn = $(this);
        var notificationId = btn.closest('.list-group-item').data('id');
        $.ajax({
            url: markReadUrl,
            type: 'POST',
            data: {
                id: notificationId,
                __RequestVerificationToken: $('input[name="__RequestVerificationToken"]').val()
            },
            success: function(result) {
                if (result.success) {
                    btn.closest('.list-group-item').addClass('bg-light');
                    btn.remove();
                } else {
                    alert('Error al marcar como leída: ' + result.message);
                }
            },
            error: function() {
                alert('Error al marcar como leída');
            }
        });
    });
    
    // Eliminar notificación
    $('.btn-delete').click(function(e) {
        e.preventDefault();
        var btn = $(this);
        var notificationId = btn.closest('.list-group-item').data('id');
        if (confirm('¿Está seguro de eliminar esta notificación?')) {
            $.ajax({
                url: deleteUrl,
                type: 'POST',
                data: {
                    id: notificationId,
                    __RequestVerificationToken: $('input[name="__RequestVerificationToken"]').val()
                },
                success: function(result) {
                    if (result.success) {
                        btn.closest('.list-group-item').fadeOut(300, function() {
                            $(this).remove();
                            if ($('.list-group-item').length === 0) {
                                $('.notifications-list').html(
                                    '<div class="text-center py-4">' +
                                    '<i class="fas fa-bell-slash fa-3x text-muted mb-3"></i>' +
                                    '<p>No hay notificaciones recientes.</p>' +
                                    '</div>'
                                );
                            }
                        });
                    } else {
                        alert('Error al eliminar: ' + result.message);
                    }
                },
                error: function() {
                    alert('Error al eliminar notificación');
                }
            });
        }
    });
});