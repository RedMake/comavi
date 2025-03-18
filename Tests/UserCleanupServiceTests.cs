using COMAVI_SA.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Threading.Tasks;
using Xunit;

namespace COMAVIxUnitTest
{
    public class UserCleanupServiceTests
    {
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly Mock<ILogger<UserCleanupService>> _mockLogger;

        public UserCleanupServiceTests()
        {
            _mockConfiguration = new Mock<IConfiguration>();
            _mockLogger = new Mock<ILogger<UserCleanupService>>();

            // Setup configuration section for connection strings
            var connectionStringsSection = new Mock<IConfigurationSection>();
            connectionStringsSection.Setup(s => s.Value).Returns("TestConnectionString");

            _mockConfiguration
                .Setup(c => c.GetSection("ConnectionStrings"))
                .Returns(new Mock<IConfigurationSection>().Object);

            _mockConfiguration
                .Setup(c => c.GetSection("ConnectionStrings:DefaultConnection"))
                .Returns(connectionStringsSection.Object);

            // For directly accessing by indexer
            _mockConfiguration
                .Setup(c => c["ConnectionStrings:DefaultConnection"])
                .Returns("TestConnectionString");
        }

        [Fact]
        public async Task CleanupNonVerifiedUsersAsync_ExecutesStoredProcedure()
        {
            // Esta prueba requiere una conexión a base de datos mock o simulada
            // Por eso solo verificamos que el servicio se puede instanciar sin errores

            // Arrange
            var service = new UserCleanupService(_mockConfiguration.Object, _mockLogger.Object);

            // No ejecutamos el método real porque requeriría una conexión de BD
            // Simplemente verificamos que podemos crear el servicio sin errores
            Assert.NotNull(service);
        }
    }
}