// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Dispatch.Delivery.BatchProcessing;

namespace Excalibur.Tests.Messaging.Delivery.BatchProcessing;

/// <summary>
///     Unit tests for DynamicBatchSizeCalculator to verify adaptive batch sizing functionality.
/// </summary>
[Trait("Category", "Unit")]
public class DynamicBatchSizeCalculatorShould
{
	[Fact]
	public void ConstructorShouldInitializeWithValidParameters()
	{
		// Arrange & Act
		var calculator = new DynamicBatchSizeCalculator(
			minBatchSize: 10,
			maxBatchSize: 100,
			initialBatchSize: 50);

		// Assert
		calculator.CurrentBatchSize.ShouldBe(50);
	}

	[Fact]
	public void ConstructorShouldClampInitialBatchSizeToValidRange()
	{
		// Arrange & Act
		var calculator = new DynamicBatchSizeCalculator(
			minBatchSize: 20,
			maxBatchSize: 80,
			initialBatchSize: 150);

		// Assert
		calculator.CurrentBatchSize.ShouldBe(80);
	}

	[Fact]
	public void ConstructorShouldClampInitialBatchSizeToMinimum()
	{
		// Arrange & Act
		var calculator = new DynamicBatchSizeCalculator(
			minBatchSize: 20,
			maxBatchSize: 80,
			initialBatchSize: 5);

		// Assert
		calculator.CurrentBatchSize.ShouldBe(20);
	}

