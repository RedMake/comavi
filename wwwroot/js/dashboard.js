/**
 * Script para inicializar y configurar los gráficos del dashboard
 */
document.addEventListener('DOMContentLoaded', function () {
    // Asegurarse de que todas las variables de datos existan para evitar errores
    if (typeof mantenimientosPorMes === 'undefined') {
        console.warn("La variable mantenimientosPorMes no está definida, inicializando como array vacío");
        window.mantenimientosPorMes = [];
    }

    if (typeof camionesEstados === 'undefined') {
        console.warn("La variable camionesEstados no está definida, inicializando como array vacío");
        window.camionesEstados = [];
    }

    if (typeof documentosEstados === 'undefined') {
        console.warn("La variable documentosEstados no está definida, inicializando como array vacío");
        window.documentosEstados = [];
    }

    // Función auxiliar para convertir hex a rgb
    window.hexToRgb = function (hex) {
        if (!hex) return { r: 0, g: 0, b: 0 };

        // Expandir forma corta (ejemplo: #03F) a forma completa (ejemplo: #0033FF)
        const shorthandRegex = /^#?([a-f\d])([a-f\d])([a-f\d])$/i;
        hex = hex.replace(shorthandRegex, function (m, r, g, b) {
            return r + r + g + g + b + b;
        });

        const result = /^#?([a-f\d]{2})([a-f\d]{2})([a-f\d]{2})$/i.exec(hex);
        return result ? {
            r: parseInt(result[1], 16),
            g: parseInt(result[2], 16),
            b: parseInt(result[3], 16)
        } : { r: 0, g: 0, b: 0 };
    };

    // Inicializar gráficos si existen los elementos en la página
    if (document.getElementById("myAreaChart")) {
        try {
            inicializarGraficoArea();
        } catch (error) {
            console.error("Error al inicializar gráfico de área:", error);
        }
    }

    if (document.getElementById("myPieChart")) {
        try {
            inicializarGraficoCamiones();
        } catch (error) {
            console.error("Error al inicializar gráfico de camiones:", error);
        }
    }

    if (document.getElementById("documentosChart")) {
        try {
            inicializarGraficoDocumentos();
        } catch (error) {
            console.error("Error al inicializar gráfico de documentos:", error);
        }
    }
});

/**
 * Inicializa el gráfico de área - Mantenimientos por Mes
 */
function inicializarGraficoArea() {
    // Verificar que los datos existan y no estén vacíos
    if (!mantenimientosPorMes || mantenimientosPorMes.length === 0) {
        console.error("No hay datos para el gráfico de mantenimientos por mes");
        return;
    }

    console.log("Datos para gráfico de mantenimientos:", mantenimientosPorMes);

    // Obtener las etiquetas (nombres de los meses)
    const labels = mantenimientosPorMes.map(item => item.label);

    // Obtener los valores
    const datos = mantenimientosPorMes.map(item => {
        const valor = typeof item.value !== 'undefined' ? item.value :
            (typeof item.valor !== 'undefined' ? item.valor : 0);
        return isNaN(valor) ? 0 : valor;
    });

    // Obtener los costos si están disponibles
    const costos = mantenimientosPorMes.map(item => {
        if (item.extra && item.extra.costo_total) {
            return item.extra.costo_total;
        }
        return 0;
    });

    var ctx = document.getElementById("myAreaChart");
    var myLineChart = new Chart(ctx, {
        type: 'line',
        data: {
            labels: labels,
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
                data: datos,
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
                        beginAtZero: true,
                        callback: function (value) {
                            return Math.round(value); // Solo mostrar valores enteros
                        }
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
                callbacks: {
                    label: function (tooltipItem, chart) {
                        const index = tooltipItem.index;
                        const cantidad = datos[index];
                        const costo = costos[index];

                        let label = cantidad + " mantenimientos";
                        if (costo > 0) {
                            label += " - Costo: $" + costo.toLocaleString();
                        }
                        return label;
                    }
                }
            }
        }
    });
}

/**
 * Inicializa el gráfico circular - Estados de Camiones
 */
