using Excalibur.Outbox;

namespace Excalibur.Outbox.Tests.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class OutboxOptionsBuilderShould
{
	[Fact]
	public void BuildHighThroughputPresetWithCorrectDefaults()
	{
		// Act
		var options = OutboxOptions.HighThroughput().Build();

		// Assert
		options.Preset.ShouldBe(OutboxPreset.HighThroughput);
		options.BatchSize.ShouldBe(1000);
		options.PollingInterval.ShouldBe(TimeSpan.FromMilliseconds(100));
		options.MaxRetryCount.ShouldBe(3);
		options.RetryDelay.ShouldBe(TimeSpan.FromMinutes(1));
		options.EnableParallelProcessing.ShouldBeTrue();
		options.MaxDegreeOfParallelism.ShouldBe(8);
		options.MessageRetentionPeriod.ShouldBe(TimeSpan.FromDays(1));
		options.CleanupInterval.ShouldBe(TimeSpan.FromMinutes(15));
		options.EnableAutomaticCleanup.ShouldBeTrue();
		options.EnableBackgroundProcessing.ShouldBeTrue();
	}

	[Fact]
	public void BuildBalancedPresetWithCorrectDefaults()
	{
		// Act
		var options = OutboxOptions.Balanced().Build();

		// Assert
		options.Preset.ShouldBe(OutboxPreset.Balanced);
		options.BatchSize.ShouldBe(100);
		options.PollingInterval.ShouldBe(TimeSpan.FromSeconds(1));
		options.MaxRetryCount.ShouldBe(5);
		options.RetryDelay.ShouldBe(TimeSpan.FromMinutes(5));
		options.EnableParallelProcessing.ShouldBeTrue();
		options.MaxDegreeOfParallelism.ShouldBe(4);
		options.MessageRetentionPeriod.ShouldBe(TimeSpan.FromDays(7));
		options.CleanupInterval.ShouldBe(TimeSpan.FromHours(1));
	}

	[Fact]
	public void BuildHighReliabilityPresetWithCorrectDefaults()
	{
		// Act
		var options = OutboxOptions.HighReliability().Build();

		// Assert
		options.Preset.ShouldBe(OutboxPreset.HighReliability);
		options.BatchSize.ShouldBe(10);
		options.PollingInterval.ShouldBe(TimeSpan.FromSeconds(5));
		options.MaxRetryCount.ShouldBe(10);
		options.RetryDelay.ShouldBe(TimeSpan.FromMinutes(15));
		options.EnableParallelProcessing.ShouldBeFalse();
		options.MaxDegreeOfParallelism.ShouldBe(1);
		options.MessageRetentionPeriod.ShouldBe(TimeSpan.FromDays(30));
		options.CleanupInterval.ShouldBe(TimeSpan.FromHours(6));
	}

	[Fact]
	public void BuildCustomPresetWithBalancedDefaults()
	{
		// Act
		var options = OutboxOptions.Custom().Build();

		// Assert
		options.Preset.ShouldBe(OutboxPreset.Custom);
		options.BatchSize.ShouldBe(100);
	}

	[Fact]
	public void OverrideBatchSize()
	{
		// Act
		var options = OutboxOptions.Balanced()
			.WithBatchSize(500)
			.Build();

		// Assert
		options.BatchSize.ShouldBe(500);
	}

	[Fact]
	public void ThrowWhenBatchSizeBelowMinimum()
	{
		Should.Throw<ArgumentOutOfRangeException>(() =>
			OutboxOptions.Custom().WithBatchSize(0));
	}

	[Fact]
	public void ThrowWhenBatchSizeAboveMaximum()
	{
		Should.Throw<ArgumentOutOfRangeException>(() =>
			OutboxOptions.Custom().WithBatchSize(10001));
	}

	[Fact]
	public void OverridePollingInterval()
	{
		// Act
		var options = OutboxOptions.Custom()
			.WithPollingInterval(TimeSpan.FromMilliseconds(500))
			.Build();

		// Assert
		options.PollingInterval.ShouldBe(TimeSpan.FromMilliseconds(500));
	}

	[Fact]
	public void ThrowWhenPollingIntervalTooSmall()
	{
		Should.Throw<ArgumentOutOfRangeException>(() =>
			OutboxOptions.Custom().WithPollingInterval(TimeSpan.FromMilliseconds(5)));
	}

	[Fact]
	public void OverrideMaxRetries()
	{
		// Act
		var options = OutboxOptions.Custom()
			.WithMaxRetries(7)
			.Build();

		// Assert
		options.MaxRetryCount.ShouldBe(7);
	}

	[Fact]
	public void ThrowWhenMaxRetriesIsNegative()
	{
		Should.Throw<ArgumentOutOfRangeException>(() =>
			OutboxOptions.Custom().WithMaxRetries(-1));
	}

	[Fact]
	public void AllowZeroMaxRetries()
	{
		// Act
		var options = OutboxOptions.Custom()
			.WithMaxRetries(0)
			.Build();

		// Assert
		options.MaxRetryCount.ShouldBe(0);
	}

