using Azure.Identity;
using Azure.Storage.Blobs;
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
using COMAVI_SA.Controllers;
using COMAVI_SA.Repository;
using Microsoft.AspNetCore.DataProtection;
using System.Security.Cryptography;
using COMAVI_SA.Filters; // Assuming this using is needed
using System; // Added for Console.WriteLine and Exception
using System.IO; // Added for Path and DirectoryInfo

var builder = WebApplication.CreateBuilder(args);

Console.WriteLine("Starting application configuration...");

try
{
    Console.WriteLine("Configuring Memory Cache...");
    builder.Services.AddMemoryCache();
    Console.WriteLine("Memory Cache configured successfully.");
}
catch (Exception ex)
{
    Console.WriteLine($"ERROR configuring Memory Cache: {ex.Message}");
    // Consider whether to throw or allow continuation depending on criticality
}

try
{
    Console.WriteLine("Configuring Distributed Memory Cache...");
    builder.Services.AddDistributedMemoryCache();
    Console.WriteLine("Distributed Memory Cache configured successfully.");
}
catch (Exception ex)
{
    Console.WriteLine($"ERROR configuring Distributed Memory Cache: {ex.Message}");
    // Consider whether to throw or allow continuation depending on criticality
}


// --- Data Protection Configuration ---
if (!builder.Environment.IsDevelopment())
{
    Console.WriteLine("Configuring Data Protection for PRODUCTION environment (Azure Blob Storage and Key Vault)...");
    try
    {
        // Validate required configuration values
        var blobStorageUriString = "https://dumpmemorycomavi1.blob.core.windows.net/"; // Hardcoded, consider moving to config
        var blobContainerName = "dataprotection-keys"; // Hardcoded, consider moving to config
        var blobName = "keys.xml"; // Hardcoded, consider moving to config
        var keyVaultEndpoint = builder.Configuration["KeyVault:Endpoint"];
        var keyVaultKeyName = builder.Configuration["KeyVault:KeyName"];

        if (string.IsNullOrEmpty(keyVaultEndpoint))
        {
            Console.WriteLine("ERROR: KeyVault:Endpoint configuration is missing for Data Protection.");
            throw new InvalidOperationException("KeyVault:Endpoint configuration is missing.");
        }
        if (string.IsNullOrEmpty(keyVaultKeyName))
        {
            Console.WriteLine("ERROR: KeyVault:KeyName configuration is missing for Data Protection.");
            throw new InvalidOperationException("KeyVault:KeyName configuration is missing.");
        }

        Console.WriteLine($"Attempting to create BlobServiceClient with URI: {blobStorageUriString}");
        var blobServiceClient = new BlobServiceClient(
            new Uri(blobStorageUriString),
            new DefaultAzureCredential());
        Console.WriteLine("BlobServiceClient created successfully.");

        var keyVaultKeyIdentifier = new Uri($"{keyVaultEndpoint.TrimEnd('/')}/keys/{keyVaultKeyName}");
        Console.WriteLine($"Attempting to configure Data Protection persistence to Blob: {blobContainerName}/{blobName}");
        Console.WriteLine($"Attempting to configure Data Protection key protection with Key Vault key: {keyVaultKeyIdentifier}");

        builder.Services.AddDataProtection()
            .PersistKeysToAzureBlobStorage(
                blobServiceClient.GetBlobContainerClient(blobContainerName).GetBlobClient(blobName))
            .ProtectKeysWithAzureKeyVault(
                keyVaultKeyIdentifier,
                new DefaultAzureCredential())
            .SetApplicationName("COMAVI_SA");

        Console.WriteLine("Data Protection for PRODUCTION configured successfully.");
    }
    catch (UriFormatException ex)
    {
        // Log the specific error during URI creation
        Console.WriteLine($"ERROR creating URI for Data Protection configuration: {ex.Message}");
        Console.WriteLine($"Ensure Blob Storage URI or Key Vault Endpoint ('{builder.Configuration["KeyVault:Endpoint"]}') is valid.");
        throw; // Re-throw the exception to halt startup if configuration is fundamentally broken
    }
    catch (Azure.RequestFailedException ex) // More specific Azure exception
    {
        Console.WriteLine($"ERROR during Azure interaction for Data Protection (check credentials/permissions/network): {ex.Message}");
        Console.WriteLine($"Status: {ex.Status}, ErrorCode: {ex.ErrorCode}");
        throw;
    }
    catch (Exception ex) // Catch other potential exceptions (e.g., credential issues, network)
    {
        Console.WriteLine($"UNEXPECTED ERROR configuring Data Protection for PRODUCTION: {ex.Message}");
        Console.WriteLine($"Stack Trace: {ex.StackTrace}"); // Log stack trace for detailed debugging
        throw;
    }
}
else
{
    Console.WriteLine("Configuring Data Protection for DEVELOPMENT environment (local file system)...");
    try
    {
        var keysPath = Path.Combine(builder.Environment.ContentRootPath, "keys");
        Console.WriteLine($"Attempting to create directory for keys: {keysPath}");
        Directory.CreateDirectory(keysPath); // Can throw exceptions if permissions are wrong
        Console.WriteLine($"Directory created or already exists: {keysPath}");

        builder.Services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
            .SetApplicationName("COMAVI_SA");

        Console.WriteLine("Data Protection for DEVELOPMENT configured successfully using local file system.");
    }
    catch (IOException ex)
    {
        Console.WriteLine($"ERROR creating directory or accessing file system for Data Protection keys at '{Path.Combine(builder.Environment.ContentRootPath, "keys")}': {ex.Message}");
        throw;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"UNEXPECTED ERROR configuring Data Protection for DEVELOPMENT: {ex.Message}");
        Console.WriteLine($"Stack Trace: {ex.StackTrace}");
        throw;
    }
}

