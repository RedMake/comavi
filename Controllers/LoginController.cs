using COMAVI_SA.Data;
using COMAVI_SA.Middleware;
using COMAVI_SA.Models;
using COMAVI_SA.Repository;
using COMAVI_SA.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace COMAVI_SA.Controllers
{
#nullable disable
#pragma warning disable CS0168

    [Authorize]
    public class LoginController : Controller
    {
        private readonly IDatabaseRepository _databaseRepository;
        private readonly IUserService _userService;
        private readonly IPasswordService _passwordService;
        private readonly IOtpService _otpService;
        private readonly IJwtService _jwtService;
        private readonly IEmailService _emailService;
        private readonly IPdfService _pdfService;
        private readonly ComaviDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly IAuthorizationService _authorizationService;
        private readonly IEmailTemplatingService _emailTemplatingService;

        public LoginController(
            IDatabaseRepository databaseRepository,
            IUserService userService,
            IPasswordService passwordService,
            IMemoryCache cache,
            IOtpService otpService,
            IJwtService jwtService,
            IEmailService emailService,
            IPdfService pdfService,
            ComaviDbContext context,
            IAuthorizationService authorizationService,
            IEmailTemplatingService emailTemplatingService)
        {
            _databaseRepository = databaseRepository;
            _userService = userService;
            _passwordService = passwordService;
            _cache = cache;
            _otpService = otpService;
            _jwtService = jwtService;
            _emailService = emailService;
            _pdfService = pdfService;
            _context = context;
            _authorizationService = authorizationService;
            _emailTemplatingService = emailTemplatingService;
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult Index()
        {
            if (User.Identity.IsAuthenticated)
                return RedirectToAction("Index", "Home");

            return View();
        }

        [RateLimit(5, 300)]
        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> Index(LoginViewModel model)
        {
            try
            {

                if (!ModelState.IsValid)
                {

                    return View(model);
                }

                if (await _userService.IsAccountLockedAsync(model.Email))
                {
                    ModelState.AddModelError("", "Su cuenta ha sido bloqueada por múltiples intentos fallidos. Intente más tarde.");
                    return View(model);
                }

                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

                var user = await _userService.AuthenticateAsync(model.Email, model.Password);

                if (user == null)
                {
                    await _userService.RecordLoginAttemptAsync(null, ipAddress, false);
                    ModelState.AddModelError("", "Correo electrónico o contraseña incorrectos.");
                    return View(model);
                }

                if (user.estado_verificacion != "verificado")
                {
                    ModelState.AddModelError("", "Su cuenta no ha sido verificada. Por favor, revise su correo electrónico para completar el proceso de verificación.");
                    return View(model);
                }

                await _userService.RecordLoginAttemptAsync(user.id_usuario, ipAddress, true);

                TempData["UserEmail"] = user.correo_electronico;
                TempData["UserId"] = user.id_usuario;
                TempData["RememberMe"] = model.RememberMe;

                var token = _jwtService.GenerateJwtToken(user);
                HttpContext.Session.SetString("JwtToken", token);

                return RedirectToAction("VerifyOtp");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error en el servidor. Por favor, intente más tarde.");
                return View(model);
            }
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> ConfigurarMFA()
        {
            try
            {
                int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
                string userEmail = User.Identity?.Name;

                // Verificar si MFA ya está habilitado
                var user = await _userService.GetUserByIdAsync(userId);
                if (user == null)
                {
                    TempData["Error"] = "Error al cargar los datos del usuario";
                    return RedirectToAction("Profile");
                }

                // Si MFA ya está habilitado, mostrar vista para desactivarlo
                if (user.mfa_habilitado)
                {
                    ViewBag.MfaHabilitado = true;
                    return View(new ConfigurarMFAViewModel());
                }

                // Si MFA no está habilitado, configurar nuevo MFA
                await _userService.SetupMfaAsync(userId);
                var secret = await _userService.GetMfaSecretAsync(userId);

                if (string.IsNullOrEmpty(secret))
                {
                    TempData["Error"] = "Error al generar el código de autenticación";
                    return RedirectToAction("Profile");
                }

                var model = new ConfigurarMFAViewModel
                {
                    Secret = secret,
                    QrCodeUrl = _otpService.GenerateQrCodeUri(secret, userEmail)
                };

                return View(model);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al configurar la autenticación de dos factores";
                return RedirectToAction("Profile");
            }
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> ConfigurarMFA(ConfigurarMFAViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            try
            {
                int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

                // Verificar si MFA ya está habilitado
                if (await _userService.IsMfaEnabledAsync(userId))
                {
                    TempData["Error"] = "La autenticación de dos factores ya está habilitada.";
                    return RedirectToAction("Profile");
                }

                // Activar MFA y verificar código
                bool activado = await _userService.EnableMfaAsync(userId, model.OtpCode);

                if (!activado)
                {
                    ModelState.AddModelError("OtpCode", "Código OTP inválido. Verifique que lo haya ingresado correctamente.");

                    // Volver a cargar datos para el QR
                    string userEmail = User.FindFirstValue(ClaimTypes.Email);
                    var secret = await _userService.GetMfaSecretAsync(userId);
                    model.Secret = secret;
                    model.QrCodeUrl = _otpService.GenerateQrCodeUri(secret, userEmail);

                    return View(model);
                }

                // Generar códigos de respaldo
                var codigosRespaldo = await _userService.GenerateBackupCodesAsync(userId);

                // Almacenar códigos en TempData para mostrarlos
                TempData["CodigosRespaldo"] = codigosRespaldo;

                TempData["SuccessMessage"] = "Autenticación de dos factores activada exitosamente.";
                return RedirectToAction("MostrarCodigosRespaldo");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al configurar la autenticación de dos factores";
                return RedirectToAction("Profile");
            }
        }

        [Authorize]
        [HttpGet]
        public IActionResult MostrarCodigosRespaldo()
        {
            // Try to get backup codes from TempData
            var codigos = TempData["CodigosRespaldo"] as List<string>;

            // If not found in TempData, try to generate new ones
            if (codigos == null || !codigos.Any())
            {
                try
                {
                    int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

                    // Try to regenerate backup codes
                    Task<List<string>> taskCodigos = _userService.GenerateBackupCodesAsync(userId);
                    taskCodigos.Wait(); // Since we're in a synchronous method
                    codigos = taskCodigos.Result;

                    if (codigos == null || !codigos.Any())
                    {
                        TempData["Error"] = "No se pudieron generar códigos de respaldo.";
                        return RedirectToAction("Profile");
                    }
                }
                catch (Exception ex)
                {
                    TempData["Error"] = "No hay códigos de respaldo disponibles";
                    return RedirectToAction("Profile");
                }
            }

            var model = new CodigosRespaldoViewModel
            {
                Codigos = codigos
            };

            return View(model);
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> VerifyOtp()
        {
            try
            {
                // Si ya está autenticado completamente, verificar si ha completado MFA
                if (User.Identity.IsAuthenticated)
                {
                    // Verificar si el usuario tiene el claim MfaCompleted
                    bool mfaCompleted = User.HasClaim(c => c.Type == "MfaCompleted" && c.Value == "true");
                    var authResult = await _authorizationService.AuthorizeAsync(User, "RequireAdminRole");

                    if (mfaCompleted && authResult.Succeeded)
                    {
                        return RedirectToAction("Index", "Admin");
                    }
                    else if (mfaCompleted)
                    {
                        return RedirectToAction("Index", "Home");
                    }

                    // Si está autenticado pero no ha completado MFA, limpiar su autenticación
                    await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    HttpContext.Session.Clear();
                    foreach (var cookie in Request.Cookies.Keys)
                    {
                        Response.Cookies.Delete(cookie);
                    }
                    return RedirectToAction("Index");
                }

                // Verificar que tenemos datos de usuario en TempData
                var userId = TempData.Peek("UserId") as int?;
                var email = TempData.Peek("UserEmail") as string;


                if (!userId.HasValue || string.IsNullOrEmpty(email))
                {
                    TempData["Error"] = "Su sesión ha expirado. Por favor, inicie sesión nuevamente.";
                    return RedirectToAction("Index");
                }

                // Verificar si el usuario existe
                var user = await _userService.GetUserByIdAsync(userId.Value);
                if (user == null)
                {
                    TempData["Error"] = "Usuario no encontrado. Por favor, inicie sesión nuevamente.";
                    return RedirectToAction("Index");
                }

                // Si el usuario no tiene MFA habilitado, pasar directamente al login
                if (!user.mfa_habilitado)
                {

                    // Crear un modelo OtpViewModel con un código ficticio "bypass"
                    var bypassModel = new OtpViewModel { Email = email, OtpCode = "bypass" };

                    // Llamar directamente al método POST de VerifyOtp
                    return await VerifyOtp(bypassModel);
                }

                var mfaSecret = await _userService.GetMfaSecretAsync(userId.Value);

                // Verificar límite máximo de intentos global (no solo de la sesión actual)
                int intentosFallidos = await _userService.GetFailedMfaAttemptsAsync(userId.Value);

                // Si excede el límite máximo (por ejemplo, 10 intentos), bloquear temporalmente la cuenta
                if (intentosFallidos >= 10)
                {
                    TempData["Error"] = "Ha excedido el número máximo de intentos. Su cuenta ha sido bloqueada temporalmente. Contacte al administrador.";
                    return RedirectToAction("Index");
                }

                var model = new OtpViewModel { Email = email, IntentosFallidos = intentosFallidos };

                // Si hay 3 o más intentos fallidos, mostrar opción de código de respaldo
                if (intentosFallidos >= 3)
                {
                    model.UsarCodigoRespaldo = true;
                    ViewBag.MostrarCodigoRespaldo = true;
                }

                // Guardar timestamp para calcular tiempo de expiración
                TempData["OtpTimestamp"] = DateTime.Now.Ticks.ToString();

                // Guardar la URL actual para poder redirigir después de la verificación
                if (TempData["ReturnUrl"] == null)
                {
                    TempData["ReturnUrl"] = "/Home/Index"; // URL predeterminada
                }

                return View(model);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error en el servidor. Por favor, intente más tarde.";
                return RedirectToAction("Index");
            }
        }

        [RateLimit(5, 300)] // 5 intentos en 5 minutos
        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> VerifyOtp(OtpViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            try
            {
                var userId = TempData["UserId"] as int?;
                var email = TempData["UserEmail"] as string;
                var rememberMe = TempData["RememberMe"] as bool? ?? false;

                if (!userId.HasValue || string.IsNullOrEmpty(email))
                {
                    TempData["Error"] = "Su sesión ha expirado. Por favor, inicie sesión nuevamente.";
                    return RedirectToAction("Index");
                }

                if (TempData["OtpTimestamp"] != null)
                {
                    long timestamp = long.Parse(TempData["OtpTimestamp"].ToString());
                    TimeSpan elapsed = TimeSpan.FromTicks(DateTime.Now.Ticks - timestamp);

                    if (elapsed.TotalMinutes > 5)
                    {

                        // Verificar si el usuario es administrador
                        var user_obtain = await _userService.GetUserByIdAsync(userId.Value);

                        if (user_obtain != null && user_obtain.rol == "admin")
                        {
                            // Para administradores, permitir regenerar el OTP
                            TempData["RegenerateOtp"] = true;
                            TempData["Error"] = "El código OTP ha expirado. Como administrador, puede regenerarlo.";
                            return View(model);
                        }
                        else
                        {
                            // Para usuarios normales, comportamiento normal
                            TempData["Error"] = "El código OTP ha expirado. Por favor, inicie sesión nuevamente.";
                            return RedirectToAction("Index");
                        }
                    }
                }

                bool verificacionExitosa = false;

                // Verificar si el usuario está utilizando código de respaldo
                if (model.UsarCodigoRespaldo)
                {
                    verificacionExitosa = await _userService.VerifyBackupCodeAsync(userId.Value, model.OtpCode);

                    if (!verificacionExitosa)
                    {
                        await _userService.RecordMfaAttemptAsync(userId.Value, false);

                        // Actualizar contador de intentos fallidos
                        model.IntentosFallidos = await _userService.GetFailedMfaAttemptsAsync(userId.Value);

                        // Verificar si excedió el límite máximo
                        if (model.IntentosFallidos >= 10)
                        {
                            TempData["Error"] = "Ha excedido el número máximo de intentos. Su cuenta ha sido bloqueada temporalmente.";
                            return RedirectToAction("Index");
                        }

                        ModelState.AddModelError("", "Código de respaldo inválido. Intente nuevamente.");
                        ViewBag.MostrarCodigoRespaldo = true;
                        return View(model);
                    }
                }
                else if (model.OtpCode == "bypass")
                {
                    // Código especial para bypass cuando MFA no está habilitado
                    var currentUser = await _userService.GetUserByIdAsync(userId.Value);
                    verificacionExitosa = currentUser != null && !currentUser.mfa_habilitado;

                    if (!verificacionExitosa)
                    {
                        TempData["Error"] = "Error de autenticación. Por favor, inicie sesión nuevamente.";
                        return RedirectToAction("Index");
                    }
                }
                else
                {
                    // Verificación normal de OTP
                    verificacionExitosa = await _userService.VerifyMfaCodeAsync(userId.Value, model.OtpCode);

                    if (!verificacionExitosa)
                    {
                        // Incrementar intentos fallidos
                        await _userService.RecordMfaAttemptAsync(userId.Value, false);

                        // Obtener número de intentos fallidos
                        var intentosFallidos = await _userService.GetFailedMfaAttemptsAsync(userId.Value);

                        ModelState.AddModelError("", "Código OTP inválido. Intente nuevamente.");

                        // Si hay 3 o más intentos fallidos, mostrar opción de código de respaldo
                        if (intentosFallidos >= 3)
                        {
                            ViewBag.MostrarCodigoRespaldo = true;
                            ModelState.AddModelError("", "Ha excedido el número máximo de intentos. Puede usar un código de respaldo para continuar.");
                        }

                        // Si excedió el límite máximo, bloquear temporalmente
                        if (intentosFallidos >= 10)
                        {
                            TempData["Error"] = "Ha excedido el número máximo de intentos. Su cuenta ha sido bloqueada temporalmente.";
                            return RedirectToAction("Index");
                        }

                        model.IntentosFallidos = intentosFallidos;

                        // Actualizar timestamp para extender el tiempo
                        TempData["OtpTimestamp"] = DateTime.Now.Ticks.ToString();

                        return View(model);
                    }
                }

                var user = await _userService.GetUserByIdAsync(userId.Value);
                if (user == null)
                {
                    TempData["Error"] = "Usuario no encontrado. Por favor, inicie sesión nuevamente.";
                    return RedirectToAction("Index");
                }

                // Actualizar último ingreso
                user.ultimo_ingreso = DateTime.Now;
                await _context.SaveChangesAsync();

                // Reiniciar contador de intentos fallidos MFA
                await _userService.ResetMfaFailedAttemptsAsync(userId.Value);

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.id_usuario.ToString()),
                    new Claim(ClaimTypes.Name, user.nombre_usuario),
                    new Claim(ClaimTypes.Email, user.correo_electronico),
                    new Claim(ClaimTypes.Role, user.rol),
                    // Añadir un claim específico para indicar que MFA ha sido completado
                    new Claim("MfaCompleted", "true")
                };

                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new ClaimsPrincipal(identity);

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    principal,
                    new AuthenticationProperties
                    {
                        IsPersistent = rememberMe,
                        ExpiresUtc = rememberMe ?
                            DateTime.UtcNow.AddDays(7) :
                            DateTime.UtcNow.AddMinutes(30)
                    });

                // Registrar sesión activa
                var sessionInfo = new SesionesActivas
                {
                    id_usuario = user.id_usuario,
                    dispositivo = HttpContext.Request.Headers["User-Agent"].ToString(),
                    ubicacion = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                    fecha_inicio = DateTime.Now,
                    fecha_ultima_actividad = DateTime.Now
                };

                _context.SesionesActivas.Add(sessionInfo);
                await _context.SaveChangesAsync();

                // Establecer indicador de MFA completado en TempData
                TempData["MfaCompleted"] = true;

                // Si es chofer y no ha completado su perfil, redirigir a completar perfil
                if (user.rol == "user")
                {
                    string userEmail = user.correo_electronico;
                    string cedula = string.Empty;

                    if (userEmail.Contains("@"))
                    {
                        cedula = userEmail.Substring(0, userEmail.IndexOf('@'));
                    }

                    var chofer = await _context.Choferes
                        .FirstOrDefaultAsync(c => c.numero_cedula == cedula);

                    if (chofer == null)
                    {
                        TempData["PerfilMessage"] = "Por favor, complete su perfil para continuar.";
                        return RedirectToAction("Profile");
                    }
                }

                // Redireccionar a la URL de retorno o a Home/Index
                string returnUrl = TempData["ReturnUrl"]?.ToString() ?? "/Home/Index";
                return Redirect(returnUrl);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error en el servidor. Por favor, intente más tarde.");
                return View(model);
            }
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> RegenerateOtp()
        {
            var userId = TempData["UserId"] as int?;
            var email = TempData["UserEmail"] as string;

            if (!userId.HasValue || string.IsNullOrEmpty(email))
            {
                TempData["Error"] = "Su sesión ha expirado. Por favor, inicie sesión nuevamente.";
                return RedirectToAction("Index");
            }

            // Verificar que el usuario sea administrador
            var user = await _userService.GetUserByIdAsync(userId.Value);
            if (user == null || user.rol != "admin")
            {
                TempData["Error"] = "No tiene permisos para regenerar el código OTP.";
                return RedirectToAction("Index");
            }

            // Reiniciar el timestamp para extender el tiempo
            TempData["OtpTimestamp"] = DateTime.Now.Ticks.ToString();

            // Mantener los datos del usuario en TempData para la siguiente solicitud
            TempData.Keep("UserId");
            TempData.Keep("UserEmail");
            TempData.Keep("RememberMe");
            TempData.Keep("ReturnUrl");

            var model = new OtpViewModel { Email = email };
            return View("VerifyOtp", model);
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> RegenerarCodigosRespaldo()
        {
            try
            {
                int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

                // Verificar si MFA está habilitado
                if (!await _userService.IsMfaEnabledAsync(userId))
                {
                    TempData["Error"] = "Debe habilitar la autenticación de dos factores primero";
                    return RedirectToAction("Profile");
                }

                // Generar nuevos códigos de respaldo
                var codigosRespaldo = await _userService.GenerateBackupCodesAsync(userId);

                // Almacenar códigos en TempData para mostrarlos
                TempData["CodigosRespaldo"] = codigosRespaldo;
                TempData["SuccessMessage"] = "Se han generado nuevos códigos de respaldo. Los anteriores ya no son válidos.";

                return RedirectToAction("MostrarCodigosRespaldo");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al regenerar los códigos de respaldo";
                return RedirectToAction("Profile");
            }
        }

        [Authorize]
        [HttpGet]
        public IActionResult DesactivarMFA()
        {
            return View();
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> DesactivarMFA(string codigoRespaldo)
        {
            try
            {
                int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

                // Verificar si MFA está habilitado
                if (!await _userService.IsMfaEnabledAsync(userId))
                {
                    TempData["Error"] = "La autenticación de dos factores ya está desactivada";
                    return RedirectToAction("Profile");
                }

                // Desactivar MFA
                bool desactivado = await _userService.DisableMfaAsync(userId, codigoRespaldo);

                if (!desactivado)
                {
                    TempData["Error"] = "Código de respaldo inválido. No se pudo desactivar la autenticación de dos factores.";
                    return View();
                }

                TempData["SuccessMessage"] = "Autenticación de dos factores desactivada exitosamente.";
                return RedirectToAction("Profile");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al desactivar la autenticación de dos factores";
                return RedirectToAction("Profile");
            }
        }

        [AllowAnonymous]
        [HttpGet]
        public Task<IActionResult> Register()
        {
            return Task.FromResult<IActionResult>(View());
        }

        [RateLimit(3, 600)] // 3 intentos en 10 minutos
        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            try
            {
                if (await _userService.IsEmailExistAsync(model.Email))
                {
                    ModelState.AddModelError("Email", "Este correo electrónico ya está registrado.");
                    return View(model);
                }
                model.UserName = model.UserName?.Trim();

                // Generar token de verificación
                var verificationToken = GenerateVerificationToken();
                var tokenExpiration = DateTime.Now.AddDays(3);

                var result = await _userService.RegisterAsync(model, verificationToken, tokenExpiration);
                if (!result)
                {
                    ModelState.AddModelError("", "Error al registrar el usuario. Por favor, intente nuevamente. Este error puede ser solo temporal no se preocupe");
                    return View(model);
                }

                // Enviar correo con instrucciones de verificación
                await EnviarCorreoVerificacion(model.Email, model.UserName, verificationToken);
                
                TempData["SuccessMessage"] = "Usuario registrado exitosamente. Se han enviado instrucciones de verificación al correo proporcionado.";

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error en el servidor. Por favor, intente más tarde.");
                return View(model);
            }
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult Verificar(string token, string email)
        {
            var model = new VerificacionViewModel { Token = token, Email = email };
            return View(model);
        }

        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> Verificar(VerificacionViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            try
            {
                var user = await _context.Usuarios
                    .FirstOrDefaultAsync(u => u.correo_electronico == model.Email &&
                                             u.token_verificacion == model.Token &&
                                             u.fecha_expiracion_token > DateTime.Now &&
                                             u.estado_verificacion == "pendiente");

                if (user == null)
                {
                    ModelState.AddModelError("", "Token inválido, expirado o ya utilizado.");
                    return View(model);
                }

                // Actualizar estado de verificación
                user.estado_verificacion = "verificado";
                user.fecha_verificacion = DateTime.Now;
                user.token_verificacion = null;
                user.fecha_expiracion_token = null;

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "¡Cuenta verificada exitosamente! Ahora puede iniciar sesión.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error en el servidor. Por favor, intente más tarde.");
                return View(model);
            }
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult InstruccionesVerificacion(string email, string nombre)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(nombre))
                return RedirectToAction("Index");

            var model = new InstruccionesVerificacionViewModel
            {
                Email = email,
                NombreUsuario = nombre,
                PasosVerificacion = ObtenerPasosVerificacion()
            };

            return View(model);
        }

        [Authorize(Roles = "admin,user")]
        [HttpGet]
        public async Task<IActionResult> CompletarPerfil()
        {

            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index");
            }

            try
            {
                // Obtener el ID del usuario actual
                int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

                var usuario = await _context.Usuarios.FindAsync(userId);

                if (usuario == null)
                {
                    return RedirectToAction("Index");
                }

                // Verificar si ya existe un perfil para este usuario usando la relación directa
                var choferExistente = await _context.Choferes
                    .FirstOrDefaultAsync(c => c.id_usuario == userId);

                // Si ya existe un perfil, redirigir a SubirDocumentos
                if (choferExistente != null)
                {

                    TempData["SuccessMessage"] = "Su perfil ya está completo. Ahora puede subir sus documentos.";
                    return RedirectToAction("SubirDocumentos");
                }

                // Si no existe perfil, mostrar la vista para completarlo
                return View();
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al verificar su perfil. Por favor, intente más tarde.";
                return RedirectToAction("Index", "Home");
            }
        }

        [Authorize(Roles = "admin,user")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompletarPerfil(PerfilViewModel model)
        {
            try
            {

                if (!ModelState.IsValid && model.Estado != null)
                {

                    return View(model);
                }


                int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

                var usuario = await _context.Usuarios.FindAsync(userId);
                if (usuario == null)
                {
                    return RedirectToAction("Index");
                }

                // Verificar si existe un registro de chofer para este usuario
                var choferExistente = await _context.Choferes
                    .FirstOrDefaultAsync(c => c.id_usuario == userId);

                if (choferExistente != null)
                {
                    TempData["SuccessMessage"] = "Su perfil ya está completo. Ahora puede subir sus documentos.";
                    return RedirectToAction("SubirDocumentos");
                }

                // Crear nuevo registro de chofer
                var nuevoChofer = new Choferes
                {
                    id_usuario = userId,  // Establecemos la relación con el usuario actual
                    nombreCompleto = usuario.nombre_usuario,
                    edad = model.Edad.Value,
                    numero_cedula = model.Numero_Cedula,
                    licencia = model.Licencia,
                    fecha_venc_licencia = model.Fecha_Venc_Licencia.Value,
                    estado = "activo",
                    genero = model.Genero
                };

                _context.Choferes.Add(nuevoChofer);

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Perfil completado exitosamente.";
                return RedirectToAction("SubirDocumentos");
            }
            catch (Exception ex)
            {

                // Intentar obtener más información sobre la excepción
                if (ex.InnerException != null)
                {
                    throw;
                }

                ModelState.AddModelError("", "Error en el servidor. Por favor, intente más tarde.");
                return View(model);
            }
        }

        [Authorize(Roles = "admin,user")]
        [HttpGet]
        public async Task<IActionResult> SubirDocumentos()
        {
            if (!User.Identity.IsAuthenticated)
                return RedirectToAction("Index");
            try
            {
                int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

                // Consulta optimizada que obtiene solo los datos necesarios
                var chofer = await _context.Choferes
                    .AsNoTracking()
                    .Where(c => c.id_usuario == userId)
                    .Select(c => new { c.id_chofer, c.nombreCompleto })
                    .FirstOrDefaultAsync();

                if (chofer == null)
                {
                    TempData["Error"] = "Debe completar su perfil primero.";
                    return RedirectToAction("Profile");
                }

                // Definir los tipos de documentos requeridos
                var tiposRequeridos = new List<string> { "licencia", "carnet", "seguro", "tarjeta" };

                // Consulta optimizada para obtener documentos con sus estados más recientes por tipo
                var tiposDocumentosExistentes = await _context.Documentos
                    .AsNoTracking()
                    .Where(d => d.id_chofer == chofer.id_chofer)
                    .GroupBy(d => d.tipo_documento)
                    .Select(g => new {
                        TipoDocumento = g.Key,
                        Estado = g.OrderByDescending(d => d.estado_validacion == "verificado")
                                  .ThenByDescending(d => d.fecha_emision)
                                  .Select(d => d.estado_validacion)
                                  .FirstOrDefault(),
                        Documento = g.OrderByDescending(d => d.fecha_emision)
                                     .Select(d => new Documentos
                                     {
                                         id_documento = d.id_documento,
                                         tipo_documento = d.tipo_documento,
                                         fecha_emision = d.fecha_emision,
                                         fecha_vencimiento = d.fecha_vencimiento,
                                         estado_validacion = d.estado_validacion
                                     })
                                     .FirstOrDefault()
                    })
                    .ToListAsync();

                // Determinar documentos faltantes con una sola operación
                var documentosFaltantes = tiposRequeridos
                    .Except(tiposDocumentosExistentes.Select(t => t.TipoDocumento))
                    .ToList();

                // Verificar si todos los documentos están verificados con una sola operación
                bool todosVerificados = tiposDocumentosExistentes.Count == tiposRequeridos.Count &&
                                       tiposDocumentosExistentes.All(d => d.Estado == "verificado");

                // Crear modelo con los documentos existentes
                var documentosExistentes = tiposDocumentosExistentes
                    .Where(t => t.Documento != null)
                    .Select(t => t.Documento)
                    .ToList();

                var model = new CargaDocumentoViewModel
                {
                    DocumentosExistentes = documentosExistentes,
                    DocumentosFaltantes = documentosFaltantes,
                    IdChofer = chofer.id_chofer
                };

                // Redireccionar si todos los documentos están verificados
                if (todosVerificados)
                {
                    TempData["InfoMessage"] = "Todos sus documentos ya están verificados.";
                    return RedirectToAction("Profile");
                }

                // Cachear los datos para reducir consultas futuras (válido por 5 minutos)
                string cacheKey = $"documentos_chofer_{chofer.id_chofer}";
                _cache.Set(cacheKey, new
                {
                    DocumentosExistentes = documentosExistentes,
                    DocumentosFaltantes = documentosFaltantes
                }, new MemoryCacheEntryOptions()
                        .SetAbsoluteExpiration(TimeSpan.FromMinutes(5))
                        .SetSize(1)
                    );

                return View(model);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Ocurrió un error al cargar la página.";
                return RedirectToAction("Profile");
            }
        }
        
        [Authorize(Roles = "admin,user")]
        [HttpPost]
        public async Task<IActionResult> SubirDocumentos(CargaDocumentoViewModel model)
        {
            try
            {
                int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var chofer = await _context.Choferes
                    .FirstOrDefaultAsync(c => c.id_usuario == userId);

                if (chofer == null)
                {
                    TempData["Error"] = "Debe completar su perfil primero.";
                    return RedirectToAction("Profile");
                }

                // Validar si el documento ya está verificado
                var documentosExistentes = await _context.Documentos
                    .Where(d => d.id_chofer == chofer.id_chofer &&
                                d.tipo_documento == model.TipoDocumento)
                    .ToListAsync();

                var documentoVerificado = documentosExistentes
                    .FirstOrDefault(d => d.estado_validacion == "verificado");

                if (documentoVerificado != null)
                {
                    ModelState.AddModelError("TipoDocumento",
                        "Este tipo de documento ya ha sido verificado y no puede ser modificado.");
                    return View(model);
                }

                // Validar PDF (códigos anteriores siguen igual)
                var (isValid, errorMessage) = await _pdfService.ValidatePdfAsync(model.ArchivoPdf);
                if (!isValid)
                {
                    ModelState.AddModelError("ArchivoPdf", errorMessage);
                    return View(model);
                }

                // Extraer texto para validar contenido
                string pdfText = await _pdfService.ExtractTextFromPdfAsync(model.ArchivoPdf);
                if (!_pdfService.ContainsRequiredInformation(pdfText, model.TipoDocumento))
                {
                    ModelState.AddModelError("ArchivoPdf",
                        $"El documento no parece contener la información necesaria para un documento tipo '{model.TipoDocumento}'.");
                    return View(model);
                }

                // Guardar archivo
                var (filePath, fileHash) = await _pdfService.SavePdfAsync(model.ArchivoPdf);

                // Manejar documentos pendientes o rechazados
                var documentoPendiente = documentosExistentes
                    .FirstOrDefault(d => d.estado_validacion == "pendiente" ||
                                         d.estado_validacion == "rechazado");

                if (documentoPendiente != null)
                {
                    // Actualizar documento existente
                    documentoPendiente.fecha_emision = model.FechaEmision;
                    documentoPendiente.fecha_vencimiento = model.FechaVencimiento;
                    documentoPendiente.ruta_archivo = filePath;
                    documentoPendiente.hash_documento = fileHash;
                    documentoPendiente.tipo_mime = "application/pdf";
                    documentoPendiente.tamano_archivo = (int)model.ArchivoPdf.Length;
                    documentoPendiente.estado_validacion = "pendiente";
                }
                else
                {
                    // Crear nuevo documento si no existe
                    var documento = new Documentos
                    {
                        id_chofer = chofer.id_chofer,
                        tipo_documento = model.TipoDocumento,
                        fecha_emision = model.FechaEmision,
                        fecha_vencimiento = model.FechaVencimiento,
                        ruta_archivo = filePath,
                        tipo_mime = "application/pdf",
                        tamano_archivo = (int)model.ArchivoPdf.Length,
                        hash_documento = fileHash,
                        estado_validacion = "pendiente"
                    };

                    _context.Documentos.Add(documento);
                }

                await _context.SaveChangesAsync();

                // Notificar a administradores (código anterior)
                var admins = await _context.Usuarios
                    .Where(u => u.rol == "admin")
                    .ToListAsync();

                var notificaciones = admins.Select(admin => new Notificaciones_Usuario
                {
                    id_usuario = admin.id_usuario,
                    tipo_notificacion = "Documento Nuevo",
                    fecha_hora = DateTime.Now,
                    mensaje = $"El chofer {chofer.nombreCompleto} ha subido un nuevo documento ({model.TipoDocumento})..."
                }).ToList();

                _context.NotificacionesUsuario.AddRange(notificaciones);

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Documento subido exitosamente. Un administrador lo validará próximamente.";
                return RedirectToAction("Profile");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error en el servidor. Por favor, intente más tarde.");
                return View(model);
            }
        }

        [Authorize(Roles = "admin,user")]
        [HttpGet]
        public IActionResult LogoutGet()
        {
            return View();
        }

        [Authorize(Roles = "admin,user")]
        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            try
            {
                string sessionId = HttpContext.Session.Id;

                // Limpiar token JWT de la sesión
                string jwtToken = HttpContext.Session.GetString("JwtToken");
                if (!string.IsNullOrEmpty(jwtToken))
                {
                    // Añadir el token a la lista negra si existe un servicio para ello
                    var blacklistService = HttpContext.RequestServices.GetService<IJwtBlacklistService>();
                    if (blacklistService != null)
                    {
                        // Añadir a la lista negra por 24 horas
                        blacklistService.AddToBlacklist(jwtToken, TimeSpan.FromHours(24));
                    }
                }

                // Eliminar solo la sesión activa actual de la base de datos
                if (User.Identity.IsAuthenticated)
                {
                    var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

                    // Obtener información para ayudar a identificar la sesión actual
                    string userAgent = Request.Headers["User-Agent"].ToString();
                    string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

                    // Buscar la sesión activa que mejor coincida con la sesión actual
                    var sesionActual = await _context.SesionesActivas
                        .Where(s => s.id_usuario == userId)
                        .FirstOrDefaultAsync(s =>
                            s.dispositivo.Contains(userAgent) ||
                            (s.ubicacion != null && s.ubicacion.Contains(ipAddress)));

                    // Si no encontramos una coincidencia exacta, tomamos la última actualizada
                    if (sesionActual == null)
                    {
                        sesionActual = await _context.SesionesActivas
                            .Where(s => s.id_usuario == userId)
                            .OrderByDescending(s => s.fecha_ultima_actividad)
                            .FirstOrDefaultAsync();
                    }

                    // Eliminar la sesión si la encontramos
                    if (sesionActual != null)
                    {
                        _context.SesionesActivas.Remove(sesionActual);
                        await _context.SaveChangesAsync();
                    }
                    
                    

                    var idSesionParam = new SqlParameter("@idSesion", SqlDbType.NVarChar, 500) { Value = sessionId };
                    var idUsuarioParam = new SqlParameter("@idUsuario", SqlDbType.Int) { Value = userId };
                    var returnValue = new SqlParameter("@returnValue", SqlDbType.Int) { Direction = ParameterDirection.ReturnValue };
                }

                HttpContext.Session.Remove("JwtToken");

                // Limpiar todos los TempData relacionados con la autenticación
                TempData.Remove("UserEmail");
                TempData.Remove("UserId");
                TempData.Remove("RememberMe");
                TempData.Remove("MfaCompleted");
                TempData.Remove("OtpTimestamp");
                TempData.Remove("ReturnUrl");

                // Eliminar todas las cookies con configuración exhaustiva
                foreach (var cookie in Request.Cookies.Keys)
                {
                    Response.Cookies.Delete(cookie, new CookieOptions
                    {
                        Expires = DateTime.Now.AddDays(-1),
                        SameSite = SameSiteMode.Strict,
                        Secure = true,
                        HttpOnly = true,
                        Path = "/"
                    });
                }

                // Asegurar que se eliminan las cookies de autenticación específicas
                Response.Cookies.Delete(".AspNetCore.Cookies", new CookieOptions
                {
                    Expires = DateTime.Now.AddDays(-1),
                    SameSite = SameSiteMode.Strict,
                    Secure = true,
                    HttpOnly = true,
                    Path = "/"
                });

                Response.Cookies.Delete("COMAVI.Auth", new CookieOptions
                {
                    Expires = DateTime.Now.AddDays(-1),
                    SameSite = SameSiteMode.Strict,
                    Secure = true,
                    HttpOnly = true,
                    Path = "/"
                });

                Response.Cookies.Delete("COMAVI.Session", new CookieOptions
                {
                    Expires = DateTime.Now.AddDays(-1),
                    SameSite = SameSiteMode.Strict,
                    Secure = true,
                    HttpOnly = true,
                    Path = "/"
                });

                // Limpiar datos de sesión
                HttpContext.Session.Clear();

                // Cerrar autenticación 
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

                // Limpiar el principal de autenticación
                HttpContext.User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity());


                // Forzar nueva sesión con parámetros para evitar cacheo
                return RedirectToAction("Index", new { t = DateTime.Now.Ticks, clean = true, forceNew = true });
            }
            catch (Exception ex)
            {

                try
                {
                    // Intento de limpieza de emergencia
                    HttpContext.Session.Clear();
                    await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                }
                catch { /* Absorber cualquier error adicional */ }

                return RedirectToAction("Index", new { t = DateTime.Now.Ticks, error = true });
            }
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult ResetPassword(string token, string email)
        {
            var model = new ResetPasswordViewModel { Codigo = token, Email = email };
            return View(model);
        }

        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            try
            {
                var result = await _userService.ResetPasswordAsync(model.Email, model.Codigo, model.NewPassword);
                if (!result)
                {
                    ModelState.AddModelError("", "Token inválido o expirado.");
                    return View(model);
                }

                TempData["SuccessMessage"] = "Contraseña actualizada exitosamente.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error en el servidor. Por favor, intente más tarde.");
                return View(model);
            }
        }

        [Authorize(Roles = "user")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SolicitarMantenimiento(int idCamion, string observaciones)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(observaciones))
                {
                    TempData["Error"] = "Debe proporcionar observaciones para solicitar mantenimiento.";
                    return RedirectToAction("Profile", "Login");
                }

                // Obtener ID del chofer actual
                int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var chofer = await _context.Choferes
                    .FirstOrDefaultAsync(c => c.id_usuario == userId);

                if (chofer == null)
                {
                    TempData["Error"] = "No se encontró su perfil de chofer.";
                    return RedirectToAction("Profile", "Login");
                }

                // Verificar que el camión está asignado al chofer
                var camion = await _context.Camiones
                    .FirstOrDefaultAsync(c => c.id_camion == idCamion && c.chofer_asignado == chofer.id_chofer);

                if (camion == null)
                {
                    TempData["Error"] = "No tiene permiso para solicitar mantenimiento para este camión.";
                    return RedirectToAction("Profile", "Login");
                }

                // Verificar si ya existe una solicitud pendiente
                bool solicitudPendiente = await _context.Solicitudes_Mantenimiento
                            .AnyAsync(s => s.id_camion == idCamion && s.id_chofer == chofer.id_chofer && s.estado == "pendiente");

                if (solicitudPendiente)
                {
                    TempData["Info"] = "Ya tiene una solicitud de mantenimiento pendiente para este camión.";
                    return RedirectToAction("Profile", "Login");
                }

                // Registrar la solicitud usando el procedimiento almacenado
                await _databaseRepository.ExecuteQueryProcedureAsync<object>(
                    "sp_SolicitarMantenimiento",
                    new
                    {
                        id_chofer = chofer.id_chofer,
                        id_camion = idCamion,
                        observaciones
                    }
                );

                TempData["SuccessMessage"] = "Solicitud de mantenimiento enviada correctamente.";

                return RedirectToAction("Profile", "Login");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al procesar su solicitud de mantenimiento.";
                return RedirectToAction("Profile", "Login");
            }
        }

        [Authorize(Roles = "admin,user")]
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            try
            {
                if (!User.Identity.IsAuthenticated)
                    return RedirectToAction("Index");

                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var user = await _context.Usuarios.FindAsync(userId);

                if (user == null)
                    return RedirectToAction("Index");

                var model = new PerfilViewModel
                {
                    NombreUsuario = user.nombre_usuario,
                    Email = user.correo_electronico,
                    Rol = user.rol,
                    UltimoIngreso = user.ultimo_ingreso,
                    MfaHabilitado = user.mfa_habilitado,
                    FechaActualizacionPassword = user.fecha_actualizacion_password 
                };

                // Si el usuario es chofer, cargar información adicional
                if (user.rol == "user")
                {
                    string userEmail = user.correo_electronico;
                    string cedula = string.Empty;

                    // Manejar la extracción de forma segura para evitar problemas con expresiones LINQ
                    if (userEmail.Contains("@"))
                    {
                        cedula = userEmail.Substring(0, userEmail.IndexOf('@'));
                    }

                    // Consultar la base de datos con el valor extraído
                    var chofer = await _context.Choferes
                        .FirstOrDefaultAsync(c => c.id_usuario == userId);

                    if (chofer != null)
                    {
                        model.Edad = chofer.edad;
                        model.Numero_Cedula = chofer.numero_cedula;
                        model.Licencia = chofer.licencia;
                        model.Fecha_Venc_Licencia = chofer.fecha_venc_licencia;
                        model.Estado = chofer.estado;
                        model.Genero = chofer.genero;

                        // Obtener información del camión asignado
                        var camion = await _context.Camiones
                            .FirstOrDefaultAsync(c => c.chofer_asignado == chofer.id_chofer);

                        if (camion != null)
                        {
                            ViewBag.TieneCamionAsignado = true;
                            ViewBag.InfoCamion = $"{camion.numero_placa} - {camion.marca} {camion.modelo} ({camion.anio})";
                        }
                        else
                        {
                            ViewBag.TieneCamionAsignado = false;
                        }

                        if (ViewBag.TieneCamionAsignado == true)
                        {
                            ViewBag.IdCamion = camion.id_camion;

                            // Verificar si hay solicitudes pendientes
                            var solicitudPendiente = await _context.Solicitudes_Mantenimiento
                                .AnyAsync(s => s.id_camion == camion.id_camion &&
                                               s.id_chofer == chofer.id_chofer &&
                                               s.estado == "pendiente");

                            ViewBag.SolicitudPendiente = solicitudPendiente;
                        }
                        // Obtener documentos del chofer
                        var documentos = await _context.Documentos
                            .Where(d => d.id_chofer == chofer.id_chofer)
                            .OrderByDescending(d => d.fecha_emision)
                            .ToListAsync();

                        ViewBag.Documentos = documentos;
                        ViewBag.TieneDocumentosPendientes = documentos.Any(d => d.estado_validacion == "pendiente");
                    }
                    else
                    {
                        // Si no hay información de chofer, sugerir completar el perfil
                        ViewBag.PerfilIncompleto = true;
                    }
                }

                if (model.Fecha_Venc_Licencia.HasValue)
                {
                    // Calcular días hasta vencimiento correctamente
                    ViewBag.DiasParaVencimiento = (model.Fecha_Venc_Licencia.Value.Date - DateTime.Now.Date).Days;

                    // Generar mensaje de alerta según los días restantes
                    if (ViewBag.DiasParaVencimiento <= 0)
                    {
                        ViewBag.AlertaLicencia = "Su licencia de conducir ha vencido. Por favor, renuévela lo antes posible.";
                    }
                    else if (ViewBag.DiasParaVencimiento <= 30)
                    {
                        ViewBag.AlertaLicencia = $"Su licencia de conducir vencerá en {ViewBag.DiasParaVencimiento} días. Considere renovarla pronto.";
                    }
                }
                return View(model);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al cargar los datos del perfil.";
                return RedirectToAction("Index", "Home");
            }
        }

        [RateLimit(3, 900)] // 3 intentos en 15 minutos
        [AllowAnonymous]
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> ForgotPassword(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                ModelState.AddModelError("", "Ingrese su correo electrónico.");
                return View();
            }

            try
            {
                var user = await _userService.GetUserByEmailAsync(email);
                if (user == null || user.estado_verificacion != "verificado")
                {
                    TempData["SuccessMessage"] = "Si su correo está registrado y verificado, recibirá instrucciones para restablecer su contraseña.";
                    return RedirectToAction("Index");
                }

                var token = await _userService.GeneratePasswordResetTokenAsync(user.id_usuario);
                var resetLink = Url.Action("ResetPassword", "Login", new { token, email }, Request.Scheme);

                var templateData = new Dictionary<string, string>
                {
                    {"ResetLink", resetLink},
                    {"NombreUsuario", user.nombre_usuario ?? "Usuario"}
                };

                var emailBody = await _emailTemplatingService.LoadAndPopulateTemplateAsync(
                    "RestablecerContrasena.html",
                    templateData);

                await _emailService.SendEmailAsync(
                    email,
                    "Restablecer Contraseña - Sistema COMAVI",
                    emailBody);

                TempData["SuccessMessage"] = "Correo enviado con instrucciones para restablecer su contraseña.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error en el servidor. Por favor, intente más tarde.");
                return View();
            }
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> CambiarContrasena()
        {
            try
            {
                int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var user = await _userService.GetUserByIdAsync(userId);

                if (user == null)
                {
                    TempData["Error"] = "No se pudo encontrar la información del usuario.";
                    return RedirectToAction("Profile");
                }

                var model = new CambiarContrasenaViewModel
                {
                    Email = user.correo_electronico
                };

                return View(model);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al cargar la página de cambio de contraseña.";
                return RedirectToAction("Profile");
            }
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarContrasena(CambiarContrasenaViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var user = await _userService.GetUserByIdAsync(userId);

                if (user == null)
                {
                    TempData["Error"] = "No se pudo encontrar la información del usuario.";
                    return RedirectToAction("Profile");
                }

                // Verificar la contraseña actual
                bool passwordValid = _passwordService.VerifyPassword(model.PasswordActual, user.contrasena);
                if (!passwordValid)
                {
                    ModelState.AddModelError("PasswordActual", "La contraseña actual es incorrecta.");
                    return View(model);
                }

                // Cambiar la contraseña
                user.contrasena = _passwordService.HashPassword(model.NuevaPassword);

                // Registrar la fecha de actualización de la contraseña
                user.fecha_actualizacion_password = DateTime.Now;

                await _context.SaveChangesAsync();

                // Registrar el cambio de contraseña
                await _userService.RecordLoginAttemptAsync(
                    userId,
                    HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                    true);

                // Enviar correo de confirmación
                await EnviarCorreoCambioContrasena(user.correo_electronico, user.nombre_usuario);

                TempData["SuccessMessage"] = "Su contraseña ha sido actualizada exitosamente.";
                return RedirectToAction("Profile");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error al cambiar la contraseña. Por favor, intente más tarde.");
                return View(model);
            }
        }

        private async Task EnviarCorreoCambioContrasena(string email, string nombre)
        {
            try
            {
                var templateData = new Dictionary<string, string>
                {
                    {"NombreUsuario", nombre},
                    {"FechaCambio", DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")}
                };

                var emailBody = await _emailTemplatingService.LoadAndPopulateTemplateAsync(
                    "CambioContrasenaConfirmacion.html",
                    templateData);

                await _emailService.SendEmailAsync(
                    email,
                    "Confirmación de Cambio de Contraseña - COMAVI S.A.",
                    emailBody);
            }
            catch (Exception ex)
            {
                // No lanzamos la excepción para que el proceso de cambio de contraseña continúe
            }
        }

        #region Métodos privados

        private string GenerateVerificationToken()
        {
            using (var rng = RandomNumberGenerator.Create())
            {
                byte[] tokenBytes = new byte[32];
                rng.GetBytes(tokenBytes);
                return Convert.ToBase64String(tokenBytes)
                    .Replace("+", "-")
                    .Replace("/", "_")
                    .Replace("=", "");
            }
        }

        private async Task EnviarCorreoVerificacion(string email, string nombre, string token)
        {
            try
            {
                var verificationLink = Url.Action("Verificar", "Login", new { token, email }, Request.Scheme);
                var instruccionesLink = Url.Action("InstruccionesVerificacion", "Login", new { email, nombre }, Request.Scheme);

                var templateData = new Dictionary<string, string>
                {
                    {"NombreUsuario", nombre},
                    {"VerificationLink", verificationLink},
                    {"InstruccionesLink", instruccionesLink}
                };

                var emailBody = await _emailTemplatingService.LoadAndPopulateTemplateAsync(
                    "VerificacionCuenta.html",
                    templateData);

                await _emailService.SendEmailAsync(
                    email,
                    "Verificación de cuenta - Sistema COMAVI",
                    emailBody);
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        private string ObtenerPasosVerificacion()
        {
            return @"
            <h3>Pasos para completar su registro:</h3>
            <ol>
                <li><strong>Verificación de cuenta:</strong> Haga clic en el enlace de verificación enviado a su correo electrónico.</li>
                <li><strong>Inicio de sesión:</strong> Una vez verificada su cuenta, inicie sesión con sus credenciales.</li>
                <li><strong>Autenticación de dos factores:</strong> Configure la autenticación de dos factores escaneando el código QR con una aplicación como Google Authenticator.</li>
                <li><strong>Completar perfil:</strong> Rellene todos los campos requeridos en su perfil, incluyendo:
                    <ul>
                        <li>Información personal (edad, género)</li>
                        <li>Número de cédula</li>
                        <li>Información de licencia</li>
                        <li>Fecha de vencimiento de la licencia</li>
                    </ul>
                </li>
                <li><strong>Subir documentos:</strong> Cargue los documentos requeridos en formato PDF:
                    <ul>
                        <li>Licencia de conducir</li>
                        <li>Documento de identidad</li>
                        <li>Otros documentos relevantes</li>
                    </ul>
                </li>
                <li><strong>Esperar aprobación:</strong> Un administrador revisará su información y documentos. Recibirá una notificación cuando su cuenta esté completamente activada.</li>
            </ol>
            <h4>Requisitos para documentos PDF:</h4>
            <ul>
                <li>Formato: PDF</li>
                <li>Tamaño máximo: 10 MB</li>
                <li>Contenido: Los documentos deben ser legibles y contener la información requerida para cada tipo de documento.</li>
                <li>Validez: Los documentos con fechas de vencimiento deben estar vigentes.</li>
            </ul>
            <h4>Importante:</h4>
            <p>Si no completa el proceso de verificación dentro de 3 días, o si la información proporcionada no es válida, su cuenta será eliminada automáticamente del sistema.</p>
            ";
        }

        #endregion
    }
#nullable enable

}