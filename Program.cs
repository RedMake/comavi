using Azure.Identity;
using COMAVI_SA.Data;
using COMAVI_SA.Services;
using Hangfire;
using Hangfire.Dashboard;
using Hangfire.MemoryStorage;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
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
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
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
            Encoding.UTF8.GetBytes(jwtSecret ?? "DefaultDevSecretKey"))
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdminRole", policy => policy.RequireRole("admin"));
    options.AddPolicy("RequireUserRole", policy => policy.RequireRole("user"));
});


builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
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
        // This is now safe to execute after Hangfire is fully initialized
        RecurringJob.AddOrUpdate<IUserCleanupService>(
            "cleanup-non-verified-users",
            service => service.CleanupNonVerifiedUsersAsync(3),
            Cron.Daily(2, 0)); // Run daily at 2:00 AM

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