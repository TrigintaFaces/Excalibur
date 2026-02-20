using Excalibur.Data.SqlServer;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.SqlServer;

[Trait("Category", "Unit")]
[Trait("Component", "Data.SqlServer")]
public sealed class SqlDataAccessPolicyFactoryShould
{
    [Fact]
    public void ThrowOnNullLogger()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new SqlDataAccessPolicyFactory(null!));
    }

    [Fact]
    public void CreateSuccessfullyWithValidLogger()
    {
        // Arrange & Act
        var factory = new SqlDataAccessPolicyFactory(
            NullLogger<SqlDataAccessPolicyFactory>.Instance);

        // Assert
        factory.ShouldNotBeNull();
    }

    [Fact]
    public void ImplementIDataAccessPolicyFactory()
    {
        // Arrange & Act
        var factory = new SqlDataAccessPolicyFactory(
            NullLogger<SqlDataAccessPolicyFactory>.Instance);

        // Assert
        factory.ShouldBeAssignableTo<IDataAccessPolicyFactory>();
    }

    [Fact]
    public void ReturnNonNullRetryPolicy()
    {
        // Arrange
        var factory = new SqlDataAccessPolicyFactory(
            NullLogger<SqlDataAccessPolicyFactory>.Instance);

        // Act
        var policy = factory.GetRetryPolicy();

        // Assert
        policy.ShouldNotBeNull();
    }

    [Fact]
    public void ReturnSameCachedRetryPolicyOnMultipleCalls()
    {
        // Arrange
        var factory = new SqlDataAccessPolicyFactory(
            NullLogger<SqlDataAccessPolicyFactory>.Instance);

        // Act
        var policy1 = factory.GetRetryPolicy();
        var policy2 = factory.GetRetryPolicy();

        // Assert
        policy1.ShouldBeSameAs(policy2);
    }

    [Fact]
    public void ReturnNonNullCircuitBreakerPolicy()
    {
        // Arrange
        var factory = new SqlDataAccessPolicyFactory(
            NullLogger<SqlDataAccessPolicyFactory>.Instance);

        // Act
        var policy = factory.CreateCircuitBreakerPolicy();

        // Assert
        policy.ShouldNotBeNull();
    }

    [Fact]
    public void ReturnNonNullComprehensivePolicy()
    {
        // Arrange
        var factory = new SqlDataAccessPolicyFactory(
            NullLogger<SqlDataAccessPolicyFactory>.Instance);

        // Act
        var policy = factory.GetComprehensivePolicy();

        // Assert
        policy.ShouldNotBeNull();
    }

    [Fact]
    public void CreateNewCircuitBreakerPolicyEachCall()
    {
        // Arrange
        var factory = new SqlDataAccessPolicyFactory(
            NullLogger<SqlDataAccessPolicyFactory>.Instance);

        // Act
        var policy1 = factory.CreateCircuitBreakerPolicy();
        var policy2 = factory.CreateCircuitBreakerPolicy();

        // Assert — circuit breaker policies should be new instances
        policy1.ShouldNotBeSameAs(policy2);
    }
}
