/**
 * maintenance-chart.js
 * Script para inicializar y configurar el gráfico de mantenimiento
 */
function initializeMaintenanceChart(labels, costos, simbolos) {
    // Verificar si el elemento canvas existe
    var ctx = document.getElementById("maintenanceChart");
    if (!ctx) return;

    // Inicializar el gráfico
    var myChart = new Chart(ctx, {
        type: 'bar',
        data: {
            labels: labels,
            datasets: [{
                label: 'Costo de Mantenimiento',
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
                        beginAtZero: true,
                        callback: function (value, index, values) {
                            // Usar el primer símbolo si no hay suficientes símbolos
                            var simbolo = simbolos && simbolos.length > 0 ?
                                (simbolos[0] || '$') : '$';
                            return simbolo + value;
                        }
                    }
                }]
            },
            tooltips: {
                callbacks: {
                    label: function (tooltipItem, data) {
                        var datasetLabel = data.datasets[tooltipItem.datasetIndex].label || '';
                        var value = tooltipItem.yLabel;
                        var index = tooltipItem.index;
                        // Usar el símbolo correspondiente al índice o el primero si no existe
                        var simbolo = simbolos && simbolos.length > index ?
                            (simbolos[index] || '$') : '$';
                        return datasetLabel + ': ' + simbolo + value;
                    }
                }
            }
        }
    });

    return myChart;
}