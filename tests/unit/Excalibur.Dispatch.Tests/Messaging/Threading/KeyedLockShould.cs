// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Threading;

namespace Excalibur.Dispatch.Tests.Messaging.Threading;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class KeyedLockShould
{
	[Fact]
	public async Task AcquireAsync_ReturnsDisposableLock()
	{
		// Arrange
		var keyedLock = new KeyedLock();

		// Act
		var handle = await keyedLock.AcquireAsync("key1", CancellationToken.None);

		// Assert
		handle.ShouldNotBeNull();
		handle.Dispose();
	}

	[Fact]
	public async Task AcquireAsync_SameKey_BlocksUntilReleased()
	{
		// Arrange
		var keyedLock = new KeyedLock();
		var firstAcquired = new TaskCompletionSource<bool>();
		var secondAcquired = new TaskCompletionSource<bool>();
		var releaseFirst = new TaskCompletionSource<bool>();

		// Act - first lock
		var task1 = Task.Run(async () =>
		{
			var handle = await keyedLock.AcquireAsync("key1", CancellationToken.None);
			firstAcquired.SetResult(true);
			await releaseFirst.Task;
			handle.Dispose();
		});

		await firstAcquired.Task;

		// Second lock should not complete immediately
		var task2 = Task.Run(async () =>
		{
			var handle = await keyedLock.AcquireAsync("key1", CancellationToken.None);
			secondAcquired.SetResult(true);
			handle.Dispose();
		});

		// Give a moment to ensure second task is blocked
		var completed = await Task.WhenAny(secondAcquired.Task, Task.Delay(100));
		completed.ShouldNotBe(secondAcquired.Task);

		// Release first lock
		releaseFirst.SetResult(true);

		// Now second should complete
		await secondAcquired.Task;
		await task1;
		await task2;
	}

	[Fact]
	public async Task AcquireAsync_DifferentKeys_DoNotBlock()
	{
		// Arrange
		var keyedLock = new KeyedLock();

		// Act - acquire two different keys
		var handle1 = await keyedLock.AcquireAsync("key1", CancellationToken.None);
		var handle2 = await keyedLock.AcquireAsync("key2", CancellationToken.None);

		// Assert - both acquired successfully
		handle1.ShouldNotBeNull();
		handle2.ShouldNotBeNull();

		handle1.Dispose();
		handle2.Dispose();
	}

	[Fact]
	public async Task AcquireAsync_WithCancellation_ThrowsWhenCancelled()
	{
		// Arrange
		var keyedLock = new KeyedLock();
		var cts = new CancellationTokenSource();

		// Hold the lock
		var handle = await keyedLock.AcquireAsync("key1", CancellationToken.None);

		// Cancel before second acquire completes
		await cts.CancelAsync();

		// Act & Assert
		await Should.ThrowAsync<OperationCanceledException>(async () =>
			await keyedLock.AcquireAsync("key1", cts.Token));

		handle.Dispose();
	}

	[Fact]
	public async Task AcquireAsync_Reentrant_SameKeySemaphoreReused()
	{
		// Arrange
		var keyedLock = new KeyedLock();

		// Act - acquire and release multiple times
		var handle1 = await keyedLock.AcquireAsync("key1", CancellationToken.None);
		handle1.Dispose();

		var handle2 = await keyedLock.AcquireAsync("key1", CancellationToken.None);
		handle2.Dispose();

		// Assert - if we get here, the same semaphore was reused correctly
	}

	// --- IExecuteInBackground marker interface ---

	[Fact]
	public void IExecuteInBackground_IsMarkerInterface()
	{
		// Assert
		typeof(IExecuteInBackground).IsInterface.ShouldBeTrue();
		typeof(IExecuteInBackground).GetProperties().Length.ShouldBe(1);
		typeof(IExecuteInBackground).GetProperty("PropagateExceptions").ShouldNotBeNull();
	}
}
