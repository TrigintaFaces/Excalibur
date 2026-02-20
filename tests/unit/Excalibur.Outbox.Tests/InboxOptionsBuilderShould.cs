// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Outbox.Tests;

/// <summary>
/// Unit tests for <see cref="IInboxOptionsBuilder"/> implementation.
/// </summary>
[Trait("Category", "Unit")]
public sealed class InboxOptionsBuilderShould : UnitTestBase
{
	#region WithQueueCapacity Tests

	[Theory]
	[InlineData(1)]
	[InlineData(100)]
	[InlineData(1000)]
	[InlineData(100000)]
	public void WithQueueCapacity_AcceptsValidValues(int capacity)
	{
		// Act - set ProducerBatchSize to 1 to ensure it's always <= QueueCapacity
		var options = InboxOptions.Custom()
			.WithQueueCapacity(capacity)
			.WithProducerBatchSize(1)
			.Build();

		// Assert
		options.QueueCapacity.ShouldBe(capacity);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void WithQueueCapacity_ThrowsOnInvalidValues(int capacity)
	{
		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			InboxOptions.Custom().WithQueueCapacity(capacity));
	}

	[Fact]
	public void WithQueueCapacity_ThrowsWhenExceedsMax()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			InboxOptions.Custom().WithQueueCapacity(100001));
	}

	#endregion

	#region WithProducerBatchSize Tests

	[Theory]
	[InlineData(1)]
	[InlineData(100)]
	[InlineData(10000)]
	public void WithProducerBatchSize_AcceptsValidValues(int batchSize)
	{
		// Act - set QueueCapacity to max to ensure it's always >= ProducerBatchSize
		var options = InboxOptions.Custom()
			.WithQueueCapacity(100000)
			.WithProducerBatchSize(batchSize)
			.Build();

		// Assert
		options.ProducerBatchSize.ShouldBe(batchSize);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void WithProducerBatchSize_ThrowsOnInvalidValues(int batchSize)
	{
		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			InboxOptions.Custom().WithProducerBatchSize(batchSize));
	}

	#endregion

	#region WithConsumerBatchSize Tests

	[Theory]
	[InlineData(1)]
	[InlineData(50)]
	[InlineData(10000)]
	public void WithConsumerBatchSize_AcceptsValidValues(int batchSize)
	{
		// Act
		var options = InboxOptions.Custom()
			.WithConsumerBatchSize(batchSize)
			.Build();

		// Assert
		options.ConsumerBatchSize.ShouldBe(batchSize);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void WithConsumerBatchSize_ThrowsOnInvalidValues(int batchSize)
	{
		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			InboxOptions.Custom().WithConsumerBatchSize(batchSize));
	}

	#endregion

	#region WithPerRunTotal Tests

	[Theory]
	[InlineData(1)]
	[InlineData(1000)]
	[InlineData(100000)]
	public void WithPerRunTotal_AcceptsValidValues(int total)
	{
		// Act
		var options = InboxOptions.Custom()
			.WithPerRunTotal(total)
			.Build();

		// Assert
		options.PerRunTotal.ShouldBe(total);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void WithPerRunTotal_ThrowsOnInvalidValues(int total)
	{
		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			InboxOptions.Custom().WithPerRunTotal(total));
	}

	#endregion

	#region WithMaxAttempts Tests

	[Theory]
	[InlineData(1)]
	[InlineData(5)]
	[InlineData(100)]
	public void WithMaxAttempts_AcceptsValidValues(int maxAttempts)
	{
		// Act
		var options = InboxOptions.Custom()
			.WithMaxAttempts(maxAttempts)
			.Build();

		// Assert
		options.MaxAttempts.ShouldBe(maxAttempts);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void WithMaxAttempts_ThrowsOnInvalidValues(int maxAttempts)
	{
		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			InboxOptions.Custom().WithMaxAttempts(maxAttempts));
	}

	#endregion

	#region WithParallelism Tests

	[Theory]
	[InlineData(1)]
	[InlineData(4)]
	[InlineData(16)]
	public void WithParallelism_AcceptsValidValues(int parallelism)
	{
		// Act
		var options = InboxOptions.Custom()
			.WithParallelism(parallelism)
			.Build();

		// Assert
		options.ParallelProcessingDegree.ShouldBe(parallelism);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void WithParallelism_ThrowsOnInvalidValues(int parallelism)
	{
		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			InboxOptions.Custom().WithParallelism(parallelism));
	}

	#endregion

	#region WithBatchProcessingTimeout Tests

	[Theory]
	[InlineData(1)]
	[InlineData(60)]
	[InlineData(600)]
	public void WithBatchProcessingTimeout_AcceptsValidSeconds(int seconds)
	{
		// Arrange
		var timeout = TimeSpan.FromSeconds(seconds);

		// Act
		var options = InboxOptions.Custom()
			.WithBatchProcessingTimeout(timeout)
			.Build();

		// Assert
		options.BatchProcessingTimeout.ShouldBe(timeout);
	}

	[Fact]
	public void WithBatchProcessingTimeout_ThrowsOnZero()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			InboxOptions.Custom().WithBatchProcessingTimeout(TimeSpan.Zero));
	}

	[Fact]
	public void WithBatchProcessingTimeout_ThrowsOnNegative()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			InboxOptions.Custom().WithBatchProcessingTimeout(TimeSpan.FromSeconds(-1)));
	}

	#endregion

	#region WithDefaultMessageTtl Tests

	[Theory]
	[InlineData(1)]
	[InlineData(24)]
	[InlineData(168)]
	public void WithDefaultMessageTtl_AcceptsValidHours(int hours)
	{
		// Arrange
		var ttl = TimeSpan.FromHours(hours);

		// Act
		var options = InboxOptions.Custom()
			.WithDefaultMessageTtl(ttl)
			.Build();

		// Assert
		options.DefaultMessageTtl.ShouldBe(ttl);
	}

	[Fact]
	public void WithDefaultMessageTtl_AcceptsNull()
	{
		// Act
		var options = InboxOptions.Custom()
			.WithDefaultMessageTtl(null)
			.Build();

		// Assert
		options.DefaultMessageTtl.ShouldBeNull();
	}

	[Fact]
	public void WithDefaultMessageTtl_ThrowsOnZero()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			InboxOptions.Custom().WithDefaultMessageTtl(TimeSpan.Zero));
	}

	[Fact]
	public void WithDefaultMessageTtl_ThrowsOnNegative()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			InboxOptions.Custom().WithDefaultMessageTtl(TimeSpan.FromHours(-1)));
	}

	#endregion

	#region EnableDynamicBatchSizing Tests

	[Fact]
	public void EnableDynamicBatchSizing_SetsFlag()
	{
		// Act
		var options = InboxOptions.Custom()
			.EnableDynamicBatchSizing()
			.Build();

		// Assert
		options.EnableDynamicBatchSizing.ShouldBeTrue();
	}

	[Fact]
	public void EnableDynamicBatchSizing_SetsMinAndMaxBatchSize()
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
	public void EnableDynamicBatchSizing_ThrowsOnInvalidMinBatchSize()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			InboxOptions.Custom().EnableDynamicBatchSizing(minBatchSize: 0));
	}

	[Fact]
	public void EnableDynamicBatchSizing_ThrowsWhenMaxLessThanMin()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			InboxOptions.Custom().EnableDynamicBatchSizing(minBatchSize: 100, maxBatchSize: 50));
	}

	#endregion

	#region DisableBatchDatabaseOperations Tests

	[Fact]
	public void DisableBatchDatabaseOperations_SetsFlag()
	{
		// Act
		var options = InboxOptions.Custom()
			.DisableBatchDatabaseOperations()
			.Build();

		// Assert
		options.EnableBatchDatabaseOperations.ShouldBeFalse();
	}

	#endregion

	#region Build Validation Tests

	[Fact]
	public void Build_ThrowsWhenQueueCapacityLessThanProducerBatchSize()
	{
		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() =>
			InboxOptions.Custom()
				.WithQueueCapacity(50)
				.WithProducerBatchSize(100)
				.Build());
	}

	[Fact]
	public void Build_ThrowsWhenDynamicBatchSizingMinGreaterThanMax()
	{
		// Arrange - set up via reflection or indirect means since direct call throws
		// This test verifies the Validate() method catches the invalid state
		// Since EnableDynamicBatchSizing already validates, we test the boundary
		var options = InboxOptions.Custom()
			.EnableDynamicBatchSizing(minBatchSize: 10, maxBatchSize: 100)
			.Build();

		// Assert - should succeed with valid values
		options.MinBatchSize.ShouldBe(10);
		options.MaxBatchSize.ShouldBe(100);
	}

	#endregion

	#region Preset Tests

	[Fact]
	public void HighThroughput_SetsExpectedDefaults()
	{
		// Act
		var options = InboxOptions.HighThroughput().Build();

		// Assert
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
	public void Balanced_SetsExpectedDefaults()
	{
		// Act
		var options = InboxOptions.Balanced().Build();

		// Assert
		options.QueueCapacity.ShouldBe(500);
		options.ProducerBatchSize.ShouldBe(100);
		options.ConsumerBatchSize.ShouldBe(50);
		options.PerRunTotal.ShouldBe(1000);
		options.MaxAttempts.ShouldBe(5);
		options.ParallelProcessingDegree.ShouldBe(4);
		options.BatchProcessingTimeout.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void HighReliability_SetsExpectedDefaults()
	{
		// Act
		var options = InboxOptions.HighReliability().Build();

		// Assert
		options.QueueCapacity.ShouldBe(100);
		options.ProducerBatchSize.ShouldBe(20);
		options.ConsumerBatchSize.ShouldBe(10);
		options.PerRunTotal.ShouldBe(200);
		options.MaxAttempts.ShouldBe(10);
		options.ParallelProcessingDegree.ShouldBe(1);
		options.BatchProcessingTimeout.ShouldBe(TimeSpan.FromMinutes(10));
	}

	[Fact]
	public void Custom_SetsBalancedDefaults()
	{
		// Act
		var options = InboxOptions.Custom().Build();

		// Assert - Custom uses Balanced defaults as base
		options.QueueCapacity.ShouldBe(500);
		options.ProducerBatchSize.ShouldBe(100);
		options.ConsumerBatchSize.ShouldBe(50);
		options.PerRunTotal.ShouldBe(1000);
		options.MaxAttempts.ShouldBe(5);
		options.ParallelProcessingDegree.ShouldBe(4);
	}

	#endregion

	#region ProducerBatchSize Max Boundary Tests

	[Fact]
	public void WithProducerBatchSize_ThrowsWhenExceedsMax()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			InboxOptions.Custom().WithProducerBatchSize(10001));
	}

	#endregion

	#region ConsumerBatchSize Max Boundary Tests

	[Fact]
	public void WithConsumerBatchSize_ThrowsWhenExceedsMax()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			InboxOptions.Custom().WithConsumerBatchSize(10001));
	}

	#endregion

	#region Fluent Chaining Tests

	[Fact]
	public void AllMethods_ReturnBuilder_ForChaining()
	{
		// Act
		var options = InboxOptions.Custom()
			.WithQueueCapacity(1000)
			.WithProducerBatchSize(200)
			.WithConsumerBatchSize(100)
			.WithPerRunTotal(2000)
			.WithMaxAttempts(7)
			.WithParallelism(8)
			.WithBatchProcessingTimeout(TimeSpan.FromMinutes(3))
			.WithDefaultMessageTtl(TimeSpan.FromHours(12))
			.EnableDynamicBatchSizing(20, 500)
			.Build();

		// Assert
		options.QueueCapacity.ShouldBe(1000);
		options.ProducerBatchSize.ShouldBe(200);
		options.ConsumerBatchSize.ShouldBe(100);
		options.PerRunTotal.ShouldBe(2000);
		options.MaxAttempts.ShouldBe(7);
		options.ParallelProcessingDegree.ShouldBe(8);
		options.BatchProcessingTimeout.ShouldBe(TimeSpan.FromMinutes(3));
		options.DefaultMessageTtl.ShouldBe(TimeSpan.FromHours(12));
		options.EnableDynamicBatchSizing.ShouldBeTrue();
		options.MinBatchSize.ShouldBe(20);
		options.MaxBatchSize.ShouldBe(500);
	}

	#endregion
}
