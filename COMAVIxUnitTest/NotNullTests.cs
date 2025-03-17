using COMAVI_SA.Utils;
using System;
using Xunit;

namespace COMAVIxUnitTest
{
    public class NotNullTests
    {
        [Fact]
        public void Check_WithNonNullValue_ReturnsValue()
        {
            // Arrange
            string testValue = "Test";

            // Act
            var result = NotNull.Check(testValue);

            // Assert
            Assert.Equal(testValue, result);
        }

        [Fact]
        public void Check_WithNullValue_ThrowsArgumentNullException()
        {
            // Arrange
            string testValue = null;
            string paramName = "testValue"; // El nombre real del parámetro según CallerArgumentExpression

            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() =>
                NotNull.Check(testValue));
            Assert.Equal(paramName, exception.ParamName);
        }

        [Fact]
        public void Check_WithNullValueAndCustomMessage_ThrowsExceptionWithMessage()
        {
            // Arrange
            string testValue = null;
            string customMessage = "Custom error message";

            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() =>
                NotNull.Check(testValue, message: customMessage));
            Assert.Contains(customMessage, exception.Message);
        }

        [Fact]
        public void CheckNotNullOrEmpty_WithValidString_ReturnsString()
        {
            // Arrange
            string testValue = "Test";

            // Act
            var result = NotNull.CheckNotNullOrEmpty(testValue);

            // Assert
            Assert.Equal(testValue, result);
        }

        [Fact]
        public void CheckNotNullOrEmpty_WithNullString_ThrowsArgumentNullException()
        {
            // Arrange
            string testValue = null;
            string paramName = "testValue"; // El nombre real del parámetro según CallerArgumentExpression

            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() =>
                NotNull.CheckNotNullOrEmpty(testValue));
            Assert.Equal(paramName, exception.ParamName);
        }

        [Fact]
        public void CheckNotNullOrEmpty_WithEmptyString_ThrowsArgumentException()
        {
            // Arrange
            string testValue = "";
            string paramName = "testValue"; // El nombre real del parámetro según CallerArgumentExpression

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() =>
                NotNull.CheckNotNullOrEmpty(testValue));
            Assert.Equal(paramName, exception.ParamName);
        }

        [Fact]
        public void CheckNotNullOrEmpty_WithWhitespaceString_ThrowsArgumentException()
        {
            // Arrange
            string testValue = "   ";
            string paramName = "testValue"; // El nombre real del parámetro según CallerArgumentExpression

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() =>
                NotNull.CheckNotNullOrEmpty(testValue));
            Assert.Equal(paramName, exception.ParamName);
        }
    }
}