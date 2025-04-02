using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;

namespace COMAVI_SA.Services
{
#pragma warning disable CS0168

    public interface ICacheKeyTracker
    {
        void TrackKey(string key);
        IEnumerable<string> GetKeysByPrefix(string prefix);
        void CleanupOldKeys(TimeSpan age);
        void PurgeAll();
        IDictionary<string, DateTime> GetAllTrackedKeys();

    }
    public class CacheKeyTracker : ICacheKeyTracker
    {
        private readonly ConcurrentDictionary<string, DateTime> _trackedKeys = new ConcurrentDictionary<string, DateTime>();
        private readonly IMemoryCache _cache;

        public CacheKeyTracker(IMemoryCache cache)
        {
            _cache = cache;
        }

        public void TrackKey(string key)
        {
            _trackedKeys.AddOrUpdate(key, DateTime.UtcNow, (_, __) => DateTime.UtcNow);
        }

        public IEnumerable<string> GetKeysByPrefix(string prefix)
        {
            return _trackedKeys.Keys.Where(k => k.StartsWith(prefix)).ToList();
        }

        public void CleanupOldKeys(TimeSpan age)
        {
            try
            {
                var cutoff = DateTime.UtcNow.Subtract(age);
                int keysRemoved = 0;


                var keysToRemove = _trackedKeys
                    .Where(kvp => kvp.Value < cutoff)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in keysToRemove)
                {
                    if (_trackedKeys.TryRemove(key, out _))
                    {
                        _cache.Remove(key);
                        keysRemoved++;
                    }
                }

            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public void PurgeAll()
        {
            try
            {

                int keysRemoved = 0;
                foreach (var key in _trackedKeys.Keys.ToList())
                {
                    if (_trackedKeys.TryRemove(key, out _))
                    {
                        _cache.Remove(key);
                        keysRemoved++;
                    }
                }

            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public IDictionary<string, DateTime> GetAllTrackedKeys()
        {
            return new Dictionary<string, DateTime>(_trackedKeys);
        }
    }

    // Extensiones para caché con soporte de operaciones atómicas
    public static class CacheExtensions
    {
        // Incrementar un valor de caché de forma segura
        public static int AddOrIncrementValue(this IMemoryCache cache, string key, int initialValue, TimeSpan expiration)
        {
            return cache.GetOrCreate(key, entry => {
                entry.SetAbsoluteExpiration(expiration);
                entry.SetSize(1);
                return initialValue;
            }) + 1;
        }

        // Ejecución con mutex (distribuido o local)
        public static async Task<T> ExecuteWithLockAsync<T>(
            this IMemoryCache cache,
            string lockKey,
            Func<Task<T>> operation,
            TimeSpan lockTimeout,
            IDistributedLockProvider lockProvider)
        {
            var lockAcquired = false;

            try
            {
                lockAcquired = await lockProvider.TryAcquireLockAsync(lockKey, lockTimeout);

                if (!lockAcquired)
                {
                    throw new TimeoutException($"No se pudo adquirir bloqueo para {lockKey}");
                }

                return await operation();
            }
            finally
            {
                if (lockAcquired)
                {
                    await lockProvider.ReleaseLockAsync(lockKey);
                }
            }
        }

        // Obtener múltiples valores del caché de forma atómica
        public static IDictionary<string, T> GetMany<T>(this IMemoryCache cache, IEnumerable<string> keys)
        {
            var result = new Dictionary<string, T>();

            foreach (var key in keys)
            {
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
                if (cache.TryGetValue(key, out T value))
                {
#pragma warning disable CS8601 // Possible null reference assignment.
                    result[key] = value;
#pragma warning restore CS8601 // Possible null reference assignment.
                }
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
            }

            return result;
        }

        // Establecer múltiples valores en caché de forma atómica
        public static void SetMany<T>(this IMemoryCache cache, IDictionary<string, T> items, TimeSpan expiration)
        {
            var options = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(expiration)
                .SetSize(1); 

            foreach (var item in items)
            {
                cache.Set(item.Key, item.Value, options);
            }
        }
    }
}
