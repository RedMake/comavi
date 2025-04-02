using COMAVI_SA.Repository;
using Microsoft.AspNetCore.Http;

namespace COMAVI_SA.Services
{
#pragma warning disable CS0168

    public interface IAuditService
    {
        Task LogAuditEventAsync(string eventType, string details, string username, string? ip = null);
        Task LogExceptionAsync(string operation, string errorMessage, string username);
    }

    public class AuditService : IAuditService
    {
        private readonly IDatabaseRepository _databaseRepository;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuditService(
            IDatabaseRepository databaseRepository,
            IHttpContextAccessor httpContextAccessor)
        {
            _databaseRepository = databaseRepository;
            _httpContextAccessor = httpContextAccessor;
        }


        public async Task LogAuditEventAsync(string eventType, string details, string username, string? ip = null)
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
                        ip = ip ?? GetCurrentUserIp()
                    }
                );
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public async Task LogExceptionAsync(string operation, string errorMessage, string username)
        {
            try
            {
                await _databaseRepository.ExecuteNonQueryProcedureAsync(
                    "sp_RegistrarAuditoria",
                    new
                    {
                        tipo_evento = "Error",
                        detalles = $"Operación: {operation}, Error: {errorMessage}",
                        usuario = username,
                        fecha_hora = DateTime.UtcNow,
                        ip = GetCurrentUserIp()
                    }
                );
            }
            catch (Exception ex)
            {
                throw;
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
