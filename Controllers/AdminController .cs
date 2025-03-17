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

        // Página principal de administración
        [HttpGet]
        public IActionResult Index()
        {
            try
            {
                using var connection = CreateConnection();
                var camiones = connection.Query<Camiones>("SELECT * FROM Camiones").ToList();
                var choferes = connection.Query<Choferes>("SELECT * FROM Choferes").ToList();

                var viewModel = new AdminDashboardViewModel
                {
                    TotalCamiones = camiones.Count,
                    CamionesActivos = camiones.Count(c => c.estado == "activo"),
                    TotalChoferes = choferes.Count,
                    ChoferesActivos = choferes.Count(c => c.estado == "activo"),
                    Camiones = camiones.Take(5).ToList(),
                    Choferes = choferes.Take(5).ToList()
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar el dashboard de administración");
                TempData["Error"] = "Error al cargar los datos del dashboard";
                return View(new AdminDashboardViewModel());
            }
        }

        [HttpPost]
        public IActionResult RegistrarCamion(Camiones camion)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return View(camion);
                }

                using (var connection = CreateConnection())
                {
                    var parameters = new
                    {
                        marca = camion.marca,
                        modelo = camion.modelo,
                        anio = camion.anio,
                        numero_placa = camion.numero_placa,
                        estado = camion.estado ?? "activo",
                        chofer_asignado = camion.chofer_asignado ?? (object)DBNull.Value
                    };

                    connection.Execute("sp_RegistrarCamion", parameters, commandType: CommandType.StoredProcedure);
                }

                TempData["Success"] = "Camión registrado exitosamente";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar camión");
                TempData["Error"] = "Error al registrar el camión";
                return View(camion);
            }
        }

        [HttpGet]
        public IActionResult RegistrarCamion()
        {
            return View(new Camiones { estado = "activo" });
        }

        [HttpPost]
        public IActionResult ActualizarCamion(int id, Camiones camion)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return View(camion);
                }

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

                TempData["Success"] = "Camión actualizado exitosamente";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar camión");
                TempData["Error"] = "Error al actualizar el camión";
                return View(camion);
            }
        }

        [HttpGet]
        public IActionResult ActualizarCamion(int id)
        {
            try
            {
                using (var connection = CreateConnection())
                {
                    var camion = connection.QueryFirstOrDefault<Camiones>(
                        "SELECT * FROM Camiones WHERE id_camion = @id_camion",
                        new { id_camion = id }
                    );

                    if (camion == null)
                    {
                        TempData["Error"] = "Camión no encontrado";
                        return RedirectToAction("Index");
                    }

                    return View(camion);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener datos del camión");
                TempData["Error"] = "Error al obtener datos del camión";
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

                    var camion = connection.QueryFirstOrDefault<Camiones>(
                        "SELECT * FROM Camiones WHERE id_camion = @id_camion",
                        new { id_camion = idCamion }
                    );

                    if (camion == null)
                    {
                        TempData["Error"] = "Camión no encontrado";
                        return RedirectToAction("Index");
                    }

                    ViewBag.Camion = camion;
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

                TempData["Success"] = "Camión desactivado exitosamente";
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

                TempData["Success"] = "Chofer asignado exitosamente";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al asignar chofer");
                TempData["Error"] = "Error al asignar el chofer";
                return RedirectToAction("Index");
            }
        }

        [HttpGet]
        public IActionResult AsignarChofer()
        {
            try
            {
                using (var connection = CreateConnection())
                {
                    var choferes = connection.Query<Choferes>(
                        "SELECT * FROM Choferes WHERE estado = 'activo'").ToList();
                    var camiones = connection.Query<Camiones>(
                        "SELECT * FROM Camiones WHERE estado = 'activo'").ToList();

                    ViewBag.Choferes = choferes;
                    ViewBag.Camiones = camiones;

                    return View();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar datos para asignar chofer");
                TempData["Error"] = "Error al cargar los datos necesarios";
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
                    {
                        TempData["Error"] = "No se puede eliminar por dependencias";
                        return RedirectToAction("Index");
                    }
                }

                TempData["Success"] = "Camión eliminado exitosamente";
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
                if (!ModelState.IsValid)
                {
                    return View(chofer);
                }

                using (var connection = CreateConnection())
                {
                    var parameters = new
                    {
                        nombreCompleto = chofer.nombreCompleto,
                        edad = chofer.edad,
                        numero_cedula = chofer.numero_cedula,
                        licencia = chofer.licencia,
                        fecha_venc_licencia = chofer.fecha_venc_licencia,
                        estado = chofer.estado ?? "activo",
                        genero = chofer.genero
                    };

                    connection.Execute("sp_RegistrarChofer", parameters, commandType: CommandType.StoredProcedure);
                }

                TempData["Success"] = "Chofer registrado exitosamente";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar chofer");
                TempData["Error"] = "Error al registrar el chofer";
                return View(chofer);
            }
        }

        [HttpGet]
        public IActionResult RegistrarChofer()
        {
            return View(new Choferes { estado = "activo" });
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

                    var chofer = connection.QueryFirstOrDefault<Choferes>(
                        "SELECT * FROM Choferes WHERE id_chofer = @id_chofer",
                        new { id_chofer = idChofer }
                    );

                    if (chofer == null)
                    {
                        TempData["Error"] = "Chofer no encontrado";
                        return RedirectToAction("Index");
                    }

                    ViewBag.IdChofer = idChofer;
                    ViewBag.Chofer = chofer;
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
                if (!ModelState.IsValid)
                {
                    return RedirectToAction("ObtenerDocumentosChofer", new { idChofer = documento.id_chofer });
                }

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

                TempData["Success"] = "Documento actualizado exitosamente";
                return RedirectToAction("ObtenerDocumentosChofer", new { idChofer = documento.id_chofer });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar documentos");
                TempData["Error"] = "Error al actualizar los documentos";
                return RedirectToAction("ObtenerDocumentosChofer", new { idChofer = documento.id_chofer });
            }
        }

        [HttpGet]
        public IActionResult ActualizarDocumentos(int idChofer)
        {
            try
            {
                using (var connection = CreateConnection())
                {
                    var chofer = connection.QueryFirstOrDefault<Choferes>(
                        "SELECT * FROM Choferes WHERE id_chofer = @id_chofer",
                        new { id_chofer = idChofer }
                    );

                    if (chofer == null)
                    {
                        TempData["Error"] = "Chofer no encontrado";
                        return RedirectToAction("Index");
                    }

                    ViewBag.Chofer = chofer;
                    return View();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar datos para actualizar documentos");
                TempData["Error"] = "Error al cargar los datos necesarios";
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

                    ViewBag.DiasPrevios = diasPrevios;
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
                if (!ModelState.IsValid)
                {
                    return View(chofer);
                }

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

                TempData["Success"] = "Datos del chofer actualizados exitosamente";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar datos del chofer");
                TempData["Error"] = "Error al actualizar los datos del chofer";
                return View(chofer);
            }
        }

        [HttpGet]
        public IActionResult ActualizarDatosChofer(int id)
        {
            try
            {
                using (var connection = CreateConnection())
                {
                    var chofer = connection.QueryFirstOrDefault<Choferes>(
                        "SELECT * FROM Choferes WHERE id_chofer = @id_chofer",
                        new { id_chofer = id }
                    );

                    if (chofer == null)
                    {
                        TempData["Error"] = "Chofer no encontrado";
                        return RedirectToAction("Index");
                    }

                    return View(chofer);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener datos del chofer");
                TempData["Error"] = "Error al obtener datos del chofer";
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

                TempData["Success"] = "Chofer desactivado exitosamente";
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
                if (!ModelState.IsValid)
                {
                    return RedirectToAction("AsignarDocumentos", new { idChofer = documento.id_chofer });
                }

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

                TempData["Success"] = "Documento asignado exitosamente";
                return RedirectToAction("AsignarDocumentos", new { idChofer = documento.id_chofer });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al asignar documento");
                TempData["Error"] = "Error al asignar el documento";
                return RedirectToAction("AsignarDocumentos", new { idChofer = documento.id_chofer });
            }
        }

        [HttpGet]
        public IActionResult AsignarDocumentos(int idChofer)
        {
            try
            {
                using (var connection = CreateConnection())
                {
                    var chofer = connection.QueryFirstOrDefault<Choferes>(
                        "SELECT * FROM Choferes WHERE id_chofer = @id_chofer",
                        new { id_chofer = idChofer }
                    );

                    if (chofer == null)
                    {
                        var choferes = connection.Query<Choferes>("SELECT * FROM Choferes WHERE estado = 'activo'").ToList();
                        ViewBag.Choferes = choferes;
                        return View();
                    }

                    ViewBag.IdChofer = idChofer;
                    ViewBag.Chofer = chofer;
                    return View();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar datos para asignar documentos");
                TempData["Error"] = "Error al cargar los datos necesarios";
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        public IActionResult RegistrarMantenimiento(Mantenimiento_Camiones mantenimiento)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return RedirectToAction("HistorialMantenimiento", new { idCamion = mantenimiento.id_camion });
                }

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

                TempData["Success"] = "Mantenimiento registrado exitosamente";
                return RedirectToAction("HistorialMantenimiento", new { idCamion = mantenimiento.id_camion });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar mantenimiento");
                TempData["Error"] = "Error al registrar el mantenimiento";
                return RedirectToAction("HistorialMantenimiento", new { idCamion = mantenimiento.id_camion });
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
                    {
                        TempData["Error"] = "No se puede eliminar: Chofer tiene registros asociados";
                        return RedirectToAction("Index");
                    }
                }

                TempData["Success"] = "Chofer eliminado exitosamente";
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
        public IActionResult ObtenerChoferesPaginados(int pagina = 1, int cantidadPorPagina = 10)
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

                    var paginacion = new PaginacionViewModel
                    {
                        pagina_actual = pagina,
                        total_paginas = (int)Math.Ceiling((double)totalRegistros / cantidadPorPagina),
                        total_registros = totalRegistros,
                        registro_inicio = inicio,
                        registro_fin = Math.Min(inicio + cantidadPorPagina - 1, totalRegistros)
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
                return StatusCode(500, new { error = "Error interno del servidor al procesar la solicitud" });
            }
        }
    }

    // ViewModel para el dashboard de administración
    public class AdminDashboardViewModel
    {
        public int TotalCamiones { get; set; }
        public int CamionesActivos { get; set; }
        public int TotalChoferes { get; set; }
        public int ChoferesActivos { get; set; }
        public List<Camiones> Camiones { get; set; } = new List<Camiones>();
        public List<Choferes> Choferes { get; set; } = new List<Choferes>();
    }
}