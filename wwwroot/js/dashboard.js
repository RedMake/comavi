/**
 * Script para inicializar y configurar los gráficos del dashboard
 */
document.addEventListener('DOMContentLoaded', function() {
    // Inicializar gráficos si existen los elementos en la página
    if (document.getElementById("myAreaChart") && document.getElementById("myPieChart")) {
        inicializarGraficos();
    }
});

/**
 * Inicializa los gráficos del dashboard
 */
function inicializarGraficos() {
    // Configuración del gráfico de área - Mantenimientos por Mes
    var ctx = document.getElementById("myAreaChart");
    var myLineChart = new Chart(ctx, {
        type: 'line',
        data: {
            labels: mantenimientosPorMes.map(item => item.mes),
            datasets: [{
                label: "Mantenimientos",
                lineTension: 0.3,
                backgroundColor: "rgba(78, 115, 223, 0.05)",
                borderColor: "rgba(78, 115, 223, 1)",
                pointRadius: 3,
                pointBackgroundColor: "rgba(78, 115, 223, 1)",
                pointBorderColor: "rgba(78, 115, 223, 1)",
                pointHoverRadius: 3,
                pointHoverBackgroundColor: "rgba(78, 115, 223, 1)",
                pointHoverBorderColor: "rgba(78, 115, 223, 1)",
                pointHitRadius: 10,
                pointBorderWidth: 2,
                data: mantenimientosPorMes.map(item => item.cantidad),
            }],
        },
        options: {
            maintainAspectRatio: false,
            layout: {
                padding: {
                    left: 10,
                    right: 25,
                    top: 25,
                    bottom: 0
                }
            },
            scales: {
                xAxes: [{
                    time: {
                        unit: 'date'
                    },
                    gridLines: {
                        display: false,
                        drawBorder: false
                    },
                    ticks: {
                        maxTicksLimit: 7
                    }
                }],
                yAxes: [{
                    ticks: {
                        maxTicksLimit: 5,
                        padding: 10,
                        beginAtZero: true
                    },
                    gridLines: {
                        color: "rgb(234, 236, 244)",
                        zeroLineColor: "rgb(234, 236, 244)",
                        drawBorder: false,
                        borderDash: [2],
                        zeroLineBorderDash: [2]
                    }
                }],
            },
            legend: {
                display: false
            },
            tooltips: {
                backgroundColor: "rgb(255,255,255)",
                bodyFontColor: "#858796",
                titleMarginBottom: 10,
                titleFontColor: '#6e707e',
                titleFontSize: 14,
                borderColor: '#dddfeb',
                borderWidth: 1,
                xPadding: 15,
                yPadding: 15,
                displayColors: false,
                caretPadding: 10,
            }
        }
    });

    // Configuración del gráfico circular - Estados de Camiones
    var ctx2 = document.getElementById("myPieChart");
    var myPieChart = new Chart(ctx2, {
        type: 'doughnut',
        data: {
            labels: camionesEstados.map(item => item.estado),
            datasets: [{
                data: camionesEstados.map(item => item.cantidad),
                backgroundColor: ['#4e73df', '#f6c23e', '#e74a3b'],
                hoverBackgroundColor: ['#2e59d9', '#dda20a', '#be2617'],
                hoverBorderColor: "rgba(234, 236, 244, 1)",
            }],
        },
        options: {
            maintainAspectRatio: false,
            tooltips: {
                backgroundColor: "rgb(255,255,255)",
                bodyFontColor: "#858796",
                borderColor: '#dddfeb',
                borderWidth: 1,
                xPadding: 15,
                yPadding: 15,
                displayColors: false,
                caretPadding: 10,
            },
            legend: {
                display: false
            },
            cutoutPercentage: 80,
        },
    });
}