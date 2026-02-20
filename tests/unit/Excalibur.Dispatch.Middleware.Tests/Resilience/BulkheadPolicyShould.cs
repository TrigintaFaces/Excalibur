// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience.Polly;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Unit tests for <see cref="BulkheadPolicy"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Resilience")]
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

		// Act - Run 3 operations with concurrency limit of 2
		var tasks = new List<Task<int>>();
		for (var i = 0; i < 3; i++)
		{
			tasks.Add(_policy.ExecuteAsync(async () =>
			{
				Interlocked.Increment(ref executionCount);
				await Task.Delay(50);
				return 1;
			}, CancellationToken.None));
		}
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

		// Small delay to let the queued task register
		await Task.Delay(50);

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
}
