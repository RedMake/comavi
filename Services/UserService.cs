using COMAVI_SA.Data;
using COMAVI_SA.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace COMAVI_SA.Services
{
    public interface IUserService
    {
        Task<Usuario> AuthenticateAsync(string email, string password);
        Task<bool> RegisterAsync(RegisterViewModel model);
        Task<bool> IsEmailExistAsync(string email);
        Task<bool> IsAccountLockedAsync(string email);
        Task RecordLoginAttemptAsync(int? userId, string ipAddress, bool success);
        Task<string> GetMfaSecretAsync(int userId);
        Task SetupMfaAsync(int userId);
        Task<bool> VerifyMfaCodeAsync(int userId, string otpCode);
        Task<bool> ResetPasswordAsync(string email, string token, string newPassword);
        Task<string> GeneratePasswordResetTokenAsync(int userId);
        Task<Usuario> GetUserByIdAsync(int userId);
        Task<Usuario> GetUserByEmailAsync(string email);
    }

    public class UserService : IUserService
    {
        private readonly ComaviDbContext _context;
        private readonly IPasswordService _passwordService;
        private readonly IOtpService _otpService;
        private readonly ILogger<UserService> _logger;
        private readonly IConfiguration _configuration;

        public UserService(
            ComaviDbContext context,
            IPasswordService passwordService,
            IOtpService otpService,
            ILogger<UserService> logger,
            IConfiguration configuration)
        {
            _context = context;
            _passwordService = passwordService;
            _otpService = otpService;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<Usuario> AuthenticateAsync(string email, string password)
        {
            try
            {
                var user = await _context.Usuarios.FirstOrDefaultAsync(u => u.correo_electronico == email);
                if (user == null)
                    return null;

                // Si sólo se proporciona el correo, devolver el usuario sin verificar la contraseña
                if (string.IsNullOrEmpty(password))
                    return user;

                if (_passwordService.VerifyPassword(password, user.contrasena))
                    return user;

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al autenticar usuario");
                return null;
            }
        }

        public async Task<bool> RegisterAsync(RegisterViewModel model)
        {
            try
            {
                var hashedPassword = _passwordService.HashPassword(model.Password);

                var newUser = new Usuario
                {
                    nombre_usuario = model.UserName,
                    correo_electronico = model.Email,
                    contrasena = hashedPassword,
                    rol = model.Role
                };

                _context.Usuarios.Add(newUser);
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar usuario");
                return false;
            }
        }

        public async Task<bool> IsEmailExistAsync(string email)
        {
            return await _context.Usuarios.AnyAsync(u => u.correo_electronico == email);
        }

        public async Task<bool> IsAccountLockedAsync(string email)
        {
            try
            {
                var maxFailedAttempts = _configuration.GetValue<int>("SecuritySettings:Lockout:MaxFailedAttempts", 5);
                var lockoutTime = _configuration.GetValue<int>("SecuritySettings:Lockout:LockoutTimeInMinutes", 15);

                var user = await _context.Usuarios.FirstOrDefaultAsync(u => u.correo_electronico == email);
                if (user == null)
                    return false;

                var recentFailedAttempts = await _context.IntentosLogin
                    .Where(i => i.id_usuario == user.id_usuario &&
                            !i.exitoso &&
                            i.fecha_hora >= DateTime.Now.AddMinutes(-lockoutTime))
                    .CountAsync();

                return recentFailedAttempts >= maxFailedAttempts;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al verificar si la cuenta está bloqueada");
                return false;
            }
        }

        public async Task RecordLoginAttemptAsync(int? userId, string ipAddress, bool success)
        {
            try
            {
                var loginAttempt = new IntentosLogin
                {
                    id_usuario = userId,
                    fecha_hora = DateTime.Now,
                    exitoso = success,
                    direccion_ip = ipAddress
                };

                _context.IntentosLogin.Add(loginAttempt);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar intento de login");
            }
        }

        public async Task<string> GetMfaSecretAsync(int userId)
        {
            try
            {
                var mfaRecord = await _context.MFA
                    .Where(m => m.id_usuario == userId && !m.usado)
                    .OrderByDescending(m => m.fecha_generacion)
                    .FirstOrDefaultAsync();

                return mfaRecord?.codigo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener secreto MFA");
                return null;
            }
        }

        public async Task SetupMfaAsync(int userId)
        {
            try
            {
                var secret = _otpService.GenerateSecret();

                var mfa = new MFA
                {
                    id_usuario = userId,
                    codigo = secret,
                    fecha_generacion = DateTime.Now,
                    usado = false
                };

                _context.MFA.Add(mfa);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al configurar MFA");
            }
        }

        public async Task<bool> VerifyMfaCodeAsync(int userId, string otpCode)
        {
            try
            {
                var mfaRecord = await _context.MFA
                    .Where(m => m.id_usuario == userId && !m.usado)
                    .OrderByDescending(m => m.fecha_generacion)
                    .FirstOrDefaultAsync();

                if (mfaRecord == null)
                    return false;

                var isValid = _otpService.VerifyOtp(mfaRecord.codigo, otpCode);
                if (isValid)
                {
                    mfaRecord.usado = true;
                    await _context.SaveChangesAsync();
                }

                return isValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al verificar código MFA");
                return false;
            }
        }

        public async Task<bool> ResetPasswordAsync(string email, string token, string newPassword)
        {
            try
            {
                var user = await _context.Usuarios.FirstOrDefaultAsync(u => u.correo_electronico == email);
                if (user == null)
                    return false;

                var resetRecord = await _context.RestablecimientoContrasena
                    .FirstOrDefaultAsync(r => r.id_usuario == user.id_usuario &&
                                          r.token == token &&
                                          r.fecha_expiracion > DateTime.Now);

                if (resetRecord == null)
                    return false;

                user.contrasena = _passwordService.HashPassword(newPassword);
                _context.RestablecimientoContrasena.Remove(resetRecord);
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al restablecer contraseña");
                return false;
            }
        }

        public async Task<string> GeneratePasswordResetTokenAsync(int userId)
        {
            try
            {
                // Generar un token seguro
                var tokenBytes = new byte[32];
                using (var rng = RandomNumberGenerator.Create())
                {
                    rng.GetBytes(tokenBytes);
                }
                var token = Convert.ToBase64String(tokenBytes)
                    .Replace("+", "-")
                    .Replace("/", "_")
                    .Replace("=", "");

                // Eliminar tokens anteriores para este usuario
                var oldResets = await _context.RestablecimientoContrasena
                    .Where(r => r.id_usuario == userId)
                    .ToListAsync();

                _context.RestablecimientoContrasena.RemoveRange(oldResets);

                // Crear nuevo registro de restablecimiento
                var reset = new RestablecimientoContrasena
                {
                    id_usuario = userId,
                    token = token,
                    fecha_solicitud = DateTime.Now,
                    fecha_expiracion = DateTime.Now.AddHours(24)
                };

                _context.RestablecimientoContrasena.Add(reset);
                await _context.SaveChangesAsync();

                return token;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar token de restablecimiento de contraseña");
                return null;
            }
        }

        public async Task<Usuario> GetUserByIdAsync(int userId)
        {
            return await _context.Usuarios.FindAsync(userId);
        }

        public async Task<Usuario> GetUserByEmailAsync(string email)
        {
            return await _context.Usuarios.FirstOrDefaultAsync(u => u.correo_electronico == email);
        }
    }
}