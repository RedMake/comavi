using COMAVI_SA.Models;
using COMAVI_SA.Services;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using NPOI.SS.Formula.Functions;
using System.Data;

namespace COMAVI_SA.Controllers
{
    [Authorize(Policy = "RequireAdminRole")]
    public class AdminController : Controller
    {
        private readonly string _connectionString;
        private readonly ILogger<AdminController> _logger;
        private readonly IEmailService _emailService;
        private readonly IPdfService _pdfService;
        private readonly IUserService _userService;
        private readonly INotificationService _notificationService;
        private readonly IReportService _reportService;

        public AdminController(
            IConfiguration configuration,
            ILogger<AdminController> logger,
            IEmailService emailService,
            IPdfService pdfService,
            IUserService userService,
            INotificationService notificationService,
            IReportService reportService)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
            _logger = logger;
            _emailService = emailService;
            _pdfService = pdfService;
            _userService = userService;
            _notificationService = notificationService;
            _reportService = reportService;
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
                var usuarios = connection.Query<Usuario>("SELECT * FROM Usuario").ToList();
                var documentosProximosVencer = connection.Query<DocumentoVencimientoViewModel>(
                    "sp_MonitorearVencimientos",
                    new { dias_previos = 30 },
                    commandType: CommandType.StoredProcedure
                ).Take(5).ToList();

                var viewModel = new AdminDashboardViewModel
                {
                    TotalCamiones = camiones.Count,
                    CamionesActivos = camiones.Count(c => c.estado == "activo"),
                    TotalChoferes = choferes.Count,
                    ChoferesActivos = choferes.Count(c => c.estado == "activo"),
                    TotalUsuarios = usuarios.Count,
                    UsuariosActivos = usuarios.Count(u => u.estado_verificacion == "verificado"),
                    DocumentosProximosVencer = documentosProximosVencer.Count,
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

        #region Gestión de Camiones

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
                return RedirectToAction("ListarCamiones");
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
            try
            {
                using (var connection = CreateConnection())
                {
                    var choferes = connection.Query<Choferes>(
                        "SELECT * FROM Choferes WHERE estado = 'activo' ORDER BY nombreCompleto").ToList();
                    ViewBag.Choferes = choferes;
                }

                return View(new Camiones { estado = "activo" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar la página de registro de camión");
                TempData["Error"] = "Error al cargar los datos necesarios";
                return RedirectToAction("Index");
            }
        }

        [HttpGet]
        public IActionResult ListarCamiones(string filtro = null, string estado = null)
        {
            try
            {
                using (var connection = CreateConnection())
                {
                    string query = @"
                        SELECT c.*, ch.nombreCompleto as NombreChofer 
                        FROM Camiones c
                        LEFT JOIN Choferes ch ON c.chofer_asignado = ch.id_chofer
                        WHERE 1=1";

                    // Agregar filtros si se proporcionan
                    if (!string.IsNullOrEmpty(filtro))
                    {
                        query += " AND (c.marca LIKE @filtro OR c.modelo LIKE @filtro OR c.numero_placa LIKE @filtro)";
                    }

                    if (!string.IsNullOrEmpty(estado))
                    {
                        query += " AND c.estado = @estado";
                    }

                    query += " ORDER BY c.id_camion DESC";

                    var parameters = new DynamicParameters();
                    parameters.Add("filtro", $"%{filtro}%");
                    parameters.Add("estado", estado);

                    var camiones = connection.Query<CamionViewModel>(query, parameters).ToList();

                    ViewBag.Filtro = filtro;
                    ViewBag.Estado = estado;
                    return View(camiones);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al listar camiones");
                TempData["Error"] = "Error al cargar la lista de camiones";
                return View(new List<CamionViewModel>());
            }
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
                        numero_placa = camion.numero_placa,
                        estado = camion.estado,
                        chofer_asignado = camion.chofer_asignado ?? (object)DBNull.Value
                    };

                    connection.Execute("sp_ActualizarCamion", parameters, commandType: CommandType.StoredProcedure);

                    // Si el estado cambió a "mantenimiento", registrarlo en el historial
                    if (camion.estado == "mantenimiento")
                    {
                        var maintenanceParams = new
                        {
                            id_camion = id,
                            descripcion = "Puesta en mantenimiento por administrador",
                            fecha_mantenimiento = DateTime.Now,
                            costo = 0
                        };

                        connection.Execute("sp_RegistrarMantenimiento", maintenanceParams, commandType: CommandType.StoredProcedure);
                    }
                }

                TempData["Success"] = "Camión actualizado exitosamente";
                return RedirectToAction("ListarCamiones");
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
                        return RedirectToAction("ListarCamiones");
                    }

                    var choferes = connection.Query<Choferes>("SELECT * FROM Choferes WHERE estado = 'activo'").ToList();
                    ViewBag.Choferes = choferes;

                    return View(camion);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener datos del camión");
                TempData["Error"] = "Error al obtener datos del camión";
                return RedirectToAction("ListarCamiones");
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
                        return RedirectToAction("ListarCamiones");
                    }

                    ViewBag.Camion = camion;
                    return View(historial);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener historial de mantenimiento");
                TempData["Error"] = "Error al obtener el historial de mantenimiento";
                return RedirectToAction("ListarCamiones");
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
                return RedirectToAction("ListarCamiones");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al desactivar camión");
                TempData["Error"] = "Error al desactivar el camión";
                return RedirectToAction("ListarCamiones");
            }
        }

        [HttpPost]
        public IActionResult ActivarCamion(int id)
        {
            try
            {
                using (var connection = CreateConnection())
                {
                    connection.Execute(
                        "UPDATE Camiones SET estado = 'activo' WHERE id_camion = @id_camion",
                        new { id_camion = id }
                    );
                }

                TempData["Success"] = "Camión activado exitosamente";
                return RedirectToAction("ListarCamiones");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al activar camión");
                TempData["Error"] = "Error al activar el camión";
                return RedirectToAction("ListarCamiones");
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

                    // Notificar al chofer sobre la asignación
                    var chofer = connection.QueryFirstOrDefault<Choferes>(
                        "SELECT c.*, u.id_usuario FROM Choferes c LEFT JOIN Usuario u ON c.id_usuario = u.id_usuario WHERE c.id_chofer = @id_chofer",
                        new { id_chofer = idChofer }
                    );

                    var camion = connection.QueryFirstOrDefault<Camiones>(
                        "SELECT * FROM Camiones WHERE id_camion = @id_camion",
                        new { id_camion = idCamion }
                    );

                    if (chofer != null && chofer.id_usuario.HasValue)
                    {
                        var mensaje = $"Se le ha asignado el camión {camion.marca} {camion.modelo} (Placa: {camion.numero_placa})";
                        Task.Run(() => _notificationService.CreateNotificationAsync(chofer.id_usuario.Value, "Asignación de Camión", mensaje));
                    }
                }

                TempData["Success"] = "Chofer asignado exitosamente";
                return RedirectToAction("ListarCamiones");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al asignar chofer");
                TempData["Error"] = "Error al asignar el chofer";
                return RedirectToAction("ListarCamiones");
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
                        return RedirectToAction("ListarCamiones");
                    }
                }

                TempData["Success"] = "Camión eliminado exitosamente";
                return RedirectToAction("ListarCamiones");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar camión");
                TempData["Error"] = "Error al eliminar el camión";
                return RedirectToAction("ListarCamiones");
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

                    // Actualizar estado del camión si es necesario
                    if (mantenimiento.fecha_mantenimiento.Date == DateTime.Today)
                    {
                        connection.Execute(
                            "UPDATE Camiones SET estado = 'mantenimiento' WHERE id_camion = @id_camion",
                            new { id_camion = mantenimiento.id_camion }
                        );
                    }
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

        [HttpGet]
        public IActionResult GenerarReporteCamiones(string estado = null)
        {
            try
            {
                using (var connection = CreateConnection())
                {
                    string query = @"
                        SELECT c.*, ch.nombreCompleto as NombreChofer 
                        FROM Camiones c
                        LEFT JOIN Choferes ch ON c.chofer_asignado = ch.id_chofer";

                    if (!string.IsNullOrEmpty(estado))
                    {
                        query += " WHERE c.estado = @estado";
                    }

                    query += " ORDER BY c.id_camion";

                    var parameters = new DynamicParameters();
                    parameters.Add("estado", estado);

                    var camiones = connection.Query<CamionViewModel>(query, parameters).ToList();

                    ViewBag.Estado = estado;
                    ViewBag.FechaGeneracion = DateTime.Now;
                    return View(camiones);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar reporte de camiones");
                TempData["Error"] = "Error al generar el reporte de camiones";
                return RedirectToAction("ListarCamiones");
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExportarReporteCamionesPDF(string estado = null)
        {
            try
            {
                using (var connection = CreateConnection())
                {
                    string query = @"
                        SELECT c.*, ch.nombreCompleto as NombreChofer 
                        FROM Camiones c
                        LEFT JOIN Choferes ch ON c.chofer_asignado = ch.id_chofer";

                    if (!string.IsNullOrEmpty(estado))
                    {
                        query += " WHERE c.estado = @estado";
                    }

                    query += " ORDER BY c.id_camion";

                    var parameters = new DynamicParameters();
                    parameters.Add("estado", estado);

                    var camiones = connection.Query<CamionViewModel>(query, parameters).ToList();

                    // Aquí invocaríamos un servicio para generar el PDF
                    // Por ahora, solo devolvemos el reporte en vista normal
                    byte[] pdfBytes = await _reportService.GenerarReporteCamionesPdf(camiones, estado);

                    string estadoTexto = string.IsNullOrEmpty(estado) ? "todos" : estado;
                    string fileName = $"Reporte_Camiones_{estadoTexto}_{DateTime.Now:yyyyMMdd}.pdf";

                    return File(pdfBytes, "application/pdf", fileName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al exportar reporte de camiones a PDF");
                TempData["Error"] = "Error al generar el PDF de camiones";
                return RedirectToAction("GenerarReporteCamiones", new { estado });
            }
        }

        #endregion

        #region Gestión de Choferes

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
                        genero = chofer.genero,
                        id_usuario = chofer.id_usuario ?? (object)DBNull.Value
                    };

                    connection.Execute("sp_RegistrarChofer", parameters, commandType: CommandType.StoredProcedure);

                    // Si tiene un usuario asignado, notificarle
                    if (chofer.id_usuario.HasValue)
                    {
                        Task.Run(() => _notificationService.CreateNotificationAsync(
                            chofer.id_usuario.Value,
                            "Perfil de Chofer",
                            "Se ha creado su perfil de chofer en el sistema."));
                    }
                }

                TempData["Success"] = "Chofer registrado exitosamente";
                return RedirectToAction("ListarChoferes");
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
            try
            {
                using (var connection = CreateConnection())
                {
                    // Obtener usuarios sin perfil de chofer para asignar
                    var query = @"
                        SELECT u.* FROM Usuario u
                        LEFT JOIN Choferes c ON u.id_usuario = c.id_usuario
                        WHERE c.id_chofer IS NULL AND u.rol IN ('user', 'driver')
                        ORDER BY u.nombre_usuario";

                    var usuarios = connection.Query<Usuario>(query).ToList();
                    ViewBag.Usuarios = usuarios;
                }

                return View(new Choferes { estado = "activo" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar la página de registro de chofer");
                TempData["Error"] = "Error al cargar los datos necesarios";
                return RedirectToAction("Index");
            }
        }

        [HttpGet]
        public IActionResult ListarChoferes(string filtro = null, string estado = null)
        {
            try
            {
                using (var connection = CreateConnection())
                {
                    string query = @"
                        SELECT ch.*, c.id_camion, CONCAT(c.marca, ' ', c.modelo, ' (', c.numero_placa, ')') as camion_asignado,
                        (SELECT COUNT(*) FROM Documentos d WHERE d.id_chofer = ch.id_chofer) as total_documentos
                        FROM Choferes ch
                        LEFT JOIN Camiones c ON c.chofer_asignado = ch.id_chofer
                        WHERE 1=1";

                    if (!string.IsNullOrEmpty(filtro))
                    {
                        query += " AND (ch.nombreCompleto LIKE @filtro OR ch.numero_cedula LIKE @filtro OR ch.licencia LIKE @filtro)";
                    }

                    if (!string.IsNullOrEmpty(estado))
                    {
                        query += " AND ch.estado = @estado";
                    }

                    query += " ORDER BY ch.id_chofer DESC";

                    var parameters = new DynamicParameters();
                    parameters.Add("filtro", $"%{filtro}%");
                    parameters.Add("estado", estado);

                    var choferes = connection.Query<ChoferViewModel>(query, parameters).ToList();

                    // Calcular estado de licencia
                    foreach (var chofer in choferes)
                    {
                        int diasParaVencimiento = (int)(chofer.fecha_venc_licencia - DateTime.Now).TotalDays;
                        chofer.estado_licencia = diasParaVencimiento <= 0 ? "Vencida" :
                                            diasParaVencimiento <= 30 ? "Por vencer" : "Vigente";
                    }

                    ViewBag.Filtro = filtro;
                    ViewBag.Estado = estado;
                    return View(choferes);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al listar choferes");
                TempData["Error"] = "Error al cargar la lista de choferes";
                return View(new List<ChoferViewModel>());
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

                    var chofer = connection.QueryFirstOrDefault<Choferes>(
                        "SELECT * FROM Choferes WHERE id_chofer = @id_chofer",
                        new { id_chofer = idChofer }
                    );

                    if (chofer == null)
                    {
                        TempData["Error"] = "Chofer no encontrado";
                        return RedirectToAction("ListarChoferes");
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
                return RedirectToAction("ListarChoferes");
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
                        fecha_vencimiento = documento.fecha_vencimiento,
                        estado_validacion = documento.estado_validacion ?? "pendiente"
                    };

                    connection.Execute("sp_ActualizarDocumento", parameters, commandType: CommandType.StoredProcedure);

                    // Notificar al chofer si se actualiza o verifica un documento
                    var chofer = connection.QueryFirstOrDefault<Choferes>(
                        "SELECT c.*, u.id_usuario FROM Choferes c LEFT JOIN Usuario u ON c.id_usuario = u.id_usuario WHERE c.id_chofer = @id_chofer",
                        new { id_chofer = documento.id_chofer }
                    );

                    if (chofer != null && chofer.id_usuario.HasValue)
                    {
                        string mensaje = $"Su documento '{documento.tipo_documento}' ha sido {documento.estado_validacion}.";
                        Task.Run(() => _notificationService.CreateNotificationAsync(
                            chofer.id_usuario.Value,
                            "Actualización de Documento",
                            mensaje));
                    }
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
                        return RedirectToAction("ListarChoferes");
                    }

                    var documentos = connection.Query<Documentos>(
                        "sp_ObtenerDocumentosChofer",
                        new { id_chofer = idChofer },
                        commandType: CommandType.StoredProcedure
                    ).ToList();

                    ViewBag.Chofer = chofer;
                    ViewBag.Documentos = documentos;
                    return View();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar datos para actualizar documentos");
                TempData["Error"] = "Error al cargar los datos necesarios";
                return RedirectToAction("ListarChoferes");
            }
        }

        [HttpGet]
        public IActionResult MonitorearVencimientos(int diasPrevios = 30)
        {
            try
            {
                using (var connection = CreateConnection())
                {
                    var documentos = connection.Query<DocumentoVencimientoViewModel>(
                        "sp_MonitorearVencimientos",
                        new { dias_previos = diasPrevios },
                        commandType: CommandType.StoredProcedure
                    ).ToList();

                    var licencias = connection.Query<ChoferViewModel>(@"
                        SELECT ch.*, DATEDIFF(DAY, GETDATE(), ch.fecha_venc_licencia) as dias_para_vencimiento
                        FROM Choferes ch
                        WHERE ch.estado = 'activo' AND ch.fecha_venc_licencia <= DATEADD(day, @dias_previos, GETDATE())
                        ORDER BY ch.fecha_venc_licencia",
                        new { dias_previos = diasPrevios }
                    ).ToList();

                    foreach (var chofer in licencias)
                    {
                        int diasParaVencimiento = (int)(chofer.fecha_venc_licencia - DateTime.Now).TotalDays;
                        chofer.estado_licencia = diasParaVencimiento <= 0 ? "Vencida" :
                                            diasParaVencimiento <= 30 ? "Por vencer" : "Vigente";
                    }

                    ViewBag.DiasPrevios = diasPrevios;
                    ViewBag.Licencias = licencias;
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
                        genero = chofer.genero,
                        id_usuario = chofer.id_usuario ?? (object)DBNull.Value
                    };

                    connection.Execute("sp_ActualizarDatosChofer", parameters, commandType: CommandType.StoredProcedure);

                    // Verificar si se cambió el usuario asociado
                    if (chofer.id_usuario.HasValue)
                    {
                        Task.Run(() => _notificationService.CreateNotificationAsync(
                            chofer.id_usuario.Value,
                            "Actualización de Perfil",
                            "Su perfil de chofer ha sido actualizado por un administrador."));
                    }
                }

                TempData["Success"] = "Datos del chofer actualizados exitosamente";
                return RedirectToAction("ListarChoferes");
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
                        return RedirectToAction("ListarChoferes");
                    }

                    // Obtener usuarios disponibles
                    var query = @"
                        SELECT u.* FROM Usuario u
                        LEFT JOIN Choferes c ON u.id_usuario = c.id_usuario AND c.id_chofer != @id_chofer
                        WHERE (c.id_chofer IS NULL OR u.id_usuario = @id_usuario) AND u.rol IN ('user', 'driver')
                        ORDER BY u.nombre_usuario";

                    var usuarios = connection.Query<Usuario>(query, new
                    {
                        id_chofer = id,
                        id_usuario = chofer.id_usuario
                    }).ToList();

                    ViewBag.Usuarios = usuarios;

                    return View(chofer);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener datos del chofer");
                TempData["Error"] = "Error al obtener datos del chofer";
                return RedirectToAction("ListarChoferes");
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
                return RedirectToAction("ListarChoferes");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al desactivar chofer");
                TempData["Error"] = "Error al desactivar el chofer";
                return RedirectToAction("ListarChoferes");
            }
        }

        [HttpPost]
        public IActionResult ActivarChofer(int id)
        {
            try
            {
                using (var connection = CreateConnection())
                {
                    connection.Execute(
                        "UPDATE Choferes SET estado = 'activo' WHERE id_chofer = @id_chofer",
                        new { id_chofer = id }
                    );
                }

                TempData["Success"] = "Chofer activado exitosamente";
                return RedirectToAction("ListarChoferes");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al activar chofer");
                TempData["Error"] = "Error al activar el chofer";
                return RedirectToAction("ListarChoferes");
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
                        fecha_vencimiento = documento.fecha_vencimiento,
                        estado_validacion = documento.estado_validacion ?? "pendiente"
                    };

                    connection.Execute("sp_RegistrarDocumento", parameters, commandType: CommandType.StoredProcedure);

                    // Notificar al chofer si tiene un usuario asignado
                    var chofer = connection.QueryFirstOrDefault<Choferes>(
                        "SELECT c.*, u.id_usuario FROM Choferes c LEFT JOIN Usuario u ON c.id_usuario = u.id_usuario WHERE c.id_chofer = @id_chofer",
                        new { id_chofer = documento.id_chofer }
                    );

                    if (chofer != null && chofer.id_usuario.HasValue)
                    {
                        string mensaje = $"Se ha registrado un nuevo documento '{documento.tipo_documento}' en su perfil.";
                        Task.Run(() => _notificationService.CreateNotificationAsync(
                            chofer.id_usuario.Value,
                            "Nuevo Documento",
                            mensaje));
                    }
                }

                TempData["Success"] = "Documento asignado exitosamente";
                return RedirectToAction("ObtenerDocumentosChofer", new { idChofer = documento.id_chofer });
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
                return RedirectToAction("ListarChoferes");
            }
        }

        [HttpPost]
        public IActionResult EliminarChofer(int id)
        {
            try
            {
                using (var connection = CreateConnection())
                {
                    // Verificar si tiene camiones asignados
                    var camionesAsignados = connection.QueryFirstOrDefault<int>(
                        "SELECT COUNT(*) FROM Camiones WHERE chofer_asignado = @id_chofer",
                        new { id_chofer = id }
                    );

                    if (camionesAsignados > 0)
                    {
                        TempData["Error"] = "No se puede eliminar: El chofer tiene camiones asignados";
                        return RedirectToAction("ListarChoferes");
                    }

                    var result = connection.Execute(
                        "sp_EliminarChofer",
                        new { id_chofer = id },
                        commandType: CommandType.StoredProcedure
                    );

                    if (result == 0)
                    {
                        TempData["Error"] = "No se puede eliminar: Chofer tiene registros asociados";
                        return RedirectToAction("ListarChoferes");
                    }
                }

                TempData["Success"] = "Chofer eliminado exitosamente";
                return RedirectToAction("ListarChoferes");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar chofer");
                TempData["Error"] = "Error al eliminar el chofer";
                return RedirectToAction("ListarChoferes");
            }
        }

        [HttpGet]
        public IActionResult GenerarReporteChoferes(string estado = null)
        {
            try
            {
                using (var connection = CreateConnection())
                {
                    string query = @"
                        SELECT ch.*, c.id_camion, CONCAT(c.marca, ' ', c.modelo, ' (', c.numero_placa, ')') as camion_asignado,
                        (SELECT COUNT(*) FROM Documentos d WHERE d.id_chofer = ch.id_chofer) as total_documentos,
                        DATEDIFF(DAY, GETDATE(), ch.fecha_venc_licencia) as dias_para_vencimiento
                        FROM Choferes ch
                        LEFT JOIN Camiones c ON c.chofer_asignado = ch.id_chofer
                        WHERE 1=1";

                    if (!string.IsNullOrEmpty(estado))
                    {
                        query += " AND ch.estado = @estado";
                    }

                    query += " ORDER BY ch.nombreCompleto";

                    var parameters = new DynamicParameters();
                    parameters.Add("estado", estado);

                    var choferes = connection.Query<ChoferViewModel>(query, parameters).ToList();

                    // Calcular estado de licencia
                    foreach (var chofer in choferes)
                    {
                        int diasParaVencimiento = (int)(chofer.fecha_venc_licencia - DateTime.Now).TotalDays;
                        chofer.estado_licencia = diasParaVencimiento <= 0 ? "Vencida" :
                                            diasParaVencimiento <= 30 ? "Por vencer" : "Vigente";
                    }

                    ViewBag.Estado = estado;
                    ViewBag.FechaGeneracion = DateTime.Now;
                    return View(choferes);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar reporte de choferes");
                TempData["Error"] = "Error al generar el reporte de choferes";
                return RedirectToAction("ListarChoferes");
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExportarReporteChoferesPDF(string estado = null)
        {
            try
            {
                using (var connection = CreateConnection())
                {
                    string query = @"
                        SELECT ch.*, c.id_camion, CONCAT(c.marca, ' ', c.modelo, ' (', c.numero_placa, ')') as camion_asignado,
                        (SELECT COUNT(*) FROM Documentos d WHERE d.id_chofer = ch.id_chofer) as total_documentos,
                        DATEDIFF(DAY, GETDATE(), ch.fecha_venc_licencia) as dias_para_vencimiento
                        FROM Choferes ch
                        LEFT JOIN Camiones c ON c.chofer_asignado = ch.id_chofer
                        WHERE 1=1";

                    if (!string.IsNullOrEmpty(estado))
                    {
                        query += " AND ch.estado = @estado";
                    }

                    query += " ORDER BY ch.nombreCompleto";

                    var parameters = new DynamicParameters();
                    parameters.Add("estado", estado);

                    var choferes = connection.Query<ChoferViewModel>(query, parameters).ToList();

                    // Calcular estado de licencia
                    foreach (var chofer in choferes)
                    {
                        int diasParaVencimiento = (int)(chofer.fecha_venc_licencia - DateTime.Now).TotalDays;
                        chofer.estado_licencia = diasParaVencimiento <= 0 ? "Vencida" :
                                            diasParaVencimiento <= 30 ? "Por vencer" : "Vigente";
                    }

                    // Generar PDF con el servicio de reportes
                    byte[] pdfBytes = await _reportService.GenerarReporteChoferesPdf(choferes, estado);

                    string estadoTexto = string.IsNullOrEmpty(estado) ? "todos" : estado;
                    string fileName = $"Reporte_Choferes_{estadoTexto}_{DateTime.Now:yyyyMMdd}.pdf";

                    return File(pdfBytes, "application/pdf", fileName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al exportar reporte de choferes a PDF");
                TempData["Error"] = "Error al generar el PDF de choferes";
                return RedirectToAction("GenerarReporteChoferes", new { estado });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerChoferesPaginados(int pagina = 1, int cantidadPorPagina = 10)
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

        #endregion

        #region Gestión de Usuarios

        [HttpGet]
        public IActionResult ListarUsuarios(string filtro = null, string rol = null)
        {
            try
            {
                using (var connection = CreateConnection())
                {
                    string query = @"
                        SELECT u.id_usuario, u.nombre_usuario, u.correo_electronico, u.rol, 
                               u.ultimo_ingreso, u.estado_verificacion, u.fecha_registro,
                               (SELECT COUNT(*) FROM SesionesActivas sa WHERE sa.id_usuario = u.id_usuario) as sesiones_activas
                        FROM Usuario u
                        WHERE 1=1";

                    if (!string.IsNullOrEmpty(filtro))
                    {
                        query += " AND (u.nombre_usuario LIKE @filtro OR u.correo_electronico LIKE @filtro)";
                    }

                    if (!string.IsNullOrEmpty(rol))
                    {
                        query += " AND u.rol = @rol";
                    }

                    query += " ORDER BY u.nombre_usuario";

                    var parameters = new DynamicParameters();
                    parameters.Add("filtro", $"%{filtro}%");
                    parameters.Add("rol", rol);

                    var usuarios = connection.Query<UsuarioAdminViewModel>(query, parameters).ToList();

                    ViewBag.Filtro = filtro;
                    ViewBag.Rol = rol;
                    return View(usuarios);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al listar usuarios");
                TempData["Error"] = "Error al cargar la lista de usuarios";
                return View(new List<UsuarioAdminViewModel>());
            }
        }

        [HttpGet]
        public IActionResult EditarUsuario(int id)
        {
            try
            {
                using (var connection = CreateConnection())
                {
                    var usuario = connection.QueryFirstOrDefault<Usuario>(
                        "SELECT * FROM Usuario WHERE id_usuario = @id_usuario",
                        new { id_usuario = id }
                    );

                    if (usuario == null)
                    {
                        TempData["Error"] = "Usuario no encontrado";
                        return RedirectToAction("ListarUsuarios");
                    }

                    var viewModel = new EditarUsuarioViewModel
                    {
                        id_usuario = usuario.id_usuario,
                        nombre_usuario = usuario.nombre_usuario,
                        correo_electronico = usuario.correo_electronico,
                        rol = usuario.rol
                    };

                    return View(viewModel);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener datos del usuario");
                TempData["Error"] = "Error al obtener datos del usuario";
                return RedirectToAction("ListarUsuarios");
            }
        }

        [HttpPost]
        public IActionResult EditarUsuario(EditarUsuarioViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            try
            {
                using (var connection = CreateConnection())
                {
                    var usuario = connection.QueryFirstOrDefault<Usuario>(
                        "SELECT * FROM Usuario WHERE id_usuario = @id_usuario",
                        new { id_usuario = model.id_usuario }
                    );

                    if (usuario == null)
                    {
                        TempData["Error"] = "Usuario no encontrado";
                        return RedirectToAction("ListarUsuarios");
                    }

                    // Verificar si se está modificando el correo y si ya existe
                    if (usuario.correo_electronico != model.correo_electronico)
                    {
                        var existeEmail = connection.QueryFirstOrDefault<int>(
                            "SELECT COUNT(*) FROM Usuario WHERE correo_electronico = @correo AND id_usuario != @id",
                            new { correo = model.correo_electronico, id = model.id_usuario }
                        );

                        if (existeEmail > 0)
                        {
                            ModelState.AddModelError("correo_electronico", "Este correo electrónico ya está en uso");
                            return View(model);
                        }
                    }

                    connection.Execute(
                        "UPDATE Usuario SET nombre_usuario = @nombre, correo_electronico = @correo, rol = @rol WHERE id_usuario = @id",
                        new
                        {
                            id = model.id_usuario,
                            nombre = model.nombre_usuario,
                            correo = model.correo_electronico,
                            rol = model.rol
                        }
                    );

                    // Notificar al usuario sobre los cambios en su cuenta
                    Task.Run(() => _notificationService.CreateNotificationAsync(
                        model.id_usuario,
                        "Actualización de Cuenta",
                        "Un administrador ha actualizado la información de tu cuenta."
                    ));
                }

                TempData["Success"] = "Usuario actualizado exitosamente";
                return RedirectToAction("ListarUsuarios");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar usuario");
                TempData["Error"] = "Error al actualizar el usuario";
                return View(model);
            }
        }
        [HttpPost]
        public IActionResult CambiarEstadoUsuario(int id, string estado)
        {
            try
            {
                using (var connection = CreateConnection())
                {
                    connection.Execute(
                        "UPDATE Usuario SET estado_verificacion = @estado WHERE id_usuario = @id",
                        new { id = id, estado = estado }
                    );

                    // Notificar al usuario sobre el cambio de estado
                    string mensaje = estado == "verificado"
                        ? "Tu cuenta ha sido verificada y activada."
                        : "Tu cuenta ha sido desactivada. Contacta al administrador para más información.";

                    Task.Run(() => _notificationService.CreateNotificationAsync(
                        id,
                        "Estado de Cuenta",
                        mensaje
                    ));
                }

                TempData["Success"] = $"Estado del usuario cambiado a '{estado}' exitosamente";
                return RedirectToAction("ListarUsuarios");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cambiar estado del usuario");
                TempData["Error"] = "Error al cambiar el estado del usuario";
                return RedirectToAction("ListarUsuarios");
            }
        }

        [HttpPost]
        public IActionResult ResetearContrasena(int id)
        {
            try
            {
                using (var connection = CreateConnection())
                {
                    var usuario = connection.QueryFirstOrDefault<Usuario>(
                        "SELECT * FROM Usuario WHERE id_usuario = @id_usuario",
                        new { id_usuario = id }
                    );

                    if (usuario == null)
                    {
                        TempData["Error"] = "Usuario no encontrado";
                        return RedirectToAction("ListarUsuarios");
                    }

                    // Generar contraseña temporal
                    string tempPassword = Guid.NewGuid().ToString().Substring(0, 8);

                    // Hash de la contraseña
                    string hashedPassword = _userService.HashPassword(tempPassword);

                    connection.Execute(
                        "UPDATE Usuario SET password_hash = @password, forzar_cambio_password = 1 WHERE id_usuario = @id",
                        new { password = hashedPassword, id = id }
                    );

                    // Enviar email con la contraseña temporal
                    Task.Run(() => _emailService.EnviarCorreoAsync(
                        usuario.correo_electronico,
                        "Reseteo de Contraseña",
                        $"Se ha reseteado su contraseña en el sistema COMAVI. Su contraseña temporal es: {tempPassword}. " +
                        "Por favor, cambie su contraseña al iniciar sesión."
                    ));

                    // Notificar al usuario
                    Task.Run(() => _notificationService.CreateNotificationAsync(
                        id,
                        "Reseteo de Contraseña",
                        "Se ha reseteado tu contraseña. Verifica tu correo electrónico para obtener la contraseña temporal."
                    ));
                }

                TempData["Success"] = "Contraseña reseteada y enviada al correo del usuario";
                return RedirectToAction("ListarUsuarios");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al resetear contraseña");
                TempData["Error"] = "Error al resetear la contraseña";
                return RedirectToAction("ListarUsuarios");
            }
        }

        [HttpGet]
        public IActionResult VerSesionesActivas(int id)
        {
            try
            {
                using (var connection = CreateConnection())
                {
                    var usuario = connection.QueryFirstOrDefault<Usuario>(
                        "SELECT * FROM Usuario WHERE id_usuario = @id_usuario",
                        new { id_usuario = id }
                    );

                    if (usuario == null)
                    {
                        TempData["Error"] = "Usuario no encontrado";
                        return RedirectToAction("ListarUsuarios");
                    }

                    var sesiones = connection.Query<SesionActivaViewModel>(
                        "SELECT sa.*, u.nombre_usuario FROM SesionesActivas sa " +
                        "JOIN Usuario u ON sa.id_usuario = u.id_usuario " +
                        "WHERE sa.id_usuario = @id_usuario ORDER BY sa.fecha_inicio DESC",
                        new { id_usuario = id }
                    ).ToList();

                    ViewBag.Usuario = usuario;
                    return View(sesiones);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener sesiones activas");
                TempData["Error"] = "Error al obtener las sesiones activas";
                return RedirectToAction("ListarUsuarios");
            }
        }

        [HttpPost]
        public IActionResult CerrarSesion(string tokenSesion)
        {
            try
            {
                using (var connection = CreateConnection())
                {
                    var sesion = connection.QueryFirstOrDefault<SesionesActivas>(
                        "SELECT * FROM SesionesActivas WHERE token_sesion = @token",
                        new { token = tokenSesion }
                    );

                    if (sesion == null)
                    {
                        TempData["Error"] = "Sesión no encontrada";
                        return RedirectToAction("ListarUsuarios");
                    }

                    connection.Execute(
                        "DELETE FROM SesionesActivas WHERE token_sesion = @token",
                        new { token = tokenSesion }
                    );

                    // Notificar al usuario
                    Task.Run(() => _notificationService.CreateNotificationAsync(
                        sesion.id_usuario,
                        "Cierre de Sesión",
                        "Un administrador ha cerrado una de tus sesiones activas por motivos de seguridad."
                    ));
                }

                TempData["Success"] = "Sesión cerrada exitosamente";
                return RedirectToAction("VerSesionesActivas", new { id = Request.Query["idUsuario"] });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cerrar sesión");
                TempData["Error"] = "Error al cerrar la sesión";
                return RedirectToAction("ListarUsuarios");
            }
        }

        [HttpGet]
        public IActionResult GenerarReporteUsuarios(string rol = null)
        {
            try
            {
                using (var connection = CreateConnection())
                {
                    string query = @"
                        SELECT u.id_usuario, u.nombre_usuario, u.correo_electronico, u.rol, 
                               u.ultimo_ingreso, u.estado_verificacion, u.fecha_registro,
                               (SELECT COUNT(*) FROM SesionesActivas sa WHERE sa.id_usuario = u.id_usuario) as sesiones_activas
                        FROM Usuario u
                        WHERE 1=1";

                    if (!string.IsNullOrEmpty(rol))
                    {
                        query += " AND u.rol = @rol";
                    }

                    query += " ORDER BY u.nombre_usuario";

                    var parameters = new DynamicParameters();
                    parameters.Add("rol", rol);

                    var usuarios = connection.Query<UsuarioAdminViewModel>(query, parameters).ToList();

                    ViewBag.Rol = rol;
                    ViewBag.FechaGeneracion = DateTime.Now;
                    return View(usuarios);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar reporte de usuarios");
                TempData["Error"] = "Error al generar el reporte de usuarios";
                return RedirectToAction("ListarUsuarios");
            }
        }

        //[HttpGet]
        //public async Task<IActionResult> ExportarReporteUsuariosPDF(string rol = null)
        //{
        //    try
        //    {
        //        using (var connection = CreateConnection())
        //        {
        //            string query = @"
        //                SELECT u.id_usuario, u.nombre_usuario, u.correo_electronico, u.rol, 
        //                       u.ultimo_ingreso, u.estado_verificacion, u.fecha_registro,
        //                       (SELECT COUNT(*) FROM SesionesActivas sa WHERE sa.id_usuario = u.id_usuario) as sesiones_activas
        //                FROM Usuario u
        //                WHERE 1=1";

        //            if (!string.IsNullOrEmpty(rol))
        //            {
        //                query += " AND u.rol = @rol";
        //            }

        //            query += " ORDER BY u.nombre_usuario";

        //            var parameters = new DynamicParameters();
        //            parameters.Add("rol", rol);

        //            var usuarios = connection.Query<UsuarioAdminViewModel>(query, parameters).ToList();

        //            // Convierte de Controllers.UsuarioAdminViewModel a Models.UsuarioAdminViewModel
        //            var modelUsuarios = usuarios.Select(u => new COMAVI_SA.Models.UsuarioAdminViewModel
        //            {
        //                id_usuario = u.id_usuario,
        //                nombre_usuario = u.nombre_usuario,
        //                correo_electronico = u.correo_electronico,
        //                rol = u.rol,
        //                ultimo_ingreso = u.ultimo_ingreso,
        //                estado_verificacion = u.estado_verificacion,
        //                fecha_registro = u.fecha_registro,
        //                sesiones_activas = u.sesiones_activas
        //            }).ToList();

        //            byte[] pdfBytes = await _reportService.GenerarReporteUsuariosPdf(modelUsuarios, rol);

        //            string rolTexto = string.IsNullOrEmpty(rol) ? "todos" : rol;
        //            string fileName = $"Reporte_Usuarios_{rolTexto}_{DateTime.Now:yyyyMMdd}.pdf";

        //            return File(pdfBytes, "application/pdf", fileName);
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error al exportar reporte de usuarios a PDF");
        //        TempData["Error"] = "Error al generar el PDF de usuarios";
        //        return RedirectToAction("GenerarReporteUsuarios", new { rol });
        //    }
        //}

        #endregion

        #region Dashboard y Reportes

        [HttpGet]
        public IActionResult Dashboard()
        {
            try
            {
                using var connection = CreateConnection();

                // Obtener indicadores para el dashboard
                var dashboardData = connection.QueryFirstOrDefault<DashboardViewModel>(
                    "sp_ObtenerIndicadoresDashboard",
                    commandType: CommandType.StoredProcedure
                );

                // Obtener datos para gráficos
                var mantenimientosPorMes = connection.Query<GraficoDataViewModel>(
                    "sp_ObtenerMantenimientosPorMes",
                    new { anio = DateTime.Now.Year },
                    commandType: CommandType.StoredProcedure
                ).ToList();

                var camionesEstados = connection.Query<GraficoDataViewModel>(
                    "sp_ObtenerEstadosCamiones",
                    commandType: CommandType.StoredProcedure
                ).ToList();

                var documentosEstados = connection.Query<GraficoDataViewModel>(
                    "sp_ObtenerEstadosDocumentos",
                    commandType: CommandType.StoredProcedure
                ).ToList();

                // Recientes actividades
                var actividades = connection.Query<ActividadRecienteViewModel>(
                    "sp_ObtenerActividadesRecientes",
                    new { cantidad = 10 },
                    commandType: CommandType.StoredProcedure
                ).ToList();

                ViewBag.MantenimientosPorMes = mantenimientosPorMes;
                ViewBag.CamionesEstados = camionesEstados;
                ViewBag.DocumentosEstados = documentosEstados;
                ViewBag.Actividades = actividades;

                return View(dashboardData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar dashboard");
                TempData["Error"] = "Error al cargar los datos del dashboard";
                return View(new DashboardViewModel());
            }
        }

        [HttpGet]
        public IActionResult ReportesGenerales()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GenerarReporteMantenimientos(DateTime? fechaInicio, DateTime? fechaFin)
        {
            try
            {
                fechaInicio ??= DateTime.Now.AddMonths(-1);
                fechaFin ??= DateTime.Now;

                using (var connection = CreateConnection())
                {
                    var mantenimientos = connection.Query<MantenimientoReporteViewModel>(
                        "sp_ObtenerMantenimientosPorFecha",
                        new { fecha_inicio = fechaInicio, fecha_fin = fechaFin },
                        commandType: CommandType.StoredProcedure
                    ).ToList();

                    // Calcular el total de costos
                    decimal totalCostos = mantenimientos.Sum(m => m.costo);

                    ViewBag.FechaInicio = fechaInicio;
                    ViewBag.FechaFin = fechaFin;
                    ViewBag.TotalCostos = totalCostos;
                    ViewBag.FechaGeneracion = DateTime.Now;

                    return View(mantenimientos);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar reporte de mantenimientos");
                TempData["Error"] = "Error al generar el reporte de mantenimientos";
                return RedirectToAction("ReportesGenerales");
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExportarReporteMantenimientosPDF(DateTime? fechaInicio, DateTime? fechaFin)
        {
            try
            {
                fechaInicio ??= DateTime.Now.AddMonths(-1);
                fechaFin ??= DateTime.Now;

                using (var connection = CreateConnection())
                {
                    var mantenimientos = connection.Query<MantenimientoReporteViewModel>(
                        "sp_ObtenerMantenimientosPorFecha",
                        new { fecha_inicio = fechaInicio, fecha_fin = fechaFin },
                        commandType: CommandType.StoredProcedure
                    ).ToList();

                    // Generar PDF con el servicio de reportes
                    byte[] pdfBytes = await _reportService.GenerarReporteMantenimientosPdf(
                        mantenimientos,
                        fechaInicio.Value,
                        fechaFin.Value
                    );

                    string fileName = $"Reporte_Mantenimientos_{fechaInicio:yyyyMMdd}_{fechaFin:yyyyMMdd}.pdf";
                    return File(pdfBytes, "application/pdf", fileName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al exportar reporte de mantenimientos a PDF");
                TempData["Error"] = "Error al generar el PDF de mantenimientos";
                return RedirectToAction("GenerarReporteMantenimientos", new { fechaInicio, fechaFin });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GenerarReporteDocumentosVencidos(int diasPrevios = 30)
        {
            try
            {
                using (var connection = CreateConnection())
                {
                    var documentos = connection.Query<DocumentoVencimientoViewModel>(
                        "sp_MonitorearVencimientos",
                        new { dias_previos = diasPrevios },
                        commandType: CommandType.StoredProcedure
                    ).ToList();

                    var licencias = connection.Query<ChoferViewModel>(@"
                        SELECT ch.*, DATEDIFF(DAY, GETDATE(), ch.fecha_venc_licencia) as dias_para_vencimiento
                        FROM Choferes ch
                        WHERE ch.estado = 'activo' AND ch.fecha_venc_licencia <= DATEADD(day, @dias_previos, GETDATE())
                        ORDER BY ch.fecha_venc_licencia",
                        new { dias_previos = diasPrevios }
                    ).ToList();

                    ViewBag.DiasPrevios = diasPrevios;
                    ViewBag.Licencias = licencias;
                    ViewBag.FechaGeneracion = DateTime.Now;

                    return View(documentos);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar reporte de documentos vencidos");
                TempData["Error"] = "Error al generar el reporte de documentos vencidos";
                return RedirectToAction("ReportesGenerales");
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExportarReporteDocumentosVencidosPDF(int diasPrevios = 30)
        {
            try
            {
                using (var connection = CreateConnection())
                {
                    var documentos = connection.Query<DocumentoVencimientoViewModel>(
                        "sp_MonitorearVencimientos",
                        new { dias_previos = diasPrevios },
                        commandType: CommandType.StoredProcedure
                    ).ToList();

                    var licencias = connection.Query<ChoferViewModel>(@"
                        SELECT ch.*, DATEDIFF(DAY, GETDATE(), ch.fecha_venc_licencia) as dias_para_vencimiento
                        FROM Choferes ch
                        WHERE ch.estado = 'activo' AND ch.fecha_venc_licencia <= DATEADD(day, @dias_previos, GETDATE())
                        ORDER BY ch.fecha_venc_licencia",
                        new { dias_previos = diasPrevios }
                    ).ToList();

                    // Generar PDF con el servicio de reportes
                    byte[] pdfBytes = await _reportService.GenerarReporteDocumentosVencidosPdf(
                        documentos,
                        licencias,
                        diasPrevios
                    );

                    string fileName = $"Reporte_Documentos_Vencidos_{DateTime.Now:yyyyMMdd}.pdf";
                    return File(pdfBytes, "application/pdf", fileName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al exportar reporte de documentos vencidos a PDF");
                TempData["Error"] = "Error al generar el PDF de documentos vencidos";
                return RedirectToAction("GenerarReporteDocumentosVencidos", new { diasPrevios });
            }
        }

        #endregion
    }
}