﻿using COMAVI_SA.Data;
using COMAVI_SA.Models;
using COMAVI_SA.Repository;
using DocumentFormat.OpenXml.InkML;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace COMAVI_SA.Services
{
#nullable disable
#pragma warning disable CS0168

    public interface IUserService
    {
        Task<Usuario> AuthenticateAsync(string email, string password);
        Task<bool> RegisterAsync(RegisterViewModel model, string verificationToken = null, DateTime? tokenExpiration = null);
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
        Task<bool> VerifyUserAsync(string email, string token);
        Task<bool> EnableMfaAsync(int userId, string otpCode);
        Task<bool> DisableMfaAsync(int userId, string otpCode);
        Task<bool> IsMfaEnabledAsync(int userId);
        Task<List<string>> GenerateBackupCodesAsync(int userId);
        Task<bool> VerifyBackupCodeAsync(int userId, string backupCode);
        Task<int> GetFailedMfaAttemptsAsync(int userId);
        Task RecordMfaAttemptAsync(int userId, bool success);
        Task ResetMfaFailedAttemptsAsync(int userId);
        string HashPassword(string password);
        Task<List<Usuario>> GetAllUsersAsync();

    }

    public class UserService : IUserService
    {
        private readonly ComaviDbContext _context;
        private readonly IPasswordService _passwordService;
        private readonly IOtpService _otpService;
        private readonly IConfiguration _configuration;
        private readonly IDatabaseRepository _databaseRepository;

        public UserService(
            ComaviDbContext context,
            IPasswordService passwordService,
            IOtpService otpService,
            IConfiguration configuration,
            IDatabaseRepository databaseRepository)

        {
            _context = context;
            _passwordService = passwordService;
            _otpService = otpService;
            _configuration = configuration;
            _databaseRepository = databaseRepository;

        }

        public async Task<Usuario> AuthenticateAsync(string email, string password)
        {
            try
            {
                var user = await _context.Usuarios
                            .AsNoTracking()
                            .Where(u => u.correo_electronico == email)
                            .Select(u => new Usuario
                            {
                                id_usuario = u.id_usuario,
                                nombre_usuario = u.nombre_usuario,
                                correo_electronico = u.correo_electronico,
                                contrasena = u.contrasena,
                                rol = u.rol,
                                ultimo_ingreso = u.ultimo_ingreso,
                                estado_verificacion = u.estado_verificacion,
                                fecha_verificacion = u.fecha_verificacion,
                                token_verificacion = u.token_verificacion,
                                fecha_expiracion_token = u.fecha_expiracion_token,
                                fecha_registro = u.fecha_registro,
                                mfa_habilitado = u.mfa_habilitado,
                                fecha_actualizacion_password = u.fecha_actualizacion_password
                            })
                            .FirstOrDefaultAsync();

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
                return null;
            }
        }

        public async Task<bool> RegisterAsync(RegisterViewModel model, string verificationToken = null, DateTime? tokenExpiration = null)
        {
            try
            {
                var hashedPassword = _passwordService.HashPassword(model.Password);

                var newUser = new Usuario
                {
                    nombre_usuario = model.UserName,
                    correo_electronico = model.Email,
                    contrasena = hashedPassword,
                    rol = model.Role,
                    estado_verificacion = "pendiente",
                    fecha_registro = DateTime.Now,
                    token_verificacion = verificationToken,
                    fecha_expiracion_token = tokenExpiration,
                    mfa_habilitado = false
                };

                _context.Usuarios.Add(newUser);
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
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
                var cutoffTime = DateTime.Now.AddMinutes(-lockoutTime);

                return await _context.Usuarios
                    .Where(u => u.correo_electronico == email)
                    .Join(_context.IntentosLogin,
                          u => u.id_usuario,
                          i => i.id_usuario,
                          (u, i) => new { User = u, Attempt = i })
                    .Where(x => !x.Attempt.exitoso && x.Attempt.fecha_hora >= cutoffTime)
                    .CountAsync() >= maxFailedAttempts;
            }
            catch (Exception ex)
            {
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
                throw;
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
                throw;
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
                return null;
            }
        }

        public async Task<Usuario> GetUserByIdAsync(int userId) => await _context.Usuarios.FindAsync(userId);

        public async Task<Usuario> GetUserByEmailAsync(string email) => await _context.Usuarios.FirstOrDefaultAsync(u => u.correo_electronico == email);

        public async Task<bool> VerifyUserAsync(string email, string token)
        {
            try
            {
                var user = await _context.Usuarios
                    .FirstOrDefaultAsync(u => u.correo_electronico == email &&
                                             u.token_verificacion == token &&
                                             u.fecha_expiracion_token > DateTime.Now &&
                                             u.estado_verificacion == "pendiente");

                if (user == null)
                    return false;

                // Actualizar estado de verificación
                user.estado_verificacion = "verificado";
                user.fecha_verificacion = DateTime.Now;
                user.token_verificacion = null;
                user.fecha_expiracion_token = null;

                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public async Task<bool> EnableMfaAsync(int userId, string otpCode)
        {
            try
            {
                var user = await _context.Usuarios.FindAsync(userId);
                if (user == null)
                    return false;

                var mfaRecord = await _context.MFA
                    .Where(m => m.id_usuario == userId && !m.usado)
                    .OrderByDescending(m => m.fecha_generacion)
                    .FirstOrDefaultAsync();

                if (mfaRecord == null)
                    return false;

                var isValid = _otpService.VerifyOtp(mfaRecord.codigo, otpCode);
                if (!isValid)
                    return false;

                // Activar MFA para el usuario
                user.mfa_habilitado = true;
                mfaRecord.esta_activo = true;
                mfaRecord.usado = true; // Marcar como usado pero activo

                // Generar códigos de respaldo
                var backupCodes = _otpService.GenerateBackupCodes();
                foreach (var code in backupCodes)
                {
                    _context.CodigosRespaldoMFA.Add(new CodigosRespaldoMFA
                    {
                        id_usuario = userId,
                        codigo = code,
                        fecha_generacion = DateTime.Now,
                        usado = false
                    });
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }


        public async Task<bool> DisableMfaAsync(int userId, string backupCode)
        {
            try
            {
                var user = await _context.Usuarios.FindAsync(userId);
                if (user == null)
                    return false;

                // Verificar si el backupCode es válido o si no se requiere verificación (código vacío)
                bool codeValid = string.IsNullOrEmpty(backupCode) ||
                                 await VerifyBackupCodeAsync(userId, backupCode);

                if (!codeValid)
                    return false;

                // Desactivar MFA para el usuario
                user.mfa_habilitado = false;

                // Desactivar todos los registros MFA
                var mfaRecords = await _context.MFA
                    .Where(m => m.id_usuario == userId)
                    .ToListAsync();

                foreach (var record in mfaRecords)
                {
                    record.esta_activo = false;
                }

                // Opcional: eliminar códigos de respaldo existentes
                var backupCodes = await _context.CodigosRespaldoMFA
                    .Where(c => c.id_usuario == userId)
                    .ToListAsync();

                _context.CodigosRespaldoMFA.RemoveRange(backupCodes);

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public async Task<bool> IsMfaEnabledAsync(int userId)
        {
            try
            {
                var user = await _context.Usuarios.FindAsync(userId);
                return user?.mfa_habilitado ?? false;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public async Task<List<string>> GenerateBackupCodesAsync(int userId)
        {
            try
            {
                var user = await _context.Usuarios.FindAsync(userId);
                if (user == null || !user.mfa_habilitado)
                    return new List<string>();

                // Eliminar códigos de respaldo existentes
                var existingCodes = await _context.CodigosRespaldoMFA
                    .Where(c => c.id_usuario == userId)
                    .ToListAsync();

                _context.CodigosRespaldoMFA.RemoveRange(existingCodes);

                // Generar nuevos códigos
                var backupCodes = _otpService.GenerateBackupCodes();
                var codesEntities = backupCodes.Select(code => new CodigosRespaldoMFA
                {
                    id_usuario = userId,
                    codigo = code,
                    fecha_generacion = DateTime.Now,
                    usado = false
                }).ToList();

                _context.CodigosRespaldoMFA.AddRange(codesEntities);
                await _context.SaveChangesAsync();

                return backupCodes;
            }
            catch (Exception ex)
            {
                return new List<string>();
            }
        }

        public async Task<bool> VerifyBackupCodeAsync(int userId, string backupCode)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(backupCode))
                    return false;

                // Normalizar el código (eliminar espacios, guiones, etc.)
                backupCode = backupCode.Replace("-", "").Replace(" ", "").ToUpper();

                CodigosRespaldoMFA backupCodeRecord = null;

                if (backupCode.Length == 5)
                {
                    var matchingCode = await _context.CodigosRespaldoMFA
                        .Where(c => c.id_usuario == userId &&
                                    c.usado == false &&
                                    EF.Functions.Like(c.codigo.Replace("-", ""), backupCode + "%"))
                        .FirstOrDefaultAsync();

                    backupCodeRecord = matchingCode;
                }

                // Buscar el código completo (10 caracteres)
                else if (backupCode.Length == 10)
                {
                    backupCodeRecord = await _context.CodigosRespaldoMFA
                        .Where(c => c.id_usuario == userId && !c.usado)
                        .FirstOrDefaultAsync(c => EF.Functions.Like(
                            c.codigo.Replace("-", ""), backupCode));
                }

                if (backupCodeRecord != null)
                {
                    backupCodeRecord.usado = true;
                    await _context.SaveChangesAsync();
                    return true;
                }


                return false;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public async Task<int> GetFailedMfaAttemptsAsync(int userId)
        {
            try
            {
                // Obtener intentos fallidos de MFA en los últimos 15 minutos
                return await _context.IntentosLogin
                    .Where(i => i.id_usuario == userId &&
                           !i.exitoso &&
                           i.fecha_hora >= DateTime.Now.AddMinutes(-15) &&
                           i.direccion_ip == "OTP_Verification")
                    .CountAsync();
            }
            catch (Exception ex)
            {
                return 0;
            }
        }

        public async Task RecordMfaAttemptAsync(int userId, bool success)
        {
            try
            {
                var mfaAttempt = new IntentosLogin
                {
                    id_usuario = userId,
                    fecha_hora = DateTime.Now,
                    exitoso = success,
                    direccion_ip = "OTP_Verification" // Marcar específicamente como intento OTP
                };

                _context.IntentosLogin.Add(mfaAttempt);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public async Task ResetMfaFailedAttemptsAsync(int userId)
        {
            try
            {
                // Eliminar intentos fallidos recientes de MFA
                var failedAttempts = await _context.IntentosLogin
                    .Where(i => i.id_usuario == userId &&
                           !i.exitoso &&
                           i.direccion_ip == "OTP_Verification" &&
                           i.fecha_hora >= DateTime.Now.AddMinutes(-15))
                    .ToListAsync();

                _context.IntentosLogin.RemoveRange(failedAttempts);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        // Modificación del método VerifyMfaCodeAsync existente
        public async Task<bool> VerifyMfaCodeAsync(int userId, string otpCode)
        {
            try
            {
                var user = await _context.Usuarios.FindAsync(userId);
                if (user == null)
                    return false;

                // Verificar si el usuario tiene MFA habilitado
                if (!user.mfa_habilitado)
                {
                    // Si no tiene MFA habilitado, verificar si está intentando configurarlo
                    var setupMfaRecord = await _context.MFA
                        .Where(m => m.id_usuario == userId && !m.usado)
                        .OrderByDescending(m => m.fecha_generacion)
                        .FirstOrDefaultAsync();

                    if (setupMfaRecord == null)
                        return true; // No tiene MFA configurado, permitir el acceso

                    return _otpService.VerifyOtp(setupMfaRecord.codigo, otpCode);
                }

                // Si tiene MFA habilitado, verificar el código
                var mfaRecord = await _context.MFA
                    .Where(m => m.id_usuario == userId && m.esta_activo)
                    .OrderByDescending(m => m.fecha_generacion)
                    .FirstOrDefaultAsync();

                if (mfaRecord == null)
                    return false;

                var isValid = _otpService.VerifyOtp(mfaRecord.codigo, otpCode);

                // Registrar el intento
                await RecordMfaAttemptAsync(userId, isValid);

                // Si es válido, reiniciar intentos fallidos
                if (isValid)
                {
                    await ResetMfaFailedAttemptsAsync(userId);
                }

                return isValid;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public async Task<List<Usuario>> GetAllUsersAsync()
        {
            try
            {
                // Obtener todos los usuarios activos
                var usuarios = await _databaseRepository.ExecuteQueryProcedureAsync<Usuario>(
                    "sp_ObtenerTodosUsuarios",
                    new { estado_verificacion = "verificado" }
                );

                return usuarios.ToList();
            }
            catch (Exception ex)
            {
                return new List<Usuario>();
            }
        }

        public string HashPassword(string password)
        {
            return _passwordService.HashPassword(password);
        }
    }
#nullable enable

}
