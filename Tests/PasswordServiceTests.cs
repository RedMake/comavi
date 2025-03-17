using COMAVI_SA.Services;

namespace COMAVIxUnitTest
{
    public class PasswordServiceTests
    {
        private readonly PasswordService _passwordService;

        public PasswordServiceTests()
        {
            _passwordService = new PasswordService();
        }

        [Fact]
        public void HashPassword_ReturnsHashedPassword_NotEqualToOriginal()
        {
            // Arrange
            var password = "TestPassword123!";

            // Act
            var hashedPassword = _passwordService.HashPassword(password);

            // Assert
            Assert.NotEqual(password, hashedPassword);
            Assert.NotEmpty(hashedPassword);
        }

        [Fact]
        public void VerifyPassword_WithCorrectPassword_ReturnsTrue()
        {
            // Arrange
            var password = "TestPassword123!";
            var hashedPassword = _passwordService.HashPassword(password);

            // Act
            var result = _passwordService.VerifyPassword(password, hashedPassword);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void VerifyPassword_WithIncorrectPassword_ReturnsFalse()
        {
            // Arrange
            var password = "TestPassword123!";
            var incorrectPassword = "WrongPassword123!";
            var hashedPassword = _passwordService.HashPassword(password);

            // Act
            var result = _passwordService.VerifyPassword(incorrectPassword, hashedPassword);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void HashPassword_WithDifferentInputs_ProducesDifferentHashes()
        {
            // Arrange
            var password1 = "TestPassword123!";
            var password2 = "TestPassword123!";

            // Act
            var hash1 = _passwordService.HashPassword(password1);
            var hash2 = _passwordService.HashPassword(password2);

            // Assert
            Assert.NotEqual(hash1, hash2); // BCrypt genera diferentes hashes incluso para la misma entrada
        }
    }
}