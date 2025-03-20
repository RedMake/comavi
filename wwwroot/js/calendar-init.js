document.addEventListener('DOMContentLoaded', function() {
    var calendarEl = document.getElementById('calendar');
    var calendar = new FullCalendar.Calendar(calendarEl, {
        initialView: 'dayGridMonth',
        headerToolbar: {
            left: 'prev,next today',
            center: 'title',
            right: 'dayGridMonth,timeGridWeek,listMonth'
        },
        locale: 'es',
        events: eventsData, // Esta variable se definirá en la vista
        eventClick: function(info) {
            // Mostrar modal con detalles del evento
            $('#eventTitle').text(info.event.title);
            $('#eventDate').text(info.event.startStr);
            $('#eventDescription').text(info.event.extendedProps.description || 'No hay descripción disponible');
            
            // Determinar estado
            var estado = '';
            if (info.event.classNames.includes('bg-danger')) {
                estado = '<span class="badge badge-danger">Vencido</span>';
            } else if (info.event.classNames.includes('bg-warning')) {
                estado = '<span class="badge badge-warning">Próximo a vencer</span>';
            } else if (info.event.classNames.includes('bg-success')) {
                estado = '<span class="badge badge-success">Vigente</span>';
            } else if (info.event.classNames.includes('bg-primary')) {
                estado = '<span class="badge badge-primary">Mantenimiento</span>';
            }
            $('#eventStatus').html(estado);
            $('#eventModal').modal('show');
        }
    });
    calendar.render();
});