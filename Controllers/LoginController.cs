﻿using COMAVI_SA.Data;
using COMAVI_SA.Models;
using COMAVI_SA.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using static COMAVI_SA.Middleware.SessionValidationMiddleware;

namespace COMAVI_SA.Controllers
{
    [Authorize]
    public class LoginController : Controller
    {
        private readonly IUserService _userService;
        private readonly IPasswordService _passwordService;
        private readonly IOtpService _otpService;
        private readonly IJwtService _jwtService;
        private readonly IEmailService _emailService;
        private readonly IPdfService _pdfService;
        private readonly ComaviDbContext _context;
        private readonly ILogger<LoginController> _logger;

        public LoginController(
            IUserService userService,
            IPasswordService passwordService,
            IOtpService otpService,
            IJwtService jwtService,
            IEmailService emailService,
            IPdfService pdfService,
            ComaviDbContext context,
            ILogger<LoginController> logger)
        {
            _userService = userService;
            _passwordService = passwordService;
            _otpService = otpService;
            _jwtService = jwtService;
            _emailService = emailService;
            _pdfService = pdfService;
            _context = context;
            _logger = logger;
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult Index()
        {
            if (User.Identity.IsAuthenticated)
                return RedirectToAction("Index", "Home");

            return View();
        }

        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> Index(LoginViewModel model)
        {
            try
            {
                _logger.LogInformation("Intento de login para: {Email}", model.Email);

                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Modelo inválido en login. Errores: {Errors}",
                        string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));

                    foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                    {
                        ModelState.AddModelError("", error.ErrorMessage);
                    }

                    return View(model);
                }

                if (await _userService.IsAccountLockedAsync(model.Email))
                {
                    _logger.LogWarning("Intento de login en cuenta bloqueada: {Email}", model.Email);
                    ModelState.AddModelError("", "Su cuenta ha sido bloqueada por múltiples intentos fallidos. Intente más tarde.");
                    return View(model);
                }

                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
                _logger.LogInformation("IP de intento de login: {IP}", ipAddress);

                var user = await _userService.AuthenticateAsync(model.Email, model.Password);

                if (user == null)
                {
                    _logger.LogWarning("Fallo de autenticación para: {Email}", model.Email);
                    await _userService.RecordLoginAttemptAsync(null, ipAddress, false);
                    ModelState.AddModelError("", "Correo electrónico o contraseña incorrectos.");
                    return View(model);
                }

                if (user.estado_verificacion != "verificado")
                {
                    _logger.LogWarning("Intento de login en cuenta no verificada: {Email}", model.Email);
                    ModelState.AddModelError("", "Su cuenta no ha sido verificada. Por favor, revise su correo electrónico para completar el proceso de verificación.");
                    return View(model);
                }

                await _userService.RecordLoginAttemptAsync(user.id_usuario, ipAddress, true);
                _logger.LogInformation("Login exitoso para: {Email}", model.Email);

                TempData["UserEmail"] = user.correo_electronico;
                TempData["UserId"] = user.id_usuario;
                TempData["RememberMe"] = model.RememberMe;

                var token = _jwtService.GenerateJwtToken(user);
                _logger.LogInformation("Token JWT generado correctamente para: {Email}", model.Email);
                HttpContext.Session.SetString("JwtToken", token);

                return RedirectToAction("VerifyOtp");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error no controlado durante el proceso de login: {Email}", model.Email);
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
                _logger.LogError(ex, "Error al configurar MFA");
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
                _logger.LogError(ex, "Error al configurar MFA");
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
                    _logger.LogError(ex, "Error al regenerar códigos de respaldo");
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

                    if (mfaCompleted)
                    {
                        _logger.LogInformation("Usuario ya autenticado con MFA completo, redirigiendo a Home");
                        return RedirectToAction("Index", "Home");
                    }

                    // Si está autenticado pero no ha completado MFA, limpiar su autenticación
                    _logger.LogWarning("Usuario autenticado sin MFA completado. Limpiando autenticación.");
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

