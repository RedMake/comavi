/**
 * Inicialización de gráficos para la página de mantenimiento
 * Este script genera visualizaciones de costos por camión y distribución mensual
 */
document.addEventListener('DOMContentLoaded', function () {
    // Preparar datos para gráficos
    const mantenimientos = JSON.parse(document.getElementById('datos-mantenimiento').textContent);

    // Datos para el gráfico de costos por camión
    const camiones = {};
    mantenimientos.forEach(m => {
        const camionId = m.id_camion;
        const placa = m.numero_placa;
        const key = `${placa} (ID: ${camionId})`;

        if (!camiones[key]) {
            camiones[key] = 0;
        }
        camiones[key] += m.costo;
    });

    // Crear el gráfico de costos por camión
    const ctxCosto = document.getElementById('costoPorCamion').getContext('2d');
    new Chart(ctxCosto, {
        type: 'bar',
        data: {
            labels: Object.keys(camiones),
            datasets: [{
                label: 'Costo Total ($)',
                data: Object.values(camiones),
                backgroundColor: 'rgba(78, 115, 223, 0.8)',
                borderColor: 'rgba(78, 115, 223, 1)',
                borderWidth: 1
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            scales: {
                y: {
                    beginAtZero: true,
                    ticks: {
                        callback: function (value) {
                            return '$' + value.toLocaleString();
                        }
                    }
                }
            }
        }
    });

    // Datos para el gráfico de distribución mensual
    const meses = {};
    mantenimientos.forEach(m => {
        const fecha = new Date(m.fecha_mantenimiento);
        const mes = fecha.toLocaleString('es-ES', { month: 'long', year: 'numeric' });

        if (!meses[mes]) {
            meses[mes] = 0;
        }
        meses[mes]++;
    });

    // Ordenar los meses cronológicamente
    const mesesOrdenados = Object.keys(meses).sort((a, b) => {
        const fechaA = new Date(a.split(' ')[1], ['enero', 'febrero', 'marzo', 'abril', 'mayo', 'junio', 'julio', 'agosto', 'septiembre', 'octubre', 'noviembre', 'diciembre'].indexOf(a.split(' ')[0]), 1);
        const fechaB = new Date(b.split(' ')[1], ['enero', 'febrero', 'marzo', 'abril', 'mayo', 'junio', 'julio', 'agosto', 'septiembre', 'octubre', 'noviembre', 'diciembre'].indexOf(b.split(' ')[0]), 1);
        return fechaA - fechaB;
    });

    // Crear el gráfico de distribución mensual
    const ctxDist = document.getElementById('distribucionMensual').getContext('2d');
    new Chart(ctxDist, {
        type: 'line',
        data: {
            labels: mesesOrdenados,
            datasets: [{
                label: 'Cantidad de Mantenimientos',
                data: mesesOrdenados.map(mes => meses[mes]),
                backgroundColor: 'rgba(28, 200, 138, 0.2)',
                borderColor: 'rgba(28, 200, 138, 1)',
                borderWidth: 2,
                tension: 0.1,
                fill: true
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            scales: {
                y: {
                    beginAtZero: true,
                    ticks: {
                        stepSize: 1
                    }
                }
            }
        }
    });
});