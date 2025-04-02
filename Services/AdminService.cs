using COMAVI_SA.Filters;
using COMAVI_SA.Models;
using COMAVI_SA.Repository;
using Hangfire.Logging;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using NPOI.SS.Formula.Functions;
using Org.BouncyCastle.Asn1.Pkcs;
using System;
using System.Collections;

namespace COMAVI_SA.Services
{
#nullable disable
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
#pragma warning disable CS0168 // Variable is declared but never used

    public interface IAdminService
    {

        //Inicio
        Task<AdminDashboardViewModel> GetDashboardDataAsync(bool forceRefresh = false);


        // Camiones
        Task<IEnumerable<CamionViewModel>> GetCamionesAsync(string filtro = null, string estado = null);
        Task<bool> RegistrarCamionAsync(Camiones camion);
        Task<bool> ActualizarCamionAsync(Camiones camion);
        Task<Camiones> GetCamionByIdAsync(int id);
        Task<bool> DesactivarCamionAsync(int id);
        Task<bool> ActivarCamionAsync(int id);
        Task<List<Mantenimiento_Camiones>> GetHistorialMantenimientoAsync(int idCamion);
        Task<bool> AsignarChoferAsync(int idCamion, int idChofer);
        Task<IEnumerable<Camiones>> GetCamionesActivosAsync();
        Task<bool> EliminarCamionAsync(int id);

        // Choferes
        Task<IEnumerable<ChoferViewModel>> GetChoferesAsync(string filtro = null, string estado = null);
        Task<(bool success, string message)> RegistrarChoferAsync(Choferes chofer);
        Task<List<Usuario>> GetUsuariosSinChoferAsync();
        Task<List<Documentos>> GetDocumentosChoferAsync(int idChofer);
        Task<Choferes> GetChoferByIdAsync(int id);
        Task<bool> ActualizarDocumentoAsync(Documentos documento);
        Task<List<DocumentoVencimientoViewModel>> MonitorearVencimientosAsync(int diasPrevios = 30);
        Task<List<ChoferViewModel>> GetLicenciasProximasVencerAsync(int diasPrevios = 30);
        Task<bool> ActualizarDatosChoferAsync(Choferes chofer);
        Task<List<Usuario>> GetUsuariosDisponiblesParaChoferAsync(int idChofer);
        Task<bool> DesactivarChoferAsync(int id);
        Task<bool> ActivarChoferAsync(int id);
        Task<bool> EliminarChoferAsync(int id);
        Task<List<ChoferViewModel>> GenerarReporteChoferesAsync(string estado = null);

        // Usuarios
        Task<IEnumerable<UsuarioAdminViewModel>> GetUsuariosAsync(string filtro = null, string rol = null);
        Task<Usuario> GetUsuarioByIdAsync(int id);
        Task<bool> ActualizarUsuarioAsync(EditarUsuarioViewModel usuario);
        Task<bool> CambiarEstadoUsuarioAsync(int id, string estado);
        Task<bool> ResetearContrasenaAsync(int id);
        Task<List<SesionActivaViewModel>> GetSesionesActivasAsync(int idUsuario);
        Task<bool> CerrarSesionAsync(string tokenSesion);
        Task<List<UsuarioAdminViewModel>> GenerarReporteUsuariosAsync(string rol = null);
        Task<List<SolicitudMantenimientoViewModel>> GetSolicitudesMantenimientoPendientesAsync();
        Task<bool> ProcesarSolicitudMantenimientoAsync(int idSolicitud, int idAdmin, string estado,
            string descripcion = null, decimal? costo = null, string moneda = "CRC", string detallesCosto = null);

        // Dashboard y Reportes
        Task<DashboardViewModel> GetDashboardIndicadoresAsync();
        Task<List<GraficoDataViewModel>> GetMantenimientosPorMesAsync(int? anio = null);
        Task<List<GraficoDataViewModel>> GetEstadosCamionesAsync();
        Task<List<GraficoDataViewModel>> GetEstadosDocumentosAsync();
        Task<List<ActividadRecienteViewModel>> GetActividadesRecientesAsync(int cantidad = 10);
        Task<List<MantenimientoReporteViewModel>> GenerarReporteMantenimientosAsync(DateTime? fechaInicio, DateTime? fechaFin);

        // MÉTODOS INTERNOS DE ADMIN SERVICE NO USAR EN OTRO LADO (MANEJADOR DE CACHE TEMPORAL Y ESPACIO DEL MISMO)!!!
        Task<T> GetOrSetCacheAsync<T>(string cacheKey, Func<Task<T>> dataFactory, TimeSpan absoluteExpiration, TimeSpan? slidingExpiration = null, bool bypassCache = false, int sizedCache = 1);

    }

    [VerificarAutenticacion]
    public class AdminService : IAdminService
    {
        private readonly IDatabaseRepository _databaseRepository;
        private readonly IMemoryCache _cache;
        private readonly ICacheKeyTracker _cacheKeyTracker;
        private readonly IDistributedLockProvider _lockProvider;
        private readonly IEmailService _emailService;
        private readonly IPdfService _pdfService;
        private readonly IUserService _userService;
        private readonly INotificationService _notificationService;
        private readonly IReportService _reportService;

        public AdminService(
            IDatabaseRepository databaseRepository,
            IMemoryCache cache,
            ICacheKeyTracker cacheKeyTracker,
            IDistributedLockProvider lockProvider,
            IEmailService emailService,
            IPdfService pdfService,
            IUserService userService,
            INotificationService notificationService,
            IReportService reportService)
        {
            _databaseRepository = databaseRepository;
            _cache = cache;
            _cacheKeyTracker = cacheKeyTracker;
            _lockProvider = lockProvider;
            _emailService = emailService;
            _pdfService = pdfService;
            _userService = userService;
            _notificationService = notificationService;
            _reportService = reportService;
        }

        #region Inicio

