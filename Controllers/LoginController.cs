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
        //private readonly EmailService _emailService;
        private readonly ComaviDbContext _context;

        public LoginController(
            IUserService userService,
            IPasswordService passwordService,
            IOtpService otpService,
            IJwtService jwtService,
           // EmailService emailService, 
            ComaviDbContext context)
        {
            _userService = userService;
            _passwordService = passwordService;
            _otpService = otpService;
            _jwtService = jwtService;
            //_emailService = emailService;
            _context = context;
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

            if (await _userService.IsAccountLockedAsync(model.Email))
            {
                ModelState.AddModelError("", "Su cuenta ha sido bloqueada por múltiples intentos fallidos. Intente más tarde.");
                return View(model);
            }

            var user = await _userService.AuthenticateAsync(model.Email, model.Password);
            var ipAddress = HttpContext.Connection.RemoteIpAddress.ToString();

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

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> VerifyOtp()
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

        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> VerifyOtp(OtpViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

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

            var user = await _userService.AuthenticateAsync(email, "");
            if (user == null)
                return RedirectToAction("Index");

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

            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public Task<IActionResult> Register()
        {
            return Task.FromResult<IActionResult>(View());
        }

        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

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

            TempData["SuccessMessage"] = "Registro exitoso. Ahora puede iniciar sesión.";

            return RedirectToAction("Index");
        }

        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            HttpContext.Session.Clear();
            return RedirectToAction("Index");
        }

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

            var result = await _userService.ResetPasswordAsync(model.Email, model.Codigo, model.NewPassword);
            if (!result)
            {
                ModelState.AddModelError("", "Token inválido o expirado.");
                return View(model);
            }

            TempData["SuccessMessage"] = "Contraseña actualizada exitosamente.";
            return RedirectToAction("Index");
        }

        //[Authorize]
        public Task<IActionResult> Profile()
        {
            
            return Task.FromResult<IActionResult>(View());
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

            var user = await _userService.AuthenticateAsync(email, "");
            if (user == null)
            {
                TempData["SuccessMessage"] = "Si su correo está registrado, recibirá instrucciones.";
                return RedirectToAction("Index");
            }

            // Generar token y enviar correo
            var token = await _userService.GeneratePasswordResetTokenAsync(user.id_usuario);
            var resetLink = Url.Action("ResetPassword", "Login", new { token, email }, Request.Scheme);

            //await _emailService.SendEmailAsync(
            //    email,
            //    "Restablecer Contraseña",
            //    $"Haga clic <a href='{resetLink}'>aquí</a> para restablecer su contraseña.");

            TempData["SuccessMessage"] = "Correo enviado con instrucciones.";
            return RedirectToAction("Index");
        }
    }
}