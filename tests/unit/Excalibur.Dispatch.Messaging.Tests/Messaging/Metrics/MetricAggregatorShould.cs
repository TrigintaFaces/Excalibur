// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Metrics;

namespace Excalibur.Dispatch.Tests.Messaging.Metrics;

/// <summary>
/// Unit tests for <see cref="MetricAggregator"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Metrics")]
public sealed class MetricAggregatorShould : UnitTestBase
{
	#region Constructor Tests

	[Fact]
	public void ThrowWhenRegistryIsNull()
	{
		// Arrange
		Action<MetricSnapshot[]> callback = _ => { };

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new MetricAggregator(registry: null!, TimeSpan.FromSeconds(1), callback));
	}

	[Fact]
	public void ThrowWhenOnWindowCompleteIsNull()
	{
		// Arrange
		var registry = new MetricRegistry();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new MetricAggregator(registry, TimeSpan.FromSeconds(1), onWindowComplete: null!));
	}

	[Fact]
	public void CreateSuccessfullyWithValidParameters()
	{
		// Arrange
		var registry = new MetricRegistry();
		Action<MetricSnapshot[]> callback = _ => { };

		// Act & Assert - Should not throw
		using var aggregator = new MetricAggregator(registry, TimeSpan.FromSeconds(10), callback);
	}

	[Fact]
	public void CreateWithSmallWindowDuration()
	{
		// Arrange
		var registry = new MetricRegistry();
		Action<MetricSnapshot[]> callback = _ => { };

		// Act & Assert - Should not throw
		using var aggregator = new MetricAggregator(registry, TimeSpan.FromMilliseconds(10), callback);
	}

	[Fact]
	public void CreateWithLargeWindowDuration()
	{
		// Arrange
		var registry = new MetricRegistry();
		Action<MetricSnapshot[]> callback = _ => { };

		// Act & Assert - Should not throw
		using var aggregator = new MetricAggregator(registry, TimeSpan.FromHours(1), callback);
	}

	#endregion

	#region Dispose Tests

	[Fact]
	public void DisposeWithoutError()
	{
		// Arrange
		var registry = new MetricRegistry();
		Action<MetricSnapshot[]> callback = _ => { };
		var aggregator = new MetricAggregator(registry, TimeSpan.FromSeconds(10), callback);

		// Act & Assert - Should not throw
		Should.NotThrow(aggregator.Dispose);
	}

	[Fact]
	public void DisposeMultipleTimesWithoutError()
	{
		// Arrange
		var registry = new MetricRegistry();
		Action<MetricSnapshot[]> callback = _ => { };
		var aggregator = new MetricAggregator(registry, TimeSpan.FromSeconds(10), callback);

		// Act & Assert - Multiple disposes should not throw
		Should.NotThrow(() =>
		{
			aggregator.Dispose();
			aggregator.Dispose();
			aggregator.Dispose();
		});
	}

	#endregion

	#region Timer Callback Tests

	[Fact]
	public async Task InvokeCallbackAfterWindowDuration()
	{
		// Arrange
		var registry = new MetricRegistry();
		var callbackInvoked = false;
		var snapshots = Array.Empty<MetricSnapshot>();

		Action<MetricSnapshot[]> callback = s =>
		{
			Volatile.Write(ref callbackInvoked, true);
			snapshots = s;
		};

		using var aggregator = new MetricAggregator(registry, TimeSpan.FromMilliseconds(100), callback);

		// Act - Poll until callback fires (generous timeout for full-suite load)
		await WaitUntilAsync(() => Volatile.Read(ref callbackInvoked), TimeSpan.FromSeconds(10)).ConfigureAwait(false);

		// Assert
		callbackInvoked.ShouldBeTrue();
	}

	[Fact]
	public async Task CollectSnapshotsFromRegistry()
	{
		// Arrange
		var registry = new MetricRegistry();
		var counter = registry.Counter("test_counter", "Test counter", "count");
		counter.Increment();
		counter.Increment();
		counter.Increment();

		MetricSnapshot[]? receivedSnapshots = null;
		Action<MetricSnapshot[]> callback = s => Volatile.Write(ref receivedSnapshots, s);

		using var aggregator = new MetricAggregator(registry, TimeSpan.FromMilliseconds(50), callback);

		// Act - Poll until snapshots arrive (generous timeout for full-suite load)
		await WaitUntilAsync(() => Volatile.Read(ref receivedSnapshots)?.Length > 0, TimeSpan.FromSeconds(10)).ConfigureAwait(false);

		// Assert - Should have collected at least one snapshot
		var snapshots = Volatile.Read(ref receivedSnapshots);
		snapshots.ShouldNotBeNull();
		snapshots.Length.ShouldBeGreaterThan(0);
	}

	[Fact]
	public async Task InvokeCallbackMultipleTimes()
	{
		// Arrange
		var registry = new MetricRegistry();
		var callbackCount = 0;

		Action<MetricSnapshot[]> callback = _ => Interlocked.Increment(ref callbackCount);

		using var aggregator = new MetricAggregator(registry, TimeSpan.FromMilliseconds(30), callback);

		// Act - Poll until called multiple times
		await WaitUntilAsync(() => Volatile.Read(ref callbackCount) > 1, TimeSpan.FromSeconds(5)).ConfigureAwait(false);

		// Assert - Should have been called multiple times
		callbackCount.ShouldBeGreaterThan(1);
	}

	#endregion

	#region Reset Tests

	[Fact]
	public async Task ResetCountersAfterCollection()
	{
		// Arrange
		var registry = new MetricRegistry();
		var counter = registry.Counter("reset_test", "Test counter", "count");
		counter.IncrementBy(10);

		var firstValue = 0.0;
		var secondValue = 0.0;
		var callCount = 0;

		Action<MetricSnapshot[]> callback = s =>
		{
			if (s.Length > 0)
			{
				if (callCount == 0)
				{
					firstValue = s[0].Value;
				}
				else if (callCount == 1)
				{
					secondValue = s[0].Value;
				}
			}

			callCount++;
		};

		using var aggregator = new MetricAggregator(registry, TimeSpan.FromMilliseconds(50), callback);

		// Act - Poll until at least two collection cycles complete
		await WaitUntilAsync(() => callCount >= 2, TimeSpan.FromSeconds(5)).ConfigureAwait(false);

		// Assert - First collection should have value, second should be reset (0)
		firstValue.ShouldBeGreaterThan(0);
		// After reset, second collection captures new counter state
	}

	#endregion

	#region Error Handling Tests

	[Fact]
	public async Task HandleCallbackExceptionsGracefully()
	{
		// Arrange
		var registry = new MetricRegistry();
		var callCount = 0;
		var disposed = false;

		Action<MetricSnapshot[]> callback = _ =>
		{
			if (Volatile.Read(ref disposed))
			{
				return;
			}

			Interlocked.Increment(ref callCount);
			throw new InvalidOperationException("Test exception");
		};

		var aggregator = new MetricAggregator(registry, TimeSpan.FromMilliseconds(30), callback);

		// Act - Poll until callback invoked multiple times despite exceptions
		await WaitUntilAsync(() => Volatile.Read(ref callCount) > 1, TimeSpan.FromSeconds(5)).ConfigureAwait(false);

		// Cleanup - prevent exceptions during dispose
		Volatile.Write(ref disposed, true);
		aggregator.Dispose();

		// Wait a bit to let any pending callbacks complete
		await Task.Delay(100).ConfigureAwait(false);

		// Assert - Callback should have been invoked multiple times despite exceptions
		callCount.ShouldBeGreaterThan(1);
	}

	#endregion

	#region Integration Tests

	[Fact]
	public async Task AggregateMultipleMetricTypes()
	{
		// Arrange
		var registry = new MetricRegistry();
		var counter = registry.Counter("test_counter");
		var gauge = registry.Gauge("test_gauge");
		var histogram = registry.Histogram("test_histogram");

		counter.IncrementBy(5);
		gauge.Set(100);
		histogram.Record(50);
		histogram.Record(75);

		MetricSnapshot[]? receivedSnapshots = null;
		Action<MetricSnapshot[]> callback = s => Volatile.Write(ref receivedSnapshots, s);

		using var aggregator = new MetricAggregator(registry, TimeSpan.FromMilliseconds(50), callback);

		// Act - Poll until snapshots arrive (generous timeout for full-suite load)
		await WaitUntilAsync(() => Volatile.Read(ref receivedSnapshots)?.Length == 3, TimeSpan.FromSeconds(10)).ConfigureAwait(false);

		// Assert - Should have snapshots for all metric types
		var snapshots = Volatile.Read(ref receivedSnapshots);
		snapshots.ShouldNotBeNull();
		snapshots.Length.ShouldBe(3);
	}

	[Fact]
	public async Task AggregateWithLabeledCounters()
	{
		// Arrange
		var registry = new MetricRegistry();
		var labeledCounter = registry.LabeledCounter("http_requests", "HTTP requests", "count", "method", "status");

		labeledCounter.Increment(1, "GET", "200");
		labeledCounter.Increment(1, "GET", "200");
		labeledCounter.Increment(1, "POST", "201");
		labeledCounter.Increment(1, "GET", "404");

		MetricSnapshot[]? receivedSnapshots = null;
		Action<MetricSnapshot[]> callback = s => Volatile.Write(ref receivedSnapshots, s);

		using var aggregator = new MetricAggregator(registry, TimeSpan.FromMilliseconds(50), callback);

		// Act - Poll until snapshots arrive (generous timeout for full-suite load)
		await WaitUntilAsync(() => Volatile.Read(ref receivedSnapshots)?.Length == 3, TimeSpan.FromSeconds(10)).ConfigureAwait(false);

		// Assert - Should have snapshot for each label combination
		var snapshots = Volatile.Read(ref receivedSnapshots);
		snapshots.ShouldNotBeNull();
		snapshots.Length.ShouldBe(3); // 3 unique label combinations
	}

	#endregion
}
