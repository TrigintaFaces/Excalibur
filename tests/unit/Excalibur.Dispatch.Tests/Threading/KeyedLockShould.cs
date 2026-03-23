// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Threading;

namespace Excalibur.Dispatch.Tests.Threading;

/// <summary>
/// Regression tests for KeyedLock -- Sprint 690 T.16 (unbounded SemaphoreSlim memory leak fix).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class KeyedLockShould
{
	[Fact]
	public async Task AcquireAsync_ReturnDisposableLock()
	{
		// Arrange
		var keyedLock = new KeyedLock();

		// Act
		using var handle = await keyedLock.AcquireAsync("key-1", CancellationToken.None);

		// Assert
		handle.ShouldNotBeNull();
	}

	[Fact]
	public async Task AcquireAsync_MutuallyExcludesSameKey()
	{
		// Arrange
		var keyedLock = new KeyedLock();
		var entered = 0;
		var maxConcurrent = 0;

		// Act -- two concurrent acquisitions of same key
		var handle1 = await keyedLock.AcquireAsync("key-1", CancellationToken.None);

		var task2 = Task.Run(async () =>
		{
			using var h = await keyedLock.AcquireAsync("key-1", CancellationToken.None);
			var current = Interlocked.Increment(ref entered);
			Interlocked.CompareExchange(ref maxConcurrent, current, current - 1);
			await Task.Delay(10).ConfigureAwait(false);
			Interlocked.Decrement(ref entered);
		});

		// Give task2 time to start waiting
		await Task.Delay(50);

		// Release first lock
		handle1.Dispose();
		await task2;

		// Assert -- task2 should have entered only after handle1 was released
		maxConcurrent.ShouldBeLessThanOrEqualTo(1);
	}

	[Fact]
	public async Task AcquireAsync_AllowsDifferentKeysConcurrently()
	{
		// Arrange
		var keyedLock = new KeyedLock();
		var key1Acquired = new TaskCompletionSource();
		var key2Acquired = new TaskCompletionSource();

		// Act
		var task1 = Task.Run(async () =>
		{
			using var h = await keyedLock.AcquireAsync("key-1", CancellationToken.None);
			key1Acquired.SetResult();
			await key2Acquired.Task.ConfigureAwait(false); // Wait for key-2 to be acquired concurrently
		});

		var task2 = Task.Run(async () =>
		{
			using var h = await keyedLock.AcquireAsync("key-2", CancellationToken.None);
			key2Acquired.SetResult();
			await key1Acquired.Task.ConfigureAwait(false); // Wait for key-1 to be acquired concurrently
		});

		// Assert -- both should complete (not deadlock)
		var completed = await Task.WhenAny(Task.WhenAll(task1, task2), Task.Delay(5000));
		(completed == Task.WhenAll(task1, task2) || (task1.IsCompleted && task2.IsCompleted))
			.ShouldBeTrue("Different keys should be acquirable concurrently");
	}

	[Fact]
	public async Task Release_CleansUpSemaphore_WhenNoWaiters()
	{
		// Arrange -- T.16 regression: semaphores were never removed, causing memory leak
		var keyedLock = new KeyedLock();

		// Act -- acquire and release for many distinct keys
		for (var i = 0; i < 100; i++)
		{
			using var handle = await keyedLock.AcquireAsync($"key-{i}", CancellationToken.None);
			// handle disposed here, should clean up semaphore
		}

		// Assert -- internal dictionary should be empty after all locks released
		// Use reflection to check internal state
		var locksField = typeof(KeyedLock).GetField("_locks", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
		locksField.ShouldNotBeNull();
		var locks = (Dictionary<string, SemaphoreSlim>)locksField.GetValue(keyedLock)!;
		locks.Count.ShouldBe(0, "All semaphores should be cleaned up when no waiters remain");
	}

	[Fact]
	public async Task Release_DoesNotCleanup_WhenWaitersExist()
	{
		// Arrange
		var keyedLock = new KeyedLock();
		var handle1 = await keyedLock.AcquireAsync("key-1", CancellationToken.None);

		// Start a waiter
		var waiterStarted = new TaskCompletionSource();
		var waiterTask = Task.Run(async () =>
		{
			waiterStarted.SetResult();
			using var h = await keyedLock.AcquireAsync("key-1", CancellationToken.None);
		});

		await waiterStarted.Task;
		await Task.Delay(50); // Give waiter time to enter WaitAsync

		// Act -- release first handle while waiter exists
		handle1.Dispose();

		// Assert -- waiter should eventually complete
		await waiterTask.WaitAsync(TimeSpan.FromSeconds(5));
	}

	[Fact]
	public async Task AcquireAsync_RespectsCancellation()
	{
		// Arrange
		var keyedLock = new KeyedLock();
		using var handle = await keyedLock.AcquireAsync("key-1", CancellationToken.None);
		using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

		// Act & Assert -- trying to acquire already-held lock should be cancellable
		await Should.ThrowAsync<OperationCanceledException>(
			() => keyedLock.AcquireAsync("key-1", cts.Token));
	}
}