// --- Database Configuration ---
Console.WriteLine("Configuring Database Context (ComaviDbContext)...");
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrEmpty(connectionString))
{
    Console.WriteLine("CRITICAL ERROR: Connection string 'DefaultConnection' not found or is empty in configuration.");
    // Depending on the app's requirements, you might throw here to prevent startup.
    // throw new InvalidOperationException("Database connection string 'DefaultConnection' is missing.");
}
else
{
    Console.WriteLine("Connection string 'DefaultConnection' retrieved successfully.");
    try
    {
        builder.Services.AddDbContext<ComaviDbContext>(options =>
        {
            Console.WriteLine("Applying SQL Server options: CommandTimeout=30s, EnableRetryOnFailure (Max 5 retries, 30s delay).");
            options.UseSqlServer(connectionString, sqlServerOptions => {
                sqlServerOptions.CommandTimeout(30);
                sqlServerOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorNumbersToAdd: null);
            });
        });
        Console.WriteLine("ComaviDbContext configured successfully.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ERROR configuring ComaviDbContext: {ex.Message}");
        Console.WriteLine($"Check if the connection string is valid and the SQL Server provider package is correctly installed.");
        Console.WriteLine($"Stack Trace: {ex.StackTrace}");
        throw; // Usually critical if DB context fails
    }
}

// --- Hangfire Configuration ---
Console.WriteLine("Configuring Hangfire...");
if (builder.Environment.IsDevelopment())
{
    Console.WriteLine("Setting up Hangfire with Memory Storage for DEVELOPMENT.");
    try
    {
        builder.Services.AddHangfire(configuration => configuration
              .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
              .UseSimpleAssemblyNameTypeSerializer()
              .UseRecommendedSerializerSettings()
              .UseMemoryStorage());
        Console.WriteLine("Hangfire configured with Memory Storage successfully.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ERROR configuring Hangfire with Memory Storage: {ex.Message}");
        Console.WriteLine($"Stack Trace: {ex.StackTrace}");
        // Decide if throwing is necessary for development
    }
}
else
{
    Console.WriteLine("Setting up Hangfire with SQL Server Storage for PRODUCTION.");
    if (string.IsNullOrEmpty(connectionString))
    {
        Console.WriteLine("ERROR: Cannot configure Hangfire SQL Server Storage because the 'DefaultConnection' string is missing or empty.");
        // Throw or handle appropriately, Hangfire likely won't work.
        // throw new InvalidOperationException("Cannot configure Hangfire SQL Server Storage due to missing connection string.");
    }
    else
    {
        try
        {
            var sqlServerStorageOptions = new SqlServerStorageOptions
            {
                CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                QueuePollInterval = TimeSpan.Zero,
                UseRecommendedIsolationLevel = true,
                DisableGlobalLocks = true, // Check implications for your workload
                SchemaName = "HangfireSchema",
                PrepareSchemaIfNecessary = true // Creates schema if not exists
            };
            Console.WriteLine($"Configuring Hangfire SQL Server Storage with SchemaName: {sqlServerStorageOptions.SchemaName}, PrepareSchema: {sqlServerStorageOptions.PrepareSchemaIfNecessary}");

            builder.Services.AddHangfire(configuration => configuration
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UseSqlServerStorage(connectionString, sqlServerStorageOptions));
            Console.WriteLine("Hangfire configured with SQL Server Storage successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR configuring Hangfire with SQL Server Storage: {ex.Message}");
            Console.WriteLine("Check the database connection string, permissions, and if the Hangfire SQL Server package is installed.");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            throw; // Often critical in production
        }
    }
}

// --- Controllers and Filters ---
try
{
    Console.WriteLine("Adding Controllers with Views and global VerificarAutenticacionAttribute filter...");
    builder.Services.AddControllersWithViews(options =>
    {
        options.Filters.Add<VerificarAutenticacionAttribute>();
    });
    Console.WriteLine("Controllers with Views and filter added successfully.");
}
catch (Exception ex)
{
    Console.WriteLine($"ERROR adding Controllers with Views or the global filter: {ex.Message}");
    Console.WriteLine($"Stack Trace: {ex.StackTrace}");
    // Decide if this is critical enough to throw
}

