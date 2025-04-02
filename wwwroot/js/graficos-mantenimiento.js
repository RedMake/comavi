// graficos-mantenimiento.js
document.addEventListener('DOMContentLoaded', function () {
    try {
        // Obtener los datos de mantenimiento desde el elemento script
        const datosJSONElement = document.getElementById('datos-mantenimiento');

        if (!datosJSONElement) {
            console.error('No se pudo encontrar el elemento "datos-mantenimiento"');
            return;
        }

        const datosJSON = datosJSONElement.textContent;
        const mantenimientos = JSON.parse(datosJSON);

        if (!Array.isArray(mantenimientos) || mantenimientos.length === 0) {
            console.warn('No hay datos de mantenimiento para mostrar');
            return;
        }


        // Preparar datos para el gráfico de costos por camión
        const datosPorCamion = prepararDatosCostoPorCamion(mantenimientos);

            datosPorCamion
        });

        // Verificar si el elemento del gráfico de costos existe
        if (document.getElementById('costoPorCamion')) {
            // Crear gráfico de costos por camión
            crearGraficoCostoPorCamion(datosPorCamion);
        } else {
            console.warn('No se encontró el elemento para el gráfico de costos por camión');
        }

        // Inicializar DataTable para la tabla de mantenimientos
        inicializarTablaMantenimientos();
    } catch (error) {
        console.error('Error al inicializar los gráficos de mantenimiento:', error);
    }
});

// Inicializar tabla de mantenimientos con configuraciones específicas
function inicializarTablaMantenimientos() {
    try {
        // Obtener la referencia a la tabla
        const tablaElement = document.getElementById('dataTableRecentAdminMaintenanceReport');
        if (!tablaElement) {
            console.warn('No se encontró la tabla para inicializar DataTable');
            return;
        }

        // Comprobar si ya existe una instancia de DataTable
        if ($.fn.dataTable.isDataTable('#' + tablaElement.id)) {
            try {
                const dataTableInstance = $(tablaElement).DataTable();

                // No intentar destruir si ya sabemos que hay problemas
                return; // Simplemente usar la instancia existente y salir
            } catch (error) {
                console.warn('Error al acceder a la instancia existente:', error);
            }
        }

        // Solo inicializar si es una nueva tabla
        try {
            $(tablaElement).DataTable({
                language: {
                    url: "//cdn.datatables.net/plug-ins/1.10.25/i18n/Spanish.json"
                },
                responsive: true,
                lengthMenu: [[10, 25, 50, -1], [10, 25, 50, "Todos"]],
                order: [[4, 'desc']], // Ordenar por fecha descendente
                columnDefs: [
                    {
                        targets: [9], // Columna de costo total
                        render: function (data, type, row) {
                            if (type === 'display' && data) {
                                return data;
                            }
                            return data;
                        }
                    }
                ]
            });
        } catch (initError) {
            console.error('Error durante la inicialización de DataTable:', initError);
        }
    } catch (error) {
        console.error('Error general al inicializar la tabla de mantenimientos: ', error);
    }
}

// Función para preparar datos de costo por camión
function prepararDatosCostoPorCamion(mantenimientos) {
    try {
        // Separar por moneda
        const costosPorCamionCRC = {};
        const costosPorCamionUSD = {};

        mantenimientos.forEach(item => {
            try {
                if (!item.marca || !item.modelo || !item.numero_placa) {
                    console.warn('Registro incompleto:', item);
                    return; // Saltar este elemento
                }

                const camionIdentifier = `${item.marca} ${item.modelo} (${item.numero_placa})`;
                const moneda = item.moneda || 'CRC';

                // Determinar el mapa correcto según moneda
                const costMap = moneda === 'USD' ? costosPorCamionUSD : costosPorCamionCRC;

                if (!costMap[camionIdentifier]) {
                    costMap[camionIdentifier] = 0;
                }

                // Asegurarnos que el costo es un número válido
                const costo = typeof item.costo === 'number' ? item.costo :
                    parseFloat(String(item.costo).replace(/[^\d.-]/g, '')) || 0;

                // Agregar el costo al mapa correspondiente
                costMap[camionIdentifier] += costo;
            } catch (itemError) {
                console.warn('Error al procesar registro:', itemError, item);
            }
        });

        // Preparar datos para cada moneda
        const prepararDatosPorMoneda = (costoMap, monedaLabel) => {
            const camiones = Object.keys(costoMap);
            if (camiones.length === 0) return null;

            const costos = camiones.map(camion => costoMap[camion]);

            // Ordenar por costo (de mayor a menor)
            const indices = Array.from({ length: camiones.length }, (_, i) => i);
            indices.sort((a, b) => costos[b] - costos[a]);

            return {
                labels: indices.map(i => camiones[i]),
                values: indices.map(i => costos[i]),
                moneda: monedaLabel
            };
        };

        // Obtener datos ordenados para cada moneda
        const datosCRC = prepararDatosPorMoneda(costosPorCamionCRC, 'CRC');
        const datosUSD = prepararDatosPorMoneda(costosPorCamionUSD, 'USD');

        return {
            CRC: datosCRC,
            USD: datosUSD
        };
    } catch (error) {
        console.error('Error al preparar datos por camión:', error);
        return { CRC: null, USD: null };
    }
}

