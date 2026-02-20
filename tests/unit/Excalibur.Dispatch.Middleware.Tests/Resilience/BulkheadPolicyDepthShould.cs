// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience.Polly;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Additional depth tests for <see cref="BulkheadPolicy"/> covering
/// DisposeAsync, queue metrics tracking, concurrent execution, and edge cases.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Resilience")]
public sealed class BulkheadPolicyDepthShould : IAsyncDisposable
{
	private BulkheadPolicy? _policy;

	public async ValueTask DisposeAsync()
	{
		if (_policy != null)
		{
			await _policy.DisposeAsync().ConfigureAwait(false);
		}
	}

	[Fact]
	public async Task DisposeAsync_CanBeCalledMultipleTimes()
	{
		// Arrange
		_policy = new BulkheadPolicy("test", new BulkheadOptions { MaxConcurrency = 5 });

		// Act & Assert — should not throw
		await _policy.DisposeAsync().ConfigureAwait(false);
		await _policy.DisposeAsync().ConfigureAwait(false);

		_policy = null;
	}

	[Fact]
	public async Task DisposeAsync_AfterDispose_DoesNotThrow()
	{
		// Arrange
		_policy = new BulkheadPolicy("test", new BulkheadOptions { MaxConcurrency = 5 });
		_policy.Dispose();

		// Act & Assert
		await _policy.DisposeAsync().ConfigureAwait(false);
		_policy = null;
	}

	[Fact]
	public async Task ExecuteAsync_TracksActiveExecutions()
	{
		// Arrange
		_policy = new BulkheadPolicy("test", new BulkheadOptions { MaxConcurrency = 5, MaxQueueLength = 10 });
		var started = new TaskCompletionSource();
		var release = new TaskCompletionSource();

		// Start an operation that blocks
		var task = _policy.ExecuteAsync(async () =>
		{
			started.SetResult();
			await release.Task.ConfigureAwait(false);
			return 1;
		}, CancellationToken.None);

		await started.Task.ConfigureAwait(false);

		// Act
		var metrics = _policy.GetMetrics();

		// Assert
		metrics.ActiveExecutions.ShouldBe(1);
		metrics.AvailableCapacity.ShouldBe(4);

		// Cleanup
		release.SetResult();
		await task.ConfigureAwait(false);
	}

	[Fact]
	public async Task ExecuteAsync_QueueTracking_RecordsQueuedExecutions()
	{
		// Arrange
		_policy = new BulkheadPolicy("test", new BulkheadOptions { MaxConcurrency = 1, MaxQueueLength = 5 });
		var blockStarted = new TaskCompletionSource();
		var releaseBlock = new TaskCompletionSource();

		// Block the single slot
		var blockingTask = _policy.ExecuteAsync(async () =>
		{
			blockStarted.SetResult();
			await releaseBlock.Task.ConfigureAwait(false);
			return 0;
		}, CancellationToken.None);

		await blockStarted.Task.ConfigureAwait(false);

		// Queue a second task (enters the queue)
		var queuedTask = _policy.ExecuteAsync(() => Task.FromResult(1), CancellationToken.None);

		// Small delay to let the queued task register as pending
		await Task.Delay(50).ConfigureAwait(false);

		// Act
		var metrics = _policy.GetMetrics();

		// Assert
		metrics.QueuedExecutions.ShouldBeGreaterThan(0);

		// Cleanup
		releaseBlock.SetResult();
		await blockingTask.ConfigureAwait(false);
		await queuedTask.ConfigureAwait(false);
	}

	[Fact]
	public async Task GetMetrics_AfterRejection_TracksTotalAndRejected()
	{
		// Arrange
		_policy = new BulkheadPolicy("test", new BulkheadOptions { MaxConcurrency = 1, MaxQueueLength = 0 });
		var blockStarted = new TaskCompletionSource();
		var releaseBlock = new TaskCompletionSource();

		// Block the single slot
		var blockingTask = _policy.ExecuteAsync(async () =>
		{
			blockStarted.SetResult();
			await releaseBlock.Task.ConfigureAwait(false);
			return 0;
		}, CancellationToken.None);

		await blockStarted.Task.ConfigureAwait(false);

		// Try second task — should be rejected
		try
		{
			await _policy.ExecuteAsync(() => Task.FromResult(1), CancellationToken.None).ConfigureAwait(false);
		}
		catch (BulkheadRejectedException)
		{
			// Expected
		}

		// Act
		var metrics = _policy.GetMetrics();

		// Assert
		metrics.TotalExecutions.ShouldBe(2);
		metrics.RejectedExecutions.ShouldBe(1);

		// Cleanup
		releaseBlock.SetResult();
		await blockingTask.ConfigureAwait(false);
	}

	[Fact]
	public void HasCapacity_WithMaxQueueLength_ReturnsTrue()
	{
		// Arrange
		_policy = new BulkheadPolicy("test", new BulkheadOptions { MaxConcurrency = 1, MaxQueueLength = 10 });

		// Act & Assert
		_policy.HasCapacity.ShouldBeTrue();
	}

	[Fact]
	public async Task GetMetrics_ReturnsNameAndCapacity()
	{
		// Arrange
		_policy = new BulkheadPolicy("my-bulkhead", new BulkheadOptions { MaxConcurrency = 8, MaxQueueLength = 16 });

		// Act
		await _policy.ExecuteAsync(() => Task.FromResult(1), CancellationToken.None).ConfigureAwait(false);
		var metrics = _policy.GetMetrics();

		// Assert
		metrics.Name.ShouldBe("my-bulkhead");
		metrics.MaxConcurrency.ShouldBe(8);
		metrics.MaxQueueLength.ShouldBe(16);
		metrics.TotalExecutions.ShouldBe(1);
	}

	[Fact]
	public void Constructor_WithNullLogger_UsesNullLogger()
	{
		// Act
		_policy = new BulkheadPolicy("test", new BulkheadOptions { MaxConcurrency = 5 }, logger: null);

		// Assert
		_policy.ShouldNotBeNull();
	}

	[Fact]
	public async Task ExecuteAsync_MultipleSequential_TracksAllExecutions()
	{
		// Arrange
		_policy = new BulkheadPolicy("test", new BulkheadOptions { MaxConcurrency = 5 });

		// Act
		for (var i = 0; i < 10; i++)
		{
			await _policy.ExecuteAsync(() => Task.FromResult(i), CancellationToken.None).ConfigureAwait(false);
		}

		// Assert
		var metrics = _policy.GetMetrics();
		metrics.TotalExecutions.ShouldBe(10);
		metrics.RejectedExecutions.ShouldBe(0);
		metrics.ActiveExecutions.ShouldBe(0);
	}
}
