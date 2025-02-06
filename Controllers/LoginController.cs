using COMAVI_SA.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Runtime.Intrinsics.X86;
using System.Security.Claims;

namespace COMAVI_SA.Controllers
{
    public class LoginController(ApplicationDbContext context, EmailService emailService) : Controller
    {
        private readonly ApplicationDbContext _context = context;
        private readonly EmailService _emailservice = emailService;

        [HttpPost]
        public async Task<IActionResult> Login(LoginModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _context.Usuarios
                .FirstOrDefaultAsync(u => u.NombreUsuario == model.Username);

            if (user == null || !BCrypt.Net.BCrypt.Verify(model.Password, user.Contrasena))
            {
                // Registrar intento fallido
                await RegistrarIntentoFallido(user?.Id, HttpContext.Connection.RemoteIpAddress?.ToString());
                ModelState.AddModelError(string.Empty, "Credenciales inválidas");
                return View(model);
            }

            // Verificar si requiere MFA
            if (await RequiereMFA(user.Id))
            {
                var mfaCode = GenerarMFACode(user.Id);
                await EnviarMFAPorEmail(user, mfaCode);
                return RedirectToAction("MFA", new { userId = user.Id });
            }

            // Crear sesión segura
            await CrearSesionSegura(user);

            return RedirectToAction("Index", "Home");
        }


        private async Task RegistrarIntentoFallido(int? userId, string ip)
        {
            _context.IntentosLogin.Add(new IntentosLogin
            {
                id_usuario = userId,
                fecha_hora = DateTime.Now,
                exitoso = false,
                direccion_ip = ip
            });

            await _context.SaveChangesAsync();

            // Bloquear después de 3 intentos fallidos
            var intentos = await _context.IntentosLogin
                .CountAsync(i => i.direccion_ip == ip &&
                                i.fecha_hora > DateTime.Now.AddMinutes(-15) &&
                                !i.exitoso);

            if (intentos >= 3)
            {
                await BloquearUsuarioTemporal(userId);
            }
        }

        private string GenerarMFACode(int userId)
        {
            var clave = KeyGeneration.GenerateRandomKey(20);
            var totp = new Totp(clave, step: 300);
            var code = totp.ComputeTotp();

            _context.MFA.Add(new MFA
            {
                id_usuario = userId,
                codigo = BCrypt.Net.BCrypt.HashPassword(code),
                fecha_generacion = DateTime.Now,
                usado = false
            });

            _context.SaveChanges();

            return code;
        }

        public async Task<bool> ValidarMFA(int userId, string code)
        {
            var mfaActivo = await _context.MFA
                .Where(m => m.id_usuario == userId &&
                           !m.usado &&
                           m.fecha_generacion > DateTime.Now.AddMinutes(-5))
                .OrderByDescending(m => m.fecha_generacion)
                .FirstOrDefaultAsync();

            if (mfaActivo == null) return false;

            var isValid = BCrypt.Net.BCrypt.Verify(code, mfaActivo.codigo);

            if (isValid)
            {
                mfaActivo.usado = true;
                await _context.SaveChangesAsync();
            }

            return isValid;
        }

        [HttpPost]
        public async Task<IActionResult> Register(RegisterModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var existingUser = await _context.Usuarios.FirstOrDefaultAsync(u => u.NombreUsuario == model.NombreUsuario);
            if (existingUser != null)
            {
                ModelState.AddModelError(string.Empty, "El nombre de usuario ya existe.");
                return View(model);
            }

            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(model.Password);
            var newUser = new Usuario
            {
                NombreUsuario = model.NombreUsuario,
                Email = model.Email,
                Contrasena = hashedPassword
            };

            _context.Usuarios.Add(newUser);
            await _context.SaveChangesAsync();

            return RedirectToAction("Login");
        }


        [HttpPost]
        public async Task<IActionResult> RestablecerContrasena(ResetPasswordModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == model.Email);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Correo electrónico no registrado.");
                return View(model);
            }

            var token = Guid.NewGuid().ToString();
            user.TokenRecuperacion = token;
            user.ExpiracionToken = DateTime.UtcNow.AddHours(1);

            await _context.SaveChangesAsync();

            var resetLink = Url.Action("ResetPassword", "Login", new { email = model.Email, token }, Request.Scheme);
            await _emailservice.EnviarCorreoAsync(model.Email, "Restablecimiento de Contraseña",
                $"Haz clic en el siguiente enlace para restablecer tu contraseña: <a href='{resetLink}'>Restablecer</a>");

            return View("RestablecimientoEnviado");
        }

        [HttpGet]
        public IActionResult ResetPassword(string email, string token)
        {
            return View(new ResetPasswordModel { Email = email, Token = token });
        }

        [HttpPost]
        public async Task<IActionResult> ResetPassword(ResetPasswordModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == model.Email && u.Token == model.Token);
            if (user == null || user.ExpiracionToken < DateTime.UtcNow)
            {
                ModelState.AddModelError(string.Empty, "Token inválido o expirado.");
                return View(model);
            }

            user.Contrasena = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
            user.TokenRecuperacion = null;
            user.ExpiracionToken = null;

            await _context.SaveChangesAsync();

            return RedirectToAction("Login");
        }






        public IActionResult MFA()
        {
            return View();
        }




        public async Task<IActionResult> CerrarSesiones()
        {
            await HttpContext.SignOutAsync();
            return RedirectToAction("Login");
        }


        [HttpPost]
        public async Task<IActionResult> PerfilConfiguracion(Usuario model)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            var user = await _context.Usuarios.FindAsync(userId);

            if (user == null)
                return NotFound();

            user.NombreUsuario = model.NombreUsuario;
            user.Email = model.Email;

            if (!string.IsNullOrEmpty(model.Contrasena))
            {
                user.Contrasena = BCrypt.Net.BCrypt.HashPassword(model.Contrasena);
            }

            await _context.SaveChangesAsync();

            return RedirectToAction("PerfilConfiguracion");
        }

    }
}
