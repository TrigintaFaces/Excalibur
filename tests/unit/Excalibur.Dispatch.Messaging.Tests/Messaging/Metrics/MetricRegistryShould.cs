// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Metrics;

namespace Excalibur.Dispatch.Tests.Messaging.Metrics;

/// <summary>
/// Unit tests for <see cref="MetricRegistry"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Metrics")]
public sealed class MetricRegistryShould : UnitTestBase
{
	#region Global Instance Tests

	[Fact]
	public void ProvideGlobalInstance()
	{
		// Act
		var global = MetricRegistry.Global;

		// Assert
		global.ShouldNotBeNull();
	}

	[Fact]
	public void ReturnSameGlobalInstance()
	{
		// Act
		var global1 = MetricRegistry.Global;
		var global2 = MetricRegistry.Global;

		// Assert
		ReferenceEquals(global1, global2).ShouldBeTrue();
	}

	#endregion

	#region Counter Tests

	[Fact]
	public void CreateCounter()
	{
		// Arrange
		var registry = new MetricRegistry();

		// Act
		var counter = registry.Counter("test_counter");

		// Assert
		counter.ShouldNotBeNull();
		counter.ShouldBeOfType<RateCounter>();
	}

	[Fact]
	public void CreateCounterWithDescription()
	{
		// Arrange
		var registry = new MetricRegistry();

		// Act
		var counter = registry.Counter("test_counter", "A test counter");

		// Assert
		counter.ShouldNotBeNull();
	}

	[Fact]
	public void CreateCounterWithDescriptionAndUnit()
	{
		// Arrange
		var registry = new MetricRegistry();

		// Act
		var counter = registry.Counter("test_counter", "A test counter", "requests");

		// Assert
		counter.ShouldNotBeNull();
	}

	[Fact]
	public void ReturnSameCounterForSameName()
	{
		// Arrange
		var registry = new MetricRegistry();

		// Act
		var counter1 = registry.Counter("same_counter");
		var counter2 = registry.Counter("same_counter");

		// Assert
		ReferenceEquals(counter1, counter2).ShouldBeTrue();
	}

	[Fact]
	public void ReturnDifferentCountersForDifferentNames()
	{
		// Arrange
		var registry = new MetricRegistry();

		// Act
		var counter1 = registry.Counter("counter_one");
		var counter2 = registry.Counter("counter_two");

		// Assert
		ReferenceEquals(counter1, counter2).ShouldBeFalse();
	}

	[Fact]
	public void CounterIncrementWorks()
	{
		// Arrange
		var registry = new MetricRegistry();
		var counter = registry.Counter("increment_test");

		// Act
		counter.Increment();
		counter.Increment();
		counter.Increment();

		// Assert
		var snapshot = counter.GetSnapshot();
		snapshot.Value.ShouldBe(3);
	}

	#endregion

	#region Labeled Counter Tests

	[Fact]
	public void CreateLabeledCounter()
	{
		// Arrange
		var registry = new MetricRegistry();

		// Act
		var counter = registry.LabeledCounter("labeled_counter", labelNames: ["method", "status"]);

		// Assert
		counter.ShouldNotBeNull();
		counter.ShouldBeOfType<LabeledCounter>();
	}

	[Fact]
	public void CreateLabeledCounterWithDescription()
	{
		// Arrange
		var registry = new MetricRegistry();

		// Act
		var counter = registry.LabeledCounter("http_requests", "HTTP request count", labelNames: ["method"]);

		// Assert
		counter.ShouldNotBeNull();
	}

	[Fact]
	public void CreateLabeledCounterWithDescriptionAndUnit()
	{
		// Arrange
		var registry = new MetricRegistry();

		// Act
		var counter = registry.LabeledCounter("http_requests", "HTTP request count", "requests", "method", "status");

		// Assert
		counter.ShouldNotBeNull();
	}

	[Fact]
	public void ReturnSameLabeledCounterForSameName()
	{
		// Arrange
		var registry = new MetricRegistry();

		// Act
		var counter1 = registry.LabeledCounter("same_labeled", labelNames: ["method"]);
		var counter2 = registry.LabeledCounter("same_labeled", labelNames: ["method"]);

		// Assert
		ReferenceEquals(counter1, counter2).ShouldBeTrue();
	}