function inicializarGraficoCamiones() {
    // Verificar que los datos existan y no estén vacíos
    if (!camionesEstados || camionesEstados.length === 0) {
        console.error("No hay datos para el gráfico de estados de camiones");
        return;
    }

    console.log("Datos para gráfico de camiones:", camionesEstados);

    var ctx2 = document.getElementById("myPieChart");
    var myPieChart = new Chart(ctx2, {
        type: 'doughnut',
        data: {
            labels: camionesEstados.map(item => {
                const estado = item.nombre || item.label || "";
                return estado.charAt(0).toUpperCase() + estado.slice(1).toLowerCase();
            }),
            datasets: [{
                data: camionesEstados.map(item => {
                    return typeof item.value !== 'undefined' ? item.value :
                        (typeof item.valor !== 'undefined' ? item.valor : 0);
                }),
                backgroundColor: camionesEstados.map(item => {
                    // Si hay un color definido, usarlo
                    if (item.color) return item.color;

                    const estado = (item.nombre || item.label || "").toLowerCase();
                    if (estado === "activo") return '#4e73df';
                    if (estado === "en mantenimiento" || estado === "mantenimiento") return '#f6c23e';
                    if (estado === "inactivo") return '#e74a3b';
                    return '#858796';
                }),
                hoverBackgroundColor: camionesEstados.map(item => {
                    // Si hay un color definido, oscurecerlo
                    if (item.color) {
                        const rgb = hexToRgb(item.color);
                        return `rgb(${Math.max(0, rgb.r - 30)}, ${Math.max(0, rgb.g - 30)}, ${Math.max(0, rgb.b - 30)})`;
                    }

                    const estado = (item.nombre || item.label || "").toLowerCase();
                    if (estado === "activo") return '#2e59d9';
                    if (estado === "en mantenimiento" || estado === "mantenimiento") return '#dda20a';
                    if (estado === "inactivo") return '#be2617';
                    return '#666666';
                }),
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

/**
 * Inicializa el gráfico circular - Estados de Documentos
 */
function inicializarGraficoDocumentos() {
    // Verificar que los datos existan y no estén vacíos
    if (!documentosEstados || documentosEstados.length === 0) {
        console.error("No hay datos para el gráfico de estados de documentos");
        return;
    }

    console.log("Datos para gráfico de documentos:", documentosEstados);

    var ctx3 = document.getElementById("documentosChart");
    var documentosPieChart = new Chart(ctx3, {
        type: 'doughnut',
        data: {
            labels: documentosEstados.map(item => item.label || item.nombre),
            datasets: [{
                data: documentosEstados.map(item => {
                    return typeof item.value !== 'undefined' ? item.value :
                        (typeof item.valor !== 'undefined' ? item.valor : 0);
                }),
                backgroundColor: documentosEstados.map(item => {
                    // Primero intentar usar el color definido en el objeto
                    if (item.color) return item.color;

                    const estado = (item.nombre || item.label || "").toLowerCase();
                    if (estado === "verificado" || estado === "vigente") return '#1cc88a'; // Verde
                    if (estado === "pendiente") return '#f6c23e'; // Amarillo
                    if (estado === "rechazado" || estado === "vencido") return '#e74a3b'; // Rojo
                    return '#858796'; // Gris por defecto
                }),
                hoverBackgroundColor: documentosEstados.map(item => {
                    // Versión más oscura del color definido o del color asignado
                    if (item.color) {
                        // Oscurecer el color existente
                        const rgb = hexToRgb(item.color);
                        return `rgb(${Math.max(0, rgb.r - 30)}, ${Math.max(0, rgb.g - 30)}, ${Math.max(0, rgb.b - 30)})`;
                    }

                    const estado = (item.nombre || item.label || "").toLowerCase();
                    if (estado === "verificado" || estado === "vigente") return '#17a673'; // Verde oscuro
                    if (estado === "pendiente") return '#dda20a'; // Amarillo oscuro
                    if (estado === "rechazado" || estado === "vencido") return '#be2617'; // Rojo oscuro
                    return '#666666'; // Gris oscuro por defecto
                }),
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
                callbacks: {
                    label: function (tooltipItem, chart) {
                        const index = tooltipItem.index;
                        const cantidad = documentosEstados[index].value || documentosEstados[index].valor;
                        const label = documentosEstados[index].label || documentosEstados[index].nombre;
                        return label + ": " + cantidad + " documentos";
                    }
                }
            },
            legend: {
                display: false
            },
            cutoutPercentage: 80,
        },
    });
}