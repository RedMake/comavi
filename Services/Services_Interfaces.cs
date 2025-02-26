using COMAVI_SA.Data;
using COMAVI_SA.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OtpNet;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace COMAVI_SA.Services
{
    public interface IUserService
    {
        Task<Usuario> AuthenticateAsync(string email, string password);
        Task<bool> RegisterAsync(RegisterViewModel model);
        Task<bool> IsEmailExistAsync(string email);
        Task<bool> SetupMfaAsync(int userId);
        Task<string> GetMfaSecretAsync(int userId);
        Task<bool> VerifyMfaCodeAsync(int userId, string code);
        Task<bool> RecordLoginAttemptAsync(int? userId, string ipAddress, bool success);
        Task<bool> IsAccountLockedAsync(string email);
        Task<string> GeneratePasswordResetTokenAsync(int userId);
        Task<bool> ResetPasswordAsync(string email, string token, string newPassword);
    }

    public interface IPasswordService
    {
        string HashPassword(string password);
        bool VerifyPassword(string password, string hashedPassword);
    }

    public interface IOtpService
    {
        string GenerateSecret();
        string GenerateQrCodeUri(string secret, string email);
        bool VerifyOtp(string secret, string code);
    }

    public interface IJwtService
    {
        string GenerateJwtToken(Usuario user);
        ClaimsPrincipal ValidateToken(string token);
    }

    public class UserService : IUserService
    {
        private readonly ComaviDbContext _context;
        private readonly IPasswordService _passwordService;
        private readonly IOtpService _otpService;
        private readonly IConfiguration _configuration;

        public UserService(
            ComaviDbContext context,
            IPasswordService passwordService,
            IOtpService otpService,
            IConfiguration configuration)
        {
            _context = context;
            _passwordService = passwordService;
            _otpService = otpService;
            _configuration = configuration;
        }

        public async Task<Usuario> AuthenticateAsync(string email, string password)
        {
            var user = await _context.Usuarios.SingleOrDefaultAsync(u => u.correo_electronico == email);

            if (user == null)
                return null;

            if (!_passwordService.VerifyPassword(password, user.contrasena))
                return null;

            return user;
        }

        public async Task<bool> RegisterAsync(RegisterViewModel model)
        {
            if (await IsEmailExistAsync(model.Email))
                return false;

            var hashedPassword = _passwordService.HashPassword(model.Password);

            var user = new Usuario
            {
                nombre_usuario = model.UserName,
                correo_electronico = model.Email,
                contrasena = hashedPassword,
                rol = (model.Role = "user"),
                ultimo_ingreso = null
            };

            _context.Usuarios.Add(user);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> IsEmailExistAsync(string email)
        {
            return await _context.Usuarios.AnyAsync(u => u.correo_electronico == email);
        }

        public async Task<bool> SetupMfaAsync(int userId)
        {
            var user = await _context.Usuarios.FindAsync(userId);
            if (user == null)
                return false;

            // Generate a new MFA secret
            string secret = _otpService.GenerateSecret();

            // Store the MFA secret in the database
            var mfaRecord = new MFA
            {
                id_usuario = userId,
                codigo = secret,
                fecha_generacion = DateTime.UtcNow,
                usado = false
            };

            _context.MFA.Add(mfaRecord);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<string> GetMfaSecretAsync(int userId)
        {
            var mfaRecord = await _context.MFA
                .Where(m => m.id_usuario == userId && !m.usado)
                .OrderByDescending(m => m.fecha_generacion)
                .FirstOrDefaultAsync();

            return mfaRecord?.codigo;
        }

        public async Task<bool> VerifyMfaCodeAsync(int userId, string code)
        {
            var secret = await GetMfaSecretAsync(userId);
            if (string.IsNullOrEmpty(secret))
                return false;

            return _otpService.VerifyOtp(secret, code);
        }
        public async Task<string> GeneratePasswordResetTokenAsync(int userId)
        {
            var user = await _context.Usuarios.FindAsync(userId);
            if (user == null) return null;

            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

            _context.RestablecimientoContrasena.Add(new RestablecimientoContrasena
            {
                id_usuario = userId,
                token = _passwordService.HashPassword(token),
                fecha_solicitud = DateTime.UtcNow,
                fecha_expiracion = DateTime.UtcNow.AddHours(2)
            });

            await _context.SaveChangesAsync();
            return token;
        }

        public async Task<bool> ResetPasswordAsync(string email, string token, string newPassword)
        {
            var user = await _context.Usuarios.FirstOrDefaultAsync(u => u.correo_electronico == email);
            if (user == null) return false;

            var resetRecord = await _context.RestablecimientoContrasena
                .FirstOrDefaultAsync(r => r.id_usuario == user.id_usuario &&
                                        r.fecha_expiracion > DateTime.UtcNow);

            if (resetRecord == null || !_passwordService.VerifyPassword(token, resetRecord.token))
                return false;

            user.contrasena = _passwordService.HashPassword(newPassword);
            resetRecord.fecha_expiracion = DateTime.UtcNow; // Invalida el token
            await _context.SaveChangesAsync();
            return true;
        }

        
        public async Task<bool> RecordLoginAttemptAsync(int? userId, string ipAddress, bool success)
        {
            var attempt = new IntentosLogin
            {
                id_usuario = userId,
                fecha_hora = DateTime.UtcNow,
                exitoso = success,
                direccion_ip = ipAddress
            };

            _context.IntentosLogin.Add(attempt);
            await _context.SaveChangesAsync();

            if (!success)
            {
                var adminEmails = await _context.Usuarios
                    .Where(u => u.rol == "admin")
                    .Select(u => u.correo_electronico)
                    .ToListAsync();

                //var emailService = new EmailService(_configuration);
                //foreach (var email in adminEmails)
                //{
                //    await emailService.SendEmailAsync(email,
                //        "Intento de inicio fallido",
                //        $"Se detectó un intento fallido desde la IP: {ipAddress} a las {DateTime.UtcNow}.");
                //}
            }
            return true;
        }

        public async Task<bool> IsAccountLockedAsync(string email)
        {
            var maxFailedAttempts = _configuration.GetValue<int>("SecuritySettings:Lockout:MaxFailedAttempts");
            var lockoutTimeInMinutes = _configuration.GetValue<int>("SecuritySettings:Lockout:LockoutTimeInMinutes");

            var user = await _context.Usuarios.SingleOrDefaultAsync(u => u.correo_electronico == email);
            if (user == null)
                return false;

            var cutoffTime = DateTime.UtcNow.AddMinutes(-lockoutTimeInMinutes);
            var failedAttempts = await _context.IntentosLogin
                .CountAsync(a => a.id_usuario == user.id_usuario &&
                                !a.exitoso &&
                                a.fecha_hora >= cutoffTime);

            return failedAttempts >= maxFailedAttempts;
        }
    }

    public class PasswordService : IPasswordService
    {
        public string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password, BCrypt.Net.BCrypt.GenerateSalt(12));
        }

        public bool VerifyPassword(string password, string hashedPassword)
        {
            return BCrypt.Net.BCrypt.Verify(password, hashedPassword);
        }
    }

    public class OtpService : IOtpService
    {
        private readonly IConfiguration _configuration;

        public OtpService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string GenerateSecret()
        {
            var secretLength = _configuration.GetValue<int>("OtpSettings:SecretLength", 20);
            var key = KeyGeneration.GenerateRandomKey(secretLength);
            return Base32Encoding.ToString(key);
        }

        public string GenerateQrCodeUri(string secret, string email)
        {
            var companyName = "COMAVI_SA";
            var encodedCompanyName = Uri.EscapeDataString(companyName);
            var encodedEmail = Uri.EscapeDataString(email);

            return $"otpauth://totp/{encodedCompanyName}:{encodedEmail}?secret={secret}&issuer={encodedCompanyName}";
        }

        public bool VerifyOtp(string secret, string code)
        {
            try
            {
                var step = _configuration.GetValue<int>("OtpSettings:Step", 30);
                var digits = _configuration.GetValue<int>("OtpSettings:Digits", 6);
                var useTimeCorrection = _configuration.GetValue<bool>("OtpSettings:UseTimeCorrection", false);

                var key = Base32Encoding.ToBytes(secret);
                var totp = new Totp(key, step: step, totpSize: digits);

                return totp.VerifyTotp(code, out long _, useTimeCorrection ? new VerificationWindow(previous: 1, future: 1) : null);
            }
            catch
            {
                return false;
            }
        }
    }

    public class JwtService : IJwtService
    {
        private readonly IConfiguration _configuration;

        public JwtService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string GenerateJwtToken(Usuario user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_configuration["JwtSettings:SecretKey"]);
            var expirationMinutes = _configuration.GetValue<int>("JwtSettings:ExpirationInMinutes", 60);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.id_usuario.ToString()),
                    new Claim(ClaimTypes.Name, user.nombre_usuario),
                    new Claim(ClaimTypes.Email, user.correo_electronico),
                    new Claim(ClaimTypes.Role, user.rol)
                }),
                Expires = DateTime.UtcNow.AddMinutes(expirationMinutes),
                Issuer = _configuration["JwtSettings:Issuer"],
                Audience = _configuration["JwtSettings:Audience"],
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        public ClaimsPrincipal ValidateToken(string token)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_configuration["JwtSettings:SecretKey"]);

            try
            {
                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = _configuration["JwtSettings:Issuer"],
                    ValidAudience = _configuration["JwtSettings:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(key)
                };

                return tokenHandler.ValidateToken(token, validationParameters, out _);
            }
            catch
            {
                return null;
            }
        }
    }
}