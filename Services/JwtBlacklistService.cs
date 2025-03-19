using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace COMAVI_SA.Services
{

    // Interfaz para el servicio de lista negra de JWT
    public interface IJwtBlacklistService
    {
        void AddToBlacklist(string token, TimeSpan expirationTime);
        bool IsTokenBlacklisted(string token);
        void CleanupExpiredTokens();
    }

    // Implementación para el servicio de lista negra de JWT
    public class JwtBlacklistService : IJwtBlacklistService
    {
        private readonly ConcurrentDictionary<string, DateTime> _blacklistedTokens = new();
        private readonly ILogger<JwtBlacklistService> _logger;

        public JwtBlacklistService(ILogger<JwtBlacklistService> logger)
        {
            _logger = logger;

            // Iniciar limpieza periódica
            var timer = new System.Threading.Timer(
                _ => CleanupExpiredTokens(),
                null,
                TimeSpan.FromMinutes(5),
                TimeSpan.FromMinutes(5));
        }

        public void AddToBlacklist(string token, TimeSpan expirationTime)
        {
            var expiration = DateTime.Now.Add(expirationTime);
            _blacklistedTokens.TryAdd(token, expiration);
            _logger.LogInformation("Token añadido a la lista negra hasta {Expiration}", expiration);
        }

        public bool IsTokenBlacklisted(string token)
        {
            return _blacklistedTokens.ContainsKey(token);
        }

        public void CleanupExpiredTokens()
        {
            var now = DateTime.Now;
            var expiredTokens = _blacklistedTokens
                .Where(pair => pair.Value < now)
                .Select(pair => pair.Key)
                .ToList();

            foreach (var token in expiredTokens)
            {
                _blacklistedTokens.TryRemove(token, out _);
            }

            _logger.LogInformation("Limpieza de tokens expirados completada. Se eliminaron {Count} tokens", expiredTokens.Count);
        }
    }
}
