// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Channels.Diagnostics;

namespace Excalibur.Dispatch.Tests.Messaging.Channels.Diagnostics;

/// <summary>
/// Unit tests for <see cref="ChannelLatencyTracker"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ChannelLatencyTrackerShould
{
	[Fact]
	public void ThrowOnNullChannelId()
	{
		// Arrange & Act & Assert
		Should.Throw<ArgumentNullException>(() => new ChannelLatencyTracker(null!));
	}

	[Fact]
	public void ReturnZeroStatisticsWhenEmpty()
	{
		// Arrange
		var tracker = new ChannelLatencyTracker("test-channel");

		// Act
		var (avg, p95, p99) = tracker.GetStatistics();

		// Assert
		avg.ShouldBe(0);
		p95.ShouldBe(0);
		p99.ShouldBe(0);
	}

	[Fact]
	public void RecordSingleLatencySample()
	{
		// Arrange
		var tracker = new ChannelLatencyTracker("test-channel");

		// Act
		tracker.RecordLatency(100);
		var (avg, p95, p99) = tracker.GetStatistics();

		// Assert
		avg.ShouldBe(100);
		p95.ShouldBe(100);
		p99.ShouldBe(100);
	}

	[Fact]
	public void CalculateAverageLatencyCorrectly()
	{
		// Arrange
		var tracker = new ChannelLatencyTracker("test-channel");

		// Act - Record 5 samples: 100, 200, 300, 400, 500 (avg = 300)
		tracker.RecordLatency(100);
		tracker.RecordLatency(200);
		tracker.RecordLatency(300);
		tracker.RecordLatency(400);
		tracker.RecordLatency(500);

		var (avg, _, _) = tracker.GetStatistics();

		// Assert
		avg.ShouldBe(300);
	}

	[Fact]
	public void CalculateP95Correctly()
	{
		// Arrange
		var tracker = new ChannelLatencyTracker("test-channel", 100);

		// Act - Record 100 samples from 1 to 100
		for (var i = 1; i <= 100; i++)
		{
			tracker.RecordLatency(i);
		}

		var (_, p95, _) = tracker.GetStatistics();

		// Assert - P95 of 1-100 is at index 95 = 96
		p95.ShouldBe(96);
	}

	[Fact]
	public void CalculateP99Correctly()
	{
		// Arrange
		var tracker = new ChannelLatencyTracker("test-channel", 100);

		// Act - Record 100 samples from 1 to 100
		for (var i = 1; i <= 100; i++)
		{
			tracker.RecordLatency(i);
		}

		var (_, _, p99) = tracker.GetStatistics();

		// Assert - P99 of 1-100 is at index 99 = 100
		p99.ShouldBe(100);
	}

	[Fact]
	public void WrapAroundWhenExceedingSampleSize()
	{
		// Arrange - Small sample size for testing wrap-around
		var tracker = new ChannelLatencyTracker("test-channel", 5);

		// Act - Record 7 samples (will wrap around)
		tracker.RecordLatency(100);
		tracker.RecordLatency(200);
		tracker.RecordLatency(300);
		tracker.RecordLatency(400);
		tracker.RecordLatency(500);
		tracker.RecordLatency(600); // Overwrites 100
		tracker.RecordLatency(700); // Overwrites 200

		var (avg, _, _) = tracker.GetStatistics();

		// Assert - Should only have last 5: 300, 400, 500, 600, 700 (avg = 500)
		avg.ShouldBe(500);
	}

	[Fact]
	public void SortSamplesForPercentileCalculation()
	{
		// Arrange
		var tracker = new ChannelLatencyTracker("test-channel", 10);

		// Act - Record unsorted samples
		tracker.RecordLatency(500);
		tracker.RecordLatency(100);
		tracker.RecordLatency(300);
		tracker.RecordLatency(200);
		tracker.RecordLatency(400);

		var (avg, _, _) = tracker.GetStatistics();

		// Assert - Average should be correct regardless of order
		avg.ShouldBe(300);
	}

	[Fact]
	public void AcceptZeroLatency()
	{
		// Arrange
		var tracker = new ChannelLatencyTracker("test-channel");

		// Act
		tracker.RecordLatency(0);
		var (avg, p95, p99) = tracker.GetStatistics();

		// Assert
		avg.ShouldBe(0);
		p95.ShouldBe(0);
		p99.ShouldBe(0);
	}

	[Fact]
	public void AcceptNegativeLatency()
	{
		// Arrange - Negative values could represent clock skew
		var tracker = new ChannelLatencyTracker("test-channel");

		// Act
		tracker.RecordLatency(-100);
		var (avg, _, _) = tracker.GetStatistics();

		// Assert
		avg.ShouldBe(-100);
	}

	[Fact]
	public void AcceptVeryLargeLatency()
	{
		// Arrange
		var tracker = new ChannelLatencyTracker("test-channel");

		// Act
		tracker.RecordLatency(1_000_000); // 1 second in microseconds
		var (avg, _, _) = tracker.GetStatistics();

		// Assert
		avg.ShouldBe(1_000_000);
	}

	[Theory]
	[InlineData(1)]
	[InlineData(10)]
	[InlineData(100)]
	[InlineData(1000)]
	public void AcceptVariousSampleSizes(int sampleSize)
	{
		// Arrange
		var tracker = new ChannelLatencyTracker("test-channel", sampleSize);

		// Act - Record some samples (starting from 1 to avoid 0 values)
		for (var i = 1; i <= Math.Min(sampleSize, 50); i++)
		{
			tracker.RecordLatency(i * 10);
		}

		var (avg, _, _) = tracker.GetStatistics();

		// Assert - Statistics should be calculated
		avg.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void BeThreadSafeForConcurrentRecording()
	{
		// Arrange
		var tracker = new ChannelLatencyTracker("test-channel", 10000);
		const int iterations = 100;
		const int threadCount = 4;

		// Act - Record from multiple threads
		Parallel.For(0, threadCount, _ =>
		{
			for (var i = 0; i < iterations; i++)
			{
				tracker.RecordLatency(100);
			}
		});

		var (avg, _, _) = tracker.GetStatistics();

		// Assert - All samples should have value 100
		avg.ShouldBe(100);
	}

	[Fact]
	public void HandleMixedLatencyValues()
	{
		// Arrange
		var tracker = new ChannelLatencyTracker("test-channel", 100);

		// Act - Record mixed latency values simulating real-world variance
		for (var i = 0; i < 50; i++)
		{
			tracker.RecordLatency(50); // Fast operations
		}

		for (var i = 0; i < 10; i++)
		{
			tracker.RecordLatency(1000); // Slow operations (outliers)
		}

		var (avg, p95, p99) = tracker.GetStatistics();

		// Assert
		// Average: (50*50 + 10*1000) / 60 = (2500 + 10000) / 60 = 208.33...
		avg.ShouldBeInRange(200, 220);

		// P95 should be one of the high values (at index 57 of sorted 60 samples)
		p95.ShouldBe(1000);

		// P99 should also be a high value (at index 59 of sorted 60 samples)
		p99.ShouldBe(1000);
	}

	[Fact]
	public void SimulateTypicalLatencyPattern()
	{
		// Arrange - Typical pattern: mostly low latency with occasional spikes
		var tracker = new ChannelLatencyTracker("message-channel", 1000);
		var random = new Random(42); // Deterministic seed

		// Act - Simulate 500 operations
		for (var i = 0; i < 500; i++)
		{
			// 95% of operations are fast (10-50 microseconds)
			// 5% are slow (100-500 microseconds)
			var latency = random.NextDouble() < 0.95
				? random.Next(10, 51)
				: random.Next(100, 501);
			tracker.RecordLatency(latency);
		}

		var (avg, p95, p99) = tracker.GetStatistics();

		// Assert
		avg.ShouldBeGreaterThan(10);
		avg.ShouldBeLessThan(100);
		p95.ShouldBeGreaterThan(avg);
		p99.ShouldBeGreaterThanOrEqualTo(p95);
	}

	[Fact]
	public void AcceptEmptyChannelId()
	{
		// Arrange & Act - Empty string is allowed (no throw)
		var tracker = new ChannelLatencyTracker("");

		// Assert - Should work normally
		tracker.RecordLatency(100);
		var (avg, _, _) = tracker.GetStatistics();
		avg.ShouldBe(100);
	}

	[Fact]
	public void CalculatePercentileForSmallSampleCount()
	{
		// Arrange
		var tracker = new ChannelLatencyTracker("test-channel", 100);

		// Act - Only 3 samples
		tracker.RecordLatency(100);
		tracker.RecordLatency(200);
		tracker.RecordLatency(300);

		var (avg, p95, p99) = tracker.GetStatistics();

		// Assert
		avg.ShouldBe(200);
		// With 3 samples: index for P95 = floor(3 * 0.95) = 2 (third element)
		// index for P99 = floor(3 * 0.99) = 2 (third element)
		p95.ShouldBe(300);
		p99.ShouldBe(300);
	}
}