// Función para crear gráfico de costos por camión
function crearGraficoCostoPorCamion(datos) {
    try {
        const canvasElement = document.getElementById('costoPorCamion');
        if (!canvasElement) {
            console.error('No se pudo encontrar el elemento "costoPorCamion"');
            return;
        }

        const ctx = canvasElement.getContext('2d');
        if (!ctx) {
            console.error('No se pudo obtener el contexto 2D para "costoPorCamion"');
            return;
        }

        // Verificar si hay datos válidos
        if ((!datos.CRC || !datos.CRC.values || datos.CRC.values.length === 0) &&
            (!datos.USD || !datos.USD.values || datos.USD.values.length === 0)) {
            console.warn("No hay datos válidos para crear el gráfico de costos por camión");

            // Mostrar mensaje en el gráfico
            mostrarMensajeEnCanvas(ctx, canvasElement, "No hay datos disponibles para mostrar");
            return;
        }

        // Preparar datasets
        const datasets = [];

        // Dataset para CRC
        if (datos.CRC && datos.CRC.values && datos.CRC.values.length > 0) {
            datasets.push({
                label: 'Costo Total (₡)',
                data: datos.CRC.values,
                backgroundColor: 'rgba(78, 115, 223, 0.8)',
                borderColor: 'rgba(78, 115, 223, 1)',
                borderWidth: 1
            });
        }

        // Dataset para USD
        if (datos.USD && datos.USD.values && datos.USD.values.length > 0) {
            datasets.push({
                label: 'Costo Total ($)',
                data: datos.USD.values,
                backgroundColor: 'rgba(28, 200, 138, 0.8)',
                borderColor: 'rgba(28, 200, 138, 1)',
                borderWidth: 1
            });
        }

        // Determinar qué juego de etiquetas usar
        let labels = [];
        if (datos.CRC && datos.CRC.labels && datos.CRC.labels.length > 0) {
            labels = datos.CRC.labels;
        } else if (datos.USD && datos.USD.labels && datos.USD.labels.length > 0) {
            labels = datos.USD.labels;
        }

        // Crear el gráfico
        new Chart(ctx, {
            type: 'bar',
            data: {
                labels: labels,
                datasets: datasets
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                scales: {
                    y: {
                        beginAtZero: true,
                        ticks: {
                            callback: function (value) {
                                return value.toLocaleString('es-ES');
                            }
                        }
                    },
                    x: {
                        ticks: {
                            maxRotation: 45,
                            minRotation: 45
                        }
                    }
                },
                plugins: {
                    tooltip: {
                        callbacks: {
                            label: function (context) {
                                const symbol = context.dataset.label.includes('$') ? '$' : '₡';
                                return context.dataset.label + ': ' + symbol + context.raw.toLocaleString('es-ES', {
                                    minimumFractionDigits: 2,
                                    maximumFractionDigits: 2
                                });
                            }
                        }
                    }
                }
            }
        });

    } catch (error) {
        console.error('Error al crear gráfico de costos por camión:', error);

        // Intentar mostrar mensaje de error en el canvas
        try {
            const canvasElement = document.getElementById('costoPorCamion');
            if (canvasElement) {
                const ctx = canvasElement.getContext('2d');
                if (ctx) {
                    mostrarMensajeEnCanvas(ctx, canvasElement, "Error al crear el gráfico");
                }
            }
        } catch (e) {
            console.error("Error adicional al mostrar mensaje de error:", e);
        }
    }
}

// Función auxiliar para mostrar mensaje en canvas cuando no hay datos
function mostrarMensajeEnCanvas(ctx, canvas, mensaje) {
    try {
        // Limpiar el canvas
        ctx.clearRect(0, 0, canvas.width, canvas.height);

        // Configurar estilo
        ctx.fillStyle = "#f8f9fc"; // Fondo claro
        ctx.fillRect(0, 0, canvas.width, canvas.height);

        ctx.font = "16px Arial";
        ctx.fillStyle = "#5a5c69"; // Color de texto gris
        ctx.textAlign = "center";
        ctx.textBaseline = "middle";

        // Dibujar mensaje
        ctx.fillText(mensaje, canvas.width / 2, canvas.height / 2);

        // Dibujar icono de información (círculo con i)
        ctx.beginPath();
        ctx.arc(canvas.width / 2, canvas.height / 2 - 30, 15, 0, 2 * Math.PI);
        ctx.fillStyle = "#4e73df"; // Color primary
        ctx.fill();

        ctx.font = "bold 14px Arial";
        ctx.fillStyle = "white";
        ctx.fillText("i", canvas.width / 2, canvas.height / 2 - 30);
    } catch (error) {
        console.error("Error al mostrar mensaje en canvas:", error);
    }
}

// Función para formatear números con formato monetario
function formatearMoneda(valor, moneda) {
    try {
        const simbolo = moneda === 'USD' ? '$' : '₡';
        const valorNumerico = parseFloat(valor) || 0;
        return simbolo + valorNumerico.toLocaleString('es-ES', {
            minimumFractionDigits: 2,
            maximumFractionDigits: 2
        });
    } catch (error) {
        console.error('Error al formatear moneda:', error);
        return (moneda === 'USD' ? '$' : '₡') + '0.00';
    }
}

// Función para parsear un valor en formato monetario
function parsearMoneda(texto) {
    try {
        if (!texto) return 0;
        return parseFloat(texto.replace(/[^\d.-]/g, '')) || 0;
    } catch (error) {
        console.error('Error al parsear moneda:', error);
        return 0;
    }
}