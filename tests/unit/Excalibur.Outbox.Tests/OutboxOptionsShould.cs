// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Outbox.Tests;

/// <summary>
/// Unit tests for <see cref="OutboxOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class OutboxOptionsShould : UnitTestBase
{
	#region Balanced Preset (Default) Tests

	[Fact]
	public void BalancedPreset_HaveCorrectBatchSize()
	{
		// Arrange & Act
		var options = OutboxOptions.Balanced().Build();

		// Assert
		options.BatchSize.ShouldBe(100);
	}

	[Fact]
	public void BalancedPreset_HaveCorrectPollingInterval()
	{
		// Arrange & Act
		var options = OutboxOptions.Balanced().Build();

		// Assert
		options.PollingInterval.ShouldBe(TimeSpan.FromSeconds(1));
	}

	[Fact]
	public void BalancedPreset_HaveCorrectMaxRetryCount()
	{
		// Arrange & Act
		var options = OutboxOptions.Balanced().Build();

		// Assert
		options.MaxRetryCount.ShouldBe(5);
	}

	[Fact]
	public void BalancedPreset_HaveCorrectRetryDelay()
	{
		// Arrange & Act
		var options = OutboxOptions.Balanced().Build();

		// Assert
		options.RetryDelay.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void BalancedPreset_HaveAutomaticCleanupEnabled()
	{
		// Arrange & Act
		var options = OutboxOptions.Balanced().Build();

		// Assert
		options.EnableAutomaticCleanup.ShouldBeTrue();
	}

	[Fact]
	public void BalancedPreset_HaveCorrectMessageRetentionPeriod()
	{
		// Arrange & Act
		var options = OutboxOptions.Balanced().Build();

		// Assert
		options.MessageRetentionPeriod.ShouldBe(TimeSpan.FromDays(7));
	}

	[Fact]
	public void BalancedPreset_HaveCorrectMaxDegreeOfParallelism()
	{
		// Arrange & Act
		var options = OutboxOptions.Balanced().Build();

		// Assert
		options.MaxDegreeOfParallelism.ShouldBe(4);
	}

	#endregion

	#region HighThroughput Preset Tests

	[Fact]
	public void HighThroughputPreset_HaveCorrectBatchSize()
	{
		// Arrange & Act
		var options = OutboxOptions.HighThroughput().Build();

		// Assert
		options.BatchSize.ShouldBe(1000);
	}

	[Fact]
	public void HighThroughputPreset_HaveCorrectPollingInterval()
	{
		// Arrange & Act
		var options = OutboxOptions.HighThroughput().Build();

		// Assert
		options.PollingInterval.ShouldBe(TimeSpan.FromMilliseconds(100));
	}

	[Fact]
	public void HighThroughputPreset_HaveCorrectMaxDegreeOfParallelism()
	{
		// Arrange & Act
		var options = OutboxOptions.HighThroughput().Build();

		// Assert
		options.MaxDegreeOfParallelism.ShouldBe(8);
	}

	[Fact]
	public void HighThroughputPreset_HaveParallelProcessingEnabled()
	{
		// Arrange & Act
		var options = OutboxOptions.HighThroughput().Build();

		// Assert
		options.EnableParallelProcessing.ShouldBeTrue();
	}

	#endregion

	#region HighReliability Preset Tests

	[Fact]
	public void HighReliabilityPreset_HaveCorrectBatchSize()
	{
		// Arrange & Act
		var options = OutboxOptions.HighReliability().Build();

		// Assert
		options.BatchSize.ShouldBe(10);
	}

	[Fact]
	public void HighReliabilityPreset_HaveCorrectPollingInterval()
	{
		// Arrange & Act
		var options = OutboxOptions.HighReliability().Build();

		// Assert
		options.PollingInterval.ShouldBe(TimeSpan.FromSeconds(5));
	}

	[Fact]
	public void HighReliabilityPreset_HaveCorrectMaxRetryCount()
	{
		// Arrange & Act
		var options = OutboxOptions.HighReliability().Build();

		// Assert
		options.MaxRetryCount.ShouldBe(10);
	}

	[Fact]
	public void HighReliabilityPreset_HaveSequentialProcessing()
	{
		// Arrange & Act
		var options = OutboxOptions.HighReliability().Build();

		// Assert
		options.EnableParallelProcessing.ShouldBeFalse();
		options.MaxDegreeOfParallelism.ShouldBe(1);
	}

	[Fact]
	public void HighReliabilityPreset_HaveCorrectRetentionPeriod()
	{
		// Arrange & Act
		var options = OutboxOptions.HighReliability().Build();

		// Assert
		options.MessageRetentionPeriod.ShouldBe(TimeSpan.FromDays(30));
	}

	#endregion

	#region Custom Builder Tests

	[Fact]
	public void Custom_AllowCustomBatchSize()
	{
		// Arrange & Act
		var options = OutboxOptions.Custom()
			.WithBatchSize(200)
			.Build();

		// Assert
		options.BatchSize.ShouldBe(200);
	}

	[Fact]
	public void Custom_AllowCustomPollingInterval()
	{
		// Arrange & Act
		var options = OutboxOptions.Custom()
			.WithPollingInterval(TimeSpan.FromSeconds(10))
			.Build();

		// Assert
		options.PollingInterval.ShouldBe(TimeSpan.FromSeconds(10));
	}

	[Fact]
	public void Custom_AllowCustomMaxRetryCount()
	{
		// Arrange & Act
		var options = OutboxOptions.Custom()
			.WithMaxRetries(5)
			.Build();

		// Assert
		options.MaxRetryCount.ShouldBe(5);
	}

	[Fact]
	public void Custom_AllowCustomRetryDelay()
	{
		// Arrange & Act
		var options = OutboxOptions.Custom()
			.WithRetryDelay(TimeSpan.FromMinutes(10))
			.Build();

		// Assert
		options.RetryDelay.ShouldBe(TimeSpan.FromMinutes(10));
	}

	[Fact]
	public void Custom_AllowDisablingAutomaticCleanup()
	{
		// Arrange & Act
		var options = OutboxOptions.Custom()
			.DisableAutomaticCleanup()
			.Build();

		// Assert
		options.EnableAutomaticCleanup.ShouldBeFalse();
	}

	[Fact]
	public void Custom_AllowCustomMessageRetentionPeriod()
	{
		// Arrange & Act
		var options = OutboxOptions.Custom()
			.WithRetentionPeriod(TimeSpan.FromDays(30))
			.Build();

		// Assert
		options.MessageRetentionPeriod.ShouldBe(TimeSpan.FromDays(30));
	}

	[Fact]
	public void Custom_AllowCustomProcessorId()
	{
		// Arrange & Act
		var options = OutboxOptions.Custom()
			.WithProcessorId("worker-1")
			.Build();

		// Assert
		options.ProcessorId.ShouldBe("worker-1");
	}

	[Fact]
	public void Custom_AllowCustomParallelism()
	{
		// Arrange & Act
		var options = OutboxOptions.Custom()
			.WithParallelism(16)
			.Build();

		// Assert
		options.EnableParallelProcessing.ShouldBeTrue();
		options.MaxDegreeOfParallelism.ShouldBe(16);
	}

	[Fact]
	public void Custom_AllowEnableBackgroundProcessing()
	{
		// Arrange & Act
		var options = OutboxOptions.Custom()
			.EnableBackgroundProcessing()
			.Build();

		// Assert
		options.EnableBackgroundProcessing.ShouldBeTrue();
	}

	#endregion

	#region Preset Override Tests

	[Fact]
	public void HighThroughput_AllowOverridingBatchSize()
	{
		// Arrange & Act
		var options = OutboxOptions.HighThroughput()
			.WithBatchSize(2000)
			.Build();

		// Assert
		options.BatchSize.ShouldBe(2000);
		// Other preset values should be preserved
		options.PollingInterval.ShouldBe(TimeSpan.FromMilliseconds(100));
	}

	[Fact]
	public void Balanced_AllowOverridingProcessorId()
	{
		// Arrange & Act
		var options = OutboxOptions.Balanced()
			.WithProcessorId("custom-processor")
			.Build();

		// Assert
		options.ProcessorId.ShouldBe("custom-processor");
		// Other preset values should be preserved
		options.BatchSize.ShouldBe(100);
	}

	[Fact]
	public void HighReliability_AllowOverridingRetentionPeriod()
	{
		// Arrange & Act
		var options = OutboxOptions.HighReliability()
			.WithRetentionPeriod(TimeSpan.FromDays(90))
			.Build();

		// Assert
		options.MessageRetentionPeriod.ShouldBe(TimeSpan.FromDays(90));
		// Other preset values should be preserved
		options.BatchSize.ShouldBe(10);
	}

	#endregion

	#region Fluent Chain Tests

	[Fact]
	public void FluentChain_AllowMultipleOverrides()
	{
		// Arrange & Act
		var options = OutboxOptions.Balanced()
			.WithBatchSize(500)
			.WithPollingInterval(TimeSpan.FromSeconds(2))
			.WithMaxRetries(5)
			.WithProcessorId("my-worker")
			.EnableBackgroundProcessing()
			.Build();

		// Assert
		options.BatchSize.ShouldBe(500);
		options.PollingInterval.ShouldBe(TimeSpan.FromSeconds(2));
		options.MaxRetryCount.ShouldBe(5);
		options.ProcessorId.ShouldBe("my-worker");
		options.EnableBackgroundProcessing.ShouldBeTrue();
	}

	#endregion

	#region Immutability Tests

	[Fact]
	public void Build_CreatesSeparateInstances()
	{
		// Arrange
		var builder = OutboxOptions.Balanced();

		// Act
		var options1 = builder.Build();
		var options2 = builder.Build();

		// Assert
		options1.ShouldNotBeSameAs(options2);
	}

	[Fact]
	public void PresetProperty_ReturnsCorrectValue()
	{
		// Arrange & Act
		var highThroughput = OutboxOptions.HighThroughput().Build();
		var balanced = OutboxOptions.Balanced().Build();
		var highReliability = OutboxOptions.HighReliability().Build();
		var custom = OutboxOptions.Custom().Build();

		// Assert
		highThroughput.Preset.ShouldBe(OutboxPreset.HighThroughput);
		balanced.Preset.ShouldBe(OutboxPreset.Balanced);
		highReliability.Preset.ShouldBe(OutboxPreset.HighReliability);
		custom.Preset.ShouldBe(OutboxPreset.Custom);
	}

	#endregion

	#region Validation Tests

	[Fact]
	public void Build_ThrowsOnInvalidBatchSize_Zero()
	{
		// Arrange & Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			OutboxOptions.Custom().WithBatchSize(0).Build());
	}

	[Fact]
	public void Build_ThrowsOnInvalidBatchSize_ExceedsMaximum()
	{
		// Arrange & Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			OutboxOptions.Custom().WithBatchSize(10001).Build());
	}

	[Fact]
	public void Build_ThrowsOnInvalidPollingInterval()
	{
		// Arrange & Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			OutboxOptions.Custom().WithPollingInterval(TimeSpan.FromMilliseconds(5)).Build());
	}

	[Fact]
	public void Build_ThrowsOnInvalidMaxRetries()
	{
		// Arrange & Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			OutboxOptions.Custom().WithMaxRetries(-1).Build());
	}

	[Fact]
	public void Build_ThrowsOnInvalidParallelism()
	{
		// Arrange & Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			OutboxOptions.Custom().WithParallelism(0).Build());
	}

	[Fact]
	public void Build_ThrowsOnInvalidRetryDelay()
	{
		// Arrange & Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			OutboxOptions.Custom().WithRetryDelay(TimeSpan.Zero).Build());
	}

	[Fact]
	public void Build_ThrowsOnNegativeRetryDelay()
	{
		// Arrange & Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			OutboxOptions.Custom().WithRetryDelay(TimeSpan.FromSeconds(-1)).Build());
	}

	[Fact]
	public void Build_ThrowsOnInvalidRetentionPeriod()
	{
		// Arrange & Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			OutboxOptions.Custom().WithRetentionPeriod(TimeSpan.Zero).Build());
	}

	[Fact]
	public void Build_ThrowsOnInvalidCleanupInterval()
	{
		// Arrange & Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			OutboxOptions.Custom().WithCleanupInterval(TimeSpan.Zero).Build());
	}

	[Fact]
	public void Build_ThrowsOnRetentionPeriodLessThanCleanupInterval()
	{
		// Arrange & Act & Assert
		_ = Should.Throw<InvalidOperationException>(() =>
			OutboxOptions.Custom()
				.WithRetentionPeriod(TimeSpan.FromMinutes(30))
				.WithCleanupInterval(TimeSpan.FromHours(1))
				.Build());
	}

	[Fact]
	public void Build_AllowsRetentionPeriodLessThanCleanupIntervalWhenCleanupDisabled()
	{
		// Arrange & Act
		var options = OutboxOptions.Custom()
			.WithRetentionPeriod(TimeSpan.FromMinutes(30))
			.WithCleanupInterval(TimeSpan.FromHours(1))
			.DisableAutomaticCleanup()
			.Build();

		// Assert - should not throw
		options.EnableAutomaticCleanup.ShouldBeFalse();
		options.MessageRetentionPeriod.ShouldBe(TimeSpan.FromMinutes(30));
	}

	[Fact]
	public void Build_ThrowsOnNullProcessorId()
	{
		// Arrange & Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			OutboxOptions.Custom().WithProcessorId(null!).Build());
	}

	[Fact]
	public void Build_ThrowsOnEmptyProcessorId()
	{
		// Arrange & Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			OutboxOptions.Custom().WithProcessorId("").Build());
	}

	[Fact]
	public void Build_ThrowsOnWhitespaceProcessorId()
	{
		// Arrange & Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			OutboxOptions.Custom().WithProcessorId("   ").Build());
	}

	#endregion
}
