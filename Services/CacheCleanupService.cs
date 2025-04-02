using Microsoft.Extensions.Caching.Memory;

namespace COMAVI_SA.Services
{
#pragma warning disable CS0168

    // Servicio para limpieza de caché
    public class CacheCleanupService : IHostedService, IDisposable
    {
        private readonly IMemoryCache _cache;
        private readonly ICacheKeyTracker _cacheKeyTracker;
        private Timer _timer;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        public CacheCleanupService(
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
            IMemoryCache cache,
            ICacheKeyTracker cacheKeyTracker)
        {
            _cache = cache;
            _cacheKeyTracker = cacheKeyTracker;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {

            // Ejecutar cada 30 minutos
#pragma warning disable CS8622 // Nullability of reference types in type of parameter doesn't match the target delegate (possibly because of nullability attributes).
            _timer = new Timer(DoCleanup, null, TimeSpan.Zero, TimeSpan.FromMinutes(30));
#pragma warning restore CS8622 // Nullability of reference types in type of parameter doesn't match the target delegate (possibly because of nullability attributes).

            return Task.CompletedTask;
        }

        private void DoCleanup(object state)
        {
            try
            {

                // Limpiar claves antiguas del tracker
                _cacheKeyTracker.CleanupOldKeys(TimeSpan.FromHours(2));

                // También puedes implementar lógica específica para diferentes tipos de caché
                CleanupDashboardCache();
                CleanupUserCache();
                CleanupInactiveSessionsCache();

            }
            catch (Exception ex)
            {
                throw;
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
                }
            }
            catch (Exception ex)
            {
                throw;
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
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw;
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
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _timer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
