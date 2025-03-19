using Azure.Identity;
using COMAVI_SA.Data;
using COMAVI_SA.Services;
using COMAVI_SA.Middleware;
using Hangfire;
using Hangfire.Dashboard;
using Hangfire.MemoryStorage;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMemoryCache();
builder.Services.AddDistributedMemoryCache();

if (!builder.Environment.IsDevelopment())
{
    var keyVaultEndpoint = new Uri(builder.Configuration["KeyVault:Endpoint"]);
    builder.Configuration.AddAzureKeyVault(keyVaultEndpoint, new DefaultAzureCredential());
}

// Obtener la cadena de conexión
var connectionString = builder.Environment.IsDevelopment()
    ? builder.Configuration.GetConnectionString("DefaultConnection")
    : builder.Configuration.GetConnectionString("AZURE_SQL_CONNECTIONSTRING")
        ?? builder.Configuration.GetConnectionString("DefaultConnection");

// Usar la misma variable al registrar el DbContext
builder.Services.AddDbContext<ComaviDbContext>(options =>
    options.UseSqlServer(
        connectionString, 
        sqlServerOptions => sqlServerOptions.CommandTimeout(60)
    ));


if (builder.Environment.IsDevelopment())
{
    builder.Services.AddHangfire(configuration => configuration
         .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
         .UseSimpleAssemblyNameTypeSerializer()
         .UseRecommendedSerializerSettings()
         .UseMemoryStorage());

    Console.WriteLine("Configured Hangfire with in-memory storage for DEVELOPMENT");
}
else
{
    var sqlServerStorageOptions = new SqlServerStorageOptions
    {
        CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
        SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
        QueuePollInterval = TimeSpan.Zero,
        UseRecommendedIsolationLevel = true,
        DisableGlobalLocks = true,
        SchemaName = "HangfireSchema",
        PrepareSchemaIfNecessary = true
    };

    builder.Services.AddHangfire(configuration => configuration
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UseSqlServerStorage(connectionString, sqlServerStorageOptions));

    Console.WriteLine("Configured Hangfire with SQL Server storage for PRODUCTION");
}


builder.Services.AddControllersWithViews();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IPasswordService, PasswordService>();
builder.Services.AddScoped<IOtpService, OtpService>();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IPdfService, PdfService>();
builder.Services.AddScoped<IUserCleanupService, UserCleanupService>();
builder.Services.AddScoped<ISessionCleanupService, SessionCleanupService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<AgendaNotificationService>();

builder.Services.AddTransient<SessionValidationMiddleware>();
builder.Services.AddSingleton<IJwtBlacklistService, JwtBlacklistService>();

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.LoginPath = "/Login/Index";
    options.LogoutPath = "/Login/Logout";
    options.AccessDeniedPath = "/Login/AccessDenied";

    // Reducir drásticamente el tiempo de vida de la cookie
    options.ExpireTimeSpan = TimeSpan.FromMinutes(30); // 30 minutos máximo

    // No usar SlidingExpiration para que la cookie expire siempre a tiempo fijo
    options.SlidingExpiration = false;

    // Configuraciones de seguridad fuertes
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.Name = "COMAVI.Auth";
    options.Cookie.Path = "/";

    // Establecer que la cookie sea solo para la sesión del navegador
    options.Cookie.IsEssential = true;

    // Eventos avanzados para manejo de autenticación
    options.Events = new CookieAuthenticationEvents
    {
        // Al cerrar sesión, invalidar completamente la cookie
        OnSigningOut = async context =>
        {
            context.CookieOptions.Expires = DateTime.Now.AddDays(-1);
        },

        // Validación de seguridad cada vez que se valida la cookie
        OnValidatePrincipal = async context =>
        {
            // Aquí podrías añadir lógica adicional para verificar 
            // continuamente la validez de la sesión

            // Ejemplo: verificar si el usuario ha cambiado su contraseña recientemente
            var userPrincipal = context.Principal;
            var userId = userPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!string.IsNullOrEmpty(userId))
            {
                using var scope = context.HttpContext.RequestServices.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ComaviDbContext>();

                // Verificar si el usuario existe en la tabla de sesiones activas
                var sesionActiva = await dbContext.SesionesActivas
                    .AnyAsync(s => s.id_usuario == int.Parse(userId));

                if (!sesionActiva)
                {
                    // Si no existe sesión activa, rechazar la autenticación
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                }
            }
        }
    };
})
.AddJwtBearer(options =>
{
    var jwtSecret = builder.Environment.IsDevelopment()
        ? builder.Configuration["JwtSettings:SecretKey"]
        : builder.Configuration["JwtSecret"];

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
        ValidAudience = builder.Configuration["JwtSettings:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtSecret ?? "DefaultDevSecretKey")),

        // Estos parámetros son críticos para JWT
        ClockSkew = TimeSpan.Zero, // Sin margen de tiempo adicional
        RequireExpirationTime = true // Requerir tiempo de expiración
    };

    // Manejar eventos de autenticación JWT
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            // Verificar si el token se encuentra en la lista negra (implementar esta lógica)
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdminRole", policy => policy.RequireRole("admin"));
    options.AddPolicy("RequireUserRole", policy => policy.RequireRole("user"));
});


builder.Services.AddSession(async options =>
{
    // Tiempo de inactividad corto
    options.IdleTimeout = TimeSpan.FromMinutes(15); // Solo 15 minutos de inactividad

    // Configuraciones de seguridad
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.Name = "COMAVI.Session";
    options.Cookie.Path = "/";

    // Configurar como cookie de sesión únicamente (no persistente)
    options.Cookie.MaxAge = null;
});


builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigins",
        builder => builder
            .WithOrigins("https://docktrack.lat", "https://www.docktrack.lat")
            .AllowAnyMethod()
            .AllowAnyHeader());
});

builder.Services.AddHangfireServer();

var app = builder.Build();

if (builder.Environment.IsProduction())
{
    // En producción, migramos la base de datos de forma automática
    app.MigrateDatabase();  
}
else
{
    // En desarrollo, mostramos información más detallada del proceso
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    await DbInitializer.Initialize(app.Services, logger);

}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}


app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseCors("AllowSpecificOrigins");

app.UseAuthentication();
app.UseAuthorization();
app.UseSession();

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireAuthorizationFilter() }
});

app.Lifetime.ApplicationStarted.Register(() => {
    try
    {
        RecurringJob.AddOrUpdate<INotificationService>(
            "send-expiration-notifications",
            service => service.SendExpirationNotificationsAsync(),
            "0 8 * * *");

        RecurringJob.AddOrUpdate<AgendaNotificationService>(
            "send-agenda-notifications",
            service => service.EnviarNotificacionesAgendaAsync(),
            Cron.Daily(7, 0)); 

        RecurringJob.AddOrUpdate<IUserCleanupService>(
            "cleanup-non-verified-users",
            service => service.CleanupNonVerifiedUsersAsync(3),
            Cron.Daily(2, 0)); // Run daily at 2:00 AM

        RecurringJob.AddOrUpdate<ISessionCleanupService>(
            "cleanup-expired-sessions",
            service => service.CleanupExpiredSessionsAsync(),
            "*/10 * * * *"); // Ejecutar cada 10 minutos

        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Successfully registered Hangfire recurring jobs");
    }
    catch (Exception ex)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Error registering Hangfire recurring jobs");
    }
});


app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .AllowAnonymous();
app.Run();

public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        return httpContext.User.Identity.IsAuthenticated && httpContext.User.IsInRole("admin");
    }
}