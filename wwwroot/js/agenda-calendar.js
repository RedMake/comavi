document.addEventListener('DOMContentLoaded', function() {
    var calendarEl = document.getElementById('calendar');
    var calendar = new FullCalendar.Calendar(calendarEl, {
        initialView: 'dayGridMonth',
        headerToolbar: {
            left: 'prev,next today',
            center: 'title',
            right: 'dayGridMonth,timeGridWeek,timeGridDay,listMonth'
        },
        locale: 'es',
        events: eventsData, // Esta variable se definirá en la vista
        eventClick: function(info) {
            $('#eventTitle').text(info.event.title);
            $('#eventStart').text(info.event.startStr);
            $('#eventEnd').text(info.event.endStr || 'N/A');
            $('#eventDescription').text(info.event.extendedProps.description || 'Sin descripción');
            $('#editEventLink').attr('href', '/Agenda/Edit/' + info.event.id);
            $('#eventModal').modal('show');
        }
    });
    calendar.render();
});