// --- Service Registrations (Dependency Injection) ---
Console.WriteLine("Registering application services...");
try
{
    // Example: Logging a specific service registration
    Console.WriteLine("Registering Scoped: IUserService, IPasswordService, IOtpService...");
    builder.Services.AddScoped<IUserService, UserService>();
    builder.Services.AddScoped<IPasswordService, PasswordService>();
    builder.Services.AddScoped<IOtpService, OtpService>();

    Console.WriteLine("Registering Scoped: IJwtService, IEmailService, IPdfService...");
    builder.Services.AddScoped<IJwtService, JwtService>();
    builder.Services.AddScoped<IEmailService, EmailService>();
    builder.Services.AddScoped<IPdfService, PdfService>();

    Console.WriteLine("Registering Scoped: IUserCleanupService, ISessionCleanupService, IReportService...");
    builder.Services.AddScoped<IUserCleanupService, UserCleanupService>();
    builder.Services.AddScoped<ISessionCleanupService, SessionCleanupService>();
    builder.Services.AddScoped<IReportService, ReportService>();

    Console.WriteLine("Registering Scoped: AgendaNotificationService, IDatabaseRepository, IAdminService...");
    builder.Services.AddScoped<AgendaNotificationService>();
    builder.Services.AddScoped<IDatabaseRepository, DatabaseRepository>();
    builder.Services.AddScoped<IAdminService, AdminService>();

    Console.WriteLine("Registering Scoped: IAuditService, INotificationService, IExcelService, IMantenimientoService...");
    builder.Services.AddScoped<IAuditService, AuditService>();
    builder.Services.AddScoped<INotificationService, NotificationService>();
    builder.Services.AddScoped<IExcelService, ExcelService>();
    builder.Services.AddScoped<IMantenimientoService, MantenimientoService>();

    Console.WriteLine("Registering Transient: SessionValidationMiddleware...");
    builder.Services.AddTransient<SessionValidationMiddleware>();

    Console.WriteLine("Registering Singleton: IJwtBlacklistService, IDistributedLockProvider, ICacheKeyTracker...");
    builder.Services.AddSingleton<IJwtBlacklistService, JwtBlacklistService>();
    builder.Services.AddSingleton<IDistributedLockProvider, MemoryCacheDistributedLockProvider>(); // Check if this is intended (in-memory lock provider)
    builder.Services.AddSingleton<ICacheKeyTracker, CacheKeyTracker>();

    Console.WriteLine("Registering Hosted Service: CacheCleanupService...");
    builder.Services.AddHostedService<CacheCleanupService>();

    Console.WriteLine("Registering HttpContextAccessor...");
    builder.Services.AddHttpContextAccessor();

    Console.WriteLine("All application services registered successfully.");
}
catch (Exception ex)
{
    // Catching errors during DI registration is less common unless there's a complex factory or issue finding types
    Console.WriteLine($"ERROR during service registration: {ex.Message}");
    Console.WriteLine($"Stack Trace: {ex.StackTrace}");
    throw; // Errors here often indicate fundamental setup problems
}

