using COMAVI_SA.Data;
using COMAVI_SA.Models;
using COMAVI_SA.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

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
        private readonly ComaviDbContext _context;
        private readonly ILogger<LoginController> _logger;

        public LoginController(
            IUserService userService,
            IPasswordService passwordService,
            IOtpService otpService,
            IJwtService jwtService,
            IEmailService emailService,
            ComaviDbContext context,
            ILogger<LoginController> logger)
        {
            _userService = userService;
            _passwordService = passwordService;
            _otpService = otpService;
            _jwtService = jwtService;
            _emailService = emailService;
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
            if (!ModelState.IsValid)
                return View(model);

            try
            {
                if (await _userService.IsAccountLockedAsync(model.Email))
                {
                    ModelState.AddModelError("", "Su cuenta ha sido bloqueada por múltiples intentos fallidos. Intente más tarde.");
                    return View(model);
                }

                var user = await _userService.AuthenticateAsync(model.Email, model.Password);
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

                if (user == null)
                {
                    await _userService.RecordLoginAttemptAsync(null, ipAddress, false);
                    ModelState.AddModelError("", "Correo electrónico o contraseña incorrectos.");
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
                _logger.LogError(ex, "Error durante el proceso de login");
                ModelState.AddModelError("", "Error en el servidor. Por favor, intente más tarde.");
                return View(model);
            }
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> VerifyOtp()
        {
            try
            {
                var userId = TempData.Peek("UserId") as int?;
                var email = TempData.Peek("UserEmail") as string;

                if (!userId.HasValue || string.IsNullOrEmpty(email))
                    return RedirectToAction("Index");

                var mfaSecret = await _userService.GetMfaSecretAsync(userId.Value);
                if (string.IsNullOrEmpty(mfaSecret))
                {
                    await _userService.SetupMfaAsync(userId.Value);
                    mfaSecret = await _userService.GetMfaSecretAsync(userId.Value);

                    var qrCode = _otpService.GenerateQrCodeUri(mfaSecret, email);
                    ViewBag.QrCode = qrCode;
                    ViewBag.Secret = mfaSecret;
                    ViewBag.IsFirstTimeSetup = true;
                }

                var model = new OtpViewModel { Email = email };
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error durante la preparación de la verificación OTP");
                TempData["Error"] = "Error en el servidor. Por favor, intente más tarde.";
                return RedirectToAction("Index");
            }
        }

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
                    return RedirectToAction("Index");

                if (!await _userService.VerifyMfaCodeAsync(userId.Value, model.OtpCode))
                {
                    ModelState.AddModelError("", "Código OTP inválido. Intente nuevamente.");
                    return View(model);
                }

                var user = await _userService.GetUserByIdAsync(userId.Value);
                if (user == null)
                    return RedirectToAction("Index");

                // Actualizar último ingreso
                user.ultimo_ingreso = DateTime.Now;
                await _context.SaveChangesAsync();

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.id_usuario.ToString()),
                    new Claim(ClaimTypes.Name, user.nombre_usuario),
                    new Claim(ClaimTypes.Email, user.correo_electronico),
                    new Claim(ClaimTypes.Role, user.rol)
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

                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error durante la verificación OTP");
                ModelState.AddModelError("", "Error en el servidor. Por favor, intente más tarde.");
                return View(model);
            }
        }

        [HttpGet]
        public Task<IActionResult> Register()
        {
            if (User.IsInRole("admin"))
                return Task.FromResult<IActionResult>(View());
            else
                return Task.FromResult<IActionResult>(RedirectToAction("AccessDenied"));
        }

        [HttpPost]
        [Authorize(Roles = "admin")]
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

                var result = await _userService.RegisterAsync(model);
                if (!result)
                {
                    ModelState.AddModelError("", "Error al registrar el usuario. Por favor, intente nuevamente.");
                    return View(model);
                }

                TempData["SuccessMessage"] = "Usuario registrado exitosamente.";

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
        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            try
            {
                // Remover sesión activa
                if (User.Identity.IsAuthenticated)
                {
                    var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                    var sesionesActivas = await _context.SesionesActivas
                        .Where(s => s.id_usuario == userId)
                        .ToListAsync();

                    _context.SesionesActivas.RemoveRange(sesionesActivas);
                    await _context.SaveChangesAsync();
                }

                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                HttpContext.Session.Clear();
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error durante el cierre de sesión");
                return RedirectToAction("Index");
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
                    UltimoIngreso = user.ultimo_ingreso
                };

                // Si el usuario esta registrado pues chofer, cargar información adicional
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
                        .FirstOrDefaultAsync(c => c.numero_cedula == cedula);

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
                if (user == null)
                {
                    TempData["SuccessMessage"] = "Si su correo está registrado, recibirá instrucciones para restablecer su contraseña.";
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
    }
}