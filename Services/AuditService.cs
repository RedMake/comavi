using COMAVI_SA.Repository;
using Microsoft.AspNetCore.Http;

namespace COMAVI_SA.Services
{
    public interface IAuditService
    {
        Task LogAuditEventAsync(string eventType, string details, string username);
        Task LogExceptionAsync(string operation, string errorMessage, string username);
    }

    public class AuditService : IAuditService
    {
        private readonly IDatabaseRepository _databaseRepository;
        private readonly ILogger<AuditService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuditService(
            IDatabaseRepository databaseRepository,
            ILogger<AuditService> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _databaseRepository = databaseRepository;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }


        public async Task LogAuditEventAsync(string eventType, string details, string username)
        {
            try
            {
                await _databaseRepository.ExecuteNonQueryProcedureAsync(
                    "sp_RegistrarAuditoria",
                    new
                    {
                        tipo_evento = eventType,
                        detalles = details,
                        usuario = username,
                        fecha_hora = DateTime.UtcNow,
                        ip = GetCurrentUserIp()
                    }
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar evento de auditoría");
            }
        }

        public async Task LogExceptionAsync(string operation, string errorMessage, string username)
        {
            try
            {
                await _databaseRepository.ExecuteNonQueryProcedureAsync(
                    "sp_RegistrarErrorAuditoria",
                    new
                    {
                        operacion = operation,
                        mensaje_error = errorMessage,
                        usuario = username,
                        fecha_hora = DateTime.UtcNow,
                        ip = GetCurrentUserIp()
                    }
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar excepción para auditoría");
            }
        }

        private string GetCurrentUserIp()
        {
            try
            {
                var httpContext = _httpContextAccessor.HttpContext;
                return httpContext?.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            }
            catch
            {
                return "unknown";
            }
        }
    }
}
