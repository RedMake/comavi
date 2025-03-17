using COMAVI_SA.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace COMAVI_SA.Services
{
    public static class DbInitializer
    {
        public static async Task Initialize(IServiceProvider serviceProvider, ILogger logger)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ComaviDbContext>();

                logger.LogInformation("Iniciando migración automática de la base de datos...");

                // Aplicar migraciones pendientes de forma programática
                await dbContext.Database.MigrateAsync();

                logger.LogInformation("Migración de la base de datos completada exitosamente.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error durante la migración de la base de datos.");
                throw;
            }
        }
    }

    public static class HostExtensions
    {
        public static IHost MigrateDatabase(this IHost host)
        {
            using (var scope = host.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                var logger = services.GetRequiredService<ILogger<Program>>();

                try
                {
                    var dbContext = services.GetRequiredService<ComaviDbContext>();

                    // Verifica si hay conexión a la base de datos
                    dbContext.Database.CanConnect();

                    // Comprueba si hay migraciones pendientes
                    if (dbContext.Database.GetPendingMigrations().Any())
                    {
                        logger.LogInformation("Aplicando migraciones automáticas...");
                        dbContext.Database.Migrate();
                        logger.LogInformation("Migraciones aplicadas correctamente.");
                    }
                    else
                    {
                        logger.LogInformation("No hay migraciones pendientes.");
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error al migrar la base de datos en el inicio de la aplicación.");
                }
            }

            return host;
        }
    }
}