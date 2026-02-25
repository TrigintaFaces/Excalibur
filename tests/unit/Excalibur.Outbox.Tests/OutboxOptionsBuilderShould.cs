// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Outbox.Tests;

/// <summary>
/// Unit tests for <see cref="IOutboxOptionsBuilder"/> implementation.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
[Trait("Priority", "0")]
public sealed class OutboxOptionsBuilderShould : UnitTestBase
{
	#region WithProcessorId Tests

	[Theory]
	[InlineData("worker-1")]
	[InlineData("processor-abc")]
	[InlineData("instance-12345")]
	public void WithProcessorId_AcceptsValidValues(string processorId)
	{
		// Act
		var options = OutboxOptions.Custom()
			.WithProcessorId(processorId)
			.Build();

		// Assert
		options.ProcessorId.ShouldBe(processorId);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void WithProcessorId_ThrowsOnInvalidValues(string? processorId)
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			OutboxOptions.Custom().WithProcessorId(processorId));
	}

	#endregion

	#region EnableBackgroundProcessing Tests

	[Fact]
	public void EnableBackgroundProcessing_SetsFlag()
	{
		// Act
		var options = OutboxOptions.Custom()
			.EnableBackgroundProcessing()
			.Build();

		// Assert
		options.EnableBackgroundProcessing.ShouldBeTrue();
	}

	[Fact]
	public void EnableBackgroundProcessing_CanBeDisabled()
	{
		// Act
		var options = OutboxOptions.Custom()
			.EnableBackgroundProcessing(false)
			.Build();

		// Assert
		options.EnableBackgroundProcessing.ShouldBeFalse();
	}

	#endregion

	#region WithBatchSize Tests

	[Theory]
	[InlineData(1)]
	[InlineData(100)]
	[InlineData(1000)]
	[InlineData(10000)]
	public void WithBatchSize_AcceptsValidValues(int batchSize)
	{
		// Act
		var options = OutboxOptions.Custom()
			.WithBatchSize(batchSize)
			.Build();

		// Assert
		options.BatchSize.ShouldBe(batchSize);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void WithBatchSize_ThrowsOnInvalidValues(int batchSize)
	{
		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			OutboxOptions.Custom().WithBatchSize(batchSize));
	}

	[Fact]
	public void WithBatchSize_ThrowsWhenExceedsMax()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			OutboxOptions.Custom().WithBatchSize(10001));
	}

	#endregion

	#region WithPollingInterval Tests

	[Theory]
	[InlineData(10)]
	[InlineData(100)]
	[InlineData(1000)]
	[InlineData(60000)]
	public void WithPollingInterval_AcceptsValidMilliseconds(int milliseconds)
	{
		// Arrange
		var interval = TimeSpan.FromMilliseconds(milliseconds);

		// Act
		var options = OutboxOptions.Custom()
			.WithPollingInterval(interval)
			.Build();

		// Assert
		options.PollingInterval.ShouldBe(interval);
	}

	[Fact]
	public void WithPollingInterval_ThrowsWhenBelowMinimum()
	{
		// Arrange
		var interval = TimeSpan.FromMilliseconds(9);

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			OutboxOptions.Custom().WithPollingInterval(interval));
	}

	[Fact]
	public void WithPollingInterval_ThrowsOnZero()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			OutboxOptions.Custom().WithPollingInterval(TimeSpan.Zero));
	}

	[Fact]
	public void WithPollingInterval_ThrowsOnNegative()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			OutboxOptions.Custom().WithPollingInterval(TimeSpan.FromMilliseconds(-1)));
	}

	#endregion

	#region WithParallelism Tests

	[Theory]
	[InlineData(1)]
	[InlineData(4)]
	[InlineData(8)]
	[InlineData(16)]
	public void WithParallelism_AcceptsValidValues(int maxDegree)
	{
		// Act
		var options = OutboxOptions.Custom()
			.WithParallelism(maxDegree)
			.Build();

		// Assert
		options.MaxDegreeOfParallelism.ShouldBe(maxDegree);
	}

	[Fact]
	public void WithParallelism_SetsParallelProcessingFlagWhenGreaterThan1()
	{
		// Act
		var options = OutboxOptions.Custom()
			.WithParallelism(4)
			.Build();

		// Assert
		options.EnableParallelProcessing.ShouldBeTrue();
	}

	[Fact]
	public void WithParallelism_DisablesParallelProcessingWhen1()
	{
		// Act
		var options = OutboxOptions.Custom()
			.WithParallelism(1)
			.Build();

		// Assert
		options.EnableParallelProcessing.ShouldBeFalse();
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void WithParallelism_ThrowsOnInvalidValues(int maxDegree)
	{
		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			OutboxOptions.Custom().WithParallelism(maxDegree));
	}

	#endregion

	#region WithMaxRetries Tests

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(5)]
	[InlineData(100)]
	public void WithMaxRetries_AcceptsValidValues(int maxRetries)
	{
		// Act
		var options = OutboxOptions.Custom()
			.WithMaxRetries(maxRetries)
			.Build();

		// Assert
		options.MaxRetryCount.ShouldBe(maxRetries);
	}

	[Fact]
	public void WithMaxRetries_ThrowsOnNegative()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			OutboxOptions.Custom().WithMaxRetries(-1));
	}

	#endregion

	#region WithRetryDelay Tests

	[Theory]
	[InlineData(1)]
	[InlineData(60)]
	[InlineData(900)]
	public void WithRetryDelay_AcceptsValidSeconds(int seconds)
	{
		// Arrange
		var delay = TimeSpan.FromSeconds(seconds);

		// Act
		var options = OutboxOptions.Custom()
			.WithRetryDelay(delay)
			.Build();

		// Assert
		options.RetryDelay.ShouldBe(delay);
	}

	[Fact]
	public void WithRetryDelay_ThrowsOnZero()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			OutboxOptions.Custom().WithRetryDelay(TimeSpan.Zero));
	}

	[Fact]
	public void WithRetryDelay_ThrowsOnNegative()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			OutboxOptions.Custom().WithRetryDelay(TimeSpan.FromSeconds(-1)));
	}

	#endregion

	#region WithRetentionPeriod Tests

	[Theory]
	[InlineData(1)]
	[InlineData(7)]
	[InlineData(30)]
	[InlineData(365)]
	public void WithRetentionPeriod_AcceptsValidDays(int days)
	{
		// Arrange
		var period = TimeSpan.FromDays(days);

		// Act
		var options = OutboxOptions.Custom()
			.WithRetentionPeriod(period)
			.Build();

		// Assert
		options.MessageRetentionPeriod.ShouldBe(period);
	}

	[Fact]
	public void WithRetentionPeriod_ThrowsOnZero()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			OutboxOptions.Custom().WithRetentionPeriod(TimeSpan.Zero));
	}

	[Fact]
	public void WithRetentionPeriod_ThrowsOnNegative()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			OutboxOptions.Custom().WithRetentionPeriod(TimeSpan.FromDays(-1)));
	}

	#endregion

	#region WithCleanupInterval Tests

	[Theory]
	[InlineData(1)]
	[InlineData(60)]
	[InlineData(360)]
	public void WithCleanupInterval_AcceptsValidMinutes(int minutes)
	{
		// Arrange
		var interval = TimeSpan.FromMinutes(minutes);

		// Act - need to set retention period >= cleanup interval
		var options = OutboxOptions.Custom()
			.WithRetentionPeriod(TimeSpan.FromDays(30))
			.WithCleanupInterval(interval)
			.Build();

		// Assert
		options.CleanupInterval.ShouldBe(interval);
	}

	[Fact]
	public void WithCleanupInterval_ThrowsOnZero()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			OutboxOptions.Custom().WithCleanupInterval(TimeSpan.Zero));
	}

	[Fact]
	public void WithCleanupInterval_ThrowsOnNegative()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			OutboxOptions.Custom().WithCleanupInterval(TimeSpan.FromMinutes(-1)));
	}

	#endregion

	#region DisableAutomaticCleanup Tests

	[Fact]
	public void DisableAutomaticCleanup_SetsFlag()
	{
		// Act
		var options = OutboxOptions.Custom()
			.DisableAutomaticCleanup()
			.Build();

		// Assert
		options.EnableAutomaticCleanup.ShouldBeFalse();
	}

	#endregion

	#region Build Validation Tests

	[Fact]
	public void Build_ThrowsWhenRetentionPeriodLessThanCleanupInterval()
	{
		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() =>
			OutboxOptions.Custom()
				.WithRetentionPeriod(TimeSpan.FromMinutes(30))
				.WithCleanupInterval(TimeSpan.FromHours(1))
				.Build());
	}

	[Fact]
	public void Build_AllowsRetentionPeriodEqualToCleanupInterval()
	{
		// Act
		var options = OutboxOptions.Custom()
			.WithRetentionPeriod(TimeSpan.FromHours(1))
			.WithCleanupInterval(TimeSpan.FromHours(1))
			.Build();

		// Assert
		options.MessageRetentionPeriod.ShouldBe(TimeSpan.FromHours(1));
		options.CleanupInterval.ShouldBe(TimeSpan.FromHours(1));
	}

	[Fact]
	public void Build_AllowsAnyCleanupIntervalWhenAutomaticCleanupDisabled()
	{
		// Act - when cleanup is disabled, retention/cleanup validation is skipped
		var options = OutboxOptions.Custom()
			.DisableAutomaticCleanup()
			.WithRetentionPeriod(TimeSpan.FromMinutes(30))
			.Build();

		// Assert
		options.EnableAutomaticCleanup.ShouldBeFalse();
	}

	#endregion

	#region Preset Tests

	[Fact]
	public void HighThroughput_SetsExpectedDefaults()
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
	public void Balanced_SetsExpectedDefaults()
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
	public void HighReliability_SetsExpectedDefaults()
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
	public void Custom_SetsDefaultValues()
	{
		// Act
		var options = OutboxOptions.Custom().Build();

		// Assert
		options.Preset.ShouldBe(OutboxPreset.Custom);
		options.BatchSize.ShouldBe(100);
		options.PollingInterval.ShouldBe(TimeSpan.FromSeconds(5));
		options.MaxRetryCount.ShouldBe(3);
		options.RetryDelay.ShouldBe(TimeSpan.FromMinutes(5));
		options.EnableParallelProcessing.ShouldBeFalse();
		options.MaxDegreeOfParallelism.ShouldBe(4);
		options.MessageRetentionPeriod.ShouldBe(TimeSpan.FromDays(7));
		options.CleanupInterval.ShouldBe(TimeSpan.FromHours(1));
	}

	#endregion

	#region Fluent Chaining Tests

	[Fact]
	public void AllMethods_ReturnBuilder_ForChaining()
	{
		// Act
		var options = OutboxOptions.Custom()
			.WithProcessorId("test-processor")
			.EnableBackgroundProcessing(true)
			.WithBatchSize(500)
			.WithPollingInterval(TimeSpan.FromMilliseconds(500))
			.WithParallelism(6)
			.WithMaxRetries(7)
			.WithRetryDelay(TimeSpan.FromMinutes(10))
			.WithRetentionPeriod(TimeSpan.FromDays(14))
			.WithCleanupInterval(TimeSpan.FromHours(2))
			.Build();

		// Assert
		options.ProcessorId.ShouldBe("test-processor");
		options.EnableBackgroundProcessing.ShouldBeTrue();
		options.BatchSize.ShouldBe(500);
		options.PollingInterval.ShouldBe(TimeSpan.FromMilliseconds(500));
		options.MaxDegreeOfParallelism.ShouldBe(6);
		options.EnableParallelProcessing.ShouldBeTrue();
		options.MaxRetryCount.ShouldBe(7);
		options.RetryDelay.ShouldBe(TimeSpan.FromMinutes(10));
		options.MessageRetentionPeriod.ShouldBe(TimeSpan.FromDays(14));
		options.CleanupInterval.ShouldBe(TimeSpan.FromHours(2));
	}

	[Fact]
	public void PresetOverride_PreservesPresetType()
	{
		// Act
		var options = OutboxOptions.HighThroughput()
			.WithBatchSize(2000)
			.Build();

		// Assert
		options.Preset.ShouldBe(OutboxPreset.HighThroughput);
		options.BatchSize.ShouldBe(2000);
	}

	#endregion
}
