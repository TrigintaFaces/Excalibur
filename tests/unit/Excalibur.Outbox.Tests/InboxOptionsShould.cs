// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Outbox.Tests;

/// <summary>
/// Unit tests for <see cref="InboxOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class InboxOptionsShould : UnitTestBase
{
	#region Balanced Preset Tests

	[Fact]
	public void BalancedPreset_HaveCorrectQueueCapacity()
	{
		// Arrange & Act
		var options = InboxOptions.Balanced().Build();

		// Assert
		options.QueueCapacity.ShouldBe(500);
	}

	[Fact]
	public void BalancedPreset_HaveCorrectProducerBatchSize()
	{
		// Arrange & Act
		var options = InboxOptions.Balanced().Build();

		// Assert
		options.ProducerBatchSize.ShouldBe(100);
	}

	[Fact]
	public void BalancedPreset_HaveCorrectConsumerBatchSize()
	{
		// Arrange & Act
		var options = InboxOptions.Balanced().Build();

		// Assert
		options.ConsumerBatchSize.ShouldBe(50);
	}

	[Fact]
	public void BalancedPreset_HaveCorrectPerRunTotal()
	{
		// Arrange & Act
		var options = InboxOptions.Balanced().Build();

		// Assert
		options.PerRunTotal.ShouldBe(1000);
	}

	[Fact]
	public void BalancedPreset_HaveCorrectMaxAttempts()
	{
		// Arrange & Act
		var options = InboxOptions.Balanced().Build();

		// Assert
		options.MaxAttempts.ShouldBe(5);
	}

	[Fact]
	public void BalancedPreset_HaveCorrectParallelProcessingDegree()
	{
		// Arrange & Act
		var options = InboxOptions.Balanced().Build();

		// Assert
		options.ParallelProcessingDegree.ShouldBe(4);
	}

	[Fact]
	public void BalancedPreset_HaveCorrectBatchProcessingTimeout()
	{
		// Arrange & Act
		var options = InboxOptions.Balanced().Build();

		// Assert
		options.BatchProcessingTimeout.ShouldBe(TimeSpan.FromMinutes(5));
	}

	#endregion

	#region HighThroughput Preset Tests

	[Fact]
	public void HighThroughputPreset_HaveCorrectQueueCapacity()
	{
		// Arrange & Act
		var options = InboxOptions.HighThroughput().Build();

		// Assert
		options.QueueCapacity.ShouldBe(2000);
	}

	[Fact]
	public void HighThroughputPreset_HaveCorrectProducerBatchSize()
	{
		// Arrange & Act
		var options = InboxOptions.HighThroughput().Build();

		// Assert
		options.ProducerBatchSize.ShouldBe(500);
	}

	[Fact]
	public void HighThroughputPreset_HaveCorrectConsumerBatchSize()
	{
		// Arrange & Act
		var options = InboxOptions.HighThroughput().Build();

		// Assert
		options.ConsumerBatchSize.ShouldBe(200);
	}

	[Fact]
	public void HighThroughputPreset_HaveCorrectPerRunTotal()
	{
		// Arrange & Act
		var options = InboxOptions.HighThroughput().Build();

		// Assert
		options.PerRunTotal.ShouldBe(5000);
	}

	[Fact]
	public void HighThroughputPreset_HaveCorrectMaxAttempts()
	{
		// Arrange & Act
		var options = InboxOptions.HighThroughput().Build();

		// Assert
		options.MaxAttempts.ShouldBe(3);
	}

	[Fact]
	public void HighThroughputPreset_HaveCorrectParallelProcessingDegree()
	{
		// Arrange & Act
		var options = InboxOptions.HighThroughput().Build();

		// Assert
		options.ParallelProcessingDegree.ShouldBe(8);
	}

	[Fact]
	public void HighThroughputPreset_HaveCorrectBatchProcessingTimeout()
	{
		// Arrange & Act
		var options = InboxOptions.HighThroughput().Build();

		// Assert
		options.BatchProcessingTimeout.ShouldBe(TimeSpan.FromMinutes(2));
	}

	#endregion

	#region HighReliability Preset Tests

	[Fact]
	public void HighReliabilityPreset_HaveCorrectQueueCapacity()
	{
		// Arrange & Act
		var options = InboxOptions.HighReliability().Build();

		// Assert
		options.QueueCapacity.ShouldBe(100);
	}

	[Fact]
	public void HighReliabilityPreset_HaveCorrectProducerBatchSize()
	{
		// Arrange & Act
		var options = InboxOptions.HighReliability().Build();

		// Assert
		options.ProducerBatchSize.ShouldBe(20);
	}

	[Fact]
	public void HighReliabilityPreset_HaveCorrectConsumerBatchSize()
	{
		// Arrange & Act
		var options = InboxOptions.HighReliability().Build();

		// Assert
		options.ConsumerBatchSize.ShouldBe(10);
	}

	[Fact]
	public void HighReliabilityPreset_HaveCorrectPerRunTotal()
	{
		// Arrange & Act
		var options = InboxOptions.HighReliability().Build();

		// Assert
		options.PerRunTotal.ShouldBe(200);
	}

	[Fact]
	public void HighReliabilityPreset_HaveCorrectMaxAttempts()
	{
		// Arrange & Act
		var options = InboxOptions.HighReliability().Build();

		// Assert
		options.MaxAttempts.ShouldBe(10);
	}

	[Fact]
	public void HighReliabilityPreset_HaveSequentialProcessing()
	{
		// Arrange & Act
		var options = InboxOptions.HighReliability().Build();

		// Assert
		options.ParallelProcessingDegree.ShouldBe(1);
	}

	[Fact]
	public void HighReliabilityPreset_HaveCorrectBatchProcessingTimeout()
	{
		// Arrange & Act
		var options = InboxOptions.HighReliability().Build();

		// Assert
		options.BatchProcessingTimeout.ShouldBe(TimeSpan.FromMinutes(10));
	}

	#endregion

	#region Custom Builder Tests

	[Fact]
	public void Custom_AllowCustomQueueCapacity()
	{
		// Arrange & Act
		var options = InboxOptions.Custom()
			.WithQueueCapacity(1000)
			.Build();

		// Assert
		options.QueueCapacity.ShouldBe(1000);
	}

	[Fact]
	public void Custom_AllowCustomProducerBatchSize()
	{
		// Arrange & Act
		var options = InboxOptions.Custom()
			.WithProducerBatchSize(200)
			.Build();

		// Assert
		options.ProducerBatchSize.ShouldBe(200);
	}

	[Fact]
	public void Custom_AllowCustomConsumerBatchSize()
	{
		// Arrange & Act
		var options = InboxOptions.Custom()
			.WithConsumerBatchSize(100)
			.Build();

		// Assert
		options.ConsumerBatchSize.ShouldBe(100);
	}

	[Fact]
	public void Custom_AllowCustomPerRunTotal()
	{
		// Arrange & Act
		var options = InboxOptions.Custom()
			.WithPerRunTotal(2000)
			.Build();

		// Assert
		options.PerRunTotal.ShouldBe(2000);
	}

	[Fact]
	public void Custom_AllowCustomMaxAttempts()
	{
		// Arrange & Act
		var options = InboxOptions.Custom()
			.WithMaxAttempts(7)
			.Build();

		// Assert
		options.MaxAttempts.ShouldBe(7);
	}

	[Fact]
	public void Custom_AllowCustomParallelism()
	{
		// Arrange & Act
		var options = InboxOptions.Custom()
			.WithParallelism(16)
			.Build();

		// Assert
		options.ParallelProcessingDegree.ShouldBe(16);
	}

	[Fact]
	public void Custom_AllowCustomBatchProcessingTimeout()
	{
		// Arrange & Act
		var options = InboxOptions.Custom()
			.WithBatchProcessingTimeout(TimeSpan.FromMinutes(15))
			.Build();

		// Assert
		options.BatchProcessingTimeout.ShouldBe(TimeSpan.FromMinutes(15));
	}

	[Fact]
	public void Custom_AllowCustomDefaultMessageTtl()
	{
		// Arrange & Act
		var options = InboxOptions.Custom()
			.WithDefaultMessageTtl(TimeSpan.FromHours(24))
			.Build();

		// Assert
		options.DefaultMessageTtl.ShouldBe(TimeSpan.FromHours(24));
	}

	[Fact]
	public void Custom_AllowEnableDynamicBatchSizing()
	{
		// Arrange & Act
		var options = InboxOptions.Custom()
			.EnableDynamicBatchSizing(minBatchSize: 5, maxBatchSize: 500)
			.Build();

		// Assert
		options.EnableDynamicBatchSizing.ShouldBeTrue();
		options.MinBatchSize.ShouldBe(5);
		options.MaxBatchSize.ShouldBe(500);
	}

	[Fact]
	public void Custom_AllowDisableBatchDatabaseOperations()
	{
		// Arrange & Act
		var options = InboxOptions.Custom()
			.DisableBatchDatabaseOperations()
			.Build();

		// Assert
		options.EnableBatchDatabaseOperations.ShouldBeFalse();
	}

	#endregion

	#region Preset Override Tests

	[Fact]
	public void HighThroughput_AllowOverridingQueueCapacity()
	{
		// Arrange & Act
		var options = InboxOptions.HighThroughput()
			.WithQueueCapacity(3000)
			.Build();

		// Assert
		options.QueueCapacity.ShouldBe(3000);
		// Other preset values should be preserved
		options.ProducerBatchSize.ShouldBe(500);
	}

	[Fact]
	public void Balanced_AllowOverridingMaxAttempts()
	{
		// Arrange & Act
		var options = InboxOptions.Balanced()
			.WithMaxAttempts(10)
			.Build();

		// Assert
		options.MaxAttempts.ShouldBe(10);
		// Other preset values should be preserved
		options.QueueCapacity.ShouldBe(500);
	}

	[Fact]
	public void HighReliability_AllowOverridingParallelism()
	{
		// Arrange & Act
		var options = InboxOptions.HighReliability()
			.WithParallelism(2)
			.Build();

		// Assert
		options.ParallelProcessingDegree.ShouldBe(2);
		// Other preset values should be preserved
		options.MaxAttempts.ShouldBe(10);
	}

	#endregion

	#region Fluent Chain Tests

	[Fact]
	public void FluentChain_AllowMultipleOverrides()
	{
		// Arrange & Act
		var options = InboxOptions.Balanced()
			.WithQueueCapacity(1000)
			.WithProducerBatchSize(200)
			.WithConsumerBatchSize(100)
			.WithMaxAttempts(7)
			.WithParallelism(8)
			.WithBatchProcessingTimeout(TimeSpan.FromMinutes(3))
			.Build();

		// Assert
		options.QueueCapacity.ShouldBe(1000);
		options.ProducerBatchSize.ShouldBe(200);
		options.ConsumerBatchSize.ShouldBe(100);
		options.MaxAttempts.ShouldBe(7);
		options.ParallelProcessingDegree.ShouldBe(8);
		options.BatchProcessingTimeout.ShouldBe(TimeSpan.FromMinutes(3));
	}

	#endregion

	#region Immutability Tests

	[Fact]
	public void Build_CreatesSeparateInstances()
	{
		// Arrange
		var builder = InboxOptions.Balanced();

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
		var highThroughput = InboxOptions.HighThroughput().Build();
		var balanced = InboxOptions.Balanced().Build();
		var highReliability = InboxOptions.HighReliability().Build();
		var custom = InboxOptions.Custom().Build();

		// Assert
		highThroughput.Preset.ShouldBe(InboxPreset.HighThroughput);
		balanced.Preset.ShouldBe(InboxPreset.Balanced);
		highReliability.Preset.ShouldBe(InboxPreset.HighReliability);
		custom.Preset.ShouldBe(InboxPreset.Custom);
	}

	#endregion

	#region Validation Tests

	[Fact]
	public void Build_ThrowsOnInvalidQueueCapacity()
	{
		// Arrange & Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			InboxOptions.Custom().WithQueueCapacity(0).Build());
	}

	[Fact]
	public void Build_ThrowsOnInvalidProducerBatchSize()
	{
		// Arrange & Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			InboxOptions.Custom().WithProducerBatchSize(0).Build());
	}

	[Fact]
	public void Build_ThrowsOnInvalidConsumerBatchSize()
	{
		// Arrange & Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			InboxOptions.Custom().WithConsumerBatchSize(0).Build());
	}

	[Fact]
	public void Build_ThrowsOnInvalidMaxAttempts()
	{
		// Arrange & Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			InboxOptions.Custom().WithMaxAttempts(0).Build());
	}

	[Fact]
	public void Build_ThrowsOnInvalidParallelism()
	{
		// Arrange & Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			InboxOptions.Custom().WithParallelism(0).Build());
	}

	[Fact]
	public void Build_ThrowsOnInvalidBatchProcessingTimeout()
	{
		// Arrange & Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			InboxOptions.Custom().WithBatchProcessingTimeout(TimeSpan.Zero).Build());
	}

	[Fact]
	public void Build_ThrowsOnQueueCapacityLessThanProducerBatchSize()
	{
		// Arrange & Act & Assert
		_ = Should.Throw<InvalidOperationException>(() =>
			InboxOptions.Custom()
				.WithQueueCapacity(50)
				.WithProducerBatchSize(100)
				.Build());
	}

	[Fact]
	public void Build_ThrowsOnQueueCapacityExceedsMaximum()
	{
		// Arrange & Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			InboxOptions.Custom().WithQueueCapacity(100001).Build());
	}

	[Fact]
	public void Build_ThrowsOnProducerBatchSizeExceedsMaximum()
	{
		// Arrange & Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			InboxOptions.Custom().WithProducerBatchSize(10001).Build());
	}

	[Fact]
	public void Build_ThrowsOnConsumerBatchSizeExceedsMaximum()
	{
		// Arrange & Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			InboxOptions.Custom().WithConsumerBatchSize(10001).Build());
	}

	[Fact]
	public void Build_ThrowsOnInvalidPerRunTotal()
	{
		// Arrange & Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			InboxOptions.Custom().WithPerRunTotal(0).Build());
	}

	[Fact]
	public void Build_ThrowsOnInvalidDefaultMessageTtl()
	{
		// Arrange & Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			InboxOptions.Custom().WithDefaultMessageTtl(TimeSpan.Zero).Build());
	}

	[Fact]
	public void Build_ThrowsOnNegativeDefaultMessageTtl()
	{
		// Arrange & Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			InboxOptions.Custom().WithDefaultMessageTtl(TimeSpan.FromSeconds(-1)).Build());
	}

	[Fact]
	public void Build_AllowsNullDefaultMessageTtl()
	{
		// Arrange & Act
		var options = InboxOptions.Custom()
			.WithDefaultMessageTtl(null)
			.Build();

		// Assert
		options.DefaultMessageTtl.ShouldBeNull();
	}

	[Fact]
	public void Build_ThrowsOnInvalidDynamicBatchSizing_MinBatchSizeZero()
	{
		// Arrange & Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			InboxOptions.Custom().EnableDynamicBatchSizing(minBatchSize: 0, maxBatchSize: 100).Build());
	}

	[Fact]
	public void Build_ThrowsOnInvalidDynamicBatchSizing_MaxLessThanMin()
	{
		// Arrange & Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			InboxOptions.Custom().EnableDynamicBatchSizing(minBatchSize: 100, maxBatchSize: 50).Build());
	}

	#endregion
}
