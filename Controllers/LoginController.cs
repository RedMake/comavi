using COMAVI_SA.Data;
using COMAVI_SA.Models;
using COMAVI_SA.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

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
                        return RedirectToAction("CompletarPerfil");
                    }
                }

                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error durante la verificación OTP");
                ModelState.AddModelError("", "Error en el servidor. Por favor, intente más tarde.");
                return View(model);
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

        [HttpGet]
        public IActionResult CompletarPerfil()
        {
            if (!User.Identity.IsAuthenticated)
                return RedirectToAction("Index");

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CompletarPerfil(PerfilViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            try
            {
                int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var usuario = await _context.Usuarios.FindAsync(userId);

                if (usuario == null)
                    return RedirectToAction("Index");

                // Verificar si existe un registro de chofer para este usuario
                string cedula = model.Numero_Cedula;
                var choferExistente = await _context.Choferes
                    .FirstOrDefaultAsync(c => c.numero_cedula == cedula);

                if (choferExistente != null)
                {
                    ModelState.AddModelError("Numero_Cedula", "Este número de cédula ya está registrado para otro chofer.");
                    return View(model);
                }

                // Crear nuevo registro de chofer
                var nuevoChofer = new Choferes
                {
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
                _logger.LogError(ex, "Error al completar perfil de usuario");
                ModelState.AddModelError("", "Error en el servidor. Por favor, intente más tarde.");
                return View(model);
            }
        }

        [HttpGet]
        public IActionResult SubirDocumentos()
        {
            if (!User.Identity.IsAuthenticated)
                return RedirectToAction("Index");

            return View(new CargaDocumentoViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> SubirDocumentos(CargaDocumentoViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            try
            {
                // Validar PDF
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
                    ModelState.AddModelError("ArchivoPdf", $"El documento no parece contener la información necesaria para un documento tipo '{model.TipoDocumento}'.");
                    return View(model);
                }

                // Guardar archivo
                var (filePath, fileHash) = await _pdfService.SavePdfAsync(model.ArchivoPdf);

                // Obtener ID del chofer actual
                int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var usuario = await _context.Usuarios.FindAsync(userId);
                string cedula = string.Empty;

                if (usuario.correo_electronico.Contains("@"))
                {
                    cedula = usuario.correo_electronico.Substring(0, usuario.correo_electronico.IndexOf('@'));
                }

                var chofer = await _context.Choferes
                    .FirstOrDefaultAsync(c => c.numero_cedula == cedula);

                if (chofer == null)
                {
                    ModelState.AddModelError("", "No se encontró información del chofer asociado a su cuenta.");
                    return View(model);
                }

                // Guardar información del documento
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
                await _context.SaveChangesAsync();

                // Notificar a administradores
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