// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience.Polly;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Unit tests for <see cref="BulkheadPolicy"/>.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Resilience)]
public sealed class BulkheadPolicyShould : UnitTestBase
{
	private BulkheadPolicy? _policy;

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			_policy?.Dispose();
		}
		base.Dispose(disposing);
	}

	#region Constructor Tests

	[Fact]
	public void Constructor_WithNullName_ThrowsArgumentNullException()
	{
		// Arrange
		var options = new BulkheadOptions();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new BulkheadPolicy(null!, options));
	}

	[Fact]
	public void Constructor_WithNullOptions_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new BulkheadPolicy("test", null!));
	}

	[Fact]
	public void Constructor_WithValidParameters_CreatesInstance()
	{
		// Arrange
		var options = new BulkheadOptions { MaxConcurrency = 5 };

		// Act
		_policy = new BulkheadPolicy("test-bulkhead", options);

		// Assert
		_ = _policy.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_WithLogger_CreatesInstance()
	{
		// Arrange
		var options = new BulkheadOptions { MaxConcurrency = 5 };
		var logger = A.Fake<ILogger<BulkheadPolicy>>();

		// Act
		_policy = new BulkheadPolicy("test-bulkhead", options, logger);

		// Assert
		_ = _policy.ShouldNotBeNull();
	}

	#endregion

	#region HasCapacity Tests

	[Fact]
	public void HasCapacity_WhenEmpty_ReturnsTrue()
	{
		// Arrange
		var options = new BulkheadOptions { MaxConcurrency = 5, MaxQueueLength = 10 };
		_policy = new BulkheadPolicy("test", options);

		// Act & Assert
		_policy.HasCapacity.ShouldBeTrue();
	}

	#endregion

	#region ExecuteAsync Tests

	[Fact]
	public async Task ExecuteAsync_WithNullOperation_ThrowsArgumentNullException()
	{
		// Arrange
		var options = new BulkheadOptions { MaxConcurrency = 5 };
		_policy = new BulkheadPolicy("test", options);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			() => _policy.ExecuteAsync<int>(null!, CancellationToken.None));
	}

	[Fact]
	public async Task ExecuteAsync_WithValidOperation_ExecutesAndReturnsResult()
	{
		// Arrange
		var options = new BulkheadOptions { MaxConcurrency = 5 };
		_policy = new BulkheadPolicy("test", options);

		// Act
		var result = await _policy.ExecuteAsync(() => Task.FromResult(42), CancellationToken.None);

		// Assert
		result.ShouldBe(42);
	}

	[Fact]
	public async Task ExecuteAsync_UpdatesTotalExecutionsCounter()
	{
		// Arrange
		var options = new BulkheadOptions { MaxConcurrency = 5 };
		_policy = new BulkheadPolicy("test", options);

		// Act
		await _policy.ExecuteAsync(() => Task.FromResult(1), CancellationToken.None);
		await _policy.ExecuteAsync(() => Task.FromResult(2), CancellationToken.None);
		await _policy.ExecuteAsync(() => Task.FromResult(3), CancellationToken.None);
		var metrics = _policy.GetMetrics();

		// Assert
		metrics.TotalExecutions.ShouldBe(3);
	}

	[Fact]
	public async Task ExecuteAsync_WhenCancelled_ThrowsOperationCancelledException()
	{
		// Arrange
		var options = new BulkheadOptions { MaxConcurrency = 5 };
		_policy = new BulkheadPolicy("test", options);
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(
			() => _policy.ExecuteAsync(() => Task.FromResult(1), cts.Token));
	}

	[Fact]
	public async Task ExecuteAsync_WithConcurrentOperations_ExecutesWithinLimit()
	{
		// Arrange
		var options = new BulkheadOptions { MaxConcurrency = 2, MaxQueueLength = 5 };
		_policy = new BulkheadPolicy("test", options);
		var executionCount = 0;
		var enteredBulkhead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var releaseOperations = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

		// Act - Run 3 operations with concurrency limit of 2
		var tasks = new List<Task<int>>();
		for (var i = 0; i < 3; i++)
		{
			tasks.Add(_policy.ExecuteAsync(async () =>
			{
				if (Interlocked.Increment(ref executionCount) == 2)
				{
					enteredBulkhead.TrySetResult();
				}

				await releaseOperations.Task.ConfigureAwait(false);
				return 1;
			}, CancellationToken.None));
		}
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			enteredBulkhead.Task,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(5)));
		releaseOperations.TrySetResult();
		await Task.WhenAll(tasks);

		// Assert
		executionCount.ShouldBe(3);
	}

	[Fact]
	public async Task ExecuteAsync_WhenQueueFull_ThrowsBulkheadRejectedException()
	{
		// Arrange
		var options = new BulkheadOptions { MaxConcurrency = 1, MaxQueueLength = 1 };
		_policy = new BulkheadPolicy("test", options);
		var blockingTaskStarted = new TaskCompletionSource();
		var releaseBlockingTask = new TaskCompletionSource();

		// Start a task that blocks the semaphore
		var blockingTask = _policy.ExecuteAsync(async () =>
		{
			blockingTaskStarted.SetResult();
			await releaseBlockingTask.Task;
			return 1;
		}, CancellationToken.None);

		// Wait for blocking task to start
		await blockingTaskStarted.Task;

		// Start a task that fills the queue
		var queuedTask = _policy.ExecuteAsync(() => Task.FromResult(2), CancellationToken.None);

		var queueObserved = await global::Tests.Shared.Infrastructure.WaitHelpers.WaitUntilAsync(
			() => _policy.GetMetrics().QueuedExecutions >= 1,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(2)),
			TimeSpan.FromMilliseconds(10),
			CancellationToken.None);
		queueObserved.ShouldBeTrue();

		// Act & Assert - Third task should be rejected
		_ = await Should.ThrowAsync<BulkheadRejectedException>(
			() => _policy.ExecuteAsync(() => Task.FromResult(3), CancellationToken.None));

		// Cleanup
		releaseBlockingTask.SetResult();
		await blockingTask;
		await queuedTask;
	}

	#endregion

	#region GetMetrics Tests

	[Fact]
	public void GetMetrics_ReturnsCorrectInitialMetrics()
	{
		// Arrange
		var options = new BulkheadOptions { MaxConcurrency = 10, MaxQueueLength = 50 };
		_policy = new BulkheadPolicy("test-bulkhead", options);

		// Act
		var metrics = _policy.GetMetrics();

		// Assert
		metrics.Name.ShouldBe("test-bulkhead");
		metrics.MaxConcurrency.ShouldBe(10);
		metrics.MaxQueueLength.ShouldBe(50);
		metrics.ActiveExecutions.ShouldBe(0);
		metrics.TotalExecutions.ShouldBe(0);
		metrics.RejectedExecutions.ShouldBe(0);
		metrics.QueuedExecutions.ShouldBe(0);
		metrics.AvailableCapacity.ShouldBe(10);
	}

	[Fact]
	public async Task GetMetrics_TracksRejectedExecutions()
	{
		// Arrange
		var options = new BulkheadOptions { MaxConcurrency = 1, MaxQueueLength = 0 };
		_policy = new BulkheadPolicy("test", options);
		var blockingTaskStarted = new TaskCompletionSource();
		var releaseBlockingTask = new TaskCompletionSource();

		// Block the only slot
		var blockingTask = _policy.ExecuteAsync(async () =>
		{
			blockingTaskStarted.SetResult();
			await releaseBlockingTask.Task;
			return 1;
		}, CancellationToken.None);

		await blockingTaskStarted.Task;

		// Try to execute another task (should be rejected)
		try
		{
			await _policy.ExecuteAsync(() => Task.FromResult(2), CancellationToken.None);
		}
		catch (BulkheadRejectedException)
		{
			// Expected
		}

		// Act
		var metrics = _policy.GetMetrics();

		// Cleanup
		releaseBlockingTask.SetResult();
		await blockingTask;

		// Assert
		metrics.RejectedExecutions.ShouldBe(1);
	}

	#endregion

	#region Dispose Tests

	[Fact]
	public void Dispose_CanBeCalledMultipleTimes()
	{
		// Arrange
		var options = new BulkheadOptions { MaxConcurrency = 5 };
		_policy = new BulkheadPolicy("test", options);

		// Act & Assert - Should not throw
		_policy.Dispose();
		_policy.Dispose();
		_policy.Dispose();

		// Clean up so Dispose in test cleanup doesn't do anything
		_policy = null;
	}

	[Fact]
	public void Dispose_ReleasesResources()
	{
		// Arrange
		var options = new BulkheadOptions { MaxConcurrency = 5 };
		_policy = new BulkheadPolicy("test", options);

		// Act
		_policy.Dispose();

		// Assert - Metrics can still be read but may show disposed state
		var metrics = _policy.GetMetrics();
		_ = metrics.ShouldNotBeNull();

		// Clean up
		_policy = null;
	}

	#endregion

	#region Concurrency Regression Locks

	[Fact]
	public async Task Reject_all_excess_admissions_keeping_MaxQueueLength_a_hard_bound_under_concurrent_burst()
	{
		// Regression lock for bd-2qhmij (P1): queue admission must be ATOMIC so MaxQueueLength is a HARD bound.
		// The fix replaced a check-then-act gate (read pending; if < max, increment) with Interlocked.Increment
		// then reject when the POST-increment value > MaxQueueLength. Under a concurrent burst the old TOCTOU let
		// several callers pass a stale check and overshoot the queue. This lock saturates the bulkhead (1 active +
		// a full queue), fires a large simultaneous burst on real threads, and asserts EVERY excess attempt is
		// rejected -- none slips past the bound. Determinism: WaitHelpers polling, no fixed wall-clock sleeps.
		const int maxConcurrency = 1;
		const int maxQueue = 2;
		const int burst = 48;
		var policy = new BulkheadPolicy("bd-2qhmij", new BulkheadOptions
		{
			MaxConcurrency = maxConcurrency,
			MaxQueueLength = maxQueue,
			OperationTimeout = TimeSpan.FromSeconds(30),
		});
		_policy = policy;

		// Gate holds the single execution slot open until released, keeping the bulkhead saturated.
		var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		using var cleanup = new CancellationTokenSource();

		Task<int> Occupy(CancellationToken ct) =>
			policy.ExecuteAsync(async () => { await gate.Task.ConfigureAwait(false); return 0; }, ct);

		// Occupy the one execution slot.
		var active = Occupy(CancellationToken.None);
		(await global::Tests.Shared.Infrastructure.WaitHelpers.WaitUntilAsync(
			() => policy.GetMetrics().ActiveExecutions == maxConcurrency, TimeSpan.FromSeconds(5)).ConfigureAwait(false))
			.ShouldBeTrue("the single execution slot should fill");

		// Queue starts EMPTY. Fire the burst simultaneously on real threads: with the only slot held by the
		// gate, the burst must FILL the queue from empty to EXACTLY MaxQueueLength and reject the rest. The
		// pre-fix check-then-act lets concurrent callers pass a stale "pending < max" read and overshoot while
		// filling -- this is the race the saturated case would miss, so we exercise the fill-from-empty path.
		var startGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var burstTasks = new List<Task>();
		for (var i = 0; i < burst; i++)
		{
			burstTasks.Add(Task.Run(async () =>
			{
				await startGate.Task.ConfigureAwait(false); // release together to maximise contention
				try
				{
					_ = await policy.ExecuteAsync(
						async () => { await gate.Task.ConfigureAwait(false); return 0; }, cleanup.Token).ConfigureAwait(false);
				}
				catch (BulkheadRejectedException) { }   // rejected (queue full)
				catch (OperationCanceledException) { }  // had entered the queue; cancelled during cleanup
			}));
		}
		startGate.SetResult();

		// Wait until every burst attempt has made its admission decision (entered the queue or was rejected).
		(await global::Tests.Shared.Infrastructure.WaitHelpers.WaitUntilAsync(
			() =>
			{
				var m = policy.GetMetrics();
				return m.QueuedExecutions + m.RejectedExecutions >= burst;
			}, TimeSpan.FromSeconds(15)).ConfigureAwait(false))
			.ShouldBeTrue("all burst attempts should reach an admission decision");

		var settled = policy.GetMetrics();

		// HARD-BOUND assertions (bd-2qhmij): exactly MaxQueueLength burst attempts may enter the queue; the rest
		// must be rejected. Pre-fix TOCTOU overshoots -> QueuedExecutions > MaxQueueLength and RejectedExecutions
		// correspondingly fewer.
		settled.QueuedExecutions.ShouldBe(maxQueue,
			"atomic admission must admit exactly MaxQueueLength into the queue under a concurrent burst (bd-2qhmij)");
		settled.RejectedExecutions.ShouldBe(burst - maxQueue,
			"every attempt beyond MaxQueueLength must be rejected (bd-2qhmij)");

		// Release and drain.
		await cleanup.CancelAsync().ConfigureAwait(false);
		gate.SetResult();
		foreach (var t in burstTasks) { await t.ConfigureAwait(false); }
		try { _ = await active.ConfigureAwait(false); } catch (OperationCanceledException) { }
	}

	#endregion
}

