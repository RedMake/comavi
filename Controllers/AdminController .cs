using COMAVI_SA.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using Dapper;

namespace COMAVI_SA.Controllers
{
    [Authorize(Policy = "RequireAdminRole")]
    public class AdminController : Controller
    {

        private readonly string _connectionString;
        private readonly ILogger<AdminController> _logger;

        public AdminController(IConfiguration configuration, ILogger<AdminController> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
            _logger = logger;
        }

        private IDbConnection CreateConnection()
        {
            var connection = new SqlConnection(_connectionString);
            connection.Open();
            return connection;
        }

        [HttpPost]
        public IActionResult RegistrarCamion(Camiones camion)
        {
            try
            {
                using (var connection = CreateConnection())
                {
                    var parameters = new
                    {
                        marca = camion.marca,
                        modelo = camion.modelo,
                        anio = camion.anio,
                        numero_placa = camion.numero_placa,
                        estado = camion.estado,
                        chofer_asignado = camion.chofer_asignado ?? (object)DBNull.Value
                    };

                    connection.Execute("sp_RegistrarCamion", parameters, commandType: CommandType.StoredProcedure);
                }
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar camión");
                TempData["Error"] = "Error al registrar el camión";
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        public IActionResult ActualizarCamion(int id, Camiones camion)
        {
            try
            {
                using (var connection = CreateConnection())
                {
                    var parameters = new
                    {
                        id_camion = id,
                        marca = camion.marca,
                        modelo = camion.modelo,
                        anio = camion.anio,
                        estado = camion.estado
                    };

                    connection.Execute("sp_ActualizarCamion", parameters, commandType: CommandType.StoredProcedure);
                }
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar camión");
                TempData["Error"] = "Error al actualizar el camión";
                return RedirectToAction("Index");
            }
        }

        [HttpGet]
        public IActionResult HistorialMantenimiento(int idCamion)
        {
            try
            {
                using (var connection = CreateConnection())
                {
                    var historial = connection.Query<Mantenimiento_Camiones>(
                        "sp_ObtenerHistorialMantenimiento",
                        new { id_camion = idCamion },
                        commandType: CommandType.StoredProcedure
                    ).ToList();

                    return View(historial);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener historial de mantenimiento");
                TempData["Error"] = "Error al obtener el historial de mantenimiento";
                return RedirectToAction("Index");
            }
        }

        [HttpGet]
        public IActionResult NotificacionesMantenimiento()
        {
            try
            {
                using (var connection = CreateConnection())
                {
                    var diasAntelacion = 30;
                    var notificaciones = connection.Query<Mantenimiento_Camiones>(
                        "sp_ObtenerNotificacionesMantenimiento",
                        new { dias_antelacion = diasAntelacion },
                        commandType: CommandType.StoredProcedure
                    ).ToList();

                    return View(notificaciones);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener notificaciones de mantenimiento");
                TempData["Error"] = "Error al obtener las notificaciones de mantenimiento";
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        public IActionResult DesactivarCamion(int id)
        {
            try
            {
                using (var connection = CreateConnection())
                {
                    connection.Execute(
                        "sp_DesactivarCamion",
                        new { id_camion = id },
                        commandType: CommandType.StoredProcedure
                    );
                }
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al desactivar camión");
                TempData["Error"] = "Error al desactivar el camión";
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        public IActionResult AsignarChofer(int idCamion, int idChofer)
        {
            try
            {
                using (var connection = CreateConnection())
                {
                    var parameters = new
                    {
                        id_camion = idCamion,
                        id_chofer = idChofer
                    };

                    connection.Execute("sp_AsignarChofer", parameters, commandType: CommandType.StoredProcedure);
                }
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al asignar chofer");
                TempData["Error"] = "Error al asignar el chofer";
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        public IActionResult EliminarCamion(int id)
        {
            try
            {
                using (var connection = CreateConnection())
                {
                    var result = connection.Execute(
                        "sp_EliminarCamion",
                        new { id_camion = id },
                        commandType: CommandType.StoredProcedure
                    );

                    if (result == 0)
                        TempData["Error"] = "No se puede eliminar por dependencias";
                }
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar camión");
                TempData["Error"] = "Error al eliminar el camión";
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        public IActionResult RegistrarChofer(Choferes chofer)
        {
            try
            {
                using (var connection = CreateConnection())
                {
                    var parameters = new
                    {
                        nombreCompleto = chofer.nombreCompleto,
                        edad = chofer.edad,
                        numero_cedula = chofer.numero_cedula,
                        licencia = chofer.licencia,
                        fecha_venc_licencia = chofer.fecha_venc_licencia,
                        estado = chofer.estado,
                        genero = chofer.genero
                    };

                    connection.Execute("sp_RegistrarChofer", parameters, commandType: CommandType.StoredProcedure);
                }
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar chofer");
                TempData["Error"] = "Error al registrar el chofer";
                return RedirectToAction("Index");
            }
        }

        [HttpGet]
        public IActionResult ObtenerDocumentosChofer(int idChofer)
        {
            try
            {
                using (var connection = CreateConnection())
                {
                    var documentos = connection.Query<Documentos>(
                        "sp_ObtenerDocumentosChofer",
                        new { id_chofer = idChofer },
                        commandType: CommandType.StoredProcedure
                    ).ToList();

                    ViewBag.IdChofer = idChofer;
                    return View(documentos);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener documentos del chofer");
                TempData["Error"] = "Error al obtener los documentos del chofer";
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        public IActionResult ActualizarDocumentos(Documentos documento)
        {
            try
            {
                using (var connection = CreateConnection())
                {
                    var parameters = new
                    {
                        id_documento = documento.id_documento > 0 ? documento.id_documento : (object)DBNull.Value,
                        id_chofer = documento.id_chofer,
                        tipo_documento = documento.tipo_documento,
                        fecha_emision = documento.fecha_emision,
                        fecha_vencimiento = documento.fecha_vencimiento
                    };

                    connection.Execute("sp_ActualizarDocumento", parameters, commandType: CommandType.StoredProcedure);
                }
                return RedirectToAction("ActualizarDocumentos", new { idChofer = documento.id_chofer });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar documentos");
                TempData["Error"] = "Error al actualizar los documentos";
                return RedirectToAction("Index");
            }
        }

        [HttpGet]
        public IActionResult MonitorearVencimientos(int diasPrevios = 30)
        {
            try
            {
                using (var connection = CreateConnection())
                {
                    var documentos = connection.Query<Documentos>(
                        "sp_MonitorearVencimientos",
                        new { dias_previos = diasPrevios },
                        commandType: CommandType.StoredProcedure
                    ).ToList();

                    return View(documentos);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al monitorear vencimientos");
                TempData["Error"] = "Error al monitorear los vencimientos";
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        public IActionResult ActualizarDatosChofer(int id, Choferes chofer)
        {
            try
            {
                using (var connection = CreateConnection())
                {
                    var parameters = new
                    {
                        id_chofer = id,
                        nombreCompleto = chofer.nombreCompleto,
                        edad = chofer.edad,
                        numero_cedula = chofer.numero_cedula,
                        licencia = chofer.licencia,
                        fecha_venc_licencia = chofer.fecha_venc_licencia,
                        genero = chofer.genero
                    };

                    connection.Execute("sp_ActualizarDatosChofer", parameters, commandType: CommandType.StoredProcedure);
                }
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar datos del chofer");
                TempData["Error"] = "Error al actualizar los datos del chofer";
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        public IActionResult DesactivarChofer(int id)
        {
            try
            {
                using (var connection = CreateConnection())
                {
                    connection.Execute(
                        "sp_DesactivarChofer",
                        new { id_chofer = id },
                        commandType: CommandType.StoredProcedure
                    );
                }
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al desactivar chofer");
                TempData["Error"] = "Error al desactivar el chofer";
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        public IActionResult AsignarDocumento(Documentos documento)
        {
            try
            {
                using (var connection = CreateConnection())
                {
                    var parameters = new
                    {
                        id_chofer = documento.id_chofer,
                        tipo_documento = documento.tipo_documento,
                        fecha_emision = documento.fecha_emision,
                        fecha_vencimiento = documento.fecha_vencimiento
                    };

                    connection.Execute("sp_RegistrarDocumento", parameters, commandType: CommandType.StoredProcedure);
                }
                return RedirectToAction("AsignarDocumentos", new { idChofer = documento.id_chofer });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al asignar documento");
                TempData["Error"] = "Error al asignar el documento";
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        public IActionResult RegistrarMantenimiento(Mantenimiento_Camiones mantenimiento)
        {
            try
            {
                using (var connection = CreateConnection())
                {
                    var parameters = new
                    {
                        id_camion = mantenimiento.id_camion,
                        descripcion = mantenimiento.descripcion,
                        fecha_mantenimiento = mantenimiento.fecha_mantenimiento,
                        costo = mantenimiento.costo
                    };

                    connection.Execute("sp_RegistrarMantenimiento", parameters, commandType: CommandType.StoredProcedure);
                }
                return RedirectToAction("HistorialMantenimiento", new { idCamion = mantenimiento.id_camion });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar mantenimiento");
                TempData["Error"] = "Error al registrar el mantenimiento";
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        public IActionResult EliminarChofer(int id)
        {
            try
            {
                using (var connection = CreateConnection())
                {
                    var result = connection.Execute(
                        "sp_EliminarChofer",
                        new { id_chofer = id },
                        commandType: CommandType.StoredProcedure
                    );

                    if (result == 0)
                        TempData["Error"] = "No se puede eliminar: Chofer tiene registros asociados";
                }
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar chofer");
                TempData["Error"] = "Error al eliminar el chofer";
                return RedirectToAction("Index");
            }
        }

        [HttpGet]
        public IActionResult ObtenerChoferesPaginados(int pagina, int cantidadPorPagina)
        {
            try
            {
                int inicio = ((pagina - 1) * cantidadPorPagina) + 1;

                using (var connection = CreateConnection())
                {
                    var parameters = new DynamicParameters();
                    parameters.Add("@inicio", inicio);
                    parameters.Add("@cantidad", cantidadPorPagina);
                    parameters.Add("@total", dbType: DbType.Int32, direction: ParameterDirection.Output);

                    var choferes = connection.Query<ChoferViewModel>(
                        "sp_ObtenerChoferesRango",
                        parameters,
                        commandType: CommandType.StoredProcedure
                    ).ToList();

                    int totalRegistros = parameters.Get<int>("@total");

                    var paginacion = new
                    {
                        PaginaActual = pagina,
                        TotalPaginas = (int)Math.Ceiling((double)totalRegistros / cantidadPorPagina),
                        TotalRegistros = totalRegistros
                    };

                    var resultado = new
                    {
                        choferes = choferes,
                        paginacion = paginacion
                    };

                    return Ok(resultado);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener choferes paginados");
                TempData["Error"] = "Error interno del servidor al procesar la solicitud";
                return RedirectToAction("Index");

            }
        }
    }
}