        public async Task<AdminDashboardViewModel> GetDashboardDataAsync(bool forceRefresh = false)
        {
            string cacheKey = $"AdminDashboard_{DateTime.Now:yyyyMMdd_HH}";

            return await GetOrSetCacheAsync<AdminDashboardViewModel>(
                cacheKey,
                async () => {
                    // Función para obtener los datos frescos
                    var estadisticas = await _databaseRepository.ExecuteScalarProcedureAsync<DashboardStats>(
                        "sp_ObtenerDatosDashboardAdmin",
                        new
                        {
                            LimiteCamiones = 5,
                            LimiteChoferes = 5,
                            LimiteDocumentos = 5,
                            DiasPreviosVencimiento = 30
                        }
                    );

                    var camiones = await _databaseRepository.ExecuteQueryProcedureAsync<Camiones>(
                        "sp_ObtenerCamionesRecientes",
                        new { Limite = 5 }
                    );

                    var choferes = await _databaseRepository.ExecuteQueryProcedureAsync<Choferes>(
                        "sp_ObtenerChoferesActivos",
                        new { Limite = 5 }
                    );

                    var documentos = await _databaseRepository.ExecuteQueryProcedureAsync<DocumentoVencimientoViewModel>(
                        "sp_ObtenerDocumentosProximosVencer",
                        new { dias_anticipacion = 30, Limite = 5 }
                    );

                    return new AdminDashboardViewModel
                    {
                        TotalCamiones = estadisticas.TotalCamiones,
                        CamionesActivos = estadisticas.CamionesActivos,
                        TotalChoferes = estadisticas.TotalChoferes,
                        ChoferesActivos = estadisticas.ChoferesActivos,
                        TotalUsuarios = estadisticas.TotalUsuarios,
                        UsuariosActivos = estadisticas.UsuariosActivos,
                        DocumentosProximosVencer = documentos.Count(),
                        DocumentosProximosVencimiento = documentos.ToList(),
                        Camiones = camiones.ToList(),
                        Choferes = choferes.ToList()
                    };
                },
                TimeSpan.FromHours(1),        // Expiración absoluta
                TimeSpan.FromMinutes(10),     // Expiración deslizante
                bypassCache: forceRefresh     // Permitir bypass de caché
            );
        }

        #endregion

        #region Gestión de Camiones

        public async Task<IEnumerable<CamionViewModel>> GetCamionesAsync(string filtro = null, string estado = null)
        {
            string cacheKey = $"Camiones_Filtro_{filtro ?? "none"}_Estado_{estado ?? "all"}";

            return await GetOrSetCacheAsync(
                cacheKey,
                async () => {
                    var camiones = await _databaseRepository.ExecuteQueryProcedureAsync<CamionViewModel>(
                        "sp_BuscarCamiones",
                        new { filtro, estado }
                    );
                    //return camiones;
                    return camiones.ToList();
                },
                TimeSpan.FromMinutes(5)   // Expiración absoluta de 5 minutos
            );

        }