	[Fact]
	public void OverrideRetryDelay()
	{
		// Act
		var options = OutboxOptions.Custom()
			.WithRetryDelay(TimeSpan.FromMinutes(2))
			.Build();

		// Assert
		options.RetryDelay.ShouldBe(TimeSpan.FromMinutes(2));
	}

	[Fact]
	public void ThrowWhenRetryDelayIsNotPositive()
	{
		Should.Throw<ArgumentOutOfRangeException>(() =>
			OutboxOptions.Custom().WithRetryDelay(TimeSpan.Zero));
		Should.Throw<ArgumentOutOfRangeException>(() =>
			OutboxOptions.Custom().WithRetryDelay(TimeSpan.FromSeconds(-1)));
	}

	[Fact]
	public void OverrideRetentionPeriod()
	{
		// Act
		var options = OutboxOptions.Custom()
			.WithRetentionPeriod(TimeSpan.FromDays(14))
			.Build();

		// Assert
		options.MessageRetentionPeriod.ShouldBe(TimeSpan.FromDays(14));
	}

	[Fact]
	public void ThrowWhenRetentionPeriodIsNotPositive()
	{
		Should.Throw<ArgumentOutOfRangeException>(() =>
			OutboxOptions.Custom().WithRetentionPeriod(TimeSpan.Zero));
	}

	[Fact]
	public void OverrideCleanupInterval()
	{
		// Act
		var options = OutboxOptions.Custom()
			.WithCleanupInterval(TimeSpan.FromMinutes(30))
			.Build();

		// Assert
		options.CleanupInterval.ShouldBe(TimeSpan.FromMinutes(30));
	}

	[Fact]
	public void ThrowWhenCleanupIntervalIsNotPositive()
	{
		Should.Throw<ArgumentOutOfRangeException>(() =>
			OutboxOptions.Custom().WithCleanupInterval(TimeSpan.Zero));
	}

	[Fact]
	public void DisableAutomaticCleanup()
	{
		// Act
		var options = OutboxOptions.Custom()
			.DisableAutomaticCleanup()
			.Build();

		// Assert
		options.EnableAutomaticCleanup.ShouldBeFalse();
	}

	[Fact]
	public void OverrideParallelism()
	{
		// Act
		var options = OutboxOptions.Custom()
			.WithParallelism(8)
			.Build();

		// Assert
		options.EnableParallelProcessing.ShouldBeTrue();
		options.MaxDegreeOfParallelism.ShouldBe(8);
	}

	[Fact]
	public void DisableParallelismWithDegreeOfOne()
	{
		// Act
		var options = OutboxOptions.Custom()
			.WithParallelism(1)
			.Build();

		// Assert
		options.EnableParallelProcessing.ShouldBeFalse();
		options.MaxDegreeOfParallelism.ShouldBe(1);
	}

	[Fact]
	public void ThrowWhenParallelismIsLessThanOne()
	{
		Should.Throw<ArgumentOutOfRangeException>(() =>
			OutboxOptions.Custom().WithParallelism(0));
	}

	[Fact]
	public void SetProcessorId()
	{
		// Act
		var options = OutboxOptions.Custom()
			.WithProcessorId("worker-1")
			.Build();

		// Assert
		options.ProcessorId.ShouldBe("worker-1");
	}

	[Fact]
	public void ThrowWhenProcessorIdIsNullOrWhiteSpace()
	{
		Should.Throw<ArgumentException>(() =>
			OutboxOptions.Custom().WithProcessorId(null!));
		Should.Throw<ArgumentException>(() =>
			OutboxOptions.Custom().WithProcessorId(""));
		Should.Throw<ArgumentException>(() =>
			OutboxOptions.Custom().WithProcessorId("   "));
	}

	[Fact]
	public void EnableBackgroundProcessing()
	{
		// Act
		var options = OutboxOptions.Custom()
			.EnableBackgroundProcessing(true)
			.Build();

		// Assert
		options.EnableBackgroundProcessing.ShouldBeTrue();
	}

	[Fact]
	public void DisableBackgroundProcessing()
	{
		// Act
		var options = OutboxOptions.Custom()
			.EnableBackgroundProcessing(false)
			.Build();

		// Assert
		options.EnableBackgroundProcessing.ShouldBeFalse();
	}

	[Fact]
	public void ThrowWhenRetentionPeriodLessThanCleanupInterval()
	{
		// Arrange - retention of 10 minutes but cleanup every 1 hour
		Should.Throw<InvalidOperationException>(() =>
			OutboxOptions.Custom()
				.WithRetentionPeriod(TimeSpan.FromMinutes(10))
				.WithCleanupInterval(TimeSpan.FromHours(1))
				.Build());
	}

	[Fact]
	public void SupportFluentChaining()
	{
		// Act
		var options = OutboxOptions.HighThroughput()
			.WithBatchSize(2000)
			.WithProcessorId("worker-1")
			.WithMaxRetries(5)
			.WithParallelism(16)
			.Build();

		// Assert
		options.BatchSize.ShouldBe(2000);
		options.ProcessorId.ShouldBe("worker-1");
		options.MaxRetryCount.ShouldBe(5);
		options.MaxDegreeOfParallelism.ShouldBe(16);
	}
}