                _logger.LogInformation("VerifyOtp: UserId={UserId}, Email={Email}", userId, email);

                if (!userId.HasValue || string.IsNullOrEmpty(email))
                {
                    _logger.LogWarning("VerifyOtp: No hay datos de usuario en TempData");
                    TempData["Error"] = "Su sesión ha expirado. Por favor, inicie sesión nuevamente.";
                    return RedirectToAction("Index");
                }

                // Verificar si el usuario existe
                var user = await _userService.GetUserByIdAsync(userId.Value);
                if (user == null)
                {
                    _logger.LogWarning("VerifyOtp: Usuario no encontrado");
                    TempData["Error"] = "Usuario no encontrado. Por favor, inicie sesión nuevamente.";
                    return RedirectToAction("Index");
                }

                // Si el usuario no tiene MFA habilitado, pasar directamente al login
                if (!user.mfa_habilitado)
                {
                    _logger.LogInformation("VerifyOtp: Usuario no tiene MFA habilitado, pasando directamente");

                    // Crear un modelo OtpViewModel con un código ficticio "bypass"
                    var bypassModel = new OtpViewModel { Email = email, OtpCode = "bypass" };

                    // Llamar directamente al método POST de VerifyOtp
                    return await VerifyOtp(bypassModel);
                }

                var mfaSecret = await _userService.GetMfaSecretAsync(userId.Value);
                _logger.LogInformation("VerifyOtp: MFA Secret obtenido={HasSecret}", !string.IsNullOrEmpty(mfaSecret));

                // Verificar límite máximo de intentos global (no solo de la sesión actual)
                int intentosFallidos = await _userService.GetFailedMfaAttemptsAsync(userId.Value);

                // Si excede el límite máximo (por ejemplo, 10 intentos), bloquear temporalmente la cuenta
                if (intentosFallidos >= 10)
                {
                    _logger.LogWarning("Usuario {Email} ha excedido el límite máximo de intentos de MFA", email);
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
                _logger.LogError(ex, "Error durante la preparación de la verificación OTP");
                TempData["Error"] = "Error en el servidor. Por favor, intente más tarde.";
                return RedirectToAction("Index");
            }
        }

