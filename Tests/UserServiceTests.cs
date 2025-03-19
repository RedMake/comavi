using COMAVI_SA.Data;
using COMAVI_SA.Models;
using COMAVI_SA.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace COMAVIxUnitTest
{
    public class UserServiceTests
    {
        private readonly ComaviDbContext _context;
        private readonly Mock<IPasswordService> _mockPasswordService;
        private readonly Mock<IOtpService> _mockOtpService;
        private readonly Mock<ILogger<UserService>> _mockLogger;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly UserService _userService;
        private List<Usuario> _usuarios;
        private List<IntentosLogin> _intentosLogin;
        private List<MFA> _mfas;
        private List<RestablecimientoContrasena> _restablecimientos;
        private List<CodigosRespaldoMFA> _codigosRespaldo;

        public UserServiceTests()
        {
            // Inicializar datos de prueba
            _usuarios = new List<Usuario>
            {
                new Usuario
                {
                    id_usuario = 1,
                    nombre_usuario = "TestUser",
                    correo_electronico = "test@example.com",
                    contrasena = "hashedpassword", // Será usado en VerifyPassword
                    rol = "admin",
                    mfa_habilitado = false // Añadido para tests de MFA
                }
            };

            _intentosLogin = new List<IntentosLogin>();
            _mfas = new List<MFA>();
            _restablecimientos = new List<RestablecimientoContrasena>();
            _codigosRespaldo = new List<CodigosRespaldoMFA>();

            // Configurar DbContext en memoria
            var options = new DbContextOptionsBuilder<ComaviDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new ComaviDbContext(options);

            // Poblar DbContext con datos de prueba
            _context.Usuarios.AddRange(_usuarios);
            _context.SaveChanges();

            // Configurar mocks
            _mockPasswordService = new Mock<IPasswordService>();
            _mockOtpService = new Mock<IOtpService>();
            _mockLogger = new Mock<ILogger<UserService>>();
            _mockConfiguration = new Mock<IConfiguration>();
            var mockSection = new Mock<IConfigurationSection>();

            // Configurar valores de configuración para bloqueo de cuenta
            mockSection.Setup(x => x.Value).Returns("5");
            _mockConfiguration.Setup(c => c.GetSection("SecuritySettings:Lockout:MaxFailedAttempts")).Returns(mockSection.Object);

            var lockoutTimeSection = new Mock<IConfigurationSection>();
            lockoutTimeSection.Setup(a => a.Value).Returns("15");
            _mockConfiguration.Setup(c => c.GetSection("SecuritySettings:Lockout:LockoutTimeInMinutes")).Returns(lockoutTimeSection.Object);

            // Setup para GetValue
            var maxFailedAttemptsSection = new Mock<IConfigurationSection>();
            maxFailedAttemptsSection.Setup(x => x.Value).Returns("5");
            _mockConfiguration.Setup(c => c.GetSection("SecuritySettings:Lockout:MaxFailedAttempts"))
                              .Returns(maxFailedAttemptsSection.Object);

            var lockoutTimeSection_1 = new Mock<IConfigurationSection>();
            lockoutTimeSection.Setup(x => x.Value).Returns("15");
            _mockConfiguration.Setup(c => c.GetSection("SecuritySettings:Lockout:LockoutTimeInMinutes"))
                              .Returns(lockoutTimeSection_1.Object);


            // Configurar comportamiento del servicio de contraseñas
            _mockPasswordService.Setup(p => p.VerifyPassword("correctpassword", "hashedpassword")).Returns(true);
            _mockPasswordService.Setup(p => p.VerifyPassword("wrongpassword", "hashedpassword")).Returns(false);
            _mockPasswordService.Setup(p => p.HashPassword(It.IsAny<string>())).Returns("new_hashed_password");

            // Configurar comportamiento del servicio OTP
            _mockOtpService.Setup(o => o.GenerateSecret()).Returns("testsecret");
            _mockOtpService.Setup(o => o.VerifyOtp("testsecret", "123456")).Returns(true);
            _mockOtpService.Setup(o => o.VerifyOtp("testsecret", "111111")).Returns(false);
            _mockOtpService.Setup(o => o.GenerateBackupCodes(It.IsAny<int>())).Returns(new List<string> { "ABCDE-FGHIJ", "KLMNO-PQRST" });

            // Inicializar servicio
            _userService = new UserService(
                _context,
                _mockPasswordService.Object,
                _mockOtpService.Object,
                _mockLogger.Object,
                _mockConfiguration.Object);
        }

        [Fact]
        public async Task AuthenticateAsync_WithCorrectCredentials_ReturnsUser()
        {
            // Arrange
            var email = "test@example.com";
            var password = "correctpassword";

            // Act
            var result = await _userService.AuthenticateAsync(email, password);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.id_usuario);
            Assert.Equal(email, result.correo_electronico);
        }

        [Fact]
        public async Task AuthenticateAsync_WithIncorrectPassword_ReturnsNull()
        {
            // Arrange
            var email = "test@example.com";
            var password = "wrongpassword";

            // Act
            var result = await _userService.AuthenticateAsync(email, password);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task RegisterAsync_AddsUserToDatabase()
        {
            // Arrange
            var model = new RegisterViewModel
            {
                UserName = "NewUser",
                Email = "newuser@example.com",
                Password = "newpassword",
                Role = "user"
            };

            // Act
            var result = await _userService.RegisterAsync(model);

            // Assert
            Assert.True(result);
            var addedUser = await _context.Usuarios.FirstOrDefaultAsync(u => u.correo_electronico == model.Email);
            Assert.NotNull(addedUser);
            Assert.Equal(model.UserName, addedUser.nombre_usuario);
            Assert.Equal(model.Email, addedUser.correo_electronico);
            Assert.Equal(model.Role, addedUser.rol);
        }

        [Fact]
        public async Task IsEmailExistAsync_WithExistingEmail_ReturnsTrue()
        {
            // Arrange
            var email = "test@example.com";

            // Act
            var result = await _userService.IsEmailExistAsync(email);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task IsEmailExistAsync_WithNonExistingEmail_ReturnsFalse()
        {
            // Arrange
            var email = "nonexistent@example.com";

            // Act
            var result = await _userService.IsEmailExistAsync(email);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task SetupMfaAsync_CreatesNewMfaRecord()
        {
            // Arrange
            var userId = 1;

            // Act
            await _userService.SetupMfaAsync(userId);

            // Assert
            var mfaRecord = await _context.MFA.FirstOrDefaultAsync(m => m.id_usuario == userId);
            Assert.NotNull(mfaRecord);
            Assert.Equal("testsecret", mfaRecord.codigo);
            Assert.False(mfaRecord.usado);
        }

        [Fact]
        public async Task VerifyMfaCodeAsync_WithValidCode_ReturnsTrue()
        {
            // Arrange
            var userId = 1;
            var mfaCode = "123456";

            // Actualizar usuario para tener MFA habilitado
            var user = await _context.Usuarios.FindAsync(userId);
            user.mfa_habilitado = true;
            await _context.SaveChangesAsync();

            var mfa = new MFA
            {
                id_mfa = 1,
                id_usuario = userId,
                codigo = "testsecret",
                fecha_generacion = DateTime.Now,
                usado = false,
                esta_activo = true  // Añadido para que el MFA esté activo
            };
            _context.MFA.Add(mfa);
            await _context.SaveChangesAsync();

            // Act
            var result = await _userService.VerifyMfaCodeAsync(userId, mfaCode);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task VerifyMfaCodeAsync_WithInvalidCode_ReturnsFalse()
        {
            // Arrange
            var userId = 1;
            var mfaCode = "111111"; // Código inválido

            // Actualizar usuario para tener MFA habilitado
            var user = await _context.Usuarios.FindAsync(userId);
            user.mfa_habilitado = true;
            await _context.SaveChangesAsync();

            var mfa = new MFA
            {
                id_mfa = 2,
                id_usuario = userId,
                codigo = "testsecret",
                fecha_generacion = DateTime.Now,
                usado = false,
                esta_activo = true  // MFA activo
            };
            _context.MFA.Add(mfa);
            await _context.SaveChangesAsync();

            // Act
            var result = await _userService.VerifyMfaCodeAsync(userId, mfaCode);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task EnableMfaAsync_WithValidCode_ActivatesMfa()
        {
            // Arrange
            var userId = 1;
            var otpCode = "123456"; // Código válido

            // Asegurarse de que MFA no esté habilitado inicialmente
            var user = await _context.Usuarios.FindAsync(userId);
            user.mfa_habilitado = false;
            await _context.SaveChangesAsync();

            // Crear registro MFA para habilitar
            var mfa = new MFA
            {
                id_mfa = 3,
                id_usuario = userId,
                codigo = "testsecret",
                fecha_generacion = DateTime.Now,
                usado = false
            };
            _context.MFA.Add(mfa);
            await _context.SaveChangesAsync();

            // Configurar OTP service para verificar el código
            _mockOtpService.Setup(o => o.VerifyOtp("testsecret", otpCode)).Returns(true);

            // Act
            var result = await _userService.EnableMfaAsync(userId, otpCode);

            // Assert
            Assert.True(result);

            // Verificar que el usuario tiene MFA habilitado
            var updatedUser = await _context.Usuarios.FindAsync(userId);
            Assert.True(updatedUser.mfa_habilitado);

            // Verificar que se crearon códigos de respaldo
            var backupCodes = await _context.CodigosRespaldoMFA.Where(c => c.id_usuario == userId).ToListAsync();
            Assert.NotEmpty(backupCodes);
        }

        [Fact]
        public async Task DisableMfaAsync_WithValidCode_DisablesMfa()
        {
            // Arrange
            var userId = 1;
            var backupCode = "ABCDE-FGHIJ"; // Código válido

            // Configurar usuario con MFA habilitado
            var user = await _context.Usuarios.FindAsync(userId);
            user.mfa_habilitado = true;
            await _context.SaveChangesAsync();

            // Añadir un MFA activo
            var mfa = new MFA
            {
                id_mfa = 4,
                id_usuario = userId,
                codigo = "testsecret",
                fecha_generacion = DateTime.Now,
                usado = false,
                esta_activo = true
            };
            _context.MFA.Add(mfa);

            // Añadir código de respaldo
            var codigo = new CodigosRespaldoMFA
            {
                id_usuario = userId,
                codigo = "ABCDE-FGHIJ",
                fecha_generacion = DateTime.Now,
                usado = false
            };
            _context.CodigosRespaldoMFA.Add(codigo);

            await _context.SaveChangesAsync();

            // Act
            var result = await _userService.DisableMfaAsync(userId, backupCode);

            // Assert
            Assert.True(result);

            // Verificar que MFA está deshabilitado
            var updatedUser = await _context.Usuarios.FindAsync(userId);
            Assert.False(updatedUser.mfa_habilitado);

            // Verificar que MFA está inactivo
            var updatedMfa = await _context.MFA.FindAsync(4);
            Assert.False(updatedMfa.esta_activo);
        }

        [Fact]
        public async Task VerifyUserAsync_WithValidToken_ReturnsTrue()
        {
            // Arrange
            var tokenExpiration = DateTime.Now.AddDays(1);
            var user = new Usuario
            {
                correo_electronico = "verificacion@example.com",
                nombre_usuario = "Usuario Verificación",
                contrasena = "hashedpassword",
                rol = "user",
                estado_verificacion = "pendiente",
                token_verificacion = "valid_verification_token",
                fecha_expiracion_token = tokenExpiration
            };

            _context.Usuarios.Add(user);
            await _context.SaveChangesAsync();

            // Act
            var result = await _userService.VerifyUserAsync("verificacion@example.com", "valid_verification_token");

            // Assert
            Assert.True(result);

            // Verificar que el usuario se ha actualizado correctamente
            var updatedUser = await _context.Usuarios.FindAsync(user.id_usuario);
            Assert.Equal("verificado", updatedUser.estado_verificacion);
            Assert.Null(updatedUser.token_verificacion);
            Assert.Null(updatedUser.fecha_expiracion_token);
            Assert.NotNull(updatedUser.fecha_verificacion);
        }

        [Fact]
        public async Task VerifyUserAsync_WithInvalidToken_ReturnsFalse()
        {
            // Arrange
            var user = new Usuario
            {
                correo_electronico = "test2@example.com",
                nombre_usuario = "Test User 2",
                contrasena = "hashedpassword",
                rol = "user",
                estado_verificacion = "pendiente",
                token_verificacion = "correct_token",
                fecha_expiracion_token = DateTime.Now.AddDays(1)
            };

            _context.Usuarios.Add(user);
            await _context.SaveChangesAsync();

            // Act
            var result = await _userService.VerifyUserAsync("test2@example.com", "wrong_token");

            // Assert
            Assert.False(result);

            // Verificar que el usuario no ha cambiado
            var unchangedUser = await _context.Usuarios.FindAsync(user.id_usuario);
            Assert.Equal("pendiente", unchangedUser.estado_verificacion);
            Assert.Equal("correct_token", unchangedUser.token_verificacion);
        }

        [Fact]
        public async Task VerifyUserAsync_WithExpiredToken_ReturnsFalse()
        {
            // Arrange
            var user = new Usuario
            {
                correo_electronico = "test3@example.com",
                nombre_usuario = "Test User 3",
                contrasena = "hashedpassword",
                rol = "user",
                estado_verificacion = "pendiente",
                token_verificacion = "expired_token",
                fecha_expiracion_token = DateTime.Now.AddDays(-1) // Token expirado
            };

            _context.Usuarios.Add(user);
            await _context.SaveChangesAsync();

            // Act
            var result = await _userService.VerifyUserAsync("test3@example.com", "expired_token");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task VerifyBackupCodeAsync_WithValidCode_ReturnsTrue()
        {
            // Arrange
            var userId = 1;
            var backupCode = "ABCDE-FGHIJ";

            // Añadir código de respaldo
            var codigo = new CodigosRespaldoMFA
            {
                id_usuario = userId,
                codigo = "ABCDE-FGHIJ",
                fecha_generacion = DateTime.Now,
                usado = false
            };
            _context.CodigosRespaldoMFA.Add(codigo);
            await _context.SaveChangesAsync();

            // Act
            var result = await _userService.VerifyBackupCodeAsync(userId, backupCode);

            // Assert
            Assert.True(result);

            // Verificar que el código se marcó como usado
            var updatedCodigo = await _context.CodigosRespaldoMFA.FirstOrDefaultAsync(c => c.id_usuario == userId);
            Assert.True(updatedCodigo.usado);
        }

        [Fact]
        public async Task IsMfaEnabledAsync_WithEnabledMfa_ReturnsTrue()
        {
            // Arrange
            var userId = 1;
            var user = await _context.Usuarios.FindAsync(userId);
            user.mfa_habilitado = true;
            await _context.SaveChangesAsync();

            // Act
            var result = await _userService.IsMfaEnabledAsync(userId);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task GenerateBackupCodesAsync_CreatesNewCodes()
        {
            // Arrange
            var userId = 1;
            var user = await _context.Usuarios.FindAsync(userId);
            user.mfa_habilitado = true;
            await _context.SaveChangesAsync();

            // Act
            var result = await _userService.GenerateBackupCodesAsync(userId);

            // Assert
            Assert.NotEmpty(result);
            Assert.Equal(2, result.Count); // Basado en el setup del mock

            // Verificar que los códigos se guardaron en la base de datos
            var savedCodes = await _context.CodigosRespaldoMFA.Where(c => c.id_usuario == userId).ToListAsync();
            Assert.Equal(result.Count, savedCodes.Count);
        }
    }
}