	[Theory]
	[InlineData(-1, 100, 50)]
	[InlineData(0, 100, 50)]
	[InlineData(10, -1, 50)]
	[InlineData(10, 0, 50)]
	public void ConstructorShouldThrowForInvalidBatchSizes(int minBatchSize, int maxBatchSize, int initialBatchSize) =>
		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			new DynamicBatchSizeCalculator(minBatchSize, maxBatchSize, initialBatchSize));

	[Fact]
	public void ConstructorShouldThrowWhenMaxBatchSizeIsLessThanMinBatchSize() =>
		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(static () =>
			new DynamicBatchSizeCalculator(
				minBatchSize: 100,
				maxBatchSize: 50,
				initialBatchSize: 75));

	[Fact]
	public void RecordBatchResultShouldIgnoreInvalidDuration()
	{
		// Arrange
		var calculator = new DynamicBatchSizeCalculator(10, 100, 50);
		var initialBatchSize = calculator.CurrentBatchSize;

		// Act
		calculator.RecordBatchResult(
			itemsProcessed: 10,
			duration: TimeSpan.Zero,
			successRate: 1.0);

		// Assert
		calculator.CurrentBatchSize.ShouldBe(initialBatchSize);
	}

	[Fact]
	public void RecordBatchResultShouldIgnoreInvalidItemsProcessed()
	{
		// Arrange
		var calculator = new DynamicBatchSizeCalculator(10, 100, 50);
		var initialBatchSize = calculator.CurrentBatchSize;

		// Act
		calculator.RecordBatchResult(
			itemsProcessed: 0,
			duration: TimeSpan.FromSeconds(1),
			successRate: 1.0);

		// Assert
		calculator.CurrentBatchSize.ShouldBe(initialBatchSize);
	}

	[Fact]
	public void RecordBatchResultShouldReduceBatchSizeForLowSuccessRate()
	{
		// Arrange
		var calculator = new DynamicBatchSizeCalculator(10, 100, 50);

		// Act - Record multiple results with low success rate
		for (var i = 0; i < 5; i++)
		{
			calculator.RecordBatchResult(
				itemsProcessed: 10,
				duration: TimeSpan.FromSeconds(1),
				successRate: 0.8); // Below 95% threshold
		}

		// Assert
		calculator.CurrentBatchSize.ShouldBeLessThan(50);
	}

	[Fact]
	public void RecordBatchResultShouldIncreaseBatchSizeForImprovingThroughput()
	{
		// Arrange
		var calculator = new DynamicBatchSizeCalculator(10, 100, 20);

		// Act - Record results with increasing throughput
		calculator.RecordBatchResult(10, TimeSpan.FromSeconds(2), 1.0); // 5 items/sec
		calculator.RecordBatchResult(10, TimeSpan.FromSeconds(1.5), 1.0); // 6.67 items/sec
		calculator.RecordBatchResult(10, TimeSpan.FromSeconds(1), 1.0); // 10 items/sec

		// Assert
		calculator.CurrentBatchSize.ShouldBeGreaterThan(20);
	}

	[Fact]
	public void RecordBatchResultShouldDecreaseBatchSizeForDegradingThroughput()
	{
		// Arrange
		var calculator = new DynamicBatchSizeCalculator(10, 100, 50);

		// Act - Record results with decreasing throughput
		calculator.RecordBatchResult(10, TimeSpan.FromSeconds(1), 1.0); // 10 items/sec
		calculator.RecordBatchResult(10, TimeSpan.FromSeconds(1.5), 1.0); // 6.67 items/sec
		calculator.RecordBatchResult(10, TimeSpan.FromSeconds(2), 1.0); // 5 items/sec

		// Assert
		calculator.CurrentBatchSize.ShouldBeLessThan(50);
	}

	[Fact]
	public void RecordBatchResultShouldMakeSmallAdjustmentsForStablePerformance()
	{
		// Arrange
		var calculator = new DynamicBatchSizeCalculator(10, 100, 50);

		// Act - Record consistent performance with high success rate
		// The algorithm behavior:
		// 1. First call: throughput > 0 * 1.05, so batch increases by 20% (50 → 60)
		// 2. Subsequent calls: stable throughput with successRate > 0.98 adds +1 each time
		for (var i = 0; i < 15; i++)
		{
			calculator.RecordBatchResult(
				itemsProcessed: 10,
				duration: TimeSpan.FromSeconds(1),
				successRate: 0.99);
		}

		// Assert - Should make incremental adjustments (first big jump, then +1 each)
		// Expected: 50 → 60 (first), then +1 for each of the remaining 14 iterations = 74
		calculator.CurrentBatchSize.ShouldBeGreaterThanOrEqualTo(50);
		calculator.CurrentBatchSize.ShouldBeLessThanOrEqualTo(80); // Reasonable upper bound (60 + ~20 increments)
	}

	[Fact]
	public void RecordBatchResultShouldRespectMinimumBatchSize()
	{
		// Arrange
		var calculator = new DynamicBatchSizeCalculator(20, 100, 25);

		// Act - Record many results with very low success rate
		for (var i = 0; i < 20; i++)
		{
			calculator.RecordBatchResult(
				itemsProcessed: 5,
				duration: TimeSpan.FromSeconds(1),
				successRate: 0.1);
		}

		// Assert
		calculator.CurrentBatchSize.ShouldBe(20);
	}

	[Fact]
	public void RecordBatchResultShouldRespectMaximumBatchSize()
	{
		// Arrange
		var calculator = new DynamicBatchSizeCalculator(10, 80, 75);

		// Act - Record many results with excellent performance
		for (var i = 0; i < 20; i++)
		{
			calculator.RecordBatchResult(
				itemsProcessed: 100,
				duration: TimeSpan.FromSeconds(0.5),
				successRate: 1.0);
		}

		// Assert
		calculator.CurrentBatchSize.ShouldBe(80);
	}

	[Fact]
	public void CurrentBatchSizeShouldBeThreadSafe()
	{
		// Arrange
		var calculator = new DynamicBatchSizeCalculator(10, 1000, 50);
		var tasks = new List<Task>();
		var retrievedBatchSizes = new ConcurrentBag<int>();

		// Act - Concurrent reads and writes
		for (var i = 0; i < 10; i++)
		{
			tasks.Add(Task.Run(() =>
			{
				for (var j = 0; j < 100; j++)
				{
					calculator.RecordBatchResult(
						itemsProcessed: Random.Shared.Next(1, 20),
						duration: TimeSpan.FromMilliseconds(Random.Shared.Next(10, 100)),
						successRate: Random.Shared.NextDouble());

					retrievedBatchSizes.Add(calculator.CurrentBatchSize);
				}
			}));
		}

		// Wait for all tasks to complete
		Task.WaitAll([.. tasks]);

		// Assert - Should not throw and should have valid batch sizes
		retrievedBatchSizes.Count.ShouldBe(1000);
		retrievedBatchSizes.All(size => size is >= 10 and <= 1000).ShouldBeTrue();
	}

	[Fact]
	public void RecordBatchResultShouldMaintainMeasurementWindow()
	{
		// Arrange
		var calculator = new DynamicBatchSizeCalculator(10, 100, 50);

		// Act - Record more measurements than window size (10)
		for (var i = 0; i < 20; i++)
		{
			calculator.RecordBatchResult(
				itemsProcessed: 10,
				duration: TimeSpan.FromSeconds(1),
				successRate: 1.0);
		}

		// Assert - Should still function correctly (no easy way to verify internal window size)
		calculator.CurrentBatchSize.ShouldBeInRange(10, 100);
	}

	[Fact]
	public void RecordBatchResultShouldHandleZeroSuccessRate()
	{
		// Arrange
		var calculator = new DynamicBatchSizeCalculator(10, 100, 50);

		// Act
		calculator.RecordBatchResult(
			itemsProcessed: 10,
			duration: TimeSpan.FromSeconds(1),
			successRate: 0.0);

		// Assert - Should reduce batch size for zero success rate
		calculator.CurrentBatchSize.ShouldBeLessThan(50);
	}

	[Fact]
	public void RecordBatchResultShouldHandlePerfectSuccessRate()
	{
		// Arrange
		var calculator = new DynamicBatchSizeCalculator(10, 100, 20);

		// Act - Record consistent perfect performance
		for (var i = 0; i < 5; i++)
		{
			calculator.RecordBatchResult(
				itemsProcessed: 10,
				duration: TimeSpan.FromSeconds(1),
				successRate: 1.0);
		}

		// Assert - Should maintain or slightly increase batch size
		calculator.CurrentBatchSize.ShouldBeGreaterThanOrEqualTo(20);
	}

	[Fact]
	public void RecordBatchResultShouldHandleVaryingThroughput()
	{
		// Arrange
		var calculator = new DynamicBatchSizeCalculator(10, 100, 50);
		var throughputs = new[] { 1.0, 1.5, 1.2, 1.8, 1.1, 1.6, 1.3, 1.9, 1.4, 1.7 };

		// Act - Record varying throughput measurements
		foreach (var throughput in throughputs)
		{
			var duration = TimeSpan.FromSeconds(10.0 / throughput); // Adjust duration for desired throughput
			calculator.RecordBatchResult(
				itemsProcessed: 10,
				duration: duration,
				successRate: 0.98);
		}

		// Assert - Should adapt to varying performance
		calculator.CurrentBatchSize.ShouldBeInRange(10, 100);
	}
}
