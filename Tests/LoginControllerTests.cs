using COMAVI_SA.Controllers;
using COMAVI_SA.Data;
using COMAVI_SA.Models;
using COMAVI_SA.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Xunit;

namespace COMAVIxUnitTest
{
    public class LoginControllerTests
    {
        private readonly Mock<IUserService> _mockUserService;
        private readonly Mock<IPasswordService> _mockPasswordService;
        private readonly Mock<IOtpService> _mockOtpService;
        private readonly Mock<IJwtService> _mockJwtService;
        private readonly Mock<IEmailService> _mockEmailService;
        private readonly Mock<IPdfService> _mockPdfService;
        private readonly ComaviDbContext _context;
        private readonly Mock<ILogger<LoginController>> _mockLogger;
        private readonly LoginController _controller;
        private readonly ITempDataDictionary _tempData;

        public LoginControllerTests()
        {
            _mockUserService = new Mock<IUserService>();
            _mockPasswordService = new Mock<IPasswordService>();
            _mockOtpService = new Mock<IOtpService>();
            _mockJwtService = new Mock<IJwtService>();
            _mockEmailService = new Mock<IEmailService>();
            _mockPdfService = new Mock<IPdfService>(); 
            _mockLogger = new Mock<ILogger<LoginController>>();

            // Configurar DbContext en memoria
            var options = new DbContextOptionsBuilder<ComaviDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new ComaviDbContext(options);

            // Configurar controlador con HttpContext para TempData y Session
            _controller = new LoginController(
                _mockUserService.Object,
                _mockPasswordService.Object,
                _mockOtpService.Object,
                _mockJwtService.Object,
                _mockEmailService.Object,
                _mockPdfService.Object, 
                _context,
                _mockLogger.Object
            );

            // Configurar HttpContext mock correctamente
            var httpContext = new DefaultHttpContext();
            var tempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
            _controller.TempData = tempData;
            _tempData = tempData;

            // Configurar Session mock adecuadamente
            var mockSession = new Mock<ISession>();
            mockSession.Setup(s => s.Set(It.IsAny<string>(), It.IsAny<byte[]>())).Verifiable();
            httpContext.Session = mockSession.Object;

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            // Asegurarnos de que el controlador tiene una URL inicial configurada
            _controller.Url = new UrlHelper(new ActionContext
            {
                HttpContext = httpContext,
                RouteData = new RouteData(),
                ActionDescriptor = new ActionDescriptor()
            });
        }

        [Fact]
        public void Index_Get_ReturnsViewResult()
        {
            // Act
            var result = _controller.Index();

            // Assert
            Assert.IsType<ViewResult>(result);
        }

        [Fact]
        public async Task Index_Post_WithValidCredentials_RedirectsToVerifyOtp()
        {
            // Arrange
            var model = new LoginViewModel
            {
                Email = "test@example.com",
                Password = "Password123!",
                RememberMe = false
            };

            var user = new Usuario
            {
                id_usuario = 1,
                nombre_usuario = "TestUser",
                correo_electronico = "test@example.com",
                rol = "admin",
                contrasena = "hashedpassword123",
                estado_verificacion = "verificado" 
            };

            // Configurar explícitamente que IsAccountLockedAsync devuelva false
            _mockUserService
                .Setup(s => s.IsAccountLockedAsync(model.Email))
                .ReturnsAsync(false);

            // Configurar explícitamente que AuthenticateAsync devuelva el usuario correctamente
            _mockUserService
                .Setup(s => s.AuthenticateAsync(model.Email, model.Password))
                .ReturnsAsync(user);

            // Configurar RecordLoginAttemptAsync para que no haga nada (void)
            _mockUserService
                .Setup(s => s.RecordLoginAttemptAsync(user.id_usuario, It.IsAny<string>(), true))
                .Returns(Task.CompletedTask);

            // Configurar JWT Token
            _mockJwtService
                .Setup(s => s.GenerateJwtToken(user))
                .Returns("test-jwt-token");

            // Act
            var result = await _controller.Index(model);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("VerifyOtp", redirectResult.ActionName);
            Assert.Equal(user.id_usuario, _tempData["UserId"]);
            Assert.Equal(user.correo_electronico, _tempData["UserEmail"]);
            Assert.Equal(model.RememberMe, _tempData["RememberMe"]);
        }