// --- Authentication Configuration ---
Console.WriteLine("Configuring Authentication (Cookie and JWT)...");
try
{
    builder.Services.AddAuthentication(options =>
    {
        Console.WriteLine($"Setting DefaultScheme: {CookieAuthenticationDefaults.AuthenticationScheme}, DefaultChallengeScheme: {JwtBearerDefaults.AuthenticationScheme}");
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        Console.WriteLine("Configuring Cookie Authentication...");
        options.LoginPath = "/Login/Index";
        options.LogoutPath = "/Login/Logout";
        options.AccessDeniedPath = "/Login/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30); // 30 minutos máximo
        options.SlidingExpiration = false; // No sliding expiration
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // Ensure HTTPS is enforced
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.Name = "COMAVI.Auth";
        options.Cookie.Path = "/";
        options.Cookie.IsEssential = true; // Mark as essential for GDPR/consent
        Console.WriteLine($"Cookie configured: Name={options.Cookie.Name}, Path={options.Cookie.Path}, ExpireTimeSpan={options.ExpireTimeSpan}, SecurePolicy={options.Cookie.SecurePolicy}, SameSite={options.Cookie.SameSite}, HttpOnly={options.Cookie.HttpOnly}");


        options.Events = new CookieAuthenticationEvents
        {
            OnSigningOut = context =>
            {
                Console.WriteLine($"Cookie Event: OnSigningOut triggered for user {context.HttpContext.User?.Identity?.Name ?? "Unknown"}. Invalidating cookie.");
                context.CookieOptions.Expires = DateTime.Now.AddDays(-1);
                return Task.CompletedTask; // Removed async keyword as no await is used
            },

            OnValidatePrincipal = async context =>
            {
                var userIdClaim = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                Console.WriteLine($"Cookie Event: OnValidatePrincipal triggered. User ID from claim: {userIdClaim ?? "Not Found"}");
                try
                {
                    if (!string.IsNullOrEmpty(userIdClaim))
                    {
                        // Using is crucial for scoped services within singleton-like event handlers
                        using var scope = context.HttpContext.RequestServices.CreateScope();
                        var dbContext = scope.ServiceProvider.GetRequiredService<ComaviDbContext>();

                        Console.WriteLine($"Validating active session for user ID: {userIdClaim}");
                        bool sesionActiva = false;
                        if (int.TryParse(userIdClaim, out int userIdInt))
                        {
                            sesionActiva = await dbContext.SesionesActivas
                                                .AnyAsync(s => s.id_usuario == userIdInt);
                        }
                        else
                        {
                            Console.WriteLine($"WARNING: Could not parse user ID '{userIdClaim}' to integer for session validation.");
                        }


                        if (!sesionActiva)
                        {
                            Console.WriteLine($"Session validation failed for user ID: {userIdClaim}. No active session found in DB. Rejecting principal and signing out.");
                            context.RejectPrincipal();
                            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                        }
                        else
                        {
                            Console.WriteLine($"Session validation successful for user ID: {userIdClaim}. Principal accepted.");
                        }
                    }
                    else
                    {
                        Console.WriteLine("No user ID claim found in principal. Skipping DB session validation.");
                        // Depending on policy, might want to reject here too if user ID is always expected
                    }
                }
                catch (CryptographicException ex) when (ex.Message.Contains("was not found in the key ring"))
                {
                    Console.WriteLine($"ERROR during OnValidatePrincipal: Data Protection key not found. Rejecting principal and signing out. Message: {ex.Message}");
                    // This usually happens after key rotation or if keys are lost/inaccessible
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                }
                catch (Exception ex) // Catch other DB or unexpected errors
                {
                    Console.WriteLine($"ERROR during OnValidatePrincipal database check for user ID {userIdClaim ?? "Unknown"}: {ex.Message}");
                    Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                    // Decide how to handle - rejecting might lock users out if DB is temporarily down
                    // context.RejectPrincipal(); // Optional: Reject on any error
                    // await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme); // Optional
                }
            }
        };
        Console.WriteLine("Cookie Authentication configured successfully with events.");
    })
    .AddJwtBearer(options =>
    {
        Console.WriteLine("Configuring JWT Bearer Authentication...");
        var jwtSecret = builder.Configuration["JwtSettings:SecretKey"] ?? builder.Configuration["JwtSecret"]; // Check both possible locations
        var jwtIssuer = builder.Configuration["JwtSettings:Issuer"];
        var jwtAudience = builder.Configuration["JwtSettings:Audience"];

        if (string.IsNullOrEmpty(jwtSecret))
        {
            Console.WriteLine("CRITICAL ERROR: JWT Secret Key ('JwtSettings:SecretKey' or 'JwtSecret') is missing or empty in configuration.");
            // Throw immediately - JWT cannot function without a secret.
            throw new InvalidOperationException("JWT Secret Key configuration is missing.");
        }
        if (string.IsNullOrEmpty(jwtIssuer))
        {
            Console.WriteLine("WARNING: JWT Issuer ('JwtSettings:Issuer') is missing or empty in configuration.");
            // Potentially allow if validation is disabled, but log warning
        }
        if (string.IsNullOrEmpty(jwtAudience))
        {
            Console.WriteLine("WARNING: JWT Audience ('JwtSettings:Audience') is missing or empty in configuration.");
            // Potentially allow if validation is disabled, but log warning
        }
        else
        {
            // Log the first few chars of the secret for verification, NEVER the whole secret
            Console.WriteLine($"JWT using Secret starting with: {jwtSecret.Substring(0, Math.Min(jwtSecret.Length, 4))}..., Issuer: {jwtIssuer}, Audience: {jwtAudience}");
        }


        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = !string.IsNullOrEmpty(jwtIssuer), // Only validate if configured
            ValidateAudience = !string.IsNullOrEmpty(jwtAudience), // Only validate if configured
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.Zero, // No tolerance for expiration time
            RequireExpirationTime = true
        };
        Console.WriteLine($"JWT TokenValidationParameters set: ValidateIssuer={options.TokenValidationParameters.ValidateIssuer}, ValidateAudience={options.TokenValidationParameters.ValidateAudience}, ValidateLifetime=True, ValidateIssuerSigningKey=True, ClockSkew=Zero");

        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                Console.WriteLine($"JWT Event: OnAuthenticationFailed. Reason: {context.Exception.GetType().Name}, Message: {context.Exception.Message}");
                // Log details about the token if possible (e.g., expiration, signature failure) without logging the token itself
                if (context.Exception is SecurityTokenExpiredException)
                {
                    Console.WriteLine("JWT Failure Reason: Token has expired.");
                }
                else if (context.Exception is SecurityTokenInvalidSignatureException)
                {
                    Console.WriteLine("JWT Failure Reason: Token signature is invalid.");
                }
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                Console.WriteLine($"JWT Event: OnTokenValidated. User ID from token: {userId ?? "Not Found"}");
                // Placeholder for potential blacklist check
                // var blacklistService = context.HttpContext.RequestServices.GetRequiredService<IJwtBlacklistService>();
                // var jti = context.Principal?.FindFirstValue(JwtRegisteredClaimNames.Jti);
                // if (blacklistService.IsBlacklisted(jti)) { context.Fail("Token is blacklisted"); }
                return Task.CompletedTask;
            },
            OnMessageReceived = context =>
            {
                // This event is useful if the token might be in a non-standard place (e.g., query string)
                // Console.WriteLine("JWT Event: OnMessageReceived.");
                // Default behavior checks Authorization header.
                return Task.CompletedTask;
            },
            OnChallenge = context => {
                // Called when authentication fails and challenge is issued
                Console.WriteLine($"JWT Event: OnChallenge triggered. AuthenticationFailure: {context.AuthenticateFailure?.Message ?? "None"}");
                // You might customize the response here (e.g., return specific error JSON)
                // context.HandleResponse(); // Prevent default redirect/response
                // context.Response.StatusCode = 401; ...
                return Task.CompletedTask;
            }
        };
        Console.WriteLine("JWT Bearer Authentication configured successfully with events.");
    });
    Console.WriteLine("Authentication configuration completed.");
}
catch (Exception ex)
{
    Console.WriteLine($"CRITICAL ERROR configuring Authentication: {ex.Message}");
    Console.WriteLine($"Stack Trace: {ex.StackTrace}");
    throw; // Authentication failure is usually critical
}

