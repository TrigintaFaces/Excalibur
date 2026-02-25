using Excalibur.Data.Persistence;

namespace Excalibur.Data.Tests.Core;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class PersistenceOptionsDepthShould
{
    [Fact]
    public void DefaultValues_SetCorrectly()
    {
        // Act
        var options = new PersistenceOptions();

        // Assert
        options.EnableTracing.ShouldBeTrue();
        options.EnableMetrics.ShouldBeTrue();
        options.EnableSensitiveDataLogging.ShouldBeFalse();
        options.DefaultCommandTimeout.ShouldBe(30);
        options.DefaultIsolationLevel.ShouldBe(IsolationLevel.ReadCommitted);
        options.EnableAutoRetry.ShouldBeTrue();
        options.MaxRetryAttempts.ShouldBe(3);
        options.RetryDelayMilliseconds.ShouldBe(100);
    }

    [Fact]
    public void AllProperties_CanBeCustomized()
    {
        // Arrange & Act
        var options = new PersistenceOptions
        {
            EnableTracing = false,
            EnableMetrics = false,
            EnableSensitiveDataLogging = true,
            DefaultCommandTimeout = 60,
            DefaultIsolationLevel = IsolationLevel.Serializable,
            EnableAutoRetry = false,
            MaxRetryAttempts = 5,
            RetryDelayMilliseconds = 500,
        };

        // Assert
        options.EnableTracing.ShouldBeFalse();
        options.EnableMetrics.ShouldBeFalse();
        options.EnableSensitiveDataLogging.ShouldBeTrue();
        options.DefaultCommandTimeout.ShouldBe(60);
        options.DefaultIsolationLevel.ShouldBe(IsolationLevel.Serializable);
        options.EnableAutoRetry.ShouldBeFalse();
        options.MaxRetryAttempts.ShouldBe(5);
        options.RetryDelayMilliseconds.ShouldBe(500);
    }

    [Theory]
    [InlineData(IsolationLevel.ReadCommitted)]
    [InlineData(IsolationLevel.ReadUncommitted)]
    [InlineData(IsolationLevel.RepeatableRead)]
    [InlineData(IsolationLevel.Serializable)]
    [InlineData(IsolationLevel.Snapshot)]
    public void IsolationLevel_AcceptAllValidValues(IsolationLevel level)
    {
        // Arrange & Act
        var options = new PersistenceOptions { DefaultIsolationLevel = level };

        // Assert
        options.DefaultIsolationLevel.ShouldBe(level);
    }
}
