using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using COMAVI_SA.Data;
using COMAVI_SA.Models;
using COMAVI_SA.Repository;
using COMAVI_SA.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace COMAVI_SA.Tests.Services
{
    public class UserServiceTests
    {
        private readonly Mock<ComaviDbContext> _mockContext;
        private readonly Mock<IPasswordService> _mockPasswordService;
        private readonly Mock<IOtpService> _mockOtpService;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly Mock<IDatabaseRepository> _mockDatabaseRepository;
        private readonly UserService _userService;
        private readonly Mock<DbSet<Usuario>> _mockUsuarios;
        private readonly Mock<DbSet<IntentosLogin>> _mockIntentosLogin;
        private readonly Mock<DbSet<RestablecimientoContrasena>> _mockRestablecimientoContrasena;
        private readonly Mock<DbSet<MFA>> _mockMFA;
        private readonly Mock<DbSet<CodigosRespaldoMFA>> _mockCodigosRespaldo;
        
        public UserServiceTests()
        {
            _mockContext = new Mock<ComaviDbContext>(new DbContextOptions<ComaviDbContext>());
            _mockPasswordService = new Mock<IPasswordService>();
            _mockOtpService = new Mock<IOtpService>();
            _mockConfiguration = new Mock<IConfiguration>();
            _mockDatabaseRepository = new Mock<IDatabaseRepository>();
            
            _mockUsuarios = new Mock<DbSet<Usuario>>();
            _mockIntentosLogin = new Mock<DbSet<IntentosLogin>>();
            _mockRestablecimientoContrasena = new Mock<DbSet<RestablecimientoContrasena>>();
            _mockMFA = new Mock<DbSet<MFA>>();
            _mockCodigosRespaldo = new Mock<DbSet<CodigosRespaldoMFA>>();



            _mockContext.Setup(c => c.Usuarios).Returns(_mockUsuarios.Object);
            _mockContext.Setup(c => c.IntentosLogin).Returns(_mockIntentosLogin.Object);
            _mockContext.Setup(c => c.RestablecimientoContrasena).Returns(_mockRestablecimientoContrasena.Object);
            _mockContext.Setup(c => c.MFA).Returns(_mockMFA.Object);
            _mockContext.Setup(c => c.CodigosRespaldoMFA).Returns(_mockCodigosRespaldo.Object);
            
            _userService = new UserService(
                _mockContext.Object,
                _mockPasswordService.Object,
                _mockOtpService.Object,
                _mockConfiguration.Object,
                _mockDatabaseRepository.Object);
        }
        
        #region AuthenticateAsync Tests
        
        [Fact]
        public async Task AuthenticateAsync_ReturnsNullWhenUserNotFound()
        {
            // Arrange
            var usuarios = new List<Usuario>().AsQueryable();
            
            _mockUsuarios.As<IQueryable<Usuario>>().Setup(m => m.Provider).Returns(usuarios.Provider);
            _mockUsuarios.As<IQueryable<Usuario>>().Setup(m => m.Expression).Returns(usuarios.Expression);
            _mockUsuarios.As<IQueryable<Usuario>>().Setup(m => m.ElementType).Returns(usuarios.ElementType);
            _mockUsuarios.As<IQueryable<Usuario>>().Setup(m => m.GetEnumerator()).Returns(usuarios.GetEnumerator());
            
            // Act
            var result = await _userService.AuthenticateAsync("nonexistent@example.com", "password");
            
            // Assert
            Assert.Null(result);
        }
        
        [Fact]
        public async Task AuthenticateAsync_ReturnsUserWhenPasswordIsCorrect()
        {
            // Arrange
            const string testEmail = "test@example.com";
            const string testPassword = "password";
            const string hashedPassword = "hashedPassword";
            
            var usuarios = new List<Usuario>
            {
                new Usuario
                {
                    id_usuario = 1,
                    nombre_usuario = "Test User",
                    correo_electronico = testEmail,
                    contrasena = hashedPassword,
                    rol = "user"
                }
            }.AsQueryable();
            
            _mockUsuarios.As<IQueryable<Usuario>>().Setup(m => m.Provider).Returns(usuarios.Provider);
            _mockUsuarios.As<IQueryable<Usuario>>().Setup(m => m.Expression).Returns(usuarios.Expression);
            _mockUsuarios.As<IQueryable<Usuario>>().Setup(m => m.ElementType).Returns(usuarios.ElementType);
            _mockUsuarios.As<IQueryable<Usuario>>().Setup(m => m.GetEnumerator()).Returns(usuarios.GetEnumerator());
            
            _mockPasswordService.Setup(p => p.VerifyPassword(testPassword, hashedPassword)).Returns(true);
            
            // Act
            var result = await _userService.AuthenticateAsync(testEmail, testPassword);
            
            // Assert
            Assert.NotNull(result);
            Assert.Equal(testEmail, result.correo_electronico);
            Assert.Equal("user", result.rol);
        }
        
        [Fact]
        public async Task AuthenticateAsync_ReturnsNullWhenPasswordIsIncorrect()
        {
            // Arrange
            const string testEmail = "test@example.com";
            const string testPassword = "password";
            const string hashedPassword = "hashedPassword";
            
            var usuarios = new List<Usuario>
            {
                new Usuario
                {
                    id_usuario = 1,
                    nombre_usuario = "Test User",
                    correo_electronico = testEmail,
                    contrasena = hashedPassword,
                    rol = "user"
                }
            }.AsQueryable();

            _mockUsuarios.As<IQueryable<Usuario>>().Setup(m => m.Provider).Returns(usuarios.Provider);
            _mockUsuarios.As<IQueryable<Usuario>>().Setup(m => m.Expression).Returns(usuarios.Expression);
            _mockUsuarios.As<IQueryable<Usuario>>().Setup(m => m.ElementType).Returns(usuarios.ElementType);
            _mockUsuarios.As<IQueryable<Usuario>>().Setup(m => m.GetEnumerator()).Returns(usuarios.GetEnumerator());
            
            _mockPasswordService.Setup(p => p.VerifyPassword(testPassword, hashedPassword)).Returns(false);
            
            // Act
            var result = await _userService.AuthenticateAsync(testEmail, testPassword);
            
            // Assert
            Assert.Null(result);
        }
        
        [Fact]
        public async Task AuthenticateAsync_ReturnsUserWhenOnlyEmailIsProvided()
        {
            // Arrange
            const string testEmail = "test@example.com";
            
            var usuarios = new List<Usuario>
            {
                new Usuario
                {
                    id_usuario = 1,
                    nombre_usuario = "Test User",
                    correo_electronico = testEmail,
                    contrasena = "hashedPassword",
                    rol = "user"
                }
            }.AsQueryable();
            
            _mockUsuarios.As<IQueryable<Usuario>>().Setup(m => m.Provider).Returns(usuarios.Provider);
            _mockUsuarios.As<IQueryable<Usuario>>().Setup(m => m.Expression).Returns(usuarios.Expression);
            _mockUsuarios.As<IQueryable<Usuario>>().Setup(m => m.ElementType).Returns(usuarios.ElementType);
            _mockUsuarios.As<IQueryable<Usuario>>().Setup(m => m.GetEnumerator()).Returns(usuarios.GetEnumerator());
            
            // Act
            var result = await _userService.AuthenticateAsync(testEmail, null);
            
            // Assert
            Assert.NotNull(result);
            Assert.Equal(testEmail, result.correo_electronico);
        }
        
        #endregion
        
        #region RegisterAsync Tests
        
        [Fact]
        public async Task RegisterAsync_CreatesNewUserSuccessfully()
        {
            // Arrange
            var model = new RegisterViewModel
            {
                UserName = "Test User",
                Email = "test@example.com",
                Password = "password",
                Role = "user"
            };
            
            const string hashedPassword = "hashedPassword";
            
            _mockPasswordService.Setup(p => p.HashPassword(model.Password)).Returns(hashedPassword);
            _mockContext.Setup(c => c.Usuarios.Add(It.IsAny<Usuario>())).Verifiable();
            _mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
            
            // Act
            var result = await _userService.RegisterAsync(model);
            
            // Assert
            Assert.True(result);
            _mockContext.Verify(c => c.Usuarios.Add(It.Is<Usuario>(u => 
                u.nombre_usuario == model.UserName && 
                u.correo_electronico == model.Email && 
                u.contrasena == hashedPassword && 
                u.rol == model.Role)), 
                Times.Once);
            _mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }
        
        [Fact]
        public async Task RegisterAsync_ReturnsFalseOnException()
        {
            // Arrange
            var model = new RegisterViewModel
            {
                UserName = "Test User",
                Email = "test@example.com",
                Password = "password",
                Role = "user"
            };
            
            _mockPasswordService.Setup(p => p.HashPassword(model.Password)).Returns("hashedPassword");
            _mockContext.Setup(c => c.Usuarios.Add(It.IsAny<Usuario>())).Throws(new Exception("DB Error"));
            
            // Act
            var result = await _userService.RegisterAsync(model);
            
            // Assert
            Assert.False(result);
        }
        
        #endregion
        
        #region IsEmailExistAsync Tests
        
        [Fact]
        public async Task IsEmailExistAsync_ReturnsTrueWhenEmailExists()
        {
            // Arrange
            const string testEmail = "existing@example.com";
            
            _mockUsuarios.Setup(m => m.AnyAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<Usuario, bool>>>(),
                It.IsAny<CancellationToken>()
            )).ReturnsAsync(true);
            
            // Act
            var result = await _userService.IsEmailExistAsync(testEmail);
            
            // Assert
            Assert.True(result);
        }
        
        [Fact]
        public async Task IsEmailExistAsync_ReturnsFalseWhenEmailDoesNotExist()
        {
            // Arrange
            const string testEmail = "nonexistent@example.com";
            
            _mockUsuarios.Setup(m => m.AnyAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<Usuario, bool>>>(),
                It.IsAny<CancellationToken>()
            )).ReturnsAsync(false);
            
            // Act
            var result = await _userService.IsEmailExistAsync(testEmail);
            
            // Assert
            Assert.False(result);
        }
        
        #endregion
        
        #region VerifyMfaCodeAsync Tests
        
        [Fact]
        public async Task VerifyMfaCodeAsync_ReturnsTrueWhenMfaNotEnabledAndNoSetup()
        {
            // Arrange
            const int userId = 1;
            const string otpCode = "123456";
            
            var user = new Usuario { id_usuario = userId, mfa_habilitado = false };
            
            _mockContext.Setup(c => c.Usuarios.FindAsync(userId)).ReturnsAsync(user);
            
            _mockMFA.Setup(m => m.Where(It.IsAny<System.Linq.Expressions.Expression<Func<MFA, bool>>>()))
                .Returns(new List<MFA>().AsQueryable());
            
            // Act
            var result = await _userService.VerifyMfaCodeAsync(userId, otpCode);
            
            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task VerifyMfaCodeAsync_ReturnsResultFromOtpServiceWhenMfaEnabled()
        {
            // Arrange
            const int userId = 1;
            const string otpCode = "123456";
            const string mfaSecret = "TESTSECRET";

            var user = new Usuario { id_usuario = userId, mfa_habilitado = true };
            var mfaRecord = new MFA { id_mfa = 1, id_usuario = userId, codigo = mfaSecret, esta_activo = true };

            _mockContext.Setup(c => c.Usuarios.FindAsync(userId)).ReturnsAsync(user);

            var mfaRecords = new List<MFA> { mfaRecord }.AsQueryable();
            var mockMfaQueryable = new Mock<IQueryable<MFA>>();
            mockMfaQueryable.As<IQueryable<MFA>>().Setup(m => m.Provider).Returns(mfaRecords.Provider);
            mockMfaQueryable.As<IQueryable<MFA>>().Setup(m => m.Expression).Returns(mfaRecords.Expression);
            mockMfaQueryable.As<IQueryable<MFA>>().Setup(m => m.ElementType).Returns(mfaRecords.ElementType);
            mockMfaQueryable.As<IQueryable<MFA>>().Setup(m => m.GetEnumerator()).Returns(mfaRecords.GetEnumerator());

            _mockMFA.Setup(m => m.Where(It.IsAny<System.Linq.Expressions.Expression<Func<MFA, bool>>>()))
                .Returns(mockMfaQueryable.Object);

            _mockOtpService
                .Setup(o => o.VerifyOtp(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(true); 
            // Mock the attempt recording
            _mockContext.Setup(c => c.IntentosLogin.Add(It.IsAny<IntentosLogin>())).Verifiable();
            _mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
            
            // Act
            var result = await _userService.VerifyMfaCodeAsync(userId, otpCode);
            
            // Assert
            Assert.True(result);
            _mockOtpService.Verify(o => o.VerifyOtp(mfaSecret, otpCode), Times.Once);
            _mockContext.Verify(c => c.IntentosLogin.Add(It.IsAny<IntentosLogin>()), Times.Once);
        }
        
        [Fact]
        public async Task VerifyMfaCodeAsync_ReturnsFalseWhenOtpCodeIsInvalid()
        {
            // Arrange
            const int userId = 1;
            const string otpCode = "123456";
            const string mfaSecret = "TESTSECRET";
            
            var user = new Usuario { id_usuario = userId, mfa_habilitado = true };
            var mfaRecord = new MFA { id_mfa = 1, id_usuario = userId, codigo = mfaSecret, esta_activo = true };
            
            _mockContext.Setup(c => c.Usuarios.FindAsync(userId)).ReturnsAsync(user);
            
            var mfaRecords = new List<MFA> { mfaRecord }.AsQueryable();
            var mockMfaQueryable = new Mock<IQueryable<MFA>>();
            mockMfaQueryable.As<IQueryable<MFA>>().Setup(m => m.Provider).Returns(mfaRecords.Provider);
            mockMfaQueryable.As<IQueryable<MFA>>().Setup(m => m.Expression).Returns(mfaRecords.Expression);
            mockMfaQueryable.As<IQueryable<MFA>>().Setup(m => m.ElementType).Returns(mfaRecords.ElementType);
            mockMfaQueryable.As<IQueryable<MFA>>().Setup(m => m.GetEnumerator()).Returns(mfaRecords.GetEnumerator());
            
            _mockMFA.Setup(m => m.Where(It.IsAny<System.Linq.Expressions.Expression<Func<MFA, bool>>>()))
                .Returns(mockMfaQueryable.Object);

            _mockOtpService
                .Setup(o => o.VerifyOtp(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(true);

            // Mock the attempt recording
            _mockContext.Setup(c => c.IntentosLogin.Add(It.IsAny<IntentosLogin>())).Verifiable();
            _mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
            
            // Act
            var result = await _userService.VerifyMfaCodeAsync(userId, otpCode);
            
            // Assert
            Assert.False(result);
            _mockOtpService.Verify(o => o.VerifyOtp(mfaSecret, otpCode), Times.Once);
            _mockContext.Verify(c => c.IntentosLogin.Add(It.Is<IntentosLogin>(i => !i.exitoso)), Times.Once);
        }
        
        #endregion
        
        #region EnableMfaAsync Tests
        
        [Fact]
        public async Task EnableMfaAsync_ReturnsFalseWhenUserDoesNotExist()
        {
            // Arrange
            const int userId = 1;
            const string otpCode = "123456";
            
            _mockContext.Setup(c => c.Usuarios.FindAsync(userId)).ReturnsAsync((Usuario)null);
            
            // Act
            var result = await _userService.EnableMfaAsync(userId, otpCode);
            
            // Assert
            Assert.False(result);
        }
        
        [Fact]
        public async Task EnableMfaAsync_ReturnsFalseWhenMfaRecordDoesNotExist()
        {
            // Arrange
            const int userId = 1;
            const string otpCode = "123456";
            
            var user = new Usuario { id_usuario = userId, mfa_habilitado = false };
            
            _mockContext.Setup(c => c.Usuarios.FindAsync(userId)).ReturnsAsync(user);
            
            _mockMFA.Setup(m => m.Where(It.IsAny<System.Linq.Expressions.Expression<Func<MFA, bool>>>()))
                .Returns(new List<MFA>().AsQueryable());
            
            // Act
            var result = await _userService.EnableMfaAsync(userId, otpCode);
            
            // Assert
            Assert.False(result);
        }
        
        [Fact]
        public async Task EnableMfaAsync_ReturnsFalseWhenOtpCodeIsInvalid()
        {
            // Arrange
            const int userId = 1;
            const string otpCode = "123456";
            const string mfaSecret = "TESTSECRET";
            
            var user = new Usuario { id_usuario = userId, mfa_habilitado = false };
            var mfaRecord = new MFA { id_mfa = 1, id_usuario = userId, codigo = mfaSecret, usado = false };
            
            _mockContext.Setup(c => c.Usuarios.FindAsync(userId)).ReturnsAsync(user);
            
            var mfaRecords = new List<MFA> { mfaRecord }.AsQueryable();
            var mockMfaQueryable = new Mock<IQueryable<MFA>>();
            mockMfaQueryable.As<IQueryable<MFA>>().Setup(m => m.Provider).Returns(mfaRecords.Provider);
            mockMfaQueryable.As<IQueryable<MFA>>().Setup(m => m.Expression).Returns(mfaRecords.Expression);
            mockMfaQueryable.As<IQueryable<MFA>>().Setup(m => m.ElementType).Returns(mfaRecords.ElementType);
            mockMfaQueryable.As<IQueryable<MFA>>().Setup(m => m.GetEnumerator()).Returns(mfaRecords.GetEnumerator());
            
            _mockMFA.Setup(m => m.Where(It.IsAny<System.Linq.Expressions.Expression<Func<MFA, bool>>>()))
                .Returns(mockMfaQueryable.Object);

            _mockOtpService
                .Setup(o => o.VerifyOtp(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(true);
            // Act
            var result = await _userService.EnableMfaAsync(userId, otpCode);
            
            // Assert
            Assert.False(result);
            _mockOtpService.Verify(o => o.VerifyOtp(mfaSecret, otpCode), Times.Once);
        }
        
        [Fact]
        public async Task EnableMfaAsync_EnablesMfaAndGeneratesBackupCodesWhenOtpIsValid()
        {
            // Arrange
            const int userId = 1;
            const string otpCode = "123456";
            const string mfaSecret = "TESTSECRET";

            var user = new Usuario { id_usuario = userId, mfa_habilitado = false };
            var mfaRecord = new MFA { id_mfa = 1, id_usuario = userId, codigo = mfaSecret, usado = false };

            _mockContext.Setup(c => c.Usuarios.FindAsync(userId)).ReturnsAsync(user);

            var mfaRecords = new List<MFA> { mfaRecord }.AsQueryable();
            var mockMfaQueryable = new Mock<IQueryable<MFA>>();
            mockMfaQueryable.As<IQueryable<MFA>>().Setup(m => m.Provider).Returns(mfaRecords.Provider);
            mockMfaQueryable.As<IQueryable<MFA>>().Setup(m => m.Expression).Returns(mfaRecords.Expression);
            mockMfaQueryable.As<IQueryable<MFA>>().Setup(m => m.ElementType).Returns(mfaRecords.ElementType);
            mockMfaQueryable.As<IQueryable<MFA>>().Setup(m => m.GetEnumerator()).Returns(mfaRecords.GetEnumerator());

            _mockMFA.Setup(m => m.Where(It.IsAny<System.Linq.Expressions.Expression<Func<MFA, bool>>>()))
                .Returns(mockMfaQueryable.Object);

            _mockOtpService.Setup(o => o.VerifyOtp(mfaSecret, otpCode)).Returns(true);

            var backupCodes = new List<string> { "CODE1-CODE2", "CODE3-CODE4" };
            _mockOtpService.Setup(o => o.GenerateBackupCodes(It.IsAny<int>())).Returns(backupCodes);

            _mockContext.Setup(c => c.CodigosRespaldoMFA.Add(It.IsAny<CodigosRespaldoMFA>())).Verifiable();
            _mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            // Act
            var result = await _userService.EnableMfaAsync(userId, otpCode);

            // Assert
            Assert.True(result);
            Assert.True(user.mfa_habilitado);
            Assert.True(mfaRecord.esta_activo);
            Assert.True(mfaRecord.usado);

            _mockContext.Verify(c => c.CodigosRespaldoMFA.Add(It.IsAny<CodigosRespaldoMFA>()), Times.Exactly(backupCodes.Count));
            _mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }
        
        #endregion
        
        #region ResetPasswordAsync Tests
        
        [Fact]
        public async Task ResetPasswordAsync_ReturnsFalseWhenUserNotFound()
        {
            // Arrange
            const string email = "nonexistent@example.com";
            const string token = "resetToken";
            const string newPassword = "newPassword";
            
            _mockContext.Setup(c => c.Usuarios.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<Usuario, bool>>>(),
                It.IsAny<CancellationToken>()
            )).ReturnsAsync((Usuario)null);
            
            // Act
            var result = await _userService.ResetPasswordAsync(email, token, newPassword);
            
            // Assert
            Assert.False(result);
        }
        
        [Fact]
        public async Task ResetPasswordAsync_ReturnsFalseWhenResetTokenInvalid()
        {
            // Arrange
            const string email = "test@example.com";
            const string token = "invalidToken";
            const string newPassword = "newPassword";
            
            var user = new Usuario { id_usuario = 1, correo_electronico = email };
            
            _mockContext.Setup(c => c.Usuarios.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<Usuario, bool>>>(),
                It.IsAny<CancellationToken>()
            )).ReturnsAsync(user);
            
            _mockContext.Setup(c => c.RestablecimientoContrasena.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<RestablecimientoContrasena, bool>>>(),
                It.IsAny<CancellationToken>()
            )).ReturnsAsync((RestablecimientoContrasena)null);
            
            // Act
            var result = await _userService.ResetPasswordAsync(email, token, newPassword);
            
            // Assert
            Assert.False(result);
        }
        
        [Fact]
        public async Task ResetPasswordAsync_ChangesPasswordAndRemovesResetTokenWhenValid()
        {
            // Arrange
            const string email = "test@example.com";
            const string token = "validToken";
            const string newPassword = "newPassword";
            const string hashedPassword = "hashedNewPassword";
            
            var user = new Usuario { id_usuario = 1, correo_electronico = email };
            var resetRecord = new RestablecimientoContrasena 
            { 
                id_reset = 1, 
                id_usuario = 1, 
                token = token, 
                fecha_expiracion = DateTime.Now.AddHours(1) 
            };
            
            _mockContext.Setup(c => c.Usuarios.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<Usuario, bool>>>(),
                It.IsAny<CancellationToken>()
            )).ReturnsAsync(user);
            
            _mockContext.Setup(c => c.RestablecimientoContrasena.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<RestablecimientoContrasena, bool>>>(),
                It.IsAny<CancellationToken>()
            )).ReturnsAsync(resetRecord);
            
            _mockPasswordService.Setup(p => p.HashPassword(newPassword)).Returns(hashedPassword);
            
            _mockContext.Setup(c => c.RestablecimientoContrasena.Remove(resetRecord)).Verifiable();
            _mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
            
            // Act
            var result = await _userService.ResetPasswordAsync(email, token, newPassword);
            
            // Assert
            Assert.True(result);
            Assert.Equal(hashedPassword, user.contrasena);
            _mockContext.Verify(c => c.RestablecimientoContrasena.Remove(resetRecord), Times.Once);
            _mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }
        
        #endregion
        
        #region VerifyUserAsync Tests
        
        [Fact]
        public async Task VerifyUserAsync_ReturnsFalseWhenUserOrTokenInvalid()
        {
            // Arrange
            const string email = "test@example.com";
            const string token = "verificationToken";
            
            _mockContext.Setup(c => c.Usuarios.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<Usuario, bool>>>(),
                It.IsAny<CancellationToken>()
            )).ReturnsAsync((Usuario)null);
            
            // Act
            var result = await _userService.VerifyUserAsync(email, token);
            
            // Assert
            Assert.False(result);
        }
        
        [Fact]
        public async Task VerifyUserAsync_VerifiesUserWhenTokenIsValid()
        {
            // Arrange
            const string email = "test@example.com";
            const string token = "validToken";
            
            var user = new Usuario 
            { 
                id_usuario = 1, 
                correo_electronico = email,
                token_verificacion = token,
                fecha_expiracion_token = DateTime.Now.AddHours(1),
                estado_verificacion = "pendiente"
            };
            
            _mockContext.Setup(c => c.Usuarios.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<Usuario, bool>>>(),
                It.IsAny<CancellationToken>()
            )).ReturnsAsync(user);
            
            _mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
            
            // Act
            var result = await _userService.VerifyUserAsync(email, token);
            
            // Assert
            Assert.True(result);
            Assert.Equal("verificado", user.estado_verificacion);
            Assert.NotNull(user.fecha_verificacion);
            Assert.Null(user.token_verificacion);
            Assert.Null(user.fecha_expiracion_token);
            _mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }
        
        #endregion
        
        #region GetAllUsersAsync Tests
        
        [Fact]
        public async Task GetAllUsersAsync_ReturnsActiveUsers()
        {
            // Arrange
            var usuarios = new List<Usuario>
            {
                new Usuario { id_usuario = 1, nombre_usuario = "User 1", estado_verificacion = "verificado" },
                new Usuario { id_usuario = 2, nombre_usuario = "User 2", estado_verificacion = "verificado" }
            };
            
            _mockDatabaseRepository.Setup(r => r.ExecuteQueryProcedureAsync<Usuario>(
                "sp_ObtenerTodosUsuarios",
                It.IsAny<object>()
            )).ReturnsAsync(usuarios);
            
            // Act
            var result = await _userService.GetAllUsersAsync();
            
            // Assert
            Assert.Equal(2, result.Count);
            Assert.Equal("User 1", result[0].nombre_usuario);
            Assert.Equal("User 2", result[1].nombre_usuario);
        }
        
        [Fact]
        public async Task GetAllUsersAsync_ReturnsEmptyListOnException()
        {
            // Arrange
            _mockDatabaseRepository.Setup(r => r.ExecuteQueryProcedureAsync<Usuario>(
                "sp_ObtenerTodosUsuarios",
                It.IsAny<object>()
            )).ThrowsAsync(new Exception("Database error"));
            
            // Act
            var result = await _userService.GetAllUsersAsync();
            
            // Assert
            Assert.Empty(result);
        }
        
        #endregion
    }
}