        // Eliminar el método VerifyOtp duplicado y mantener solo esta versión completa
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
                    _logger.LogWarning("VerifyOtp POST: Faltan datos de usuario en TempData");
                    TempData["Error"] = "Su sesión ha expirado. Por favor, inicie sesión nuevamente.";
                    return RedirectToAction("Index");
                }

                if (TempData["OtpTimestamp"] != null)
                {
                    long timestamp = long.Parse(TempData["OtpTimestamp"].ToString());
                    TimeSpan elapsed = TimeSpan.FromTicks(DateTime.Now.Ticks - timestamp);

                    if (elapsed.TotalMinutes > 5)
                    {
                        _logger.LogWarning("OTP expirado para usuario {Email}", email);
                        TempData["Error"] = "El código OTP ha expirado. Por favor, inicie sesión nuevamente.";
                        return RedirectToAction("Index");
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
                            _logger.LogWarning("Usuario {Email} ha excedido el límite máximo de intentos de MFA", email);
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
                        _logger.LogWarning("Intento de bypass de MFA no autorizado para usuario {Email}", email);
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
                            _logger.LogWarning("Usuario {Email} ha excedido el límite máximo de intentos de MFA", email);
                            TempData["Error"] = "Ha excedido el número máximo de intentos. Su cuenta ha sido bloqueada temporalmente.";
                            return RedirectToAction("Index");
                        }

                        model.IntentosFallidos = intentosFallidos;

                        // Actualizar timestamp para extender el tiempo
                        TempData["OtpTimestamp"] = DateTime.Now.Ticks;

                        return View(model);
                    }
                }

                var user = await _userService.GetUserByIdAsync(userId.Value);
                if (user == null)
                {
                    _logger.LogWarning("VerifyOtp POST: Usuario no encontrado después de verificación");
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
                _logger.LogInformation("Verificación OTP completada con éxito para usuario {Email}", email);

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
                _logger.LogError(ex, "Error durante la verificación OTP");
                ModelState.AddModelError("", "Error en el servidor. Por favor, intente más tarde.");
                return View(model);
            }
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
                _logger.LogError(ex, "Error al regenerar códigos de respaldo");
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
                _logger.LogError(ex, "Error al desactivar MFA");
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
                    ModelState.AddModelError("", "Error al registrar el usuario. Por favor, intente nuevamente.");
                    return View(model);
                }

                // Enviar correo con instrucciones de verificación
                await EnviarCorreoVerificacion(model.Email, model.UserName, verificationToken);

                TempData["SuccessMessage"] = "Usuario registrado exitosamente. Se han enviado instrucciones de verificación al correo proporcionado.";

                return RedirectToAction("Usuarios", "Sistema");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar usuario");
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
                _logger.LogError(ex, "Error al verificar usuario");
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
            _logger.LogInformation("Iniciando método CompletarPerfil (GET)");

            if (!User.Identity.IsAuthenticated)
            {
                _logger.LogWarning("Intento de acceso sin autenticación a CompletarPerfil");
                return RedirectToAction("Index");
            }

            try
            {
                // Obtener el ID del usuario actual
                int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                _logger.LogInformation("CompletarPerfil: Usuario autenticado con ID {UserId}", userId);

                var usuario = await _context.Usuarios.FindAsync(userId);

                if (usuario == null)
                {
                    _logger.LogWarning("No se encontró el usuario con ID {UserId} en la base de datos", userId);
                    return RedirectToAction("Index");
                }

                // Verificar si ya existe un perfil para este usuario usando la relación directa
                var choferExistente = await _context.Choferes
                    .FirstOrDefaultAsync(c => c.id_usuario == userId);

                // Si ya existe un perfil, redirigir a SubirDocumentos
                if (choferExistente != null)
                {
                    _logger.LogInformation("Se encontró un perfil existente para el usuario. ID de chofer: {IdChofer}, ID usuario: {IdUsuario}",
                        choferExistente.id_chofer, choferExistente.id_usuario);
                    TempData["SuccessMessage"] = "Su perfil ya está completo. Ahora puede subir sus documentos.";
                    _logger.LogInformation("Redirigiendo a SubirDocumentos, ya que el perfil está completo");
                    return RedirectToAction("SubirDocumentos");
                }

                _logger.LogInformation("No se encontró un perfil existente para el usuario con ID: {UserId}. Mostrando vista para completar el perfil", userId);
                // Si no existe perfil, mostrar la vista para completarlo
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al verificar el perfil del usuario: {Message}", ex.Message);
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
                _logger.LogInformation("CompletarPerfil POST recibido. IsValid: {IsValid}", ModelState.IsValid);

                if (!ModelState.IsValid && model.Estado != null)
                {
                    // Registrar errores específicos de validación
                    foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                    {
                        _logger.LogWarning("Error de validación: {Error}", error.ErrorMessage);
                    }
                    return View(model);
                }

                _logger.LogInformation("Datos del modelo: Edad={Edad}, Cedula={Cedula}, Licencia={Licencia}",
                    model.Edad, model.Numero_Cedula, model.Licencia);

                int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                _logger.LogInformation("UserId obtenido: {UserId}", userId);

                var usuario = await _context.Usuarios.FindAsync(userId);
                if (usuario == null)
                {
                    _logger.LogWarning("No se encontró el usuario con ID {UserId}", userId);
                    return RedirectToAction("Index");
                }

                // Verificar si existe un registro de chofer para este usuario
                var choferExistente = await _context.Choferes
                    .FirstOrDefaultAsync(c => c.id_usuario == userId);

                if (choferExistente != null)
                {
                    _logger.LogInformation("Ya existe un perfil para este usuario con ID: {IdUsuario}", userId);
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

                _logger.LogInformation("Guardando cambios en la base de datos para el usuario con ID: {UserId}", userId);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Cambios guardados exitosamente. Redirigiendo a SubirDocumentos");

                TempData["SuccessMessage"] = "Perfil completado exitosamente.";
                return RedirectToAction("SubirDocumentos");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al completar perfil de usuario: {Message}", ex.Message);

                // Intentar obtener más información sobre la excepción
                if (ex.InnerException != null)
                {
                    _logger.LogError(ex.InnerException, "Inner Exception: {Message}", ex.InnerException.Message);
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
                var chofer = await _context.Choferes
                    .FirstOrDefaultAsync(c => c.id_usuario == userId);
                if (chofer == null)
                {
                    TempData["Error"] = "Debe completar su perfil primero.";
                    return RedirectToAction("Profile");
                }

                var documentosExistentes = await _context.Documentos
                    .Where(d => d.id_chofer == chofer.id_chofer)
                    .ToListAsync();

                // Definir los tipos de documentos requeridos
                var tiposRequeridos = new List<string> { "licencia", "carnet", "seguro", "tarjeta" }; // Ajusta según tus requisitos

                // Verificar qué documentos faltan
                var tiposExistentes = documentosExistentes.Select(d => d.tipo_documento).ToList();
                var documentosFaltantes = tiposRequeridos.Except(tiposExistentes).ToList();

                var model = new CargaDocumentoViewModel
                {
                    DocumentosExistentes = documentosExistentes,
                    DocumentosFaltantes = documentosFaltantes 
                };

                bool tieneTodosLosDocumentos = !documentosFaltantes.Any();
                bool todosVerificados = documentosExistentes.All(d => d.estado_validacion == "verificado");

                if (tieneTodosLosDocumentos && todosVerificados)
                {
                    TempData["InfoMessage"] = "Todos sus documentos ya están verificados.";
                    return RedirectToAction("Profile");
                }

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

                foreach (var admin in admins)
                {
                    var notificacion = new Notificaciones_Usuario
                    {
                        id_usuario = admin.id_usuario,
                        tipo_notificacion = "Documento Nuevo",
                        fecha_hora = DateTime.Now,
                        mensaje = $"El chofer {chofer.nombreCompleto} ha subido un nuevo documento ({model.TipoDocumento}) que requiere validación."
                    };

                    _context.NotificacionesUsuario.Add(notificacion);
                }

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Documento subido exitosamente. Un administrador lo validará próximamente.";
                return RedirectToAction("Profile");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al subir documento");
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
                _logger.LogInformation("Iniciando proceso de logout para sesión {SessionId}", sessionId);

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
                        _logger.LogInformation("Token JWT añadido a la lista negra");
                    }
                }

                // Eliminar solo la sesión activa actual de la base de datos
                if (User.Identity.IsAuthenticated)
                {
                    var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                    _logger.LogInformation("Eliminando sesión activa actual para usuario {UserId}", userId);

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
                        _logger.LogInformation("Eliminada sesión activa con ID {SesionId} de la base de datos", sesionActual.id_sesion);
                    }
                    else
                    {
                        _logger.LogWarning("No se encontró una sesión activa para eliminar para el usuario {UserId}", userId);
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
                _logger.LogInformation("Eliminando todas las cookies");
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
                    _logger.LogDebug("Cookie eliminada: {CookieName}", cookie);
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
                _logger.LogInformation("Limpiando datos de sesión");
                HttpContext.Session.Clear();

                // Cerrar autenticación 
                _logger.LogInformation("Cerrando autenticación por cookies");
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignOutAsync(JwtBearerDefaults.AuthenticationScheme);
                await HttpContext.SignOutAsync("Identity.Application"); // Para asegurarnos

                // Limpiar el principal de autenticación
                HttpContext.User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity());

                _logger.LogInformation("Proceso de logout completado correctamente");

                // Forzar nueva sesión con parámetros para evitar cacheo
                return RedirectToAction("Index", new { t = DateTime.Now.Ticks, clean = true, forceNew = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error grave durante el cierre de sesión");

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
                _logger.LogError(ex, "Error al restablecer contraseña");
                ModelState.AddModelError("", "Error en el servidor. Por favor, intente más tarde.");
                return View(model);
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
                _logger.LogError(ex, "Error al cargar perfil de usuario");
                TempData["Error"] = "Error al cargar los datos del perfil.";
                return RedirectToAction("Index", "Home");
            }
        }

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

                // Generar token y enviar correo
                var token = await _userService.GeneratePasswordResetTokenAsync(user.id_usuario);
                var resetLink = Url.Action("ResetPassword", "Login", new { token, email }, Request.Scheme);

                var emailBody = $@"
                <h2>Restablecer Contraseña - Sistema COMAVI</h2>
                <p>Hemos recibido una solicitud para restablecer la contraseña de su cuenta.</p>
                <p>Haga clic en el siguiente enlace para crear una nueva contraseña:</p>
                <p><a href='{resetLink}' style='padding: 10px; background-color: #4e73df; color: white; text-decoration: none; border-radius: 5px;'>Restablecer Contraseña</a></p>
                <p>Si no solicitó restablecer su contraseña, puede ignorar este correo.</p>
                <p>El enlace expirará en 24 horas.</p>
                <p>Atentamente,<br>Equipo COMAVI</p>";

                await _emailService.SendEmailAsync(
                    email,
                    "Restablecer Contraseña - Sistema COMAVI",
                    emailBody);

                TempData["SuccessMessage"] = "Correo enviado con instrucciones para restablecer su contraseña.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al procesar solicitud de restablecimiento de contraseña");
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
                _logger.LogError(ex, "Error al cargar la página de cambio de contraseña");
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
                _logger.LogError(ex, "Error al cambiar la contraseña del usuario");
                ModelState.AddModelError("", "Error al cambiar la contraseña. Por favor, intente más tarde.");
                return View(model);
            }
        }

        private async Task EnviarCorreoCambioContrasena(string email, string nombre)
        {
            try
            {
                var emailBody = $@"
        <h2>Confirmación de Cambio de Contraseña - COMAVI S.A.</h2>
        <p>Estimado/a {nombre},</p>
        <p>Le informamos que la contraseña de su cuenta ha sido actualizada exitosamente.</p>
        <p>Si usted no realizó este cambio, por favor contacte inmediatamente al administrador del sistema.</p>
        <p>Fecha y hora del cambio: {DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")}</p>
        <p>Atentamente,<br>Equipo COMAVI</p>";

                await _emailService.SendEmailAsync(
                    email,
                    "Confirmación de Cambio de Contraseña - COMAVI S.A.",
                    emailBody);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar correo de confirmación de cambio de contraseña");
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

                var emailBody = $@"
                <h2>Bienvenido a COMAVI S.A. - Sistema de Seguimiento de Licencias</h2>
                <p>Estimado/a {nombre},</p>
                <p>Gracias por registrarse en nuestro sistema. Para completar su registro, por favor siga estos pasos:</p>
                <ol>
                    <li>Verifique su cuenta haciendo clic en el siguiente enlace: <a href='{verificationLink}'>Verificar cuenta</a></li>
                    <li>Una vez verificada, inicie sesión en el sistema.</li>
                    <li>Complete su perfil con su información personal y de licencia.</li>
                    <li>Suba los documentos requeridos (licencia, identificación, etc.) en formato PDF.</li>
                </ol>
                <p>Para obtener instrucciones detalladas, visite: <a href='{instruccionesLink}'>Ver instrucciones completas</a></p>
                <p><strong>Importante:</strong> Si no completa el proceso de verificación en 3 días, su cuenta será eliminada automáticamente.</p>
                <p>Atentamente,<br>Equipo COMAVI</p>";

                await _emailService.SendEmailAsync(
                    email,
                    "Verificación de cuenta - Sistema COMAVI",
                    emailBody);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar correo de verificación");
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
}