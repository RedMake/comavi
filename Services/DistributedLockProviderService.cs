using Microsoft.Extensions.Caching.Memory;

namespace COMAVI_SA.Services
{
    public interface IDistributedLockProvider
    {
        Task<bool> TryAcquireLockAsync(string key, TimeSpan timeout);
        Task ReleaseLockAsync(string key);
    }

    // Implementación de bloqueo distribuido basado en memoria caché
    public class MemoryCacheDistributedLockProvider : IDistributedLockProvider
    {
        private readonly IMemoryCache _cache;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public MemoryCacheDistributedLockProvider(IMemoryCache cache)
        {
            _cache = cache;
        }

        public async Task<bool> TryAcquireLockAsync(string key, TimeSpan timeout)
        {
            try
            {
                if (await _semaphore.WaitAsync(timeout))
                {
                    if (_cache.TryGetValue(key, out _))
                    {
                        _semaphore.Release();
                        return false;
                    }

                    _cache.Set(key, true, timeout);
                    return true;
                }

                return false;
            }
            catch
            {
                _semaphore.Release();
                return false;
            }
        }

        public Task ReleaseLockAsync(string key)
        {
            try
            {
                _cache.Remove(key);
                _semaphore.Release();
            }
            catch
            {
                // Ignorar errores al liberar el bloqueo
            }

            return Task.CompletedTask;
        }
    }
}
