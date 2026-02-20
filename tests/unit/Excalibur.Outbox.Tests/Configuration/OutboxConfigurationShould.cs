using Excalibur.Outbox;

namespace Excalibur.Outbox.Tests.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class OutboxConfigurationShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var config = new OutboxConfiguration();

		// Assert
		config.BatchSize.ShouldBe(100);
		config.PollingInterval.ShouldBe(TimeSpan.FromSeconds(5));
		config.MaxRetryCount.ShouldBe(3);
		config.RetryDelay.ShouldBe(TimeSpan.FromMinutes(5));
		config.MessageRetentionPeriod.ShouldBe(TimeSpan.FromDays(7));
		config.EnableAutomaticCleanup.ShouldBeTrue();
		config.CleanupInterval.ShouldBe(TimeSpan.FromHours(1));
		config.EnableBackgroundProcessing.ShouldBeTrue();
		config.ProcessorId.ShouldBeNull();
		config.EnableParallelProcessing.ShouldBeFalse();
		config.MaxDegreeOfParallelism.ShouldBe(4);
	}

	[Fact]
	public void AllowSettingBatchSize()
	{
		// Arrange & Act
		var config = new OutboxConfiguration { BatchSize = 500 };

		// Assert
		config.BatchSize.ShouldBe(500);
	}

	[Fact]
	public void AllowSettingPollingInterval()
	{
		// Arrange & Act
		var config = new OutboxConfiguration { PollingInterval = TimeSpan.FromSeconds(10) };

		// Assert
		config.PollingInterval.ShouldBe(TimeSpan.FromSeconds(10));
	}

	[Fact]
	public void AllowSettingProcessorId()
	{
		// Arrange & Act
		var config = new OutboxConfiguration { ProcessorId = "worker-1" };

		// Assert
		config.ProcessorId.ShouldBe("worker-1");
	}

	[Fact]
	public void ConvertToOutboxOptionsWithCustomPreset()
	{
		// Arrange
		var config = new OutboxConfiguration
		{
			BatchSize = 250,
			PollingInterval = TimeSpan.FromMilliseconds(500),
			MaxRetryCount = 7,
			RetryDelay = TimeSpan.FromMinutes(2),
			MessageRetentionPeriod = TimeSpan.FromDays(14),
			EnableAutomaticCleanup = true,
			CleanupInterval = TimeSpan.FromHours(2),
			EnableBackgroundProcessing = true,
			ProcessorId = "test",
			EnableParallelProcessing = true,
			MaxDegreeOfParallelism = 8
		};

		// Act
		var options = config.ToOptions();

		// Assert
		options.Preset.ShouldBe(OutboxPreset.Custom);
		options.BatchSize.ShouldBe(250);
		options.PollingInterval.ShouldBe(TimeSpan.FromMilliseconds(500));
		options.MaxRetryCount.ShouldBe(7);
		options.RetryDelay.ShouldBe(TimeSpan.FromMinutes(2));
		options.MessageRetentionPeriod.ShouldBe(TimeSpan.FromDays(14));
		options.EnableAutomaticCleanup.ShouldBeTrue();
		options.CleanupInterval.ShouldBe(TimeSpan.FromHours(2));
		options.EnableBackgroundProcessing.ShouldBeTrue();
		options.ProcessorId.ShouldBe("test");
		options.EnableParallelProcessing.ShouldBeTrue();
		options.MaxDegreeOfParallelism.ShouldBe(8);
	}

	[Fact]
	public void ConvertDefaultConfigToOptions()
	{
		// Arrange
		var config = new OutboxConfiguration();

		// Act
		var options = config.ToOptions();

		// Assert
		options.Preset.ShouldBe(OutboxPreset.Custom);
		options.BatchSize.ShouldBe(100);
		options.ProcessorId.ShouldBeNull();
	}
}
