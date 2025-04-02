using COMAVI_SA.Data;
using COMAVI_SA.Models;
using COMAVI_SA.Services;
using iText.Commons.Actions.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace COMAVIxUnitTest
{
    public class SessionCleanupServiceTests
    {
        private readonly ComaviDbContext _dbContext;
        private readonly SessionCleanupService _sessionCleanupService;

        public SessionCleanupServiceTests()
        {
            // Configurar DbContext en memoria
            var options = new DbContextOptionsBuilder<ComaviDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _dbContext = new ComaviDbContext(options);
            _sessionCleanupService = new SessionCleanupService(_dbContext);
        }

        [Fact]
        public async Task CleanupExpiredSessionsAsync_ShouldRemoveExpiredSessions()
        {
            // Arrange
            var now = DateTime.Now;
            var expiredTime = now.AddMinutes(-45); // Sesión expirada (más de 30 min)
            var activeTime = now.AddMinutes(-15);  // Sesión activa (menos de 30 min)

            // Crear sesiones de prueba
            var sessions = new List<SesionesActivas>
            {
                new SesionesActivas
                {
                    id_sesion = 1,
                    id_usuario = 1,
                    dispositivo = "Dispositivo 1",
                    ubicacion = "Ubicación 1",
                    fecha_inicio = expiredTime,
                    fecha_ultima_actividad = expiredTime
                },
                new SesionesActivas
                {
                    id_sesion = 2,
                    id_usuario = 2,
                    dispositivo = "Dispositivo 2",
                    ubicacion = "Ubicación 2",
                    fecha_inicio = activeTime,
                    fecha_ultima_actividad = activeTime
                }
            };

            _dbContext.SesionesActivas.AddRange(sessions);
            await _dbContext.SaveChangesAsync();

            // Act
            await _sessionCleanupService.CleanupExpiredSessionsAsync();

            // Assert
            var remainingSessions = await _dbContext.SesionesActivas.ToListAsync();
            Assert.Single(remainingSessions);
            Assert.Equal(2, remainingSessions[0].id_sesion);
            Assert.Equal("Dispositivo 2", remainingSessions[0].dispositivo);
        }

        [Fact]
        public async Task CleanupExpiredSessionsAsync_WithNoExpiredSessions_ShouldNotRemoveAnySessions()
        {
            // Arrange
            var now = DateTime.Now;
            var activeTime = now.AddMinutes(-15);  // Sesión activa (menos de 30 min)

            // Crear sesiones de prueba (ninguna expirada)
            var sessions = new List<SesionesActivas>
            {
                new SesionesActivas
                {
                    id_sesion = 1,
                    id_usuario = 1,
                    dispositivo = "Dispositivo 1",
                    ubicacion = "Ubicación 1",
                    fecha_inicio = activeTime,
                    fecha_ultima_actividad = activeTime
                },
                new SesionesActivas
                {
                    id_sesion = 2,
                    id_usuario = 2,
                    dispositivo = "Dispositivo 2",
                    ubicacion = "Ubicación 2",
                    fecha_inicio = activeTime,
                    fecha_ultima_actividad = activeTime
                }
            };

            _dbContext.SesionesActivas.AddRange(sessions);
            await _dbContext.SaveChangesAsync();

            // Act
            await _sessionCleanupService.CleanupExpiredSessionsAsync();

            // Assert
            var remainingSessions = await _dbContext.SesionesActivas.ToListAsync();
            Assert.Equal(2, remainingSessions.Count);
        }

        [Fact]
        public async Task CleanupExpiredSessionsAsync_WithExpiredSessions_RemovesSessions()
        {
            // Arrange
            var expiredSession = new SesionesActivas
            {
                id_sesion = 1,
                id_usuario = 1,
                dispositivo = "TestDevice",
                ubicacion = "TestLocation",
                fecha_inicio = DateTime.Now.AddMinutes(-60),
                fecha_ultima_actividad = DateTime.Now.AddMinutes(-60)
            };


            _dbContext.SesionesActivas.Add(expiredSession);
            await _dbContext.SaveChangesAsync();

            // Act
            await _sessionCleanupService.CleanupExpiredSessionsAsync();

            // Assert
            var remainingSessions = await _dbContext.SesionesActivas.ToListAsync();
            Assert.Empty(remainingSessions);
        }
    }
}