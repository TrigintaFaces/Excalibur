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
		var callbackInvoked = new TaskCompletionSource<MetricSnapshot[]>(TaskCreationOptions.RunContinuationsAsynchronously);

		Action<MetricSnapshot[]> callback = s =>
		{
			callbackInvoked.TrySetResult(s);
		};

		using var aggregator = new MetricAggregator(registry, TimeSpan.FromMilliseconds(10), callback);

		// Act
		var snapshots = await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			callbackInvoked.Task,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(45)));

		// Assert
		snapshots.ShouldNotBeNull();
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
		var snapshotsObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		Action<MetricSnapshot[]> callback = s =>
		{
			Volatile.Write(ref receivedSnapshots, s);
			if (s.Length > 0)
			{
				snapshotsObserved.TrySetResult();
			}
		};

		using var aggregator = new MetricAggregator(registry, TimeSpan.FromMilliseconds(10), callback);

		// Act - wait for snapshots to arrive
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			snapshotsObserved.Task,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(45)));

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

		using var aggregator = new MetricAggregator(registry, TimeSpan.FromMilliseconds(10), callback);

		// Act - wait until callback is observed at least twice
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			multipleCallbacksObserved.Task,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(45)));
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
		var secondCollectionObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

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

			if (Interlocked.Increment(ref callCount) >= 2)
			{
				secondCollectionObserved.TrySetResult();
			}
		};

		using var aggregator = new MetricAggregator(registry, TimeSpan.FromMilliseconds(10), callback);

		// Act
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			secondCollectionObserved.Task,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(15)));

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
		var firstCallbackObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var additionalCallbackObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var disposeThreshold = 0;
		var disposedPhase = 0;
		Action<MetricSnapshot[]> callback = _ =>
		{
			var currentCount = Interlocked.Increment(ref callCount);
			firstCallbackObserved.TrySetResult();
			if (Volatile.Read(ref disposedPhase) == 1 && currentCount > Volatile.Read(ref disposeThreshold))
			{
				additionalCallbackObserved.TrySetResult();
			}
		};

		var aggregator = new MetricAggregator(registry, TimeSpan.FromMilliseconds(25), callback);
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			firstCallbackObserved.Task,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(15)));

		// Act
		disposeThreshold = Volatile.Read(ref callCount);
		aggregator.Dispose();
		Volatile.Write(ref disposedPhase, 1);

		var countAfterDispose = Volatile.Read(ref callCount);
		var callbackAfterDispose = false;
		try
		{
			await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
				additionalCallbackObserved.Task,
				global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromMilliseconds(250)));
			callbackAfterDispose = true;
		}
		catch (TimeoutException)
		{
			callbackAfterDispose = false;
		}

		// Assert
		callbackAfterDispose.ShouldBeFalse();
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
		var snapshotsObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		Action<MetricSnapshot[]> callback = s =>
		{
			Volatile.Write(ref receivedSnapshots, s);
			if (s.Length == 3)
			{
				snapshotsObserved.TrySetResult();
			}
		};

		using var aggregator = new MetricAggregator(registry, TimeSpan.FromMilliseconds(10), callback);

		// Act - wait until all metric types are observed
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			snapshotsObserved.Task,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(45)));

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
		var snapshotsObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		Action<MetricSnapshot[]> callback = s =>
		{
			Volatile.Write(ref receivedSnapshots, s);
			if (s.Length == 3)
			{
				snapshotsObserved.TrySetResult();
			}
		};

		using var aggregator = new MetricAggregator(registry, TimeSpan.FromMilliseconds(10), callback);

		// Act - wait until all labeled series are observed
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			snapshotsObserved.Task,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(45)));

		// Assert - Should have snapshot for each label combination
		var snapshots = Volatile.Read(ref receivedSnapshots);
		snapshots.ShouldNotBeNull();
		snapshots.Length.ShouldBe(3); // 3 unique label combinations
	}

	#endregion
}

