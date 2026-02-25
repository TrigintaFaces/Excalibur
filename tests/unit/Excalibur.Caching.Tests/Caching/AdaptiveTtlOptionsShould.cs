using Excalibur.Caching.AdaptiveTtl;

namespace Excalibur.Tests.Caching;

/// <summary>
/// Unit tests for AdaptiveTtlOptions configuration.
/// </summary>
[Trait("Category", "Unit")]
public sealed class AdaptiveTtlOptionsShould : UnitTestBase
{
	[Fact]
	public void Create_WithDefaults_HasExpectedDefaultValues()
	{
		// Arrange & Act
		var options = new AdaptiveTtlOptions();

		// Assert
		options.MinTtl.ShouldBe(TimeSpan.FromSeconds(5));
		options.MaxTtl.ShouldBe(TimeSpan.FromHours(24));
		options.TargetHitRate.ShouldBe(0.8);
		options.TargetResponseTime.ShouldBe(TimeSpan.FromMilliseconds(50));
		options.LearningRate.ShouldBe(0.1);
		options.DiscountFactor.ShouldBe(0.9);
		options.Weights.HitRateWeight.ShouldBe(0.3);
		options.Weights.AccessFrequencyWeight.ShouldBe(0.25);
		options.Weights.TemporalWeight.ShouldBe(0.15);
		options.Weights.CostWeight.ShouldBe(0.15);
		options.Weights.LoadWeight.ShouldBe(0.1);
		options.Weights.VolatilityWeight.ShouldBe(0.05);
		options.Thresholds.HighLoadThreshold.ShouldBe(0.8);
		options.Thresholds.LowLoadThreshold.ShouldBe(0.3);
		options.Thresholds.MaxExpectedFrequency.ShouldBe(1000);
		options.Thresholds.MaxExpectedMissCostMs.ShouldBe(1000);
		options.Thresholds.LargeContentThresholdMb.ShouldBe(10);
	}

	[Fact]
	public void MinTtl_CanBeCustomized()
	{
		// Arrange
		var options = new AdaptiveTtlOptions();

		// Act
		options.MinTtl = TimeSpan.FromSeconds(10);

		// Assert
		options.MinTtl.ShouldBe(TimeSpan.FromSeconds(10));
	}

	[Fact]
	public void MaxTtl_CanBeCustomized()
	{
		// Arrange
		var options = new AdaptiveTtlOptions();

		// Act
		options.MaxTtl = TimeSpan.FromHours(48);

		// Assert
		options.MaxTtl.ShouldBe(TimeSpan.FromHours(48));
	}

	[Fact]
	public void TargetHitRate_CanBeCustomized()
	{
		// Arrange
		var options = new AdaptiveTtlOptions();

		// Act
		options.TargetHitRate = 0.95;

		// Assert
		options.TargetHitRate.ShouldBe(0.95);
	}

	[Fact]
	public void LearningRate_CanBeCustomized()
	{
		// Arrange
		var options = new AdaptiveTtlOptions();

		// Act
		options.LearningRate = 0.05;

		// Assert
		options.LearningRate.ShouldBe(0.05);
	}

	[Fact]
	public void Weights_SumToOne()
	{
		// Arrange
		var options = new AdaptiveTtlOptions();

		// Act
		var sum = options.Weights.HitRateWeight +
			options.Weights.AccessFrequencyWeight +
			options.Weights.TemporalWeight +
			options.Weights.CostWeight +
			options.Weights.LoadWeight +
			options.Weights.VolatilityWeight;

		// Assert
		sum.ShouldBe(1.0, 0.001);
	}
}
