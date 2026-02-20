using Excalibur.Outbox;

namespace Excalibur.Outbox.Tests.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InboxOptionsBuilderShould
{
	[Fact]
	public void BuildHighThroughputPresetWithCorrectDefaults()
	{
		// Act
		var options = InboxOptions.HighThroughput().Build();

		// Assert
		options.Preset.ShouldBe(InboxPreset.HighThroughput);
		options.QueueCapacity.ShouldBe(2000);
		options.ProducerBatchSize.ShouldBe(500);
		options.ConsumerBatchSize.ShouldBe(200);
		options.PerRunTotal.ShouldBe(5000);
		options.MaxAttempts.ShouldBe(3);
		options.ParallelProcessingDegree.ShouldBe(8);
		options.BatchProcessingTimeout.ShouldBe(TimeSpan.FromMinutes(2));
		options.EnableBatchDatabaseOperations.ShouldBeTrue();
	}

	[Fact]
	public void BuildBalancedPresetWithCorrectDefaults()
	{
		// Act
		var options = InboxOptions.Balanced().Build();

		// Assert
		options.Preset.ShouldBe(InboxPreset.Balanced);
		options.QueueCapacity.ShouldBe(500);
		options.ProducerBatchSize.ShouldBe(100);
		options.ConsumerBatchSize.ShouldBe(50);
		options.PerRunTotal.ShouldBe(1000);
		options.MaxAttempts.ShouldBe(5);
		options.ParallelProcessingDegree.ShouldBe(4);
		options.BatchProcessingTimeout.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void BuildHighReliabilityPresetWithCorrectDefaults()
	{
		// Act
		var options = InboxOptions.HighReliability().Build();

		// Assert
		options.Preset.ShouldBe(InboxPreset.HighReliability);
		options.QueueCapacity.ShouldBe(100);
		options.ProducerBatchSize.ShouldBe(20);
		options.ConsumerBatchSize.ShouldBe(10);
		options.PerRunTotal.ShouldBe(200);
		options.MaxAttempts.ShouldBe(10);
		options.ParallelProcessingDegree.ShouldBe(1);
		options.BatchProcessingTimeout.ShouldBe(TimeSpan.FromMinutes(10));
	}

	[Fact]
	public void BuildCustomPresetWithBalancedDefaults()
	{
		// Act
		var options = InboxOptions.Custom().Build();

		// Assert
		options.Preset.ShouldBe(InboxPreset.Custom);
		options.QueueCapacity.ShouldBe(500);
	}

	[Fact]
	public void OverrideQueueCapacity()
	{
		// Act
		var options = InboxOptions.Custom()
			.WithQueueCapacity(1000)
			.Build();

		// Assert
		options.QueueCapacity.ShouldBe(1000);
	}

	[Fact]
	public void ThrowWhenQueueCapacityBelowMinimum()
	{
		Should.Throw<ArgumentOutOfRangeException>(() =>
			InboxOptions.Custom().WithQueueCapacity(0));
	}

	[Fact]
	public void ThrowWhenQueueCapacityAboveMaximum()
	{
		Should.Throw<ArgumentOutOfRangeException>(() =>
			InboxOptions.Custom().WithQueueCapacity(100001));
	}

	[Fact]
	public void OverrideProducerBatchSize()
	{
		// Act
		var options = InboxOptions.Custom()
			.WithProducerBatchSize(200)
			.Build();

		// Assert
		options.ProducerBatchSize.ShouldBe(200);
	}

	[Fact]
	public void ThrowWhenProducerBatchSizeBelowMinimum()
	{
		Should.Throw<ArgumentOutOfRangeException>(() =>
			InboxOptions.Custom().WithProducerBatchSize(0));
	}

	[Fact]
	public void OverrideConsumerBatchSize()
	{
		// Act
		var options = InboxOptions.Custom()
			.WithConsumerBatchSize(100)
			.Build();

		// Assert
		options.ConsumerBatchSize.ShouldBe(100);
	}

	[Fact]
	public void ThrowWhenConsumerBatchSizeBelowMinimum()
	{
		Should.Throw<ArgumentOutOfRangeException>(() =>
			InboxOptions.Custom().WithConsumerBatchSize(0));
	}

	[Fact]
	public void OverridePerRunTotal()
	{
		// Act
		var options = InboxOptions.Custom()
			.WithPerRunTotal(500)
			.Build();

		// Assert
		options.PerRunTotal.ShouldBe(500);
	}

	[Fact]
	public void ThrowWhenPerRunTotalBelowMinimum()
	{
		Should.Throw<ArgumentOutOfRangeException>(() =>
			InboxOptions.Custom().WithPerRunTotal(0));
	}

	[Fact]
	public void OverrideMaxAttempts()
	{
		// Act
		var options = InboxOptions.Custom()
			.WithMaxAttempts(7)
			.Build();

		// Assert
		options.MaxAttempts.ShouldBe(7);
	}

	[Fact]
	public void ThrowWhenMaxAttemptsBelowMinimum()
	{
		Should.Throw<ArgumentOutOfRangeException>(() =>
			InboxOptions.Custom().WithMaxAttempts(0));
	}

	[Fact]
	public void OverrideParallelism()
	{
		// Act
		var options = InboxOptions.Custom()
			.WithParallelism(16)
			.Build();

		// Assert
		options.ParallelProcessingDegree.ShouldBe(16);
	}

	[Fact]
	public void ThrowWhenParallelismBelowMinimum()
	{
		Should.Throw<ArgumentOutOfRangeException>(() =>
			InboxOptions.Custom().WithParallelism(0));
	}

	[Fact]
	public void OverrideBatchProcessingTimeout()
	{
		// Act
		var options = InboxOptions.Custom()
			.WithBatchProcessingTimeout(TimeSpan.FromMinutes(15))
			.Build();

		// Assert
		options.BatchProcessingTimeout.ShouldBe(TimeSpan.FromMinutes(15));
	}

	[Fact]
	public void ThrowWhenBatchProcessingTimeoutIsNotPositive()
	{
		Should.Throw<ArgumentOutOfRangeException>(() =>
			InboxOptions.Custom().WithBatchProcessingTimeout(TimeSpan.Zero));
		Should.Throw<ArgumentOutOfRangeException>(() =>
			InboxOptions.Custom().WithBatchProcessingTimeout(TimeSpan.FromSeconds(-1)));
	}

	[Fact]
	public void SetDefaultMessageTtl()
	{
		// Act
		var options = InboxOptions.Custom()
			.WithDefaultMessageTtl(TimeSpan.FromHours(1))
			.Build();

		// Assert
		options.DefaultMessageTtl.ShouldBe(TimeSpan.FromHours(1));
	}

	[Fact]
	public void AllowNullDefaultMessageTtl()
	{
		// Act
		var options = InboxOptions.Custom()
			.WithDefaultMessageTtl(null)
			.Build();

		// Assert
		options.DefaultMessageTtl.ShouldBeNull();
	}

	[Fact]
	public void ThrowWhenDefaultMessageTtlIsNotPositive()
	{
		Should.Throw<ArgumentOutOfRangeException>(() =>
			InboxOptions.Custom().WithDefaultMessageTtl(TimeSpan.Zero));
		Should.Throw<ArgumentOutOfRangeException>(() =>
			InboxOptions.Custom().WithDefaultMessageTtl(TimeSpan.FromSeconds(-1)));
	}

	[Fact]
	public void EnableDynamicBatchSizing()
	{
		// Act
		var options = InboxOptions.Custom()
			.EnableDynamicBatchSizing(minBatchSize: 5, maxBatchSize: 500)
			.Build();

		// Assert
		options.EnableDynamicBatchSizing.ShouldBeTrue();
		options.MinBatchSize.ShouldBe(5);
		options.MaxBatchSize.ShouldBe(500);
	}

	[Fact]
	public void ThrowWhenDynamicBatchSizingMinBelowOne()
	{
		Should.Throw<ArgumentOutOfRangeException>(() =>
			InboxOptions.Custom().EnableDynamicBatchSizing(minBatchSize: 0, maxBatchSize: 100));
	}

	[Fact]
	public void ThrowWhenDynamicBatchSizingMaxBelowMin()
	{
		Should.Throw<ArgumentOutOfRangeException>(() =>
			InboxOptions.Custom().EnableDynamicBatchSizing(minBatchSize: 100, maxBatchSize: 50));
	}

	[Fact]
	public void DisableBatchDatabaseOperations()
	{
		// Act
		var options = InboxOptions.Custom()
			.DisableBatchDatabaseOperations()
			.Build();

		// Assert
		options.EnableBatchDatabaseOperations.ShouldBeFalse();
	}

	[Fact]
	public void ThrowWhenQueueCapacityLessThanProducerBatchSize()
	{
		Should.Throw<InvalidOperationException>(() =>
			InboxOptions.Custom()
				.WithQueueCapacity(10)
				.WithProducerBatchSize(100)
				.Build());
	}

	[Fact]
	public void SupportFluentChaining()
	{
		// Act
		var options = InboxOptions.HighThroughput()
			.WithQueueCapacity(3000)
			.WithProducerBatchSize(600)
			.WithConsumerBatchSize(300)
			.WithMaxAttempts(5)
			.WithParallelism(16)
			.Build();

		// Assert
		options.QueueCapacity.ShouldBe(3000);
		options.ProducerBatchSize.ShouldBe(600);
		options.ConsumerBatchSize.ShouldBe(300);
		options.MaxAttempts.ShouldBe(5);
		options.ParallelProcessingDegree.ShouldBe(16);
	}
}
