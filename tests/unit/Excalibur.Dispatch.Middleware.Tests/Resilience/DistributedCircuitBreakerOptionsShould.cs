using Excalibur.Dispatch.Resilience.Polly;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Unit tests for DistributedCircuitBreakerOptions configuration.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DistributedCircuitBreakerOptionsShould : UnitTestBase
{
	[Fact]
	public void Create_WithDefaults_HasExpectedDefaultValues()
	{
		// Arrange & Act
		var options = new DistributedCircuitBreakerOptions();

		// Assert
		options.FailureRatio.ShouldBe(0.5);
		options.MinimumThroughput.ShouldBe(10);
		options.SamplingDuration.ShouldBe(TimeSpan.FromSeconds(30));
		options.BreakDuration.ShouldBe(TimeSpan.FromSeconds(30));
		options.ConsecutiveFailureThreshold.ShouldBe(5);
		options.SuccessThresholdToClose.ShouldBe(3);
		options.SyncInterval.ShouldBe(TimeSpan.FromSeconds(5));
		options.MetricsRetention.ShouldBe(TimeSpan.FromMinutes(10));
	}

	[Fact]
	public void FailureRatio_CanBeCustomized()
	{
		// Arrange
		var options = new DistributedCircuitBreakerOptions();

		// Act
		options.FailureRatio = 0.75;

		// Assert
		options.FailureRatio.ShouldBe(0.75);
	}

	[Fact]
	public void MinimumThroughput_CanBeCustomized()
	{
		// Arrange
		var options = new DistributedCircuitBreakerOptions();

		// Act
		options.MinimumThroughput = 20;

		// Assert
		options.MinimumThroughput.ShouldBe(20);
	}

	[Fact]
	public void SamplingDuration_CanBeCustomized()
	{
		// Arrange
		var options = new DistributedCircuitBreakerOptions();

		// Act
		options.SamplingDuration = TimeSpan.FromMinutes(1);

		// Assert
		options.SamplingDuration.ShouldBe(TimeSpan.FromMinutes(1));
	}

	[Fact]
	public void BreakDuration_CanBeCustomized()
	{
		// Arrange
		var options = new DistributedCircuitBreakerOptions();

		// Act
		options.BreakDuration = TimeSpan.FromMinutes(2);

		// Assert
		options.BreakDuration.ShouldBe(TimeSpan.FromMinutes(2));
	}

	[Fact]
	public void ConsecutiveFailureThreshold_CanBeCustomized()
	{
		// Arrange
		var options = new DistributedCircuitBreakerOptions();

		// Act
		options.ConsecutiveFailureThreshold = 10;

		// Assert
		options.ConsecutiveFailureThreshold.ShouldBe(10);
	}

	[Fact]
	public void SuccessThresholdToClose_CanBeCustomized()
	{
		// Arrange
		var options = new DistributedCircuitBreakerOptions();

		// Act
		options.SuccessThresholdToClose = 5;

		// Assert
		options.SuccessThresholdToClose.ShouldBe(5);
	}

	[Fact]
	public void SyncInterval_CanBeCustomized()
	{
		// Arrange
		var options = new DistributedCircuitBreakerOptions();

		// Act
		options.SyncInterval = TimeSpan.FromSeconds(10);

		// Assert
		options.SyncInterval.ShouldBe(TimeSpan.FromSeconds(10));
	}

	[Fact]
	public void MetricsRetention_CanBeCustomized()
	{
		// Arrange
		var options = new DistributedCircuitBreakerOptions();

		// Act
		options.MetricsRetention = TimeSpan.FromHours(1);

		// Assert
		options.MetricsRetention.ShouldBe(TimeSpan.FromHours(1));
	}
}
