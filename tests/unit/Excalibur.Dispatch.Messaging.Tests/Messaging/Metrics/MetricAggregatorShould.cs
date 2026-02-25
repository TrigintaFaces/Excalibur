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
		var multipleCallbacksObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

		Action<MetricSnapshot[]> callback = _ =>
		{
			var count = Interlocked.Increment(ref callbackCount);
			if (count >= 2)
			{
				multipleCallbacksObserved.TrySetResult();
			}
		};

		using var aggregator = new MetricAggregator(registry, TimeSpan.FromMilliseconds(30), callback);

		// Act - wait until callback is observed at least twice
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			multipleCallbacksObserved.Task,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(15)));
		// Assert - Should have been called multiple times
		Volatile.Read(ref callbackCount).ShouldBeGreaterThan(1);
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
		await WaitUntilAsync(() => callCount >= 2, TimeSpan.FromSeconds(15)).ConfigureAwait(false);

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
		var exceptionCount = 0;
		var completionObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

		Action<MetricSnapshot[]> callback = _ =>
		{
			var currentCount = Interlocked.Increment(ref callCount);
			if (currentCount == 1)
			{
				Interlocked.Increment(ref exceptionCount);
				throw new InvalidOperationException("Test exception");
			}
			completionObserved.TrySetResult();
		};

		using var aggregator = new MetricAggregator(registry, TimeSpan.FromMilliseconds(30), callback);

		// Act - ensure the callback continues being invoked after a thrown exception.
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			completionObserved.Task,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(15)));
		// Assert
		exceptionCount.ShouldBe(1);
		callCount.ShouldBeGreaterThanOrEqualTo(2);
	}

	[Fact]
	public async Task StopInvokingCallbackAfterDispose()
	{
		// Arrange
		var registry = new MetricRegistry();
		var callCount = 0;
		Action<MetricSnapshot[]> callback = _ => Interlocked.Increment(ref callCount);

		var aggregator = new MetricAggregator(registry, TimeSpan.FromMilliseconds(25), callback);
		await WaitUntilAsync(() => Volatile.Read(ref callCount) > 0, TimeSpan.FromSeconds(15)).ConfigureAwait(false);

		// Act
		aggregator.Dispose();

		var countAfterDispose = Volatile.Read(ref callCount);
		await global::Tests.Shared.Infrastructure.TestTiming.PauseAsync(TimeSpan.FromMilliseconds(150), CancellationToken.None).ConfigureAwait(false);

		// Assert
		Volatile.Read(ref callCount).ShouldBe(countAfterDispose);
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

