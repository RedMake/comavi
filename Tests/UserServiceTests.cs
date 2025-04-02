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