        public async Task<bool> RegistrarCamionAsync(Camiones camion)
        {
            try
            {
                await _databaseRepository.ExecuteNonQueryProcedureAsync(
                    "sp_RegistrarCamion",
                    new
                    {
                        marca = camion.marca,
                        modelo = camion.modelo,
                        anio = camion.anio,
                        numero_placa = camion.numero_placa,
                        estado = camion.estado ?? "activo",
                        chofer_asignado = camion.chofer_asignado ?? (object)DBNull.Value
                    }
                );

                // Invalidar cachés relacionadas
                await InvalidarCacheCamionesAsync();

                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public async Task<bool> ActualizarCamionAsync(Camiones camion)
        {
            try
            {
                var result = await _databaseRepository.ExecuteNonQueryProcedureAsync(
                    "sp_ActualizarCamion",
                    new
                    {
                        id_camion = camion.id_camion,
                        marca = camion.marca,
                        modelo = camion.modelo,
                        anio = camion.anio,
                        numero_placa = camion.numero_placa,
                        estado = camion.estado,
                        chofer_asignado = camion.chofer_asignado ?? (object)DBNull.Value
                    }
                );

                // Si el estado cambió a "mantenimiento", registrarlo en el historial
                if (camion.estado == "mantenimiento")
                {
                    await _databaseRepository.ExecuteNonQueryProcedureAsync(
                        "sp_RegistrarMantenimiento",
                        new
                        {
                            id_camion = camion.id_camion,
                            descripcion = "Puesta en mantenimiento por administrador",
                            fecha_mantenimiento = DateTime.Now,
                            costo = 0
                        }
                    );
                }

                // Invalidar cachés relacionadas
                await InvalidarCacheCamionesAsync();

                return result > 0;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public async Task<Camiones> GetCamionByIdAsync(int id)
        {
            string cacheKey = $"Camion_{id}";

            if (_cache.TryGetValue(cacheKey, out Camiones cachedCamion))
            {
                return cachedCamion;
            }

            var camion = await _databaseRepository.ExecuteScalarProcedureAsync<Camiones>(
                "sp_ObtenerCamionPorId",
                new { id_camion = id }
            );

            if (camion != null)
            {
                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(5))
                    .SetSize(1);

                _cache.Set(cacheKey, camion, cacheEntryOptions);
                _cacheKeyTracker.TrackKey(cacheKey);
            }

            return camion;
        }

        public async Task<bool> DesactivarCamionAsync(int id)
        {
            try
            {
                await _databaseRepository.ExecuteNonQueryProcedureAsync(
                    "sp_DesactivarCamion",
                    new { id_camion = id }
                );

                // Invalidar cachés
                _cache.Remove($"Camion_{id}");
                await InvalidarCacheCamionesAsync();

                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public async Task<bool> ActivarCamionAsync(int id)
        {
            try
            {
                await _databaseRepository.ExecuteNonQueryProcedureAsync(
                    "sp_ActivarCamion",
                    new { id_camion = id }
                );

                // Invalidar cachés
                _cache.Remove($"Camion_{id}");
                await InvalidarCacheCamionesAsync();

                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public async Task<List<Mantenimiento_Camiones>> GetHistorialMantenimientoAsync(int idCamion)
        {
            string cacheKey = $"HistorialMantenimiento_{idCamion}";

            return await GetOrSetCacheAsync(
                cacheKey,
                async () =>
                {
                    var historial = await _databaseRepository.ExecuteQueryProcedureAsync<Mantenimiento_Camiones>(
                        "sp_ObtenerHistorialMantenimiento",
                        new { id_camion = idCamion }
                    );
                    return historial.ToList();
                },
                TimeSpan.FromMinutes(10)
            );
        }

        public async Task<bool> AsignarChoferAsync(int idCamion, int idChofer)
        {
            try
            {
                // Utiliza el procedimiento almacenado sp_AsignarChofer
                await _databaseRepository.ExecuteNonQueryProcedureAsync(
                    "sp_AsignarChofer",
                    new
                    {
                        id_camion = idCamion,
                        id_chofer = idChofer
                    }
                );

                // Obtener información del chofer y camión para la notificación
                var chofer = await _databaseRepository.ExecuteScalarProcedureAsync<Choferes>(
                    "sp_ObtenerChoferConUsuario",
                    new { id_chofer = idChofer }
                );

                var camion = await GetCamionByIdAsync(idCamion);

                // Notificar al chofer sobre la asignación
                if (chofer != null && chofer.id_usuario.HasValue)
                {
                    var mensaje = $"Se le ha asignado el camión {camion.marca} {camion.modelo} (Placa: {camion.numero_placa})";
                    await _notificationService.CreateNotificationAsync(chofer.id_usuario.Value, "Asignación de Camión", mensaje);
                }

                // Invalidar cachés relacionadas
                await InvalidarCacheCamionesAsync();

                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public async Task<IEnumerable<Camiones>> GetCamionesActivosAsync()
        {
            string cacheKey = "CamionesActivos";

            return await GetOrSetCacheAsync(
                cacheKey,
                async () =>
                {
                    // Utiliza el procedimiento almacenado sp_ObtenerCamionesActivos
                    var camiones = await _databaseRepository.ExecuteQueryProcedureAsync<Camiones>(
                        "sp_ObtenerCamionesActivos"
                    );
                    return camiones;
                },
                TimeSpan.FromMinutes(5)
            );
        }

        public async Task<bool> EliminarCamionAsync(int id)
        {
            try
            {
                // Utiliza el procedimiento almacenado sp_EliminarCamion
                var result = await _databaseRepository.ExecuteNonQueryProcedureAsync(
                    "sp_EliminarCamion",
                    new { id_camion = id }
                );

                // Invalidar cachés
                _cache.Remove($"Camion_{id}");
                await InvalidarCacheCamionesAsync();

                return result > 0;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        private async Task InvalidarCacheCamionesAsync()
        {
#pragma warning disable CS0168 // Variable is declared but never used
            try
            {
                var cacheKeys = _cacheKeyTracker.GetKeysByPrefix("Camiones_");
                foreach (var key in cacheKeys)
                {
                    _cache.Remove(key);
                }

                var dashboardKeys = _cacheKeyTracker.GetKeysByPrefix("AdminDashboard_");
                foreach (var key in dashboardKeys)
                {
                    _cache.Remove(key);
                }
            }
            catch (Exception ex)
            {
                throw;
            }
#pragma warning restore CS0168 // Variable is declared but never used
        }

        #endregion

        #region Gestión de Choferes
        public async Task<IEnumerable<ChoferViewModel>> GetChoferesAsync(string filtro = null, string estado = null)
        {
            string cacheKey = $"Choferes_Filtro_{filtro ?? "none"}_Estado_{estado ?? "all"}";

            return await GetOrSetCacheAsync(
                cacheKey,
                async () =>
                {
                    // Usar el procedimiento almacenado optimizado
                    var choferes = await _databaseRepository.ExecuteQueryProcedureAsync<ChoferViewModel>(
                        "sp_BuscarChoferes",
                        new { filtro, estado }
                    );

                    return choferes;
                },
                TimeSpan.FromMinutes(5)
            );
        }

        public async Task<(bool success, string message)> RegistrarChoferAsync(Choferes chofer)
        {
            try
            {
                await _databaseRepository.ExecuteNonQueryProcedureAsync(
                    "sp_RegistrarChofer",
                    new
                    {
                        nombreCompleto = chofer.nombreCompleto,
                        edad = chofer.edad,
                        numero_cedula = chofer.numero_cedula,
                        licencia = chofer.licencia,
                        fecha_venc_licencia = chofer.fecha_venc_licencia,
                        estado = chofer.estado ?? "activo",
                        genero = chofer.genero,
                        id_usuario = chofer.id_usuario ?? (object)DBNull.Value
                    }
                );

                if (chofer.id_usuario.HasValue)
                {
                    await _notificationService.CreateNotificationAsync(
                        chofer.id_usuario.Value,
                        "Perfil de Chofer",
                        "Se ha creado su perfil de chofer en el sistema."
                    );
                }

                await InvalidarCacheChoferesAsync();
                return (true, "Chofer registrado exitosamente");
            }
            catch (SqlException sqlEx)
            {
                // Captura específicamente los mensajes de error SQL
                return (false, sqlEx.Message);
            }
            catch (Exception ex)
            {
                return (false, "Error al registrar el chofer: " + ex.Message);
            }
        }

        public async Task<List<Usuario>> GetUsuariosSinChoferAsync()
        {
            string cacheKey = "UsuariosSinChofer";

            return await GetOrSetCacheAsync(
                cacheKey,
                async () =>
                {
                    // Usa el procedimiento almacenado optimizado en lugar de la consulta directa
                    var usuarios = await _databaseRepository.ExecuteQueryProcedureAsync<Usuario>(
                        "sp_ObtenerUsuariosSinChofer"
                    );
                    return usuarios.ToList();
                },
                TimeSpan.FromMinutes(5)
            );
        }

        public async Task<List<Documentos>> GetDocumentosChoferAsync(int idChofer)
        {
            string cacheKey = $"DocumentosChofer_{idChofer}";

            return await GetOrSetCacheAsync(
                cacheKey,
                async () =>
                {
                    var documentos = await _databaseRepository.ExecuteQueryProcedureAsync<Documentos>(
                        "sp_ObtenerDocumentosChofer",
                        new { id_chofer = idChofer }
                    );
                    return documentos.ToList();
                },
                TimeSpan.FromMinutes(5)
            );
        }

        public async Task<Choferes> GetChoferByIdAsync(int id)
        {
            string cacheKey = $"Chofer_{id}";

            return await GetOrSetCacheAsync(
                cacheKey,
                async () =>
                {
                    // Usa el procedimiento almacenado optimizado
                    var chofer = await _databaseRepository.ExecuteScalarProcedureAsync<Choferes>(
                        "sp_ObtenerChoferPorId",
                        new { id_chofer = id }
                    );
                    return chofer;
                },
                TimeSpan.FromMinutes(5)
            );
        }

        public async Task<bool> ActualizarDocumentoAsync(Documentos documento)
        {
#pragma warning disable CS0168 // Variable is declared but never used
            try
            {
                // Utiliza el procedimiento almacenado sp_ActualizarDocumento
                await _databaseRepository.ExecuteNonQueryProcedureAsync(
                    "sp_ActualizarDocumento",
                    new
                    {
                        id_documento = documento.id_documento,
                        id_chofer = documento.id_chofer,
                        tipo_documento = documento.tipo_documento,
                        fecha_emision = documento.fecha_emision,
                        fecha_vencimiento = documento.fecha_vencimiento,
                        estado_validacion = documento.estado_validacion ?? "pendiente",
                        ruta_archivo = documento.ruta_archivo,
                        contenido_archivo = documento.contenido_archivo,
                        tipo_mime = documento.tipo_mime,
                        tamano_archivo = documento.tamano_archivo,
                        hash_documento = documento.hash_documento
                    }
                );

                // Notificar al chofer si se actualiza o verifica un documento
                var chofer = await _databaseRepository.ExecuteScalarProcedureAsync<Choferes>(
                    "sp_ObtenerChoferConUsuario",
                    new { id_chofer = documento.id_chofer }
                );

                if (chofer != null && chofer.id_usuario.HasValue)
                {
                    string mensaje = $"Su documento '{documento.tipo_documento}' ha sido {documento.estado_validacion}.";
                    await _notificationService.CreateNotificationAsync(
                        chofer.id_usuario.Value,
                        "Actualización de Documento",
                        mensaje
                    );
                }

                // Invalidar cachés relacionadas
                _cache.Remove($"DocumentosChofer_{documento.id_chofer}`");

                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public async Task<List<DocumentoVencimientoViewModel>> MonitorearVencimientosAsync(int diasPrevios = 30)
        {
            string cacheKey = $"Vencimientos_{diasPrevios}";

            return await GetOrSetCacheAsync(
                cacheKey,
                async () =>
                {
                    var documentos = await _databaseRepository.ExecuteQueryProcedureAsync<DocumentoVencimientoViewModel>(
                        "sp_MonitorearVencimientos",
                        new { dias_previos = diasPrevios }
                    );
                    return documentos.ToList();
                },
                TimeSpan.FromHours(6)
            );
        }

        public async Task<List<ChoferViewModel>> GetLicenciasProximasVencerAsync(int diasPrevios = 30)
        {
            string cacheKey = $"LicenciasVencimiento_{diasPrevios}";

            return await GetOrSetCacheAsync(
                cacheKey,
                async () =>
                {
                    // Usa el procedimiento almacenado optimizado
                    var licencias = await _databaseRepository.ExecuteQueryProcedureAsync<ChoferViewModel>(
                        "sp_ObtenerLicenciasProximasVencer",
                        new { dias_previos = diasPrevios }
                    );

                    // No necesitamos calcular el estado_licencia manualmente ya que
                    // el procedimiento almacenado ya lo hace por nosotros
                    return licencias.ToList();
                },
                TimeSpan.FromHours(6)
            );
        }

        public async Task<bool> ActualizarDatosChoferAsync(Choferes chofer)
        {
            try
            {
                // Utiliza el procedimiento almacenado sp_ActualizarDatosChofer
                await _databaseRepository.ExecuteNonQueryProcedureAsync(
                    "sp_ActualizarDatosChofer",
                    new
                    {
                        id_chofer = chofer.id_chofer,
                        nombreCompleto = chofer.nombreCompleto,
                        edad = chofer.edad,
                        numero_cedula = chofer.numero_cedula,
                        licencia = chofer.licencia,
                        fecha_venc_licencia = chofer.fecha_venc_licencia,
                        genero = chofer.genero,
                        id_usuario = chofer.id_usuario ?? (object)DBNull.Value
                    }
                );

                // Verificar si se cambió el usuario asociado
                if (chofer.id_usuario.HasValue)
                {
                    await _notificationService.CreateNotificationAsync(
                        chofer.id_usuario.Value,
                        "Actualización de Perfil",
                        "Su perfil de chofer ha sido actualizado por un administrador."
                    );
                }

                // Invalidar cachés relacionadas
                _cache.Remove($"Chofer_{chofer.id_chofer}");
                await InvalidarCacheChoferesAsync();

                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public async Task<List<Usuario>> GetUsuariosDisponiblesParaChoferAsync(int idChofer)
        {
            string cacheKey = $"UsuariosDisponiblesChofer_{idChofer}";

            return await GetOrSetCacheAsync(
                cacheKey,
                async () =>
                {
                    // Obtener chofer actual para preservar su id_usuario si lo tiene
                    var chofer = await GetChoferByIdAsync(idChofer);

                    // Usa el procedimiento almacenado optimizado
                    var usuarios = await _databaseRepository.ExecuteQueryProcedureAsync<Usuario>(
                        "sp_ObtenerUsuariosDisponiblesParaChofer",
                        new { id_chofer = idChofer, id_usuario = chofer?.id_usuario }
                    );

                    return usuarios.ToList();
                },
                TimeSpan.FromMinutes(5)
            );
        }

        public async Task<bool> DesactivarChoferAsync(int id)
        {
            try
            {
                // Utiliza el procedimiento almacenado sp_DesactivarChofer
                await _databaseRepository.ExecuteNonQueryProcedureAsync(
                    "sp_DesactivarChofer",
                    new { id_chofer = id }
                );

                // Invalidar cachés relacionadas
                _cache.Remove($"Chofer_{id}");
                await InvalidarCacheChoferesAsync();

                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public async Task<bool> ActivarChoferAsync(int id)
        {
            try
            {
                // Usa el procedimiento almacenado optimizado
                var result = await _databaseRepository.ExecuteScalarProcedureAsync<int>(
                    "sp_ActivarChofer",
                    new { id_chofer = id }
                );

                // Invalidar cachés relacionadas solo si hubo cambios
                if (result != 0) // Si devuelve 0, no hubo cambios
                {
                    _cache.Remove($"Chofer_{id}");
                    await InvalidarCacheChoferesAsync();
                }

                return result >= 0; // Retorna true si fue exitoso (0 o 1), false si hubo error (-1)
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public async Task<bool> EliminarChoferAsync(int id)
        {
            try
            {
                // Utiliza el procedimiento almacenado sp_EliminarChofer
                var result = await _databaseRepository.ExecuteNonQueryProcedureAsync(
                    "sp_EliminarChofer",
                    new { id_chofer = id }
                );

                // Invalidar cachés relacionadas
                _cache.Remove($"Chofer_{id}");
                _cache.Remove($"DocumentosChofer_{id}");
                await InvalidarCacheChoferesAsync();

                return result > 0;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public async Task<List<ChoferViewModel>> GenerarReporteChoferesAsync(string estado = null)
        {
            try
            {
                // Utiliza el procedimiento almacenado sp_GenerarReporteChoferes
                var choferes = await _databaseRepository.ExecuteQueryProcedureAsync<ChoferViewModel>(
                    "sp_GenerarReporteChoferes",
                    new { estado }
                );

                // Calcular estado de licencia
                foreach (var chofer in choferes)
                {
                    int diasParaVencimiento = (int)(chofer.fecha_venc_licencia - DateTime.Now).TotalDays;
                    chofer.estado_licencia = diasParaVencimiento <= 0 ? "Vencida" :
                                        diasParaVencimiento <= 30 ? "Por vencer" : "Vigente";
                }

                return choferes.ToList();
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        private async Task InvalidarCacheChoferesAsync()
        {
            try
            {
                var cacheKeys = _cacheKeyTracker.GetKeysByPrefix("Choferes_");
                foreach (var key in cacheKeys)
                {
                    _cache.Remove(key);
                }

                var dashboardKeys = _cacheKeyTracker.GetKeysByPrefix("AdminDashboard_");
                foreach (var key in dashboardKeys)
                {
                    _cache.Remove(key);
                }

                _cache.Remove("ChoferesActivos");
            }
            catch (Exception ex)
            {
                throw;
            }
        }
        #endregion

        #region Gestión de Usuarios

        public async Task<IEnumerable<UsuarioAdminViewModel>> GetUsuariosAsync(string filtro = null, string rol = null)
        {
            string cacheKey = $"Usuarios_Filtro_{filtro ?? "none"}_Rol_{rol ?? "all"}";

            return await GetOrSetCacheAsync(
                cacheKey,
                async () =>
                {
                    // Usar el procedimiento almacenado optimizado
                    var usuarios = await _databaseRepository.ExecuteQueryProcedureAsync<UsuarioAdminViewModel>(
                        "sp_BuscarUsuarios",
                        new { filtro, rol }
                    );

                    return usuarios;
                },
                TimeSpan.FromMinutes(5)
            );
        }
        
        public async Task<Usuario> GetUsuarioByIdAsync(int id)
        {
            string cacheKey = $"Usuario_{id}";

            return await GetOrSetCacheAsync(
                cacheKey,
                async () =>
                {
                    // Usar el procedimiento almacenado optimizado
                    var usuario = await _databaseRepository.ExecuteScalarProcedureAsync<Usuario>(
                        "sp_ObtenerUsuarioPorId",
                        new { id_usuario = id }
                    );
                    return usuario;
                },
                TimeSpan.FromMinutes(5)
            );
        }

        public async Task<bool> ActualizarUsuarioAsync(COMAVI_SA.Models.EditarUsuarioViewModel model)
        {
            try
            {
                // Usar el procedimiento almacenado optimizado
                var result = await _databaseRepository.ExecuteScalarProcedureAsync<int>(
                    "sp_ActualizarUsuario",
                    new
                    {
                        id_usuario = model.id_usuario,
                        nombre_usuario = model.nombre_usuario,
                        rol = model.rol
                    }
                );
                // Interpretar resultado
                if (result == 1 || result == 0) // Éxito
                {
                    // Notificar al usuario sobre los cambios en su cuenta
                    await _notificationService.CreateNotificationAsync(
                        model.id_usuario,
                        "Actualización de Cuenta",
                        "Un administrador ha actualizado la información de tu cuenta."
                    );

                    // Invalidar cachés relacionadas
                    _cache.Remove($"Usuario_{model.id_usuario}");
                    await InvalidarCacheUsuariosAsync();

                    return true;
                }
                else // Otros errores
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public async Task<bool> CambiarEstadoUsuarioAsync(int id, string estado)
        {
            try
            {
                // Usar el procedimiento almacenado optimizado
                var result = await _databaseRepository.ExecuteScalarProcedureAsync<int>(
                    "sp_CambiarEstadoUsuario",
                    new { id_usuario = id, estado }
                );

                if (result == 1 || result == 0) // Éxito
                {
                    // Notificar al usuario sobre el cambio de estado
                    string mensaje = estado == "verificado"
                        ? "Tu cuenta ha sido verificada y activada."
                        : "Tu cuenta ha sido desactivada. Contacta al administrador para más información.";

                    await _notificationService.CreateNotificationAsync(
                        id,
                        "Estado de Cuenta",
                        mensaje
                    );

                    // Invalidar cachés relacionadas
                    _cache.Remove($"Usuario_{id}");
                    await InvalidarCacheUsuariosAsync();

                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public async Task<bool> ResetearContrasenaAsync(int id)
        {
#pragma warning disable CS0168 // Variable is declared but never used
            try
            {
                // Generar contraseña temporal
                string tempPassword = Guid.NewGuid().ToString().Substring(0, 8);

                // Hash de la contraseña
                string hashedPassword = _userService.HashPassword(tempPassword);

                // Usar el procedimiento almacenado optimizado
                var result = await _databaseRepository.ExecuteScalarProcedureAsync<dynamic>(
                    "sp_ResetearContrasena",
                    new { id_usuario = id, password_hash = hashedPassword }
                );

                // Verificar resultado
                if (result.correo_electronico != null)
                {
                    // Enviar email con la contraseña temporal
                    await _emailService.EnviarCorreoAsync(
                        result.correo_electronico,
                        "Reseteo de Contraseña",
                        $"Se ha reseteado su contraseña en el sistema COMAVI. Su contraseña temporal es: {tempPassword}. " +
                        "Por favor, cambie su contraseña al iniciar sesión."
                    );

                    // Notificar al usuario
                    await _notificationService.CreateNotificationAsync(
                        id,
                        "Reseteo de Contraseña",
                        "Se ha reseteado tu contraseña. Verifica tu correo electrónico para obtener la contraseña temporal."
                    );

                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public async Task<List<SesionActivaViewModel>> GetSesionesActivasAsync(int idUsuario)
        {
            string cacheKey = $"SesionesActivas_{idUsuario}";

            return await GetOrSetCacheAsync(
                cacheKey,
                async () =>
                {
                    // Usar el procedimiento almacenado optimizado
                    var sesiones = await _databaseRepository.ExecuteQueryProcedureAsync<SesionActivaViewModel>(
                        "sp_ObtenerSesionesActivas",
                        new { id_usuario = idUsuario }
                    );

                    return sesiones.ToList();
                },
                TimeSpan.FromMinutes(1) // Tiempo corto porque esta información cambia con frecuencia
            );
        }

        public async Task<bool> CerrarSesionAsync(string tokenSesion)
        {
            try
            {
                // Usar el procedimiento almacenado optimizado
                var result = await _databaseRepository.ExecuteScalarProcedureAsync<dynamic>(
                    "sp_CerrarSesion",
                    new { token_sesion = tokenSesion }
                );

                // Verificar resultado
                if (result?.id_usuario != null)
                {
                    int idUsuario = result.id_usuario;

                    // Notificar al usuario
                    await _notificationService.CreateNotificationAsync(
                        idUsuario,
                        "Cierre de Sesión",
                        "Un administrador ha cerrado una de tus sesiones activas por motivos de seguridad."
                    );

                    // Invalidar cachés relacionadas
                    _cache.Remove($"SesionesActivas_{idUsuario}");

                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public async Task<List<UsuarioAdminViewModel>> GenerarReporteUsuariosAsync(string rol = null)
        {
            try
            {
                // Usar el procedimiento almacenado optimizado
                var usuarios = await _databaseRepository.ExecuteQueryProcedureAsync<UsuarioAdminViewModel>(
                    "sp_GenerarReporteUsuarios",
                    new { rol }
                );

                return usuarios.ToList();
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        private async Task InvalidarCacheUsuariosAsync()
        {
            try
            {
                var cacheKeys = _cacheKeyTracker.GetKeysByPrefix("Usuarios_");
                foreach (var key in cacheKeys)
                {
                    _cache.Remove(key);
                }

                var dashboardKeys = _cacheKeyTracker.GetKeysByPrefix("AdminDashboard_");
                foreach (var key in dashboardKeys)
                {
                    _cache.Remove(key);
                }
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public async Task<List<SolicitudMantenimientoViewModel>> GetSolicitudesMantenimientoPendientesAsync()
        {
            string cacheKey = "SolicitudesMantenimientoPendientes";
            return await GetOrSetCacheAsync(
                cacheKey,
                async () =>
                {
                    var solicitudes = await _databaseRepository.ExecuteQueryProcedureAsync<SolicitudMantenimientoViewModel>(
                        "sp_ObtenerSolicitudesMantenimientoPendientes",
                        null
                    );
                    return solicitudes.ToList();
                },
                TimeSpan.FromMinutes(2) // Caché corto porque esta información cambia con frecuencia
            );
        }

        public async Task<bool> ProcesarSolicitudMantenimientoAsync(int idSolicitud, int idAdmin, string estado,
            string descripcion = null, decimal? costo = null, string moneda = "CRC", string detallesCosto = null)
        {
            try
            {
                // Validar datos según estado
                if (estado == "aprobado" && (string.IsNullOrWhiteSpace(descripcion) || !costo.HasValue))
                {
                    throw new ArgumentException("Debe proporcionar descripción y costo para aprobar la solicitud.");
                }

                // Preparar detalles de costo como JSON válido
                string jsonDetalles = null;
                if (estado == "aprobado" && costo.HasValue)
                {
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(detallesCosto))
                        {
                            // Verificar si es JSON válido
                            Newtonsoft.Json.Linq.JObject.Parse(detallesCosto);
                            jsonDetalles = detallesCosto;
                        }
                        else
                        {
                            // Crear JSON básico
                            jsonDetalles = Newtonsoft.Json.JsonConvert.SerializeObject(new
                            {
                                costo_base = costo.Value,
                                impuesto_iva = 0m,
                                otros_costos = 0m,
                                tipo_cambio = 625m
                            });
                        }
                    }
                    catch (Exception jsonEx)
                    {

                        // Si hay error, crear JSON válido
                        jsonDetalles = Newtonsoft.Json.JsonConvert.SerializeObject(new
                        {
                            costo_base = costo.Value,
                            impuesto_iva = 0m,
                            otros_costos = 0m,
                            tipo_cambio = 625m
                        });
                    }
                }

                // Construir parámetros
                var parameters = new
                {
                    id_solicitud = idSolicitud,
                    id_admin = idAdmin,
                    estado = estado,
                    descripcion = estado == "aprobado" ? descripcion : (object)DBNull.Value,
                    costo = estado == "aprobado" ? costo.Value : 0M,
                    moneda = estado == "aprobado" ? moneda : (object)DBNull.Value,
                    detalles_costo = jsonDetalles != null ? jsonDetalles : (object)DBNull.Value
                };

                // Ejecutar procedimiento almacenado
                await _databaseRepository.ExecuteNonQueryProcedureAsync(
                    "sp_ProcesarSolicitudMantenimiento",
                    parameters
                );

                // Invalidar caché relacionado con solicitudes de mantenimiento
                _cache.Remove("SolicitudesMantenimientoPendientes");
                _cache.Remove("HistorialMantenimiento");

                return true;
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        #endregion

        #region Dashboard y Reportes
        public async Task<DashboardViewModel> GetDashboardIndicadoresAsync()
        {
            string cacheKey = "DashboardIndicadores";

            return await GetOrSetCacheAsync(
                cacheKey,
                async () =>
                {
                    var dashboardData = await _databaseRepository.ExecuteScalarProcedureAsync<DashboardViewModel>(
                        "sp_ObtenerIndicadoresDashboard"
                    );

                    return dashboardData;
                },
                TimeSpan.FromHours(1)
            );
        }

        public async Task<List<GraficoDataViewModel>> GetMantenimientosPorMesAsync(int? anio = null)
        {
            // Si no se proporciona un año, usar el año actual
            if (!anio.HasValue)
            {
                anio = DateTime.Now.Year;
            }

            string cacheKey = $"MantenimientosPorMes_{anio}";
            return await GetOrSetCacheAsync(
                cacheKey,
                async () =>
                {
                    try
                    {
                        var data = await _databaseRepository.ExecuteQueryProcedureAsync<dynamic>(
                            "sp_ObtenerMantenimientosPorMes",
                            new { anio }
                        );

                        // Mapear los nombres de los meses
                        string[] nombresMeses = new[] {
                    "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio",
                    "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre"
                        };

                        List<GraficoDataViewModel> formattedData = new List<GraficoDataViewModel>();
                        int index = 0;

                        foreach (var item in data)
                        {
                            try
                            {
                                // Usar reflection para acceder a las propiedades dinámicas
                                int mes = 0;
                                int cantidad = 0;
                                decimal costo = 0;

                                // Intentar obtener las propiedades por nombre (si el resultado es IExpandoObject)
                                if (item is IDictionary<string, object> dict)
                                {
                                    if (dict.ContainsKey("mes")) mes = Convert.ToInt32(dict["mes"]);
                                    if (dict.ContainsKey("cantidad")) cantidad = Convert.ToInt32(dict["cantidad"]);
                                    if (dict.ContainsKey("costo_total")) costo = Convert.ToDecimal(dict["costo_total"]);
                                }
                                // Intentar obtener por posición (si el resultado es un tipo anónimo)
                                else
                                {
                                    // Obtener todas las propiedades públicas
                                    var props = item.GetType().GetProperties();
                                    if (props.Length > 0) mes = Convert.ToInt32(props[0].GetValue(item));
                                    if (props.Length > 1) cantidad = Convert.ToInt32(props[1].GetValue(item));
                                    if (props.Length > 2) costo = Convert.ToDecimal(props[2].GetValue(item));
                                }

                                // Asegurarse de que el mes sea válido
                                if (mes < 1 || mes > 12)
                                {
                                    mes = index + 1; // Usar el índice como respaldo
                                }

                                var graficoData = new GraficoDataViewModel
                                {
                                    Label = nombresMeses[mes - 1], // Ajuste para índice base 0
                                    Value = cantidad,
                                    Color = cantidad > 0 ? "#4e73df" : "#cccccc",
                                    Extra = new Dictionary<string, object> {
                                { "mes", mes },
                                { "costo_total", costo }
                                    }
                                };

                                formattedData.Add(graficoData);
                            }
                            catch (Exception ex)
                            {
                                // Continuar con el siguiente elemento
                            }

                            index++;
                        }

                        // Si no se obtuvieron datos, crear una lista vacía con los 12 meses
                        if (formattedData.Count == 0)
                        {
                            for (int i = 0; i < 12; i++)
                            {
                                formattedData.Add(new GraficoDataViewModel
                                {
                                    Label = nombresMeses[i],
                                    Value = 0,
                                    Color = "#cccccc",
                                    Extra = new Dictionary<string, object> {
                                { "mes", i + 1 },
                                { "costo_total", 0m }
                                    }
                                });
                            }
                        }

                        return formattedData;
                    }
                    catch (Exception ex)
                    {
                        return new List<GraficoDataViewModel>();
                    }
                },
                TimeSpan.FromHours(6)
            );
        }

        public async Task<List<GraficoDataViewModel>> GetEstadosDocumentosAsync()
        {
            string cacheKey = "EstadosDocumentos";
            return await GetOrSetCacheAsync(
                cacheKey,
                async () =>
                {
                    try
                    {
                        var data = await _databaseRepository.ExecuteQueryProcedureAsync<dynamic>(
                            "sp_ObtenerEstadosDocumentos"
                        );

                        List<GraficoDataViewModel> formattedData = new List<GraficoDataViewModel>();
                        int index = 0;

                        foreach (var item in data)
                        {
                            try
                            {
                                string estado = string.Empty;
                                int valor = 0;

                                // Intentar obtener las propiedades por nombre (si el resultado es IExpandoObject)
                                if (item is IDictionary<string, object> dict)
                                {
                                    if (dict.ContainsKey("nombre")) estado = Convert.ToString(dict["nombre"]);
                                    if (dict.ContainsKey("valor")) valor = Convert.ToInt32(dict["valor"]);
                                }
                                // Intentar obtener por posición (si el resultado es un tipo anónimo)
                                else
                                {
                                    // Obtener todas las propiedades públicas
                                    var props = item.GetType().GetProperties();
                                    if (props.Length > 0) estado = Convert.ToString(props[0].GetValue(item));
                                    if (props.Length > 1) valor = Convert.ToInt32(props[1].GetValue(item));
                                }

                                if (string.IsNullOrEmpty(estado))
                                {
                                    continue;
                                }

                                var graficoData = new GraficoDataViewModel
                                {
                                    Label = PrimeraLetraMayuscula(estado),
                                    Value = valor,
                                    Color = estado.ToLower() switch
                                    {
                                        "verificado" => "#1cc88a", // Verde
                                        "pendiente" => "#f6c23e", // Amarillo
                                        "rechazado" => "#e74a3b", // Rojo
                                        _ => "#858796" // Gris por defecto
                                    }
                                };

                                formattedData.Add(graficoData);
                            }
                            catch (Exception ex)
                            {
                                // Continuar con el siguiente elemento
                            }

                            index++;
                        }

                        return formattedData;
                    }
                    catch (Exception ex)
                    {
                        return new List<GraficoDataViewModel>();
                    }
                },
                TimeSpan.FromHours(2)
            );
        }

        public async Task<List<GraficoDataViewModel>> GetEstadosCamionesAsync()
        {
            string cacheKey = "EstadosCamiones";

            return await GetOrSetCacheAsync(
                cacheKey,
                async () =>
                {
                    var data = await _databaseRepository.ExecuteQueryProcedureAsync<dynamic>(
                        "sp_ObtenerEstadosCamiones"
                    );

                    var formattedData = data.Select(item => new GraficoDataViewModel
                    {
                        Label = item.nombre,
                        Value = item.valor,
                        Color = item.nombre.ToString().ToLower() switch
                        {
                            "activo" => "#4e73df",
                            "mantenimiento" => "#f6c23e",
                            "inactivo" => "#e74a3b",
                            _ => "#858796"
                        }
                    }).ToList();
                    return formattedData;
                },
                TimeSpan.FromHours(2)
            );
        }

        public async Task<List<ActividadRecienteViewModel>> GetActividadesRecientesAsync(int cantidad = 10)
        {
            string cacheKey = $"ActividadesRecientes_{cantidad}";

            return await GetOrSetCacheAsync(
                cacheKey,
                async () =>
                {
                    var actividades = await _databaseRepository.ExecuteQueryProcedureAsync<ActividadRecienteViewModel>(
                        "sp_ObtenerActividadesRecientes",
                        new { cantidad }
                    );

                    return actividades.ToList();
                },
                TimeSpan.FromMinutes(15) // Tiempo corto porque son actividades recientes
            );
        }

        public async Task<List<MantenimientoReporteViewModel>> GenerarReporteMantenimientosAsync(DateTime? fechaInicio, DateTime? fechaFin)
        {
            fechaInicio ??= DateTime.Now.AddMonths(-1);
            fechaFin ??= DateTime.Now;

            string cacheKey = $"ReporteMantenimientos_{fechaInicio:yyyyMMdd}_{fechaFin:yyyyMMdd}";

            return await GetOrSetCacheAsync(
                cacheKey,
                async () =>
                {
                    var mantenimientos = await _databaseRepository.ExecuteQueryProcedureAsync<MantenimientoReporteViewModel>(
                        "sp_ObtenerMantenimientosPorFecha",
                        new { fecha_inicio = fechaInicio, fecha_fin = fechaFin }
                    );

                    return mantenimientos.ToList();
                },
                TimeSpan.FromHours(12)
            );
        }
        
        #endregion

        #region Otros
        public async Task<T> GetOrSetCacheAsync<T>(
            string cacheKey,
            Func<Task<T>> dataFactory,
            TimeSpan absoluteExpiration,
            TimeSpan? slidingExpiration = null,
            bool bypassCache = false,
            int sizedCache = -1)
        {
            // Si se solicita bypass del caché o actualización forzada, no verificar caché
            if (!bypassCache && _cache.TryGetValue(cacheKey, out T cachedData))
            {
                return cachedData;
            }

            // Crear una clave de bloqueo para evitar múltiples solicitudes simultáneas
            var lockKey = $"lock_{cacheKey}";
            var lockTaken = false;

            try
            {
                // Intentar adquirir un bloqueo distribuido
                if (_lockProvider != null)
                {
                    try
                    {
                        lockTaken = await _lockProvider.TryAcquireLockAsync(lockKey, TimeSpan.FromSeconds(30));
                    }
                    catch (Exception ex)
                    {
                        throw;
                    }
                }

                // Si no se pudo adquirir el bloqueo, esperar un momento y reintentar
                if (!lockTaken)
                {
                    await Task.Delay(200);

                    // Verificar caché de nuevo
                    if (!bypassCache && _cache.TryGetValue(cacheKey, out cachedData))
                    {
                        return cachedData;
                    }
                }

                // Obtener datos frescos
                var data = await dataFactory();

                // Si los datos son nulos, no intentar cachear
                if (data == null)
                {
                    return data;
                }

                // Determinar el tamaño a utilizar
                int cacheSize = sizedCache;
                if (cacheSize < 0)
                {
                    // Calcular tamaño automáticamente
                    cacheSize = CalcularTamañoCache(data);
                }

                // Asegurarse de que el tamaño nunca sea menor que 1
                if (cacheSize < 1)
                {
                    cacheSize = 1;
                }

                // Configurar opciones de caché CON tamaño (siempre)
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(absoluteExpiration)
                    .SetPriority(CacheItemPriority.Normal)
                    .SetSize(cacheSize); // Siempre establecer un tamaño

                if (slidingExpiration.HasValue)
                {
                    cacheOptions.SetSlidingExpiration(slidingExpiration.Value);
                }

                // Almacenar en caché con tamaño específico
                _cache.Set(cacheKey, data, cacheOptions);

                // Registrar clave en el tracker si está disponible
                if (_cacheKeyTracker != null)
                {
                    try
                    {
                        _cacheKeyTracker.TrackKey(cacheKey);
                    }
                    catch (Exception ex)
                    {
                        throw;
                    }
                }

                return data;
            }
            finally
            {
                // Liberar el bloqueo si fue adquirido
                if (lockTaken && _lockProvider != null)
                {
                    try
                    {
                        await _lockProvider.ReleaseLockAsync(lockKey);
                    }
                    catch (Exception ex)
                    {
                        throw;
                    }
                }
            }
        }
        private string PrimeraLetraMayuscula(string texto)
        {
            if (string.IsNullOrEmpty(texto))
                return texto;

            return char.ToUpper(texto[0]) + texto.Substring(1).ToLower();
        }


        // Método auxiliar para calcular el tamaño de caché
        private int CalcularTamañoCache<T>(T data)
        {
            try
            {
                int cacheSize = 1; // Valor por defecto

                if (data == null)
                {
                    return cacheSize;
                }

                // Determinar tamaño basado en el tipo de datos
                if (data is ICollection collection)
                {
                    int count = Math.Min(collection.Count, 1000);
                    cacheSize = Math.Clamp(count / 20, 1, 50);
                }
                else if (data is System.Collections.IEnumerable enumerable && !(data is string)) // Excluir string
                {
                    int count = 0;
                    foreach (var item in enumerable)
                    {
                        if (count++ == 1000) break;
                    }
                    cacheSize = Math.Clamp(count / 20, 1, 50);
                }
                else if (data is string str)
                {
                    cacheSize = Math.Clamp(str.Length / 1000, 1, 50);
                }

                return Math.Max(cacheSize, 1); // Garantizar que nunca sea menor que 1
            }
            catch (Exception ex)
            {
                return 1; // Valor seguro por defecto
            }
        }
        
        
        #endregion
    }
#nullable enable
#pragma warning restore CS0168 // Variable is declared but never used


}