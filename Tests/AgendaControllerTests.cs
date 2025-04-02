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
        public async Task Index_ReturnsEventsForAuthenticatedUser()
        {
            // Arrange
            const int userId = 1;
            var eventos = new List<EventoAgenda>
            {
                new EventoAgenda { id_evento = 1, titulo = "Test Event 1", id_usuario = userId },
                new EventoAgenda { id_evento = 2, titulo = "Test Event 2", id_usuario = userId }
            }.AsQueryable();
            
            _mockEventos.As<IQueryable<EventoAgenda>>().Setup(m => m.Provider).Returns(eventos.Provider);
            _mockEventos.As<IQueryable<EventoAgenda>>().Setup(m => m.Expression).Returns(eventos.Expression);
            _mockEventos.As<IQueryable<EventoAgenda>>().Setup(m => m.ElementType).Returns(eventos.ElementType);
            _mockEventos.As<IQueryable<EventoAgenda>>().Setup(m => m.GetEnumerator()).Returns(eventos.GetEnumerator());
            
            _mockContext.Setup(c => c.EventosAgenda).Returns(_mockEventos.Object);
            SetupUserIdClaim(_controller, userId);
            
            // Act
            var result = await _controller.Index();
            
            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<EventoAgenda>>(viewResult.Model);
            Assert.Equal(2, model.Count());
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
        
        #region Calendar Tests
        
        [Fact]
        public async Task Calendar_ReturnsValidViewModel()
        {
            // Arrange
            const int userId = 1;
            var eventos = new List<EventoAgenda>
            {
                new EventoAgenda 
                { 
                    id_evento = 1, 
                    titulo = "Test Event 1", 
                    id_usuario = userId,
                    fecha_inicio = DateTime.Now,
                    fecha_fin = DateTime.Now.AddHours(2),
                    tipo_evento = "Renovación",
                    estado = "Pendiente",
                    descripcion = "Test Description"
                }
            }.AsQueryable();
            
            _mockEventos.As<IQueryable<EventoAgenda>>().Setup(m => m.Provider).Returns(eventos.Provider);
            _mockEventos.As<IQueryable<EventoAgenda>>().Setup(m => m.Expression).Returns(eventos.Expression);
            _mockEventos.As<IQueryable<EventoAgenda>>().Setup(m => m.ElementType).Returns(eventos.ElementType);
            _mockEventos.As<IQueryable<EventoAgenda>>().Setup(m => m.GetEnumerator()).Returns(eventos.GetEnumerator());
            
            _mockContext.Setup(c => c.EventosAgenda).Returns(_mockEventos.Object);
            SetupUserIdClaim(_controller, userId);
            
            // Act
            var result = await _controller.Calendar();
            
            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.NotNull(viewResult.ViewData["Eventos"] ?? viewResult.ViewData["Events"]);
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
        
        [Fact]
        public async Task CreatePost_SavesValidEvent()
        {
            // Arrange
            const int userId = 1;
            var eventoAgenda = new EventoAgenda
            {
                titulo = "Test Event",
                descripcion = "Test Description",
                fecha_inicio = DateTime.Now.AddDays(1),
                fecha_fin = DateTime.Now.AddDays(1).AddHours(2),
                tipo_evento = "Reunión",
                estado = "Pendiente",
                id_chofer = 1
            };
            
            _mockContext.Setup(c => c.Add(It.IsAny<EventoAgenda>())).Verifiable();
            _mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
            
            SetupUserIdClaim(_controller, userId);
            _controller.ModelState.Clear(); // Clear ModelState to ensure it's valid
            
            // Act
            var result = await _controller.Create(eventoAgenda);
            
            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirectResult.ActionName);
            _mockContext.Verify(c => c.Add(It.IsAny<EventoAgenda>()), Times.Once);
            _mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }
        
        [Fact]
        public async Task CreatePost_ReturnsViewWhenModelStateInvalid()
        {
            // Arrange
            var eventoAgenda = new EventoAgenda
            {
                // Missing required fields
            };
            
            var choferes = new List<Choferes>
            {
                new Choferes { id_chofer = 1, nombreCompleto = "Test Driver 1" }
            }.AsQueryable();
            
            _mockChoferes.As<IQueryable<Choferes>>().Setup(m => m.Provider).Returns(choferes.Provider);
            _mockChoferes.As<IQueryable<Choferes>>().Setup(m => m.Expression).Returns(choferes.Expression);
            _mockChoferes.As<IQueryable<Choferes>>().Setup(m => m.ElementType).Returns(choferes.ElementType);
            _mockChoferes.As<IQueryable<Choferes>>().Setup(m => m.GetEnumerator()).Returns(choferes.GetEnumerator());
            
            _mockContext.Setup(c => c.Choferes).Returns(_mockChoferes.Object);
            
            _controller.ModelState.AddModelError("titulo", "El título es requerido");
            
            // Act
            var result = await _controller.Create(eventoAgenda);
            
            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal(eventoAgenda, viewResult.Model);
            Assert.NotNull(viewResult.ViewData["Choferes"]);
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
        public async Task Edit_ReturnsNotFoundWhenEventoNotExists()
        {
            // Arrange
            const int userId = 1;
            const int eventoId = 99;
            var eventos = new List<EventoAgenda>().AsQueryable();
            
            _mockEventos.As<IQueryable<EventoAgenda>>().Setup(m => m.Provider).Returns(eventos.Provider);
            _mockEventos.As<IQueryable<EventoAgenda>>().Setup(m => m.Expression).Returns(eventos.Expression);
            _mockEventos.As<IQueryable<EventoAgenda>>().Setup(m => m.ElementType).Returns(eventos.ElementType);
            _mockEventos.As<IQueryable<EventoAgenda>>().Setup(m => m.GetEnumerator()).Returns(eventos.GetEnumerator());
            
            _mockContext.Setup(c => c.EventosAgenda).Returns(_mockEventos.Object);
            SetupUserIdClaim(_controller, userId);
            
            // Act
            var result = await _controller.Edit(eventoId);
            
            // Assert
            Assert.IsType<NotFoundResult>(result);
        }
        
        [Fact]
        public async Task Edit_ReturnsViewWithEventoWhenExists()
        {
            // Arrange
            const int userId = 1;
            const int eventoId = 1;
            var eventos = new List<EventoAgenda>
            {
                new EventoAgenda { id_evento = eventoId, titulo = "Test Event", id_usuario = userId }
            }.AsQueryable();
            
            var choferes = new List<Choferes>
            {
                new Choferes { id_chofer = 1, nombreCompleto = "Test Driver 1", id_usuario = userId }
            }.AsQueryable();
            
            _mockEventos.As<IQueryable<EventoAgenda>>().Setup(m => m.Provider).Returns(eventos.Provider);
            _mockEventos.As<IQueryable<EventoAgenda>>().Setup(m => m.Expression).Returns(eventos.Expression);
            _mockEventos.As<IQueryable<EventoAgenda>>().Setup(m => m.ElementType).Returns(eventos.ElementType);
            _mockEventos.As<IQueryable<EventoAgenda>>().Setup(m => m.GetEnumerator()).Returns(eventos.GetEnumerator());
            
            _mockChoferes.As<IQueryable<Choferes>>().Setup(m => m.Provider).Returns(choferes.Provider);
            _mockChoferes.As<IQueryable<Choferes>>().Setup(m => m.Expression).Returns(choferes.Expression);
            _mockChoferes.As<IQueryable<Choferes>>().Setup(m => m.ElementType).Returns(choferes.ElementType);
            _mockChoferes.As<IQueryable<Choferes>>().Setup(m => m.GetEnumerator()).Returns(choferes.GetEnumerator());
            
            _mockContext.Setup(c => c.EventosAgenda).Returns(_mockEventos.Object);
            _mockContext.Setup(c => c.Choferes).Returns(_mockChoferes.Object);
            SetupUserIdClaim(_controller, userId);
            
            // Act
            var result = await _controller.Edit(eventoId);
            
            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<EventoAgenda>(viewResult.Model);
            Assert.Equal(eventoId, model.id_evento);
            Assert.NotNull(viewResult.ViewData["Choferes"]);
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
        
        [Fact]
        public async Task EditPost_UpdatesValidEvent()
        {
            // Arrange
            const int userId = 1;
            const int eventoId = 1;
            var eventoAgenda = new EventoAgenda
            {
                id_evento = eventoId,
                id_usuario = userId,
                titulo = "Updated Event",
                descripcion = "Updated Description",
                fecha_inicio = DateTime.Now.AddDays(2),
                fecha_fin = DateTime.Now.AddDays(2).AddHours(2),
                tipo_evento = "Renovación",
                estado = "Pendiente"
            };
            
            var eventoOriginal = new EventoAgenda
            {
                id_evento = eventoId,
                id_usuario = userId,
                titulo = "Original Event",
                descripcion = "Original Description",
                fecha_inicio = DateTime.Now.AddDays(1),
                fecha_fin = DateTime.Now.AddDays(1).AddHours(2),
                tipo_evento = "Reunión",
                estado = "Pendiente"
            };
            
            var eventosAsNoTracking = new List<EventoAgenda> { eventoOriginal }.AsQueryable();
            
            var mockEventosAsNoTracking = new Mock<DbSet<EventoAgenda>>();
            mockEventosAsNoTracking.As<IQueryable<EventoAgenda>>().Setup(m => m.Provider).Returns(eventosAsNoTracking.Provider);
            mockEventosAsNoTracking.As<IQueryable<EventoAgenda>>().Setup(m => m.Expression).Returns(eventosAsNoTracking.Expression);
            mockEventosAsNoTracking.As<IQueryable<EventoAgenda>>().Setup(m => m.ElementType).Returns(eventosAsNoTracking.ElementType);
            mockEventosAsNoTracking.As<IQueryable<EventoAgenda>>().Setup(m => m.GetEnumerator()).Returns(eventosAsNoTracking.GetEnumerator());
            
            _mockContext.Setup(c => c.EventosAgenda.AsNoTracking()).Returns(mockEventosAsNoTracking.Object);
            _mockContext.Setup(c => c.Update(It.IsAny<EventoAgenda>())).Verifiable();
            _mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
            
            SetupUserIdClaim(_controller, userId);
            _controller.ModelState.Clear(); // Clear ModelState to ensure it's valid
            
            // Act
            var result = await _controller.Edit(eventoId, eventoAgenda);
            
            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirectResult.ActionName);
            _mockContext.Verify(c => c.Update(It.IsAny<EventoAgenda>()), Times.Once);
            _mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
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
        
        [Fact]
        public async Task Delete_ReturnsNotFoundWhenEventoNotExists()
        {
            // Arrange
            const int userId = 1;
            const int eventoId = 99;
            var eventos = new List<EventoAgenda>().AsQueryable();
            
            _mockEventos.As<IQueryable<EventoAgenda>>().Setup(m => m.Provider).Returns(eventos.Provider);
            _mockEventos.As<IQueryable<EventoAgenda>>().Setup(m => m.Expression).Returns(eventos.Expression);
            _mockEventos.As<IQueryable<EventoAgenda>>().Setup(m => m.ElementType).Returns(eventos.ElementType);
            _mockEventos.As<IQueryable<EventoAgenda>>().Setup(m => m.GetEnumerator()).Returns(eventos.GetEnumerator());
            
            _mockContext.Setup(c => c.EventosAgenda).Returns(_mockEventos.Object);
            SetupUserIdClaim(_controller, userId);
            
            // Act
            var result = await _controller.Delete(eventoId);
            
            // Assert
            Assert.IsType<NotFoundResult>(result);
        }
        
        [Fact]
        public async Task DeleteConfirmed_DeletesEventoAndRedirectsToIndex()
        {
            // Arrange
            const int userId = 1;
            const int eventoId = 1;
            var evento = new EventoAgenda { id_evento = eventoId, id_usuario = userId };
            var eventos = new List<EventoAgenda> { evento }.AsQueryable();
            
            _mockEventos.As<IQueryable<EventoAgenda>>().Setup(m => m.Provider).Returns(eventos.Provider);
            _mockEventos.As<IQueryable<EventoAgenda>>().Setup(m => m.Expression).Returns(eventos.Expression);
            _mockEventos.As<IQueryable<EventoAgenda>>().Setup(m => m.ElementType).Returns(eventos.ElementType);
            _mockEventos.As<IQueryable<EventoAgenda>>().Setup(m => m.GetEnumerator()).Returns(eventos.GetEnumerator());
            
            _mockContext.Setup(c => c.EventosAgenda).Returns(_mockEventos.Object);
            _mockContext.Setup(c => c.EventosAgenda.Remove(It.IsAny<EventoAgenda>())).Verifiable();
            _mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
            
            SetupUserIdClaim(_controller, userId);
            
            // Act
            var result = await _controller.DeleteConfirmed(eventoId);
            
            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirectResult.ActionName);
            _mockContext.Verify(c => c.EventosAgenda.Remove(It.IsAny<EventoAgenda>()), Times.Once);
            _mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
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