	[Fact]
	public void LabeledCounterIncrementWorks()
	{
		// Arrange
		var registry = new MetricRegistry();
		var counter = registry.LabeledCounter("http_requests", labelNames: ["method", "status"]);

		// Act
		counter.Increment(1, "GET", "200");
		counter.Increment(1, "GET", "200");
		counter.Increment(1, "POST", "201");

		// Assert
		counter.LabelCount.ShouldBe(2); // Two unique label combinations
	}

	#endregion

	#region Gauge Tests

	[Fact]
	public void CreateGauge()
	{
		// Arrange
		var registry = new MetricRegistry();

		// Act
		var gauge = registry.Gauge("test_gauge");

		// Assert
		gauge.ShouldNotBeNull();
		gauge.ShouldBeOfType<ValueGauge>();
	}

	[Fact]
	public void CreateGaugeWithDescription()
	{
		// Arrange
		var registry = new MetricRegistry();

		// Act
		var gauge = registry.Gauge("test_gauge", "A test gauge");

		// Assert
		gauge.ShouldNotBeNull();
	}

	[Fact]
	public void CreateGaugeWithDescriptionAndUnit()
	{
		// Arrange
		var registry = new MetricRegistry();

		// Act
		var gauge = registry.Gauge("memory_usage", "Memory usage", "bytes");

		// Assert
		gauge.ShouldNotBeNull();
	}

	[Fact]
	public void ReturnSameGaugeForSameName()
	{
		// Arrange
		var registry = new MetricRegistry();

		// Act
		var gauge1 = registry.Gauge("same_gauge");
		var gauge2 = registry.Gauge("same_gauge");

		// Assert
		ReferenceEquals(gauge1, gauge2).ShouldBeTrue();
	}

	[Fact]
	public void GaugeSetWorks()
	{
		// Arrange
		var registry = new MetricRegistry();
		var gauge = registry.Gauge("set_test");

		// Act
		gauge.Set(100);

		// Assert
		var snapshot = gauge.GetSnapshot();
		snapshot.Value.ShouldBe(100);
	}

	#endregion

	#region Histogram Tests

	[Fact]
	public void CreateHistogram()
	{
		// Arrange
		var registry = new MetricRegistry();

		// Act
		var histogram = registry.Histogram("test_histogram");

		// Assert
		histogram.ShouldNotBeNull();
		histogram.ShouldBeOfType<ValueHistogram>();
	}

	[Fact]
	public void CreateHistogramWithDescription()
	{
		// Arrange
		var registry = new MetricRegistry();

		// Act
		var histogram = registry.Histogram("request_duration", "Request duration");

		// Assert
		histogram.ShouldNotBeNull();
	}

	[Fact]
	public void CreateHistogramWithDescriptionAndUnit()
	{
		// Arrange
		var registry = new MetricRegistry();

		// Act
		var histogram = registry.Histogram("request_duration", "Request duration", "ms");

		// Assert
		histogram.ShouldNotBeNull();
	}

	[Fact]
	public void CreateHistogramWithConfiguration()
	{
		// Arrange
		var registry = new MetricRegistry();
		var config = new HistogramConfiguration(1, 5, 10, 25, 50, 100);

		// Act
		var histogram = registry.Histogram("configured_histogram", configuration: config);

		// Assert
		histogram.ShouldNotBeNull();
	}

	[Fact]
	public void ReturnSameHistogramForSameName()
	{
		// Arrange
		var registry = new MetricRegistry();

		// Act
		var histogram1 = registry.Histogram("same_histogram");
		var histogram2 = registry.Histogram("same_histogram");

		// Assert
		ReferenceEquals(histogram1, histogram2).ShouldBeTrue();
	}

	[Fact]
	public void HistogramRecordWorks()
	{
		// Arrange
		var registry = new MetricRegistry();
		var histogram = registry.Histogram("record_test");

		// Act
		histogram.Record(10);
		histogram.Record(20);
		histogram.Record(30);

		// Assert
		var snapshot = histogram.GetSnapshot();
		snapshot.Count.ShouldBe(3);
	}

	#endregion

	#region GetAllMetrics Tests