        [Fact]
        public async Task Index_Post_WithInvalidCredentials_ReturnsViewWithError()
        {
            // Arrange
            var model = new LoginViewModel
            {
                Email = "test@example.com",
                Password = "WrongPassword",
                RememberMe = false
            };

            _mockUserService
                .Setup(s => s.IsAccountLockedAsync(model.Email))
                .ReturnsAsync(false);

            _mockUserService
                .Setup(s => s.AuthenticateAsync(model.Email, model.Password))
                .ReturnsAsync((Usuario)null);

            // Act
            var result = await _controller.Index(model);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Same(model, viewResult.Model);
            Assert.False(_controller.ModelState.IsValid);
            _mockUserService.Verify(s => s.RecordLoginAttemptAsync(null, It.IsAny<string>(), false), Times.Once);
        }

        [Fact]
        public async Task Index_Post_WithLockedAccount_ReturnsViewWithError()
        {
            // Arrange
            var model = new LoginViewModel
            {
                Email = "test@example.com",
                Password = "Password123!",
                RememberMe = false
            };

            _mockUserService
                .Setup(s => s.IsAccountLockedAsync(model.Email))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.Index(model);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Same(model, viewResult.Model);
            Assert.False(_controller.ModelState.IsValid);
        }

        [Fact]
        public async Task VerifyOtp_Get_ReturnsViewResult()
        {
            // Arrange
            int userId = 1;
            string email = "test@example.com";
            _tempData["UserId"] = userId;
            _tempData["UserEmail"] = email;

            _mockUserService
                .Setup(s => s.GetMfaSecretAsync(userId))
                .ReturnsAsync("testsecret");

            // Act
            var result = await _controller.VerifyOtp();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<OtpViewModel>(viewResult.Model);
            Assert.Equal(email, model.Email);
        }

        [Fact]
        public async Task VerifyOtp_Post_WithValidCode_RedirectsToHome()
        {
            // Arrange
            int userId = 1;
            string email = "test@example.com";
            _tempData["UserId"] = userId;
            _tempData["UserEmail"] = email;
            _tempData["RememberMe"] = false;

            var model = new OtpViewModel
            {
                Email = email,
                OtpCode = "123456"
            };

            var user = new Usuario
            {
                id_usuario = userId,
                nombre_usuario = "TestUser",
                correo_electronico = email,
                rol = "admin",
                ultimo_ingreso = null,
                contrasena = "hashedpassword123" // Añadido el campo requerido
            };

            // Configurar explícitamente todas las operaciones necesarias
            _mockUserService
                .Setup(s => s.VerifyMfaCodeAsync(userId, model.OtpCode))
                .ReturnsAsync(true);

            _mockUserService
                .Setup(s => s.GetUserByIdAsync(userId))
                .ReturnsAsync(user);

            // Agregar el usuario al DbContext en memoria para que pueda ser actualizado
            _context.Usuarios.Add(user);
            await _context.SaveChangesAsync();

            // Setup para HttpContext.SignInAsync
            var authServiceMock = new Mock<IAuthenticationService>();
            authServiceMock
                .Setup(a => a.SignInAsync(
                    It.IsAny<HttpContext>(),
                    It.IsAny<string>(),
                    It.IsAny<ClaimsPrincipal>(),
                    It.IsAny<AuthenticationProperties>()))
                .Returns(Task.CompletedTask);

            var serviceProviderMock = new Mock<IServiceProvider>();
            serviceProviderMock
                .Setup(s => s.GetService(typeof(IAuthenticationService)))
                .Returns(authServiceMock.Object);

            _controller.HttpContext.RequestServices = serviceProviderMock.Object;

            // Act
            var result = await _controller.VerifyOtp(model);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirectResult.ActionName);
            Assert.Equal("Home", redirectResult.ControllerName);

