using Microsoft.Extensions.Caching.Memory;

namespace COMAVI_SA.Middleware
{
    public class RateLimitingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IMemoryCache _cache;
        private readonly ILogger<RateLimitingMiddleware> _logger;

        public RateLimitingMiddleware(
            RequestDelegate next,
            IMemoryCache cache,
            ILogger<RateLimitingMiddleware> logger)
        {
            _next = next;
            _cache = cache;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var endpoint = context.GetEndpoint();

            // Solo aplicar rate limiting a endpoints de autenticación y admin
            if (endpoint?.Metadata.GetMetadata<RateLimitAttribute>() != null)
            {
                var attribute = endpoint.Metadata.GetMetadata<RateLimitAttribute>();
                var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                string key = $"ratelimit_{clientIp}_{context.Request.Path}";

                // Verificar si ya ha alcanzado el límite
                if (_cache.TryGetValue(key, out int requestCount))
                {
                    if (requestCount >= attribute.MaxRequests)
                    {
                        _logger.LogWarning($"Rate limit exceeded for IP {clientIp} on {context.Request.Path}");
                        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                        await context.Response.WriteAsync("Too many requests. Please try again later.");
                        return;
                    }

                    // Incrementar contador
                    _cache.Set(key, requestCount + 1,
                        new MemoryCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromSeconds(attribute.TimeWindowSeconds)));
                }
                else
                {
                    // Primera solicitud en esta ventana de tiempo
                    _cache.Set(key, 1,
                        new MemoryCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromSeconds(attribute.TimeWindowSeconds)));
                }
            }

            await _next(context);
        }
    }

    // Atributo para marcar controladores o acciones con límites de tasa
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public class RateLimitAttribute : Attribute
    {
        public int MaxRequests { get; }
        public int TimeWindowSeconds { get; }

        public RateLimitAttribute(int maxRequests, int timeWindowSeconds)
        {
            MaxRequests = maxRequests;
            TimeWindowSeconds = timeWindowSeconds;
        }
    }
}