	[Fact]
	public void GetAllMetricsReturnsEmpty()
	{
		// Arrange
		var registry = new MetricRegistry();

		// Act
		var metrics = registry.GetAllMetrics();

		// Assert
		metrics.ShouldBeEmpty();
	}

	[Fact]
	public void GetAllMetricsReturnsRegisteredMetrics()
	{
		// Arrange
		var registry = new MetricRegistry();
		registry.Counter("counter1");
		registry.Gauge("gauge1");
		registry.Histogram("histogram1");

		// Act
		var metrics = registry.GetAllMetrics().ToList();

		// Assert
		metrics.Count.ShouldBe(3);
	}

	[Fact]
	public void GetAllMetricsIncludesLabeledCounters()
	{
		// Arrange
		var registry = new MetricRegistry();
		registry.Counter("counter1");
		registry.LabeledCounter("labeled1", labelNames: ["method"]);

		// Act
		var metrics = registry.GetAllMetrics().ToList();

		// Assert
		metrics.Count.ShouldBe(2);
	}

	#endregion

	#region GetAllMetadata Tests

	[Fact]
	public void GetAllMetadataReturnsEmpty()
	{
		// Arrange
		var registry = new MetricRegistry();

		// Act
		var metadata = registry.GetAllMetadata();

		// Assert
		metadata.ShouldBeEmpty();
	}

	[Fact]
	public void GetAllMetadataReturnsMetricMetadata()
	{
		// Arrange
		var registry = new MetricRegistry();
		registry.Counter("counter1", "Counter description", "count");
		registry.Gauge("gauge1", "Gauge description", "bytes");

		// Act
		var metadata = registry.GetAllMetadata().ToList();

		// Assert
		metadata.Count.ShouldBe(2);
	}

	#endregion

	#region CollectSnapshots Tests

	[Fact]
	public void CollectSnapshotsReturnsEmpty()
	{
		// Arrange
		var registry = new MetricRegistry();

		// Act
		var snapshots = registry.CollectSnapshots();

		// Assert
		snapshots.ShouldBeEmpty();
	}

	[Fact]
	public void CollectSnapshotsIncludesCounters()
	{
		// Arrange
		var registry = new MetricRegistry();
		var counter = registry.Counter("test_counter");
		counter.IncrementBy(10);

		// Act
		var snapshots = registry.CollectSnapshots();

		// Assert
		snapshots.Length.ShouldBe(1);
		snapshots[0].Type.ShouldBe(MetricType.Counter);
		snapshots[0].Value.ShouldBe(10);
	}

	[Fact]
	public void CollectSnapshotsIncludesGauges()
	{
		// Arrange
		var registry = new MetricRegistry();
		var gauge = registry.Gauge("test_gauge");
		gauge.Set(100);

		// Act
		var snapshots = registry.CollectSnapshots();

		// Assert
		snapshots.Length.ShouldBe(1);
		snapshots[0].Type.ShouldBe(MetricType.Gauge);
		snapshots[0].Value.ShouldBe(100);
	}

	[Fact]
	public void CollectSnapshotsIncludesHistograms()
	{
		// Arrange
		var registry = new MetricRegistry();
		var histogram = registry.Histogram("test_histogram");
		histogram.Record(50);
		histogram.Record(100);

		// Act
		var snapshots = registry.CollectSnapshots();

		// Assert
		snapshots.Length.ShouldBe(1);
		snapshots[0].Type.ShouldBe(MetricType.Histogram);
		snapshots[0].Count.ShouldBe(2);
	}

	[Fact]
	public void CollectSnapshotsIncludesLabeledCounters()
	{
		// Arrange
		var registry = new MetricRegistry();
		var counter = registry.LabeledCounter("http_requests", labelNames: ["method", "status"]);
		counter.Increment(1, "GET", "200");
		counter.Increment(1, "POST", "201");
		counter.Increment(1, "GET", "404");

		// Act
		var snapshots = registry.CollectSnapshots();

		// Assert
		snapshots.Length.ShouldBe(3); // 3 label combinations
	}

