using Excalibur.Data.Abstractions.Resilience;

namespace Excalibur.Data.Tests.Abstractions;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DataProviderCircuitBreakerOptionsShould
{
    [Fact]
    public void HaveCorrectDefaults()
    {
        // Arrange & Act
        var options = new DataProviderCircuitBreakerOptions();

        // Assert
        options.FailureThreshold.ShouldBe(5);
        options.BreakDuration.ShouldBe(TimeSpan.FromSeconds(30));
        options.SamplingWindow.ShouldBe(TimeSpan.FromSeconds(60));
        options.HalfOpenTrialCount.ShouldBe(1);
    }

    [Fact]
    public void AllowSettingFailureThreshold()
    {
        // Arrange & Act
        var options = new DataProviderCircuitBreakerOptions { FailureThreshold = 10 };

        // Assert
        options.FailureThreshold.ShouldBe(10);
    }

    [Fact]
    public void AllowSettingBreakDuration()
    {
        // Arrange
        var duration = TimeSpan.FromMinutes(2);

        // Act
        var options = new DataProviderCircuitBreakerOptions { BreakDuration = duration };

        // Assert
        options.BreakDuration.ShouldBe(duration);
    }

    [Fact]
    public void AllowSettingSamplingWindow()
    {
        // Arrange
        var window = TimeSpan.FromMinutes(5);

        // Act
        var options = new DataProviderCircuitBreakerOptions { SamplingWindow = window };

        // Assert
        options.SamplingWindow.ShouldBe(window);
    }

    [Fact]
    public void AllowSettingHalfOpenTrialCount()
    {
        // Arrange & Act
        var options = new DataProviderCircuitBreakerOptions { HalfOpenTrialCount = 3 };

        // Assert
        options.HalfOpenTrialCount.ShouldBe(3);
    }
}
