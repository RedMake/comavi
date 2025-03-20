/**
 * maintenance-chart.js
 * Script para inicializar y configurar el gráfico de mantenimiento
 */
function initializeMaintenanceChart(labels, costos) {
    // Verificar si el elemento canvas existe
    var ctx = document.getElementById("maintenanceChart");
    if (!ctx) return;
    
    // Inicializar el gráfico
    var myChart = new Chart(ctx, {
        type: 'bar',
        data: {
            labels: labels,
            datasets: [{
                label: 'Costo de Mantenimiento ($)',
                data: costos,
                backgroundColor: 'rgba(78, 115, 223, 0.2)',
                borderColor: 'rgba(78, 115, 223, 1)',
                borderWidth: 1
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            scales: {
                yAxes: [{
                    ticks: {
                        beginAtZero: true
                    }
                }]
            }
        }
    });
    
    return myChart;
}