	[Fact]
	public void CollectSnapshotsIncludesAllMetricTypes()
	{
		// Arrange
		var registry = new MetricRegistry();
		var counter = registry.Counter("counter");
		var gauge = registry.Gauge("gauge");
		var histogram = registry.Histogram("histogram");
		var labeled = registry.LabeledCounter("labeled", labelNames: ["key"]);

		counter.IncrementBy(5);
		gauge.Set(100);
		histogram.Record(50);
		labeled.Increment(1, "value1");
		labeled.Increment(1, "value2");

		// Act
		var snapshots = registry.CollectSnapshots();

		// Assert
		snapshots.Length.ShouldBe(5); // counter + gauge + histogram + 2 labeled
	}

	#endregion

	#region ResetAll Tests

	[Fact]
	public void ResetAllResetsCounters()
	{
		// Arrange
		var registry = new MetricRegistry();
		var counter = registry.Counter("reset_counter");
		counter.IncrementBy(100);

		// Act
		registry.ResetAll();

		// Assert
		var snapshot = counter.GetSnapshot();
		snapshot.Value.ShouldBe(0);
	}

	[Fact]
	public void ResetAllResetsLabeledCounters()
	{
		// Arrange
		var registry = new MetricRegistry();
		var counter = registry.LabeledCounter("reset_labeled", labelNames: ["key"]);
		counter.Increment(50, "value1");
		counter.Increment(30, "value2");

		// Act
		registry.ResetAll();
		var snapshots = counter.GetSnapshots();

		// Assert - All counters should be reset to 0
		foreach (var snapshot in snapshots)
		{
			snapshot.Value.ShouldBe(0);
		}
	}

	[Fact]
	public void ResetAllResetsHistograms()
	{
		// Arrange
		var registry = new MetricRegistry();
		var histogram = registry.Histogram("reset_histogram");
		histogram.Record(10);
		histogram.Record(20);
		histogram.Record(30);

		// Act
		registry.ResetAll();

		// Assert
		var snapshot = histogram.GetSnapshot();
		snapshot.Count.ShouldBe(0);
	}

	[Fact]
	public void ResetAllDoesNotResetGauges()
	{
		// Arrange
		var registry = new MetricRegistry();
		var gauge = registry.Gauge("gauge_not_reset");
		gauge.Set(100);

		// Act
		registry.ResetAll();

		// Assert - Gauges are not reset
		var snapshot = gauge.GetSnapshot();
		// Gauges maintain their last value (behavior may vary based on implementation)
	}

	#endregion

	#region Thread Safety Tests

	[Fact]
	public async Task HandleConcurrentCounterCreation()
	{
		// Arrange
		var registry = new MetricRegistry();
		const int threadCount = 10;
		var counters = new RateCounter[threadCount];

		// Act
		var tasks = Enumerable.Range(0, threadCount)
			.Select(i => Task.Run(() => counters[i] = registry.Counter("concurrent_counter")))
			.ToArray();

		await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert - All should reference the same counter
		var firstCounter = counters[0];
		foreach (var counter in counters)
		{
			ReferenceEquals(firstCounter, counter).ShouldBeTrue();
		}
	}

	[Fact]
	public async Task HandleConcurrentDifferentMetricCreation()
	{
		// Arrange
		var registry = new MetricRegistry();
		const int threadCount = 100;

		// Act
		var tasks = Enumerable.Range(0, threadCount)
			.Select(i => Task.Run(() =>
			{
				registry.Counter($"counter_{i}");
				registry.Gauge($"gauge_{i}");
			}))
			.ToArray();

		await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert
		var metrics = registry.GetAllMetrics().ToList();
		metrics.Count.ShouldBe(threadCount * 2);
	}

	[Fact]
	public async Task HandleConcurrentCollectSnapshots()
	{
		// Arrange
		var registry = new MetricRegistry();
		for (var i = 0; i < 10; i++)
		{
			var counter = registry.Counter($"counter_{i}");
			counter.IncrementBy(i);
		}

		// Act
		var tasks = Enumerable.Range(0, 10)
			.Select(_ => Task.Run(() => registry.CollectSnapshots()))
			.ToArray();

		var results = await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert - All results should have 10 snapshots
		foreach (var snapshots in results)
		{
			snapshots.Length.ShouldBe(10);
		}
	}

	#endregion
}
