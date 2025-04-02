using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using COMAVI_SA.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Data.SqlClient;
using Microsoft.Data.SqlClient;

namespace COMAVI_SA.Middleware
{
    public class DatabaseResilienceMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IServiceProvider _serviceProvider;
        private static DateTime _lastCheckTime = DateTime.MinValue;
        private static bool _isDbAvailable = true;
        private static readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(15);
        private static int _lastErrorCode = 0;
        private static string _lastErrorMessage = string.Empty;

        public DatabaseResilienceMiddleware(
            RequestDelegate next,
            IServiceProvider serviceProvider)
        {
            _next = next;
            _serviceProvider = serviceProvider;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Para rutas estáticas, permitir paso sin verificación
            if (context.Request.Path.StartsWithSegments("/css") ||
                context.Request.Path.StartsWithSegments("/js") ||
                context.Request.Path.StartsWithSegments("/lib") ||
                context.Request.Path.StartsWithSegments("/images"))
            {
                await _next(context);
                return;
            }

            // Verificar estado de la base de datos
            if (!_isDbAvailable && (DateTime.UtcNow - _lastCheckTime) > _checkInterval)
            {
                await CheckDatabaseStatusAsync();
            }

            // Si la base de datos está caída y la ruta no es de mantenimiento, redirigir a página de mantenimiento
            if (!_isDbAvailable && !context.Request.Path.StartsWithSegments("/Maintenance"))
            {
                RedirectToMaintenance(context);
                return;
            }

            try
            {
                await _next(context);
            }
            catch (Exception ex) when (IsDatabaseException(ex))
            {

                // Marcar la base de datos como no disponible
                _isDbAvailable = false;
                _lastCheckTime = DateTime.UtcNow;

                // Extraer información del error
                ExtractErrorInfo(ex);

                // Redirigir a página de mantenimiento si no estamos ya ahí
                if (!context.Request.Path.StartsWithSegments("/Maintenance"))
                {
                    RedirectToMaintenance(context);
                }
            }
        }

        private void RedirectToMaintenance(HttpContext context)
        {
            // Añadir código de error y mensaje a la redirección
            string redirectUrl = $"/Maintenance?errorCode={_lastErrorCode}&errorMessage={Uri.EscapeDataString(_lastErrorMessage)}";
            context.Response.Redirect(redirectUrl);
        }

        private void ExtractErrorInfo(Exception ex)
        {
            if (ex is SqlException sqlEx)
            {
                _lastErrorCode = sqlEx.Number;
                _lastErrorMessage = sqlEx.Message;
            }
            else if (ex.InnerException != null)
            {
                // Intentar extraer información del error interno
                ExtractErrorInfo(ex.InnerException);
            }
            else
            {
                // Si no es un SqlException específico
                _lastErrorCode = -1;
                _lastErrorMessage = ex.Message;
            }

            // Sanitizar el mensaje para evitar inyección
            _lastErrorMessage = _lastErrorMessage?.Replace("<", "&lt;").Replace(">", "&gt;") ?? "Error desconocido";

        }

        private async Task CheckDatabaseStatusAsync()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ComaviDbContext>();

                // Intentar una consulta simple para verificar conectividad
                await dbContext.Database.ExecuteSqlRawAsync("SELECT 1");

                _isDbAvailable = true;
            }
            catch (Exception ex)
            {
                _isDbAvailable = false;
                ExtractErrorInfo(ex);
            }
            finally
            {
                _lastCheckTime = DateTime.UtcNow;
            }
        }

        private bool IsDatabaseException(Exception ex)
        {
            // Verificar si la excepción es relacionada con la base de datos
            if (ex is SqlException sqlException)
            {
                // Errores comunes de disponibilidad de Azure SQL
                return sqlException.Number == 40613 || // Base de datos no disponible
                       sqlException.Number == 40197 || // Error de cuota
                       sqlException.Number == 40501 || // Servicio ocupado
                       sqlException.Number == 49918 || // No se puede procesar la solicitud
                       sqlException.Number == 40540 || // Operación abortada
                       sqlException.Number == 40143 || // Error de inicio de sesión
                       sqlException.Number == 233 ||   // Conexión cerrada
                       sqlException.Number == -2;      // Timeout
            }

            // Verificar si es una excepción de Entity Framework relacionada con la base de datos
            return ex.InnerException != null && IsDatabaseException(ex.InnerException);
        }
    }

    // Extension method
    public static class DatabaseResilienceMiddlewareExtensions
    {
        public static IApplicationBuilder UseDatabaseResilience(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<DatabaseResilienceMiddleware>();
        }
    }
}