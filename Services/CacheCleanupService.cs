using Microsoft.Extensions.Caching.Memory;

namespace COMAVI_SA.Services
{
    // Servicio para limpieza de caché
    public class CacheCleanupService : IHostedService, IDisposable
    {
        private readonly IMemoryCache _cache;
        private readonly ICacheKeyTracker _cacheKeyTracker;
        private readonly ILogger<CacheCleanupService> _logger;
        private Timer _timer;

        public CacheCleanupService(
            IMemoryCache cache,
            ICacheKeyTracker cacheKeyTracker,
            ILogger<CacheCleanupService> logger)
        {
            _cache = cache;
            _cacheKeyTracker = cacheKeyTracker;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Servicio de limpieza de caché iniciado");

            // Ejecutar cada 30 minutos
            _timer = new Timer(DoCleanup, null, TimeSpan.Zero, TimeSpan.FromMinutes(30));

            return Task.CompletedTask;
        }

        private void DoCleanup(object state)
        {
            try
            {
                _logger.LogInformation("Iniciando limpieza programada de caché");

                // Limpiar claves antiguas del tracker
                _cacheKeyTracker.CleanupOldKeys(TimeSpan.FromHours(2));

                // También puedes implementar lógica específica para diferentes tipos de caché
                CleanupDashboardCache();
                CleanupUserCache();
                CleanupInactiveSessionsCache();

                _logger.LogInformation("Limpieza de caché completada");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error durante la limpieza de caché");
            }
        }

        private void CleanupDashboardCache()
        {
            try
            {
                // Mantener solo las versiones más recientes del dashboard (últimas 3 horas)
                var dashboardKeys = _cacheKeyTracker.GetKeysByPrefix("AdminDashboard_")
                    .OrderByDescending(k => k)
                    .Skip(3);  // Mantener solo las últimas 3

                foreach (var key in dashboardKeys)
                {
                    _cache.Remove(key);
                    _logger.LogDebug($"Eliminada clave de caché de dashboard antigua: {key}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al limpiar caché de dashboard");
            }
        }

        private void CleanupUserCache()
        {
            try
            {
                // Limpiar cachés de usuario que tengan más de 1 hora
                var userKeys = _cacheKeyTracker.GetKeysByPrefix("User_");
                var allKeys = _cacheKeyTracker.GetAllTrackedKeys();

                foreach (var key in userKeys)
                {
                    if (allKeys.TryGetValue(key, out DateTime timestamp))
                    {
                        if (DateTime.UtcNow.Subtract(timestamp) > TimeSpan.FromHours(1))
                        {
                            _cache.Remove(key);
                            _logger.LogDebug($"Eliminada clave de caché de usuario antigua: {key}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al limpiar caché de usuarios");
            }
        }

        private void CleanupInactiveSessionsCache()
        {
            try
            {
                // Limpiar cachés de sesiones inactivas
                var sessionKeys = _cacheKeyTracker.GetKeysByPrefix("Session_");
                var allKeys = _cacheKeyTracker.GetAllTrackedKeys();

                foreach (var key in sessionKeys)
                {
                    if (allKeys.TryGetValue(key, out DateTime timestamp))
                    {
                        if (DateTime.UtcNow.Subtract(timestamp) > TimeSpan.FromMinutes(30))
                        {
                            _cache.Remove(key);
                            _logger.LogDebug($"Eliminada clave de caché de sesión inactiva: {key}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al limpiar caché de sesiones");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Servicio de limpieza de caché detenido");
            _timer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
