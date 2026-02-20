using Excalibur.Data.Abstractions.Resilience;

namespace Excalibur.Data.Tests.Abstractions;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CircuitBreakerOpenExceptionShould
{
    [Fact]
    public void CreateWithDefaultMessage()
    {
        // Arrange & Act
        var exception = new CircuitBreakerOpenException();

        // Assert
        exception.Message.ShouldContain("circuit breaker is open");
        exception.InnerException.ShouldBeNull();
        exception.RetryAfter.ShouldBeNull();
    }

    [Fact]
    public void CreateWithCustomMessage()
    {
        // Arrange
        const string message = "Custom circuit breaker message";

        // Act
        var exception = new CircuitBreakerOpenException(message);

        // Assert
        exception.Message.ShouldBe(message);
        exception.InnerException.ShouldBeNull();
    }

    [Fact]
    public void CreateWithMessageAndInnerException()
    {
        // Arrange
        const string message = "Outer error";
        var inner = new InvalidOperationException("Inner error");

        // Act
        var exception = new CircuitBreakerOpenException(message, inner);

        // Assert
        exception.Message.ShouldBe(message);
        exception.InnerException.ShouldBeSameAs(inner);
    }

    [Fact]
    public void SupportRetryAfterProperty()
    {
        // Arrange
        var retryAfter = TimeSpan.FromSeconds(30);

        // Act
        var exception = new CircuitBreakerOpenException("Open") { RetryAfter = retryAfter };

        // Assert
        exception.RetryAfter.ShouldBe(retryAfter);
    }

    [Fact]
    public void BeAssignableToInvalidOperationException()
    {
        // Arrange & Act
        var exception = new CircuitBreakerOpenException();

        // Assert
        exception.ShouldBeAssignableTo<InvalidOperationException>();
    }
}
