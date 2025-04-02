using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using COMAVI_SA.Controllers;
using COMAVI_SA.Data;
using COMAVI_SA.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace COMAVI_SA.Tests.Controllers
{
    public class AgendaControllerTests
    {
        private readonly Mock<ComaviDbContext> _mockContext;
        private readonly AgendaController _controller;
        private readonly Mock<DbSet<EventoAgenda>> _mockEventos;
        private readonly Mock<DbSet<Choferes>> _mockChoferes;
        
        public AgendaControllerTests()
        {
            // Setup mock context and DbSets
            _mockContext = new Mock<ComaviDbContext>(new DbContextOptions<ComaviDbContext>());
            _mockEventos = new Mock<DbSet<EventoAgenda>>();
            _mockChoferes = new Mock<DbSet<Choferes>>();
            
            // Setup controller with authenticated user
            _controller = new AgendaController(_mockContext.Object);
            SetupAuthenticatedUser(_controller);
            SetupTempData(_controller);
        }
        
        #region Index Tests
        
        [Fact]
        public async Task Index_ReturnsForbiddenWhenUserNotAuthenticated()
        {
            // Arrange
            var controller = new AgendaController(_mockContext.Object);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() }
            };
            SetupTempData(controller);
            
            // Act
            var result = await controller.Index();
            
            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirectResult.ActionName);
            Assert.Equal("Home", redirectResult.ControllerName);

        }

        
        [Fact]
        public async Task Index_HandlesExceptionCorrectly()
        {
            // Arrange
            _mockContext.Setup(c => c.EventosAgenda).Throws(new Exception("Database error"));
            SetupTempData(_controller);
            
            // Act
            var result = await _controller.Index();
            
            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirectResult.ActionName);
            Assert.Equal("Home", redirectResult.ControllerName);
            Assert.Equal("Ocurrió un error al cargar los eventos. Por favor, inténtelo de nuevo.", _controller.TempData["ErrorMessage"]);
        }
        
        #endregion
        
        #region Create Tests
        
        [Fact]
        public void Create_ReturnsViewWithChoferes()
        {
            // Arrange
            const int userId = 1;
            var choferes = new List<Choferes>
            {
                new Choferes { id_chofer = 1, nombreCompleto = "Test Driver 1", id_usuario = userId },
                new Choferes { id_chofer = 2, nombreCompleto = "Test Driver 2", id_usuario = null }
            }.AsQueryable();
            
            _mockChoferes.As<IQueryable<Choferes>>().Setup(m => m.Provider).Returns(choferes.Provider);
            _mockChoferes.As<IQueryable<Choferes>>().Setup(m => m.Expression).Returns(choferes.Expression);
            _mockChoferes.As<IQueryable<Choferes>>().Setup(m => m.ElementType).Returns(choferes.ElementType);
            _mockChoferes.As<IQueryable<Choferes>>().Setup(m => m.GetEnumerator()).Returns(choferes.GetEnumerator());
            
            _mockContext.Setup(c => c.Choferes).Returns(_mockChoferes.Object);
            SetupUserIdClaim(_controller, userId);
            
            // Act
            var result = _controller.Create();
            
            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.NotNull(viewResult.ViewData["Choferes"]);
            var modelo = Assert.IsType<EventoAgenda>(viewResult.Model);
            Assert.Equal("Pendiente", modelo.estado);
        }
        
       
        #endregion
        
        #region Edit Tests
        
        [Fact]
        public async Task Edit_ReturnsNotFoundWhenIdIsNull()
        {
            // Act
            var result = await _controller.Edit(null);
            
            // Assert
            Assert.IsType<NotFoundResult>(result);
        }
        
        
        
        [Fact]
        public async Task EditPost_ReturnsNotFoundWhenIdDoesNotMatchEvento()
        {
            // Arrange
            const int userId = 1;
            const int eventoId = 1;
            var eventoAgenda = new EventoAgenda { id_evento = 2, id_usuario = userId };
            
            // Act
            var result = await _controller.Edit(eventoId, eventoAgenda);
            
            // Assert
            Assert.IsType<NotFoundResult>(result);
        }
        
       
        
        #endregion
        
        #region Delete Tests
        
        [Fact]
        public async Task Delete_ReturnsNotFoundWhenIdIsNull()
        {
            // Act
            var result = await _controller.Delete(null);
            
            // Assert
            Assert.IsType<NotFoundResult>(result);
        }   
        
        #endregion
        
        #region Helper Methods
        
        private void SetupAuthenticatedUser(AgendaController controller)
        {
            var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
            {
                new Claim(ClaimTypes.NameIdentifier, "1"),
                new Claim(ClaimTypes.Name, "testuser@example.com"),
                new Claim(ClaimTypes.Role, "user")
            }, "mock"));
            
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };
        }
        
        private void SetupUserIdClaim(AgendaController controller, int userId)
        {
            var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Name, "testuser@example.com"),
                new Claim(ClaimTypes.Role, "user")
            }, "mock"));
            
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };
        }
        
        private void SetupTempData(AgendaController controller)
        {
            controller.TempData = new TempDataDictionary(
                new DefaultHttpContext(),
                Mock.Of<ITempDataProvider>());
        }
        
        #endregion
    }
}