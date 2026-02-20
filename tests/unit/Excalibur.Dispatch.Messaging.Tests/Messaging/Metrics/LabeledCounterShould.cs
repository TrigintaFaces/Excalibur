// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Metrics;

namespace Excalibur.Dispatch.Tests.Messaging.Metrics;

/// <summary>
/// Unit tests for <see cref="LabeledCounter"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Metrics")]
public sealed class LabeledCounterShould : UnitTestBase
{
	#region Constructor Tests

	[Fact]
	public void CreateWithValidMetadata()
	{
		// Arrange
		var metadata = new MetricMetadata(1, "requests_total", "Total requests", "requests", MetricType.Counter);

		// Act
		var counter = new LabeledCounter(metadata);

		// Assert
		counter.Metadata.ShouldBe(metadata);
		counter.LabelCount.ShouldBe(0);
	}

	[Fact]
	public void ThrowOnNullMetadata()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new LabeledCounter(null!));
	}

	[Fact]
	public void ThrowOnNonCounterMetadata()
	{
		// Arrange
		var gaugeMetadata = new MetricMetadata(1, "temperature", null, null, MetricType.Gauge);

		// Act & Assert
		Should.Throw<ArgumentException>(() => new LabeledCounter(gaugeMetadata));
	}

	[Fact]
	public void ThrowOnHistogramMetadata()
	{
		// Arrange
		var histogramMetadata = new MetricMetadata(1, "latency", null, null, MetricType.Histogram);

		// Act & Assert
		Should.Throw<ArgumentException>(() => new LabeledCounter(histogramMetadata));
	}

	[Fact]
	public void ThrowOnSummaryMetadata()
	{
		// Arrange
		var summaryMetadata = new MetricMetadata(1, "summary", null, null, MetricType.Summary);

		// Act & Assert
		Should.Throw<ArgumentException>(() => new LabeledCounter(summaryMetadata));
	}

	#endregion

	#region Increment Tests

	[Fact]
	public void IncrementByDefaultValue()
	{
		// Arrange
		var metadata = new MetricMetadata(1, "requests_total", null, null, MetricType.Counter);
		var counter = new LabeledCounter(metadata);

		// Act
		counter.Increment(1, "GET", "/api/users");

		// Assert
		counter.LabelCount.ShouldBe(1);
		var snapshots = counter.GetSnapshots();
		snapshots.Length.ShouldBe(1);
		snapshots[0].Value.ShouldBe(1);
	}

	[Fact]
	public void IncrementBySpecificValue()
	{
		// Arrange
		var metadata = new MetricMetadata(1, "bytes_total", null, null, MetricType.Counter);
		var counter = new LabeledCounter(metadata);

		// Act
		counter.Increment(1024, "download");

		// Assert
		var snapshots = counter.GetSnapshots();
		snapshots[0].Value.ShouldBe(1024);
	}

	[Fact]
	public void IncrementMultipleTimes()
	{
		// Arrange
		var metadata = new MetricMetadata(1, "requests_total", null, null, MetricType.Counter);
		var counter = new LabeledCounter(metadata);

		// Act
		counter.Increment(1, "GET", "/api/users");
		counter.Increment(1, "GET", "/api/users");
		counter.Increment(1, "GET", "/api/users");

		// Assert
		var snapshots = counter.GetSnapshots();
		snapshots[0].Value.ShouldBe(3);
	}

	[Fact]
	public void IncrementWithDifferentLabels()
	{
		// Arrange
		var metadata = new MetricMetadata(1, "requests_total", null, null, MetricType.Counter, "method", "path");
		var counter = new LabeledCounter(metadata);

		// Act
		counter.Increment(1, "GET", "/api/users");
		counter.Increment(1, "POST", "/api/users");
		counter.Increment(1, "GET", "/api/orders");

		// Assert
		counter.LabelCount.ShouldBe(3);
	}

	[Fact]
	public void ThrowOnNegativeIncrement()
	{
		// Arrange
		var metadata = new MetricMetadata(1, "requests_total", null, null, MetricType.Counter);
		var counter = new LabeledCounter(metadata);

		// Act & Assert
		Should.Throw<ArgumentException>(() => counter.Increment(-1));
	}

	[Fact]
	public void AllowZeroIncrement()
	{
		// Arrange
		var metadata = new MetricMetadata(1, "requests_total", null, null, MetricType.Counter);
		var counter = new LabeledCounter(metadata);

		// Act
		counter.Increment(0, "test");

		// Assert - should not throw and counter should exist
		counter.LabelCount.ShouldBe(1);
	}

	[Fact]
	public void IncrementWithNoLabels()
	{
		// Arrange
		var metadata = new MetricMetadata(1, "requests_total", null, null, MetricType.Counter);
		var counter = new LabeledCounter(metadata);

		// Act
		counter.Increment();
		counter.Increment();

		// Assert
		var snapshots = counter.GetSnapshots();
		snapshots.Length.ShouldBe(1);
		snapshots[0].Value.ShouldBe(2);
	}

	#endregion

	#region GetSnapshots Tests

	[Fact]
	public void GetSnapshotsReturnEmptyArrayWhenNoData()
	{
		// Arrange
		var metadata = new MetricMetadata(1, "requests_total", null, null, MetricType.Counter);
		var counter = new LabeledCounter(metadata);

		// Act
		var snapshots = counter.GetSnapshots();

		// Assert
		snapshots.ShouldBeEmpty();
	}

	[Fact]
	public void GetSnapshotsReturnCorrectMetricId()
	{
		// Arrange
		var metadata = new MetricMetadata(42, "requests_total", null, null, MetricType.Counter);
		var counter = new LabeledCounter(metadata);
		counter.Increment(1, "test");

		// Act
		var snapshots = counter.GetSnapshots();

		// Assert
		snapshots[0].MetricId.ShouldBe(42);
	}

	[Fact]
	public void GetSnapshotsReturnCounterType()
	{
		// Arrange
		var metadata = new MetricMetadata(1, "requests_total", null, null, MetricType.Counter);
		var counter = new LabeledCounter(metadata);
		counter.Increment(1, "test");

		// Act
		var snapshots = counter.GetSnapshots();

		// Assert
		snapshots[0].Type.ShouldBe(MetricType.Counter);
	}

	[Fact]
	public void GetSnapshotsReturnValidTimestamp()
	{
		// Arrange
		var metadata = new MetricMetadata(1, "requests_total", null, null, MetricType.Counter);
		var counter = new LabeledCounter(metadata);
		counter.Increment(1, "test");
		var beforeTicks = DateTime.UtcNow.Ticks;

		// Act
		var snapshots = counter.GetSnapshots();

		// Assert
		var afterTicks = DateTime.UtcNow.Ticks;
		snapshots[0].TimestampTicks.ShouldBeGreaterThanOrEqualTo(beforeTicks);
		snapshots[0].TimestampTicks.ShouldBeLessThanOrEqualTo(afterTicks);
	}

	[Fact]
	public void GetSnapshotsReturnAllLabelCombinations()
	{
		// Arrange
		var metadata = new MetricMetadata(1, "requests_total", null, null, MetricType.Counter, "method", "status");
		var counter = new LabeledCounter(metadata);
		counter.Increment(5, "GET", "200");
		counter.Increment(2, "POST", "201");
		counter.Increment(1, "GET", "404");

		// Act
		var snapshots = counter.GetSnapshots();

		// Assert
		snapshots.Length.ShouldBe(3);
		snapshots.Sum(s => (long)s.Value).ShouldBe(8);
	}

	[Fact]
	public void GetSnapshotsSetCountToOne()
	{
		// Arrange
		var metadata = new MetricMetadata(1, "requests_total", null, null, MetricType.Counter);
		var counter = new LabeledCounter(metadata);
		counter.Increment(100, "test");

		// Act
		var snapshots = counter.GetSnapshots();

		// Assert
		snapshots[0].Count.ShouldBe(1);
	}

	[Fact]
	public void GetSnapshotsSetSumToValue()
	{
		// Arrange
		var metadata = new MetricMetadata(1, "requests_total", null, null, MetricType.Counter);
		var counter = new LabeledCounter(metadata);
		counter.Increment(100, "test");

		// Act
		var snapshots = counter.GetSnapshots();

		// Assert
		snapshots[0].Sum.ShouldBe(100);
	}

	#endregion

	#region Reset Tests

	[Fact]
	public void ResetAllCounters()
	{
		// Arrange
		var metadata = new MetricMetadata(1, "requests_total", null, null, MetricType.Counter);
		var counter = new LabeledCounter(metadata);
		counter.Increment(5, "label1");
		counter.Increment(10, "label2");
		counter.Increment(15, "label3");

		// Act
		counter.Reset();

		// Assert
		var snapshots = counter.GetSnapshots();
		snapshots.All(s => s.Value == 0).ShouldBeTrue();
	}

	[Fact]
	public void ResetPreserveLabelCombinations()
	{
		// Arrange
		var metadata = new MetricMetadata(1, "requests_total", null, null, MetricType.Counter);
		var counter = new LabeledCounter(metadata);
		counter.Increment(5, "label1");
		counter.Increment(10, "label2");

		// Act
		counter.Reset();

		// Assert
		counter.LabelCount.ShouldBe(2);
	}

	[Fact]
	public void ResetAllowReincrement()
	{
		// Arrange
		var metadata = new MetricMetadata(1, "requests_total", null, null, MetricType.Counter);
		var counter = new LabeledCounter(metadata);
		counter.Increment(100, "test");
		counter.Reset();

		// Act
		counter.Increment(50, "test");

		// Assert
		var snapshots = counter.GetSnapshots();
		snapshots[0].Value.ShouldBe(50);
	}

	[Fact]
	public void ResetHandleEmptyCounter()
	{
		// Arrange
		var metadata = new MetricMetadata(1, "requests_total", null, null, MetricType.Counter);
		var counter = new LabeledCounter(metadata);

		// Act & Assert - should not throw
		Should.NotThrow(() => counter.Reset());
	}

	#endregion

	#region LabelCount Tests

	[Fact]
	public void LabelCountReturnZeroInitially()
	{
		// Arrange
		var metadata = new MetricMetadata(1, "requests_total", null, null, MetricType.Counter);
		var counter = new LabeledCounter(metadata);

		// Assert
		counter.LabelCount.ShouldBe(0);
	}

	[Fact]
	public void LabelCountIncreaseWithNewLabels()
	{
		// Arrange
		var metadata = new MetricMetadata(1, "requests_total", null, null, MetricType.Counter);
		var counter = new LabeledCounter(metadata);

		// Act
		counter.Increment(1, "label1");
		counter.Increment(1, "label2");
		counter.Increment(1, "label3");

		// Assert
		counter.LabelCount.ShouldBe(3);
	}

	[Fact]
	public void LabelCountNotIncreaseForSameLabels()
	{
		// Arrange
		var metadata = new MetricMetadata(1, "requests_total", null, null, MetricType.Counter);
		var counter = new LabeledCounter(metadata);

		// Act
		counter.Increment(1, "same");
		counter.Increment(1, "same");
		counter.Increment(1, "same");

		// Assert
		counter.LabelCount.ShouldBe(1);
	}

	#endregion

	#region Thread Safety Tests

	[Fact]
	public async Task SupportConcurrentIncrements()
	{
		// Arrange
		var metadata = new MetricMetadata(1, "requests_total", null, null, MetricType.Counter);
		var counter = new LabeledCounter(metadata);
		const long iterations = 1000L;
		const long threads = 10L;

		// Act
		var tasks = Enumerable.Range(0, (int)threads)
			.Select(unused => Task.Run(() =>
			{
				for (long i = 0; i < iterations; i++)
				{
					counter.Increment(1, "concurrent");
				}
			}));

		await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert
		var snapshots = counter.GetSnapshots();
		snapshots[0].Value.ShouldBe(iterations * threads);
	}

	[Fact]
	public async Task SupportConcurrentIncrementsWithDifferentLabels()
	{
		// Arrange
		var metadata = new MetricMetadata(1, "requests_total", null, null, MetricType.Counter, "thread");
		var counter = new LabeledCounter(metadata);
		const long iterations = 100L;
		const int threads = 10;

		// Act
		var tasks = Enumerable.Range(0, threads)
			.Select(threadId => Task.Run(() =>
			{
				for (long i = 0; i < iterations; i++)
				{
					counter.Increment(1, $"thread-{threadId}");
				}
			}));

		await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert
		counter.LabelCount.ShouldBe(threads);
		var snapshots = counter.GetSnapshots();
		snapshots.All(s => s.Value == iterations).ShouldBeTrue();
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void TrackHttpRequests()
	{
		// Arrange
		var metadata = new MetricMetadata(
			metricId: 100,
			name: "http_requests_total",
			description: "Total HTTP requests",
			unit: "requests",
			type: MetricType.Counter,
			labelNames: ["method", "path", "status"]);
		var counter = new LabeledCounter(metadata);

		// Act - Simulate request traffic
		counter.Increment(1, "GET", "/api/users", "200");
		counter.Increment(1, "GET", "/api/users", "200");
		counter.Increment(1, "GET", "/api/users", "200");
		counter.Increment(1, "POST", "/api/users", "201");
		counter.Increment(1, "GET", "/api/users", "404");

		// Assert
		counter.LabelCount.ShouldBe(3);
		var snapshots = counter.GetSnapshots();
		snapshots.Sum(s => (long)s.Value).ShouldBe(5);
	}

	[Fact]
	public void TrackBytesProcessed()
	{
		// Arrange
		var metadata = new MetricMetadata(
			metricId: 200,
			name: "bytes_processed_total",
			description: "Total bytes processed",
			unit: "bytes",
			type: MetricType.Counter,
			labelNames: ["direction"]);
		var counter = new LabeledCounter(metadata);

		// Act
		counter.Increment(1024, "inbound");
		counter.Increment(2048, "inbound");
		counter.Increment(512, "outbound");

		// Assert
		var snapshots = counter.GetSnapshots();
		snapshots.Length.ShouldBe(2);
	}

	#endregion
}