            // Verificar que se agregó una sesión activa a la base de datos
            Assert.True(await _context.SesionesActivas.AnyAsync(s => s.id_usuario == userId));
        }

        [Fact]
        public async Task VerifyOtp_Post_WithInvalidCode_ReturnsViewWithError()
        {
            // Arrange
            int userId = 1;
            string email = "test@example.com";
            _tempData["UserId"] = userId;
            _tempData["UserEmail"] = email;

            var model = new OtpViewModel
            {
                Email = email,
                OtpCode = "111111" // Código inválido
            };

            _mockUserService
                .Setup(s => s.VerifyMfaCodeAsync(userId, model.OtpCode))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.VerifyOtp(model);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Same(model, viewResult.Model);
            Assert.False(_controller.ModelState.IsValid);
        }

        [Fact]
        public async Task Index_Post_WithUnverifiedAccount_ReturnsViewWithError()
        {
            // Arrange
            var model = new LoginViewModel
            {
                Email = "unverified@example.com",
                Password = "Password123!"
            };

            var unverifiedUser = new Usuario
            {
                id_usuario = 3,
                nombre_usuario = "Unverified User",
                correo_electronico = "unverified@example.com",
                contrasena = "hashedpassword",
                rol = "user",
                estado_verificacion = "pendiente" // Usuario no verificado
            };

            _mockUserService.Setup(s => s.IsAccountLockedAsync(model.Email)).ReturnsAsync(false);
            _mockUserService.Setup(s => s.AuthenticateAsync(model.Email, model.Password)).ReturnsAsync(unverifiedUser);

            // Act
            var result = await _controller.Index(model);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Same(model, viewResult.Model);
            Assert.False(_controller.ModelState.IsValid);
            Assert.Contains("no ha sido verificada", _controller.ModelState.Values.First().Errors[0].ErrorMessage);
        }

        [Fact]
        public async Task Verificar_Get_ReturnsView()
        {
            // Arrange
            string token = "test_token";
            string email = "verify@example.com";

            // Act
            var result = _controller.Verificar(token, email);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<VerificacionViewModel>(viewResult.Model);
            Assert.Equal(token, model.Token);
            Assert.Equal(email, model.Email);
        }

        [Fact]
        public async Task Verificar_Post_WithValidTokenAndEmail_Succeeds()
        {
            // Arrange
            var model = new VerificacionViewModel
            {
                Token = "valid_token",
                Email = "verify@example.com"
            };

            // Ensure ModelState is valid
            _controller.ModelState.Clear();

            // Set up the context DB with a test user
            var user = new Usuario
            {
                id_usuario = 3,
                nombre_usuario = "Verification Test User",
                correo_electronico = "verify@example.com",
                contrasena = "hashedpassword",
                rol = "user",
                estado_verificacion = "pendiente",
                token_verificacion = "valid_token",
                fecha_expiracion_token = DateTime.Now.AddDays(1)
            };

            // Add the user to the test database context
            _context.Usuarios.Add(user);
            await _context.SaveChangesAsync();

            // Setup the mock - note we're bypassing VerifyUserAsync since 
            // the controller is likely using EF Core directly
            _mockUserService
                .Setup(s => s.VerifyUserAsync(model.Email, model.Token))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.Verificar(model);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirectResult.ActionName);

            // Verify TempData contains success message
            Assert.Contains("SuccessMessage", _tempData.Keys);

            // Verify the user status was updated
            var updatedUser = await _context.Usuarios.FindAsync(user.id_usuario);
            Assert.Equal("verificado", updatedUser.estado_verificacion);
        }
    }
}