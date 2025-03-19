using COMAVI_SA.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace COMAVI_SA.Services
{
    // Interfaz para el servicio de limpieza de sesiones
    public interface ISessionCleanupService
    {
        Task CleanupExpiredSessionsAsync();
    }

    // Implementación para el servicio de limpieza de sesiones
    public class SessionCleanupService : ISessionCleanupService
    {
        private readonly ComaviDbContext _context;
        private readonly ILogger<SessionCleanupService> _logger;

        public SessionCleanupService(
            ComaviDbContext context,
            ILogger<SessionCleanupService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task CleanupExpiredSessionsAsync()
        {
            try
            {
                // Considerar sesiones inactivas por más de 30 minutos como expiradas
                var cutoffTime = DateTime.Now.AddMinutes(-30);

                var expiredSessions = await _context.SesionesActivas
                    .Where(s => s.fecha_ultima_actividad < cutoffTime)
                    .ToListAsync();

                if (expiredSessions.Any())
                {
                    _context.SesionesActivas.RemoveRange(expiredSessions);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Se eliminaron {Count} sesiones expiradas", expiredSessions.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al limpiar sesiones expiradas");
            }
        }
    }
}
