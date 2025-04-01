using COMAVI_SA.Filters;
using COMAVI_SA.Models;
using COMAVI_SA.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace COMAVI_SA.Controllers
{
    
    [Authorize(Policy = "RequireAdminRole")]
    [VerificarAutenticacion]
    public class AdminController : Controller
    {
        private readonly IAdminService _adminService;
        private readonly IAuditService _auditService;
        private readonly ILogger<AdminController> _logger;
        private readonly INotificationService _notificationService;
        private readonly IEmailService _emailService;
        private readonly IReportService _reportService;
        private readonly IUserService _userService;
        private readonly IMantenimientoService _mantenimientoService;

        public AdminController(
            IAdminService adminService,
            IAuditService auditService,
            ILogger<AdminController> logger,
            INotificationService notificationService,
            IEmailService emailService,
            IReportService reportService,
            IUserService userService,
            IMantenimientoService mantenimientoService) 
        {
            _adminService = adminService;
            _auditService = auditService;
            _logger = logger;
            _notificationService = notificationService;
            _emailService = emailService;
            _reportService = reportService;
            _userService = userService;
            _mantenimientoService = mantenimientoService; 
        }

        #region Dashboard y Reportes

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            try
            {
                // Usar CancellationToken con timeout para evitar espera infinita
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

                Task<AdminDashboardViewModel> task = _adminService.GetDashboardDataAsync();
                var completedTask = await Task.WhenAny(task, Task.Delay(15000, cts.Token));

                if (completedTask == task)
                {
                    // La tarea se completó dentro del tiempo límite
                    return View(await task);
                }
                else
                {
                    // Timeout ocurrió
                    _logger.LogWarning("Timeout al cargar el dashboard de administración");
                    TempData["Warning"] = "El dashboard está tardando demasiado en cargar. Mostrando datos básicos.";
                    return View(new AdminDashboardViewModel());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar el dashboard de administración");
                TempData["Error"] = "Error al cargar el dashboard de administración";
                return RedirectToAction("ListarChoferes");
            }
        }

        [HttpGet]
        public IActionResult ConsejosAdvertencias()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            try
            {
                // Inicializar ViewBag con valores por defecto para evitar NullReferenceException
                ViewBag.MantenimientosPorMes = new List<GraficoDataViewModel>();
                ViewBag.CamionesEstados = new List<GraficoDataViewModel>();
                ViewBag.DocumentosEstados = new List<GraficoDataViewModel>();
                ViewBag.Actividades = new List<dynamic>();

                // Obtener indicadores para el dashboard
                var dashboardData = await _adminService.GetDashboardIndicadoresAsync();

                try
                {
                    // Obtener datos para gráficos
                    var mantenimientosPorMes = await _adminService.GetMantenimientosPorMesAsync(DateTime.Now.Year);
                    ViewBag.MantenimientosPorMes = mantenimientosPorMes;
                    _logger.LogInformation("MantenimientosPorMes obtenidos correctamente: {0} registros",
                        mantenimientosPorMes?.Count ?? 0);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al obtener mantenimientos por mes");
                    // No propagar la excepción, continuar con el resto de datos
                }

                try
                {
                    var camionesEstados = await _adminService.GetEstadosCamionesAsync();
                    ViewBag.CamionesEstados = camionesEstados;
                    _logger.LogInformation("CamionesEstados: {0}",
                        System.Text.Json.JsonSerializer.Serialize(camionesEstados));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al obtener estados de camiones");
                    // No propagar la excepción, continuar con el resto de datos
                }

                try
                {
                    var documentosEstados = await _adminService.GetEstadosDocumentosAsync();
                    ViewBag.DocumentosEstados = documentosEstados;
                    _logger.LogInformation("DocumentosEstados obtenidos correctamente: {0} registros",
                        documentosEstados?.Count ?? 0);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al obtener estados de documentos");
                    // No propagar la excepción, continuar con el resto de datos
                }

                try
                {
                    // Recientes actividades
                    var actividades = await _adminService.GetActividadesRecientesAsync(10);
                    ViewBag.Actividades = actividades;
                    _logger.LogInformation("Actividades recientes obtenidas correctamente: {0} registros",
                        actividades?.Count ?? 0);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al obtener actividades recientes");
                    // No propagar la excepción, continuar con el resto de datos
                }

                return View(dashboardData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error general al cargar dashboard");
                await _auditService.LogExceptionAsync("Dashboard", ex.Message, User.Identity.Name);
                TempData["Error"] = "Error al cargar los datos del dashboard";
                return View(new COMAVI_SA.Models.DashboardViewModel());
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

                var mantenimientos = await _adminService.GenerarReporteMantenimientosAsync(fechaInicio, fechaFin);

                // Calcular el total de costos
                decimal totalCostos = mantenimientos.Sum(m => m.costo);

                ViewBag.FechaInicio = fechaInicio;
                ViewBag.FechaFin = fechaFin;
                ViewBag.TotalCostos = totalCostos;
                ViewBag.FechaGeneracion = DateTime.Now;

                return View(mantenimientos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar reporte de mantenimientos");
                await _auditService.LogExceptionAsync("ReporteMantenimientos", ex.Message, User.Identity.Name);
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

                var mantenimientos = await _adminService.GenerarReporteMantenimientosAsync(fechaInicio, fechaFin);

                // Generar PDF con el servicio de reportes
                byte[] pdfBytes = await _reportService.GenerarReporteMantenimientosPdf(
                    mantenimientos,
                    fechaInicio.Value,
                    fechaFin.Value
                );

                string fileName = $"Reporte_Mantenimientos_{fechaInicio:yyyyMMdd}_{fechaFin:yyyyMMdd}.pdf";
                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al exportar reporte de mantenimientos a PDF");
                await _auditService.LogExceptionAsync("ExportarReporteMantenimientosPDF", ex.Message, User.Identity.Name);
                TempData["Error"] = "Error al generar el PDF de mantenimientos";
                return RedirectToAction("GenerarReporteMantenimientos", new { fechaInicio, fechaFin });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GenerarReporteDocumentosVencidos(int diasPrevios = 30)
        {
            try
            {
                var documentos = await _adminService.MonitorearVencimientosAsync(diasPrevios);
                var licencias = await _adminService.GetLicenciasProximasVencerAsync(diasPrevios);

                ViewBag.DiasPrevios = diasPrevios;
                ViewBag.Licencias = licencias;
                ViewBag.FechaGeneracion = DateTime.Now;

                return View(documentos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar reporte de documentos vencidos");
                await _auditService.LogExceptionAsync("ReporteDocumentosVencidos", ex.Message, User.Identity.Name);
                TempData["Error"] = "Error al generar el reporte de documentos vencidos";
                return RedirectToAction("ReportesGenerales");
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExportarReporteDocumentosVencidosPDF(int diasPrevios = 30)
        {
            try
            {
                var documentos = await _adminService.MonitorearVencimientosAsync(diasPrevios);
                var licencias = await _adminService.GetLicenciasProximasVencerAsync(diasPrevios);

                // Generar PDF con el servicio de reportes
                byte[] pdfBytes = await _reportService.GenerarReporteDocumentosVencidosPdf(
                    documentos,
                    licencias,
                    diasPrevios
                );

                string fileName = $"Reporte_Documentos_Vencidos_{DateTime.Now:yyyyMMdd}.pdf";
                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al exportar reporte de documentos vencidos a PDF");
                await _auditService.LogExceptionAsync("ExportarReporteDocumentosVencidosPDF", ex.Message, User.Identity.Name);
                TempData["Error"] = "Error al generar el PDF de documentos vencidos";
                return RedirectToAction("GenerarReporteDocumentosVencidos", new { diasPrevios });
            }
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task ActualizarCacheDashboard()
        {
            try
            {
                _logger.LogInformation("Iniciando actualización programada del caché del dashboard");
                await _adminService.GetDashboardDataAsync(forceRefresh: true);
                _logger.LogInformation("Caché del dashboard actualizado correctamente");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar caché del dashboard en segundo plano");
            }
        }

        #endregion

        #region Gestión de Usuarios

        [HttpGet]
        public async Task<IActionResult> ListarUsuarios(string filtro = null, string rol = null)
        {
            try
            {
                var usuarios = await _adminService.GetUsuariosAsync(filtro, rol);

                ViewBag.Filtro = filtro;
                ViewBag.Rol = rol;
                return View(usuarios);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al listar usuarios");
                await _auditService.LogExceptionAsync("ListarUsuarios", ex.Message, User.Identity.Name);
                TempData["Error"] = "Error al cargar la lista de usuarios";
                return View(new List<UsuarioAdminViewModel>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> EditarUsuario(int id)
        {
            try
            {
                var usuario = await _adminService.GetUsuarioByIdAsync(id);

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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener datos del usuario");
                await _auditService.LogExceptionAsync("EditarUsuarioForm", ex.Message, User.Identity.Name);
                TempData["Error"] = "Error al obtener datos del usuario";
                return RedirectToAction("ListarUsuarios");
            }
        }

        [HttpPost]
        public async Task<IActionResult> EditarUsuario(COMAVI_SA.Models.EditarUsuarioViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Error en el Formulario/Modelo";
                return View(model);
            }
               
            try
            {
                var result = await _adminService.ActualizarUsuarioAsync(model);

                if (result)
                {
                    TempData["Success"] = "Usuario actualizado exitosamente";

                    // Registrar en auditoría
                    await _auditService.LogAuditEventAsync(
                        "UsuarioUpdate",
                        $"Actualización exitosa de usuario ID: {model.id_usuario}",
                        User.Identity.Name
                    );

                    return RedirectToAction("ListarUsuarios");
                }
                else
                {
                    TempData["Error"] = "Error al Actualizar";
                    return View(model);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar usuario");
                await _auditService.LogExceptionAsync("ActualizarUsuario", ex.Message, User.Identity.Name);
                TempData["Error"] = "Error al actualizar el usuario";
                return View(model);
            }
        }

        [HttpPost]
        public async Task<IActionResult> CambiarEstadoUsuario(int id, string estado)
        {
            try
            {
                var result = await _adminService.CambiarEstadoUsuarioAsync(id, estado);

                if (result)
                {
                    TempData["Success"] = $"Estado del usuario cambiado a '{estado}' exitosamente";

                    // Registrar en auditoría
                    await _auditService.LogAuditEventAsync(
                        "UsuarioEstado",
                        $"Cambio de estado a '{estado}' de usuario ID: {id}",
                        User.Identity.Name
                    );
                }
                else
                {
                    TempData["Error"] = "Error al cambiar el estado del usuario";
                }

                return RedirectToAction("ListarUsuarios");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cambiar estado del usuario");
                await _auditService.LogExceptionAsync("CambiarEstadoUsuario", ex.Message, User.Identity.Name);
                TempData["Error"] = "Error al cambiar el estado del usuario";
                return RedirectToAction("ListarUsuarios");
            }
        }

        [HttpPost]
        public async Task<IActionResult> ResetearContrasena(int id)
        {
            try
            {
                var result = await _adminService.ResetearContrasenaAsync(id);

                if (result)
                {
                    TempData["Success"] = "Contraseña reseteada y enviada al correo del usuario";

                    // Registrar en auditoría
                    await _auditService.LogAuditEventAsync(
                        "UsuarioResetearPassword",
                        $"Reseteo de contraseña para usuario ID: {id}",
                        User.Identity.Name
                    );
                }
                else
                {
                    TempData["Error"] = "Error al resetear la contraseña";
                }

                return RedirectToAction("ListarUsuarios");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al resetear contraseña");
                await _auditService.LogExceptionAsync("ResetearContrasena", ex.Message, User.Identity.Name);
                TempData["Error"] = "Error al resetear la contraseña";
                return RedirectToAction("ListarUsuarios");
            }
        }

        [HttpGet]
        public async Task<IActionResult> VerSesionesActivas(int id)
        {
            try
            {
                var usuario = await _adminService.GetUsuarioByIdAsync(id);

                if (usuario == null)
                {
                    TempData["Error"] = "Usuario no encontrado";
                    return RedirectToAction("ListarUsuarios");
                }

                var sesiones = await _adminService.GetSesionesActivasAsync(id);

                ViewBag.Usuario = usuario;
                return View(sesiones);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener sesiones activas");
                await _auditService.LogExceptionAsync("VerSesionesActivas", ex.Message, User.Identity.Name);
                TempData["Error"] = "Error al obtener las sesiones activas";
                return RedirectToAction("ListarUsuarios");
            }
        }

        [HttpPost]
        public async Task<IActionResult> CerrarSesion(string tokenSesion)
        {
            try
            {
                var result = await _adminService.CerrarSesionAsync(tokenSesion);

                if (result)
                {
                    TempData["Success"] = "Sesión cerrada exitosamente";

                    // Registrar en auditoría
                    await _auditService.LogAuditEventAsync(
                        "CerrarSesion",
                        $"Cierre forzado de sesión, token: {tokenSesion.Substring(0, 8)}...",
                        User.Identity.Name
                    );
                }
                else
                {

                    TempData["Error"] = "Error al cerrar la sesión";
                    
                }

                return RedirectToAction("VerSesionesActivas", new { id = Request.Query["idUsuario"] });
            }
            catch (Exception ex)
            {
                await _auditService.LogExceptionAsync("CerrarSesion", ex.Message, User.Identity.Name);
                return RedirectToAction("ListarUsuarios");
            }
        }

        [HttpGet]
        public async Task<IActionResult> GenerarReporteUsuarios(string rol = null)
        {
            try
            {
                var usuarios = await _adminService.GenerarReporteUsuariosAsync(rol);

                ViewBag.Rol = rol;
                ViewBag.FechaGeneracion = DateTime.Now;
                return View(usuarios);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar reporte de usuarios");
                await _auditService.LogExceptionAsync("ReporteUsuarios", ex.Message, User.Identity.Name);
                TempData["Error"] = "Error al generar el reporte de usuarios";
                return RedirectToAction("ListarUsuarios");
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExportarReporteUsuariosPDF(string rol = null)
        {
            try
            {
                var usuarios = await _adminService.GenerarReporteUsuariosAsync(rol);

                // Generar PDF con el servicio de reportes
                byte[] pdfBytes = await _reportService.GenerarReporteUsuariosPdf(usuarios, rol);

                string rolTexto = string.IsNullOrEmpty(rol) ? "todos" : rol;
                string fileName = $"Reporte_Usuarios_{rolTexto}_{DateTime.Now:yyyyMMdd}.pdf";

                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al exportar reporte de usuarios a PDF");
                await _auditService.LogExceptionAsync("ExportarReporteUsuariosPDF", ex.Message, User.Identity.Name);
                TempData["Error"] = "Error al generar el PDF de usuarios";
                return RedirectToAction("GenerarReporteUsuarios", new { rol });
            }
        }

        #endregion

        #region Gestión de Camiones

        [HttpGet]
        public async Task<IActionResult> ListarCamiones(string filtro = null, string estado = null)
        {
            try
            {
                var camiones = await _adminService.GetCamionesAsync(filtro, estado);

                ViewBag.Filtro = filtro;
                ViewBag.Estado = estado;
                return View(camiones);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al listar camiones");
                await _auditService.LogExceptionAsync("ListarCamiones", ex.Message, User.Identity.Name);
                TempData["Error"] = "Error al cargar la lista de camiones";
                return View(new List<CamionViewModel>());
            }
        }

        [HttpPost]
        public async Task<IActionResult> RegistrarCamion(Camiones camion)
        {
            try
            {
                if (ModelState.ContainsKey("Chofer"))
                {
                    ModelState.Remove("Chofer");
                }

                if (!ModelState.IsValid)
                {
                    return RedirectToAction("ListarCamiones");
                }


                var result = await _adminService.RegistrarCamionAsync(camion);

                if (result)
                {
                    TempData["Success"] = "Camión registrado exitosamente";

                    // Registrar éxito en auditoría
                    await _auditService.LogAuditEventAsync(
                        "CamionCreate",
                        $"Registro exitoso de camión: {camion.marca} {camion.modelo}",
                        User.Identity?.Name ?? "sistema"
                    );
                }
                else
                {
                    TempData["Error"] = "Error al registrar el camión";
                }
                return RedirectToAction("ListarCamiones");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar camión");
                await _auditService.LogExceptionAsync("RegistrarCamion", ex.Message, User.Identity.Name ?? "sistema");
                TempData["Error"] = "Error al registrar el camión";
                return RedirectToAction("ListarCamiones");
            }
        }

        [HttpGet]
        public async Task<IActionResult> ActualizarCamion(int id)
        {
            try
            {
                var camion = await _adminService.GetCamionByIdAsync(id);

                if (camion == null)
                {
                    TempData["Error"] = "Camión no encontrado";
                    return RedirectToAction("ListarCamiones");
                }

                var choferes = await _adminService.GetChoferesAsync(estado: "activo");
                ViewBag.Choferes = choferes;

                return View(camion);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener datos del camión");
                await _auditService.LogExceptionAsync("ObtenerCamion", ex.Message, User.Identity.Name);
                TempData["Error"] = "Error al obtener datos del camión";
                return RedirectToAction("ListarCamiones");
            }
        }

        [HttpPost]
        public async Task<IActionResult> ActualizarCamion(Camiones camion)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return RedirectToAction("ListarCamiones");
                }

                var result = await _adminService.ActualizarCamionAsync(camion);

                if (result)
                {
                    TempData["Success"] = "Camión actualizado exitosamente";

                    // Registrar en auditoría
                    await _auditService.LogAuditEventAsync(
                        "CamionUpdate",
                        $"Actualización exitosa de camión ID: {camion.id_camion}",
                        User.Identity.Name
                    );

                    return RedirectToAction("ListarCamiones");
                }
                else
                {
                    TempData["Error"] = "Error al actualizar el camión";
                    return View(camion);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar camión");
                await _auditService.LogExceptionAsync("ActualizarCamion", ex.Message, User.Identity.Name);
                TempData["Error"] = "Error al actualizar el camión";
                return View(camion);
            }
        }

        [HttpPost]
        public async Task<IActionResult> DesactivarCamion(int id)
        {
            try
            {
                var result = await _adminService.DesactivarCamionAsync(id);

                if (result)
                {
                    TempData["Success"] = "Camión desactivado exitosamente";

                    // Registrar en auditoría
                    await _auditService.LogAuditEventAsync(
                        "CamionDesactivar",
                        $"Desactivación exitosa de camión ID: {id}",
                        User.Identity.Name
                    );
                }
                else
                {
                    TempData["Error"] = "Error al desactivar el camión";
                }

                return RedirectToAction("ListarCamiones");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al desactivar camión");
                await _auditService.LogExceptionAsync("DesactivarCamion", ex.Message, User.Identity.Name);
                TempData["Error"] = "Error al desactivar el camión";
                return RedirectToAction("ListarCamiones");
            }
        }

        [HttpPost]
        public async Task<IActionResult> ActivarCamion(int id)
        {
            try
            {
                var result = await _adminService.ActivarCamionAsync(id);

                if (result)
                {
                    TempData["Success"] = "Camión activado exitosamente";

                    // Registrar en auditoría
                    await _auditService.LogAuditEventAsync(
                        "CamionActivar",
                        $"Activación exitosa de camión ID: {id}",
                        User.Identity.Name
                    );
                }
                else
                {
                    TempData["Error"] = "Error al activar el camión";
                }

                return RedirectToAction("ListarCamiones");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al activar camión");
                await _auditService.LogExceptionAsync("ActivarCamion", ex.Message, User.Identity.Name);
                TempData["Error"] = "Error al activar el camión";
                return RedirectToAction("ListarCamiones");
            }
        }

        [HttpPost]
        public async Task<IActionResult> EliminarCamion(int id)
        {
            try
            {
                var result = await _adminService.EliminarCamionAsync(id);

                if (result)
                {
                    TempData["Success"] = "Camión eliminado exitosamente";

                    // Registrar en auditoría
                    await _auditService.LogAuditEventAsync(
                        "CamionEliminar",
                        $"Eliminación exitosa de camión ID: {id}",
                        User.Identity.Name
                    );
                }
                else
                {
                    TempData["Error"] = "No se puede eliminar por dependencias";
                }

                return RedirectToAction("ListarCamiones");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar camión");
                await _auditService.LogExceptionAsync("EliminarCamion", ex.Message, User.Identity.Name);
                TempData["Error"] = "Error al eliminar el camión";
                return RedirectToAction("ListarCamiones");
            }
        }

        [HttpGet]
        public async Task<IActionResult> AsignarChofer()
        {
            try
            {
                var choferes = await _adminService.GetChoferesAsync(estado: "activo");
                var camiones = await _adminService.GetCamionesActivosAsync();

                ViewBag.Choferes = choferes;
                ViewBag.Camiones = camiones;

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar datos para asignar chofer");
                await _auditService.LogExceptionAsync("AsignarChoferForm", ex.Message, User.Identity.Name);
                TempData["Error"] = "Error al cargar los datos necesarios";
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        public async Task<IActionResult> AsignarChofer(int idCamion, int idChofer)
        {
            try
            {
                var result = await _adminService.AsignarChoferAsync(idCamion, idChofer);

                if (result)
                {
                    TempData["Success"] = "Chofer asignado exitosamente";

                    // Registrar en auditoría
                    await _auditService.LogAuditEventAsync(
                        "AsignarChofer",
                        $"Asignación exitosa de chofer ID: {idChofer} a camión ID: {idCamion}",
                        User.Identity.Name
                    );
                }
                else
                {
                    TempData["Error"] = "Error al asignar el chofer";
                }

                return RedirectToAction("ListarCamiones");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al asignar chofer");
                await _auditService.LogExceptionAsync("AsignarChofer", ex.Message, User.Identity.Name);
                TempData["Error"] = "Error al asignar el chofer";
                return RedirectToAction("ListarCamiones");
            }
        }

        [HttpGet]
        public async Task<IActionResult> HistorialMantenimiento(int idCamion)
        {
            try
            {
                var historial = await _adminService.GetHistorialMantenimientoAsync(idCamion);
                var camion = await _adminService.GetCamionByIdAsync(idCamion);

                if (camion == null)
                {
                    TempData["Error"] = "Camión no encontrado";
                    return RedirectToAction("ListarCamiones");
                }

                ViewBag.Camion = camion;
                return View(historial);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener historial de mantenimiento");
                await _auditService.LogExceptionAsync("HistorialMantenimiento", ex.Message, User.Identity.Name);
                TempData["Error"] = "Error al obtener el historial de mantenimiento";
                return RedirectToAction("ListarCamiones");
            }
        } 

        [HttpGet]
        public async Task<IActionResult> ActualizarEstadoMantenimiento()
        {
            try
            {
                await _mantenimientoService.ActualizarEstadosCamionesAsync();
                TempData["Success"] = "Estados de camiones actualizados correctamente";
                return RedirectToAction("NotificacionesMantenimiento");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar estados de mantenimiento");
                await _auditService.LogExceptionAsync("ActualizarEstadoMantenimiento", ex.Message, User.Identity.Name);
                TempData["Error"] = "Error al actualizar estados de mantenimiento";
                return RedirectToAction("NotificacionesMantenimiento");
            }
        }

        [HttpPost]
        public async Task<IActionResult> RegistrarMantenimiento(Mantenimiento_Camiones mantenimiento)
        {
            try
            {
                if (ModelState.ContainsKey("Camion"))
                {
                    ModelState.Remove("Camion");
                }
                if (!ModelState.IsValid)
                {
                    return RedirectToAction("HistorialMantenimiento", new { idCamion = mantenimiento.id_camion });
                }

                // Usar el servicio de mantenimiento para programar y notificar
                var result = await _mantenimientoService.ProgramarMantenimientoAsync(mantenimiento);

                if (result)
                {
                    TempData["Success"] = "Mantenimiento programado exitosamente. Se han enviado notificaciones.";

                    // Registrar en auditoría
                    await _auditService.LogAuditEventAsync(
                        "MantenimientoRegistrar",
                        $"Registro de mantenimiento para camión ID: {mantenimiento.id_camion}",
                        User.Identity.Name ?? "sistema"
                    );
                }
                else
                {
                    TempData["Error"] = "Error al registrar el mantenimiento";
                }

                return RedirectToAction("HistorialMantenimiento", new { idCamion = mantenimiento.id_camion });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar mantenimiento");
                await _auditService.LogExceptionAsync("RegistrarMantenimiento", ex.Message, User.Identity.Name);
                TempData["Error"] = "Error al registrar el mantenimiento";
                return RedirectToAction("HistorialMantenimiento", new { idCamion = mantenimiento.id_camion });
            }
        }

        [HttpGet]
        public async Task<IActionResult> NotificacionesMantenimiento(int diasAntelacion = 30)
        {
            try
            {
                var notificaciones = await _mantenimientoService.GetMantenimientosProgramadosAsync(diasAntelacion);

                var camionesSinMantenimiento = await _adminService.GetCamionesAsync(estado: "activo");

                ViewBag.CamionesSinMantenimiento = camionesSinMantenimiento;
                ViewBag.DiasAntelacion = diasAntelacion;

                return View(notificaciones);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener notificaciones de mantenimiento");
                await _auditService.LogExceptionAsync("NotificacionesMantenimiento", ex.Message, User.Identity.Name);
                TempData["Error"] = "Error al obtener las notificaciones de mantenimiento";
                return RedirectToAction("Index");
            }
        }


        [HttpGet]
        public async Task<IActionResult> GenerarReporteCamiones(string estado = null)
        {
            try
            {
                var camiones = await _adminService.GetCamionesAsync(estado: estado);

                ViewBag.Estado = estado;
                ViewBag.FechaGeneracion = DateTime.Now;
                return View(camiones);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar reporte de camiones");
                await _auditService.LogExceptionAsync("ReporteCamiones", ex.Message, User.Identity.Name);
                TempData["Error"] = "Error al generar el reporte de camiones";
                return RedirectToAction("ListarCamiones");
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExportarReporteCamionesPDF(string estado = null)
        {
            try
            {
                var camiones = await _adminService.GetCamionesAsync(estado: estado);

                byte[] pdfBytes = await _reportService.GenerarReporteCamionesPdf(camiones.ToList(), estado);

                string estadoTexto = string.IsNullOrEmpty(estado) ? "todos" : estado;
                string fileName = $"Reporte_Camiones_{estadoTexto}_{DateTime.Now:yyyyMMdd}.pdf";

                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al exportar reporte de camiones a PDF");
                await _auditService.LogExceptionAsync("ExportarReporteCamionesPDF", ex.Message, User.Identity.Name);
                TempData["Error"] = "Error al generar el PDF de camiones";
                return RedirectToAction("GenerarReporteCamiones", new { estado });
            }
        }

        #endregion

        #region Gestión de Choferes

        [HttpGet]
        public async Task<IActionResult> ListarChoferes(string filtro = null, string estado = null)
        {
            try
            {
                var choferes = await _adminService.GetChoferesAsync(filtro, estado);

                ViewBag.Filtro = filtro;
                ViewBag.Estado = estado;
                return View(choferes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al listar choferes");
                await _auditService.LogExceptionAsync("ListarChoferes", ex.Message, User.Identity.Name);
                TempData["Error"] = "Error al cargar la lista de choferes";
                return View(new List<ChoferViewModel>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> RegistrarChofer()
        {
            try
            {
                var usuarios = await _adminService.GetUsuariosSinChoferAsync();
                ViewBag.Usuarios = usuarios;
                return View(new Choferes { estado = "activo" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar la página de registro de chofer");
                await _auditService.LogExceptionAsync("RegistrarChoferForm", ex.Message, User.Identity.Name);
                TempData["Error"] = "Error al cargar los datos necesarios";
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        public async Task<IActionResult> RegistrarChofer(Choferes chofer)
        {
            try
            {
                if (ModelState.ContainsKey("Usuario"))
                {
                    ModelState.Remove("Usuario");
                }
                // Verificación de datos básica
                if (!ModelState.IsValid)
                {
                    var usuarios = await _adminService.GetUsuariosSinChoferAsync();
                    ViewBag.Usuarios = usuarios;
                    return View(chofer);
                }

                // Intenta registrar el chofer
                var result = await _adminService.RegistrarChoferAsync(chofer);

                if (result.success)
                {
                    TempData["Success"] = "Chofer registrado exitosamente";
                    await _auditService.LogAuditEventAsync(
                        "ChoferCreate",
                        $"Registro exitoso de chofer: {chofer.nombreCompleto}",
                         User.Identity?.Name ?? "sistema"  
                    );
                    return RedirectToAction("ListarChoferes");
                }
                else
                {
                    // Mejora: Obtén el mensaje específico del error desde el servicio
                    TempData["Error"] = "Error al registrar el chofer. Verifique que la cédula y licencia no estén duplicadas.";
                    var usuarios = await _adminService.GetUsuariosSinChoferAsync();
                    ViewBag.Usuarios = usuarios;
                    return View(chofer);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar chofer");
                await _auditService.LogExceptionAsync("RegistrarChofer", ex.Message, User.Identity.Name ?? "sistema");

                // Mejora: Muestra un mensaje más específico basado en la excepción
                TempData["Error"] = $"Error al registrar el chofer: {ex.Message}";

                var usuarios = await _adminService.GetUsuariosSinChoferAsync();
                ViewBag.Usuarios = usuarios;
                return View(chofer);
            }
        }

        [HttpGet]
        public async Task<IActionResult> ActualizarDatosChofer(int id)
        {
            try
            {
                var chofer = await _adminService.GetChoferByIdAsync(id);

                if (chofer == null)
                {
                    TempData["Error"] = "Chofer no encontrado";
                    return RedirectToAction("ListarChoferes");
                }

                var usuarios = await _adminService.GetUsuariosDisponiblesParaChoferAsync(id);
                ViewBag.Usuarios = usuarios;

                return View(chofer);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener datos del chofer");
                await _auditService.LogExceptionAsync("ActualizarDatosChoferForm", ex.Message, User.Identity.Name);
                TempData["Error"] = "Error al obtener datos del chofer";
                return RedirectToAction("ListarChoferes");
            }
        }

        [HttpPost]
        public async Task<IActionResult> ActualizarDatosChofer(Choferes chofer)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var usuarios = await _adminService.GetUsuariosDisponiblesParaChoferAsync(chofer.id_chofer);
                    ViewBag.Usuarios = usuarios;
                    return View(chofer);
                }

                var result = await _adminService.ActualizarDatosChoferAsync(chofer);

                if (result)
                {
                    TempData["Success"] = "Datos del chofer actualizados exitosamente";

                    // Registrar en auditoría
                    await _auditService.LogAuditEventAsync(
                        "ChoferUpdate",
                        $"Actualización exitosa de chofer ID: {chofer.id_chofer}",
                        User.Identity.Name
                    );

                    return RedirectToAction("ListarChoferes");
                }
                else
                {
                    TempData["Error"] = "Error al actualizar los datos del chofer";

                    var usuarios = await _adminService.GetUsuariosDisponiblesParaChoferAsync(chofer.id_chofer);
                    ViewBag.Usuarios = usuarios;
                    return View(chofer);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar datos del chofer");
                await _auditService.LogExceptionAsync("ActualizarDatosChofer", ex.Message, User.Identity.Name);
                TempData["Error"] = "Error al actualizar los datos del chofer";

                var usuarios = await _adminService.GetUsuariosDisponiblesParaChoferAsync(chofer.id_chofer);
                ViewBag.Usuarios = usuarios;
                return View(chofer);
            }
        }

        [HttpPost]
        public async Task<IActionResult> DesactivarChofer(int id)
        {
            try
            {
                var result = await _adminService.DesactivarChoferAsync(id);

                if (result)
                {
                    TempData["Success"] = "Chofer desactivado exitosamente";

                    // Registrar en auditoría
                    await _auditService.LogAuditEventAsync(
                        "ChoferDesactivar",
                        $"Desactivación exitosa de chofer ID: {id}",
                        User.Identity.Name
                    );
                }
                else
                {
                    TempData["Error"] = "Error al desactivar el chofer";
                }

                return RedirectToAction("ListarChoferes");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al desactivar chofer");
                await _auditService.LogExceptionAsync("DesactivarChofer", ex.Message, User.Identity.Name);
                TempData["Error"] = "Error al desactivar el chofer";
                return RedirectToAction("ListarChoferes");
            }
        }

        [HttpPost]
        public async Task<IActionResult> ActivarChofer(int id)
        {
            try
            {
                var result = await _adminService.ActivarChoferAsync(id);

                if (result)
                {
                    TempData["Success"] = "Chofer activado exitosamente";

                    // Registrar en auditoría
                    await _auditService.LogAuditEventAsync(
                        "ChoferActivar",
                        $"Activación exitosa de chofer ID: {id}",
                        User.Identity.Name
                    );
                }
                else
                {
                    TempData["Error"] = "Error al activar el chofer";
                }

                return RedirectToAction("ListarChoferes");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al activar chofer");
                await _auditService.LogExceptionAsync("ActivarChofer", ex.Message, User.Identity.Name);
                TempData["Error"] = "Error al activar chofer";
                return RedirectToAction("ListarChoferes");
            }
        }

        [HttpPost]
        public async Task<IActionResult> EliminarChofer(int id)
        {
            try
            {
                var result = await _adminService.EliminarChoferAsync(id);

                if (result)
                {
                    TempData["Success"] = "Chofer eliminado exitosamente";

                    // Registrar en auditoría
                    await _auditService.LogAuditEventAsync(
                        "ChoferEliminar",
                        $"Eliminación exitosa de chofer ID: {id}",
                        User.Identity.Name
                    );
                }
                else
                {
                    TempData["Error"] = "No se puede eliminar: Chofer tiene registros asociados";
                }

                return RedirectToAction("ListarChoferes");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar chofer");
                await _auditService.LogExceptionAsync("EliminarChofer", ex.Message, User.Identity.Name);
                TempData["Error"] = "Error al eliminar el chofer";
                return RedirectToAction("ListarChoferes");
            }
        }

        [HttpGet]
        public async Task<IActionResult> GenerarReporteChoferes(string estado = null)
        {
            try
            {
                var choferes = await _adminService.GenerarReporteChoferesAsync(estado);

                ViewBag.Estado = estado;
                ViewBag.FechaGeneracion = DateTime.Now;
                return View(choferes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar reporte de choferes");
                await _auditService.LogExceptionAsync("ReporteChoferes", ex.Message, User.Identity.Name);
                TempData["Error"] = "Error al generar el reporte de choferes";
                return RedirectToAction("ListarChoferes");
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExportarReporteChoferesPDF(string estado = null)
        {
            try
            {
                var choferes = await _adminService.GenerarReporteChoferesAsync(estado);

                byte[] pdfBytes = await _reportService.GenerarReporteChoferesPdf(choferes, estado);

                string estadoTexto = string.IsNullOrEmpty(estado) ? "todos" : estado;
                string fileName = $"Reporte_Choferes_{estadoTexto}_{DateTime.Now:yyyyMMdd}.pdf";

                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al exportar reporte de choferes a PDF");
                await _auditService.LogExceptionAsync("ExportarReporteChoferesPDF", ex.Message, User.Identity.Name);
                TempData["Error"] = "Error al generar el PDF de choferes";
                return RedirectToAction("GenerarReporteChoferes", new { estado });
            }
        }
        
        [HttpGet]
        public async Task<IActionResult> ObtenerDocumentosChofer(int idChofer)
        {
            try
            {
                var documentos = await _adminService.GetDocumentosChoferAsync(idChofer);
                var chofer = await _adminService.GetChoferByIdAsync(idChofer);

                if (chofer == null)
                {
                    TempData["Error"] = "Chofer no encontrado";
                    return RedirectToAction("ListarChoferes");
                }

                ViewBag.IdChofer = idChofer;
                ViewBag.Chofer = chofer;
                return View(documentos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener documentos del chofer");
                await _auditService.LogExceptionAsync("ObtenerDocumentosChofer", ex.Message, User.Identity.Name);
                TempData["Error"] = "Error al obtener los documentos del chofer";
                return RedirectToAction("ListarChoferes");
            }
        }

        [HttpGet]
        public async Task<IActionResult> ActualizarDocumentos(int idChofer)
        {
            try
            {
                var chofer = await _adminService.GetChoferByIdAsync(idChofer);

                if (chofer == null)
                {
                    TempData["Error"] = "Chofer no encontrado";
                    return RedirectToAction("ListarChoferes");
                }

                var documentos = await _adminService.GetDocumentosChoferAsync(idChofer);

                ViewBag.Chofer = chofer;
                ViewBag.Documentos = documentos;
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar datos para actualizar documentos");
                await _auditService.LogExceptionAsync("ActualizarDocumentosForm", ex.Message, User.Identity.Name);
                TempData["Error"] = "Error al cargar los datos necesarios";
                return RedirectToAction("ListarChoferes");
            }
        }

        [HttpPost]
        public async Task<IActionResult> ActualizarDocumentos(Documentos documento)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return RedirectToAction("ObtenerDocumentosChofer", new { idChofer = documento.id_chofer });
                }

                var result = await _adminService.ActualizarDocumentoAsync(documento);

                if (result)
                {
                    TempData["Success"] = "Documento actualizado exitosamente";

                    // Registrar en auditoría
                    await _auditService.LogAuditEventAsync(
                        "DocumentoActualizar",
                        $"Actualización de documento ID: {documento.id_documento}, tipo: {documento.tipo_documento}",
                        User.Identity.Name
                    );
                }
                else
                {
                    TempData["Error"] = "Error al actualizar el documento";
                }

                return RedirectToAction("ObtenerDocumentosChofer", new { idChofer = documento.id_chofer });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar documentos");
                await _auditService.LogExceptionAsync("ActualizarDocumento", ex.Message, User.Identity.Name);
                TempData["Error"] = "Error al actualizar los documentos";
                return RedirectToAction("ObtenerDocumentosChofer", new { idChofer = documento.id_chofer });
            }
        }

        [HttpGet]
        public async Task<IActionResult> MonitorearVencimientos(int diasPrevios = 30)
        {
            try
            {
                var documentos = await _adminService.MonitorearVencimientosAsync(diasPrevios);
                var licencias = await _adminService.GetLicenciasProximasVencerAsync(diasPrevios);

                ViewBag.DiasPrevios = diasPrevios;
                ViewBag.Licencias = licencias;
                return View(documentos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al monitorear vencimientos");
                await _auditService.LogExceptionAsync("MonitorearVencimientos", ex.Message, User.Identity.Name);
                TempData["Error"] = "Error al monitorear los vencimientos";
                return RedirectToAction("Index");
            }
        }

        #endregion
    
    }
}