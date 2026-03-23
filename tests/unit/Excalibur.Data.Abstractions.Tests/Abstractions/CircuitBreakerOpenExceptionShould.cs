using Excalibur.Dispatch.Resilience;

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
        exception.Message.ShouldContain("circuit breaker");
        exception.InnerException.ShouldBeNull();
        exception.RetryAfter.ShouldBeNull();
    }

    [Fact]
    public void CreateWithCircuitName()
    {
        // Arrange
        const string circuitName = "my-circuit";

        // Act
        var exception = new CircuitBreakerOpenException(circuitName);

        // Assert
        exception.Message.ShouldContain(circuitName);
        exception.CircuitName.ShouldBe(circuitName);
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
        var exception = new CircuitBreakerOpenException("test-circuit", retryAfter);

        // Assert
        exception.RetryAfter.ShouldBe(retryAfter);
        exception.CircuitName.ShouldBe("test-circuit");
    }

    [Fact]
    public void SupportRetryAfterViaInitializer()
    {
        // Arrange
        var retryAfter = TimeSpan.FromSeconds(30);

        // Act -- init accessor allows setting RetryAfter via object initializer
        var exception = new CircuitBreakerOpenException("test-circuit") { RetryAfter = retryAfter };

        // Assert
        exception.RetryAfter.ShouldBe(retryAfter);
    }

    [Fact]
    public void BeAssignableToInvalidOperationException()
    {
        // Arrange & Act
        var exception = new CircuitBreakerOpenException();

        // Assert
        // Sprint 697 T.13: reparented to ApiException
        exception.ShouldBeAssignableTo<Excalibur.Dispatch.Abstractions.ApiException>();
    }
}