// --- Authorization Configuration ---
try
{
    Console.WriteLine("Configuring Authorization Policies (RequireAdminRole, RequireUserRole)...");
    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("RequireAdminRole", policy => policy.RequireRole("admin"));
        options.AddPolicy("RequireUserRole", policy => policy.RequireRole("user"));
        // Add console logs inside policy definitions if needed for complex policies
    });
    Console.WriteLine("Authorization Policies configured successfully.");
}
catch (Exception ex)
{
    Console.WriteLine($"ERROR configuring Authorization: {ex.Message}");
    Console.WriteLine($"Stack Trace: {ex.StackTrace}");
    // Decide if throwing is necessary
}

// --- Session Configuration ---
try
{
    Console.WriteLine("Configuring Session state...");
    builder.Services.AddSession(options =>
    {
        options.IdleTimeout = TimeSpan.FromMinutes(15); // 15 minutes inactivity timeout
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true; // Mark as essential
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // HTTPS only
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.Name = "COMAVI.Session";
        options.Cookie.Path = "/";
        options.Cookie.MaxAge = null; // Session cookie (expires when browser closes)
        Console.WriteLine($"Session configured: Name={options.Cookie.Name}, IdleTimeout={options.IdleTimeout}, SecurePolicy={options.Cookie.SecurePolicy}, SameSite={options.Cookie.SameSite}, HttpOnly={options.Cookie.HttpOnly}");
    });
    Console.WriteLine("Session state configured successfully.");
}
catch (Exception ex)
{
    Console.WriteLine($"ERROR configuring Session state: {ex.Message}");
    Console.WriteLine($"Stack Trace: {ex.StackTrace}");
    // Consider if session failure is critical
}

// --- Memory Cache Options ---
try
{
    Console.WriteLine("Configuring Memory Cache Options (SizeLimit, CompactionPercentage)...");
    builder.Services.AddMemoryCache(options =>
    {
        options.SizeLimit = 1024; // Example limit (e.g., 1024 MB or items, depends on usage)
        options.CompactionPercentage = 0.2; // Remove 20% when limit is reached
        Console.WriteLine($"Memory Cache Options set: SizeLimit={options.SizeLimit}, CompactionPercentage={options.CompactionPercentage}");
    });
    Console.WriteLine("Memory Cache Options configured successfully.");
}
catch (Exception ex)
{
    Console.WriteLine($"ERROR configuring Memory Cache Options: {ex.Message}");
    Console.WriteLine($"Stack Trace: {ex.StackTrace}");
}


// --- CORS Configuration ---
try
{
    Console.WriteLine("Configuring CORS Policies (AllowSpecificOrigins)...");
    builder.Services.AddCors(options =>
    {
        var allowedOrigins = new[] { "https://docktrack.lat", "https://www.docktrack.lat" };
        Console.WriteLine($"Adding CORS policy 'AllowSpecificOrigins' for origins: {string.Join(", ", allowedOrigins)}");
        options.AddPolicy("AllowSpecificOrigins",
            policyBuilder => policyBuilder // Renamed variable for clarity
                .WithOrigins(allowedOrigins)
                .AllowAnyMethod()
                .AllowAnyHeader());
        // Add more policies if needed
    });
    Console.WriteLine("CORS Policies configured successfully.");
}
catch (Exception ex)
{
    Console.WriteLine($"ERROR configuring CORS: {ex.Message}");
    Console.WriteLine($"Stack Trace: {ex.StackTrace}");
}


// --- Hangfire Server ---
try
{
    Console.WriteLine("Adding Hangfire Server...");
    builder.Services.AddHangfireServer(options => {
        // Log Hangfire server options if customized
        Console.WriteLine($"Hangfire Server Options: WorkerCount={options.WorkerCount}, Queues={string.Join(",", options.Queues ?? new string[] { "default" })}, ServerName={options.ServerName}");
    });
    Console.WriteLine("Hangfire Server added successfully.");
}
catch (Exception ex)
{
    Console.WriteLine($"ERROR adding Hangfire Server: {ex.Message}");
    Console.WriteLine("Ensure Hangfire storage was configured correctly before adding the server.");
    Console.WriteLine($"Stack Trace: {ex.StackTrace}");
    throw; // Hangfire server is likely essential
}

// --- Antiforgery Configuration ---
try
{
    Console.WriteLine("Configuring Antiforgery...");
    builder.Services.AddAntiforgery(options =>
    {
        options.HeaderName = "X-CSRF-TOKEN"; // For AJAX requests
        options.Cookie.Name = "CSRF-TOKEN";
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // HTTPS only
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.HttpOnly = true; // Cannot be accessed by client-side script
        options.SuppressXFrameOptionsHeader = true; // If needed, ensure frame security is handled elsewhere
        Console.WriteLine($"Antiforgery configured: HeaderName={options.HeaderName}, CookieName={options.Cookie.Name}, Secure={options.Cookie.SecurePolicy}, SameSite={options.Cookie.SameSite}, HttpOnly={options.Cookie.HttpOnly}");
    });
    Console.WriteLine("Antiforgery configured successfully.");
}
catch (Exception ex)
{
    Console.WriteLine($"ERROR configuring Antiforgery: {ex.Message}");
    Console.WriteLine($"Stack Trace: {ex.StackTrace}");
}


// ==================================================
// Build the Application
// ==================================================
Console.WriteLine("Building the WebApplication...");
WebApplication app;
try
{
    app = builder.Build();
    Console.WriteLine("WebApplication built successfully.");
}
catch (Exception ex)
{
    Console.WriteLine($"CRITICAL ERROR building the WebApplication: {ex.Message}");
    Console.WriteLine("This often indicates a fundamental issue in service configuration (DI container validation failed). Check previous logs.");
    Console.WriteLine($"Stack Trace: {ex.StackTrace}");
    // Cannot continue if app build fails. Rethrow to terminate.
    throw;
}


// ==================================================
// Configure the HTTP Request Pipeline
// ==================================================
Console.WriteLine("Configuring the HTTP request pipeline...");

// --- Database Migration/Initialization ---
if (builder.Environment.IsProduction())
{
    Console.WriteLine("PRODUCTION environment detected. Attempting to migrate database...");
    try
    {
        // Assuming MigrateDatabase is an extension method on IApplicationBuilder or WebApplication
        // You might need to resolve DbContext here if the method requires it
        // using var scope = app.Services.CreateScope();
        // var dbContext = scope.ServiceProvider.GetRequiredService<ComaviDbContext>();
        // dbContext.Database.Migrate(); // Example direct migration call
        app.MigrateDatabase(); // Call your extension method
        Console.WriteLine("Database migration attempted successfully (or no migrations needed).");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ERROR during database migration: {ex.Message}");
        Console.WriteLine("Check database connection, permissions, and migration scripts.");
        Console.WriteLine($"Stack Trace: {ex.StackTrace}");
        // Decide if startup should halt on migration failure
        // throw;
    }
}
else
{
    Console.WriteLine("DEVELOPMENT environment detected. Attempting to initialize database...");
    try
    {
        // DbInitializer likely needs a scope
        using var scope = app.Services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>(); // Assuming logger is needed by initializer
        // Ensure DbInitializer.Initialize is awaitable if it performs async operations
        await DbInitializer.Initialize(scope.ServiceProvider, logger); // Pass scope's provider
        Console.WriteLine("Database initialization completed successfully.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ERROR during database initialization: {ex.Message}");
        Console.WriteLine($"Stack Trace: {ex.StackTrace}");
        // Decide if startup should halt on init failure
        // throw;
    }
}

// --- Exception Handling and Security Headers ---
if (!app.Environment.IsDevelopment())
{
    try
    {
        Console.WriteLine("Configuring Exception Handler for production (/Home/Error)...");
        app.UseExceptionHandler("/Home/Error");
        Console.WriteLine("Exception Handler configured.");

        Console.WriteLine("Configuring HSTS (Strict Transport Security)...");
        app.UseHsts(); // The default HSTS value is 30 days. You may want to change this.
        Console.WriteLine("HSTS configured.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ERROR configuring production error handling or HSTS: {ex.Message}");
        Console.WriteLine($"Stack Trace: {ex.StackTrace}");
    }
}
else
{
    Console.WriteLine("Skipping production Exception Handler and HSTS in Development environment.");
    // Consider adding UseDeveloperExceptionPage() here if not already implicitly added by default templates
    // app.UseDeveloperExceptionPage();
}

// --- Middleware Pipeline Ordering ---
// Order is critical here!

try { Console.WriteLine("Adding SessionValidationMiddleware..."); app.UseMiddleware<SessionValidationMiddleware>(); }
catch (Exception ex) { Console.WriteLine($"ERROR adding SessionValidationMiddleware: {ex.Message}"); }

try { Console.WriteLine("Adding RateLimitingMiddleware..."); app.UseMiddleware<RateLimitingMiddleware>(); }
catch (Exception ex) { Console.WriteLine($"ERROR adding RateLimitingMiddleware: {ex.Message}"); }

// Assuming UseDatabaseResilience is a custom middleware
try { Console.WriteLine("Adding custom DatabaseResilience middleware..."); app.UseDatabaseResilience(); }
catch (Exception ex) { Console.WriteLine($"ERROR adding DatabaseResilience middleware: {ex.Message}"); }

try { Console.WriteLine("Adding HTTPS Redirection middleware..."); app.UseHttpsRedirection(); }
catch (Exception ex) { Console.WriteLine($"ERROR adding HttpsRedirection: {ex.Message}"); }

try { Console.WriteLine("Adding Static Files middleware..."); app.UseStaticFiles(); }
catch (Exception ex) { Console.WriteLine($"ERROR adding StaticFiles: {ex.Message}"); }

try { Console.WriteLine("Adding Routing middleware..."); app.UseRouting(); }
catch (Exception ex) { Console.WriteLine($"ERROR adding Routing: {ex.Message}"); }

try { Console.WriteLine("Adding CORS middleware (using 'AllowSpecificOrigins' policy)..."); app.UseCors("AllowSpecificOrigins"); }
catch (Exception ex) { Console.WriteLine($"ERROR adding CORS: {ex.Message}"); }

try { Console.WriteLine("Adding Authentication middleware..."); app.UseAuthentication(); }
catch (Exception ex) { Console.WriteLine($"ERROR adding Authentication middleware: {ex.Message}"); }

try { Console.WriteLine("Adding Authorization middleware..."); app.UseAuthorization(); }
catch (Exception ex) { Console.WriteLine($"ERROR adding Authorization middleware: {ex.Message}"); }

try { Console.WriteLine("Adding Session middleware..."); app.UseSession(); } // Must be after AuthN/AuthZ typically, before endpoints
catch (Exception ex) { Console.WriteLine($"ERROR adding Session middleware: {ex.Message}"); }

// --- Hangfire Dashboard ---
try
{
    Console.WriteLine("Configuring Hangfire Dashboard at /hangfire...");
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = new[] { new HangfireAuthorizationFilter() },
        // Add other options here if needed
        // AppPath = "/" // Link back to the main application
    });
    Console.WriteLine("Hangfire Dashboard configured successfully.");
}
catch (Exception ex)
{
    Console.WriteLine($"ERROR configuring Hangfire Dashboard: {ex.Message}");
    Console.WriteLine("Ensure Hangfire services and server were added correctly.");
    Console.WriteLine($"Stack Trace: {ex.StackTrace}");
}


// --- Register Hangfire Recurring Jobs ---
app.Lifetime.ApplicationStarted.Register(() => {
    Console.WriteLine("Application started. Registering Hangfire recurring jobs...");
    // It's better practice to use ILogger here, but using Console for consistency with the request.
    var logger = app.Services.GetRequiredService<ILogger<Program>>(); // Get logger for internal logging if needed
    try
    {
        Console.WriteLine("Registering recurring job: send-expiration-notifications (Daily at 08:00 UTC)...");
        RecurringJob.AddOrUpdate<INotificationService>(
            "send-expiration-notifications",
            service => service.SendExpirationNotificationsAsync(),
            "0 8 * * *", // Cron format: Minute Hour DayOfMonth Month DayOfWeek (UTC by default in Hangfire)
            TimeZoneInfo.FindSystemTimeZoneById("Central America Standard Time") // Example: Specify Time Zone if needed
            );

        Console.WriteLine("Registering recurring job: send-agenda-notifications (Daily at 07:00 local time)...");
        RecurringJob.AddOrUpdate<AgendaNotificationService>(
            "send-agenda-notifications",
            service => service.EnviarNotificacionesAgendaAsync(),
             Cron.Daily(7, 0), // Runs daily at 7:00 AM server local time (Check Hangfire docs for timezone specifics)
             TimeZoneInfo.Local // Explicitly use local time zone
             );

        Console.WriteLine("Registering recurring job: cleanup-non-verified-users (Daily at 02:00 local time)...");
        RecurringJob.AddOrUpdate<IUserCleanupService>(
            "cleanup-non-verified-users",
            service => service.CleanupNonVerifiedUsersAsync(3), // Passing parameter
            Cron.Daily(2, 0),
            TimeZoneInfo.Local);

        Console.WriteLine("Registering recurring job: cleanup-expired-sessions (Every 10 minutes)...");
        RecurringJob.AddOrUpdate<ISessionCleanupService>(
            "cleanup-expired-sessions",
            service => service.CleanupExpiredSessionsAsync(),
            "*/10 * * * *"); // Every 10 minutes

        Console.WriteLine("Registering recurring job: dashboard-cache-refresh (Every 15 minutes)...");
        RecurringJob.AddOrUpdate<AdminController>( // Assuming AdminController has a parameterless constructor or can be resolved by DI
            "dashboard-cache-refresh",
            service => service.ActualizarCacheDashboard(), // Ensure this method is suitable for Hangfire (idempotent, handles scope)
            "*/15 * * * *");

        Console.WriteLine("Registering recurring job: actualizarEstadoCamiones (Daily at 06:00 local time)...");
        RecurringJob.AddOrUpdate<IMantenimientoService>(
            "actualizarEstadoCamiones",
            x => x.ActualizarEstadosCamionesAsync(),
            Cron.Daily(6, 0),
            TimeZoneInfo.Local);

        Console.WriteLine("Registering recurring job: notificarMantenimientosHoy (Daily at 08:00 local time)...");
        RecurringJob.AddOrUpdate<IMantenimientoService>(
            "notificarMantenimientosHoy",
            x => x.NotificarMantenimientosAsync(),
            Cron.Daily(8, 0),
             TimeZoneInfo.Local);

        Console.WriteLine("Hangfire recurring jobs registered successfully.");
        logger.LogInformation("Successfully registered Hangfire recurring jobs"); // Use logger too
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ERROR registering Hangfire recurring jobs: {ex.Message}");
        Console.WriteLine($"Stack Trace: {ex.StackTrace}");
        logger.LogError(ex, "Error registering Hangfire recurring jobs"); // Use logger too
    }
});

// --- Controller Routing ---
try
{
    Console.WriteLine("Mapping default controller route...");
    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
        .AllowAnonymous(); // Be careful with AllowAnonymous on the default route

    Console.WriteLine("Mapping maintenance controller route...");
    app.MapControllerRoute(
       name: "maintenance",
       pattern: "Maintenance/{action=Index}/{id?}")
       .AllowAnonymous(); // Allow anonymous access to maintenance info? Double-check necessity.

    Console.WriteLine("Controller routes mapped successfully.");
}
catch (Exception ex)
{
    Console.WriteLine($"ERROR mapping controller routes: {ex.Message}");
    Console.WriteLine($"Stack Trace: {ex.StackTrace}");
}


// ==================================================
// Run the Application
// ==================================================
try
{
    Console.WriteLine("Application configuration complete. Starting application...");
    app.Run();
}
catch (Exception ex)
{
    // Catch errors that might occur during the final stages of startup or web server initialization
    Console.WriteLine($"FATAL ERROR during application run: {ex.Message}");
    Console.WriteLine($"Stack Trace: {ex.StackTrace}");
    // Optional: Log to a file or event log here as console might not be visible
    throw; // Re-throw to ensure process termination and logging by hosting environment
}

// ==================================================
// Supporting Classes (ensure visibility if needed)
// ==================================================
public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        // Add null checks for robustness
        var isAuthenticated = httpContext?.User?.Identity?.IsAuthenticated ?? false;
        var isAdmin = httpContext?.User?.IsInRole("admin") ?? false;

        if (!isAuthenticated)
        {
            Console.WriteLine("Hangfire Dashboard Access Denied: User not authenticated.");
            return false;
        }
        if (!isAdmin)
        {
            Console.WriteLine($"Hangfire Dashboard Access Denied: User '{httpContext?.User?.Identity?.Name ?? "Unknown"}' is not in 'admin' role.");
            return false;
        }

        Console.WriteLine($"Hangfire Dashboard Access Granted: User '{httpContext?.User?.Identity?.Name}' is authenticated and in 'admin' role.");
        return true; // Only allow if authenticated AND in admin role
    }
}
