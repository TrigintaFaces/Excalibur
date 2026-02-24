// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Threading;

namespace Excalibur.Dispatch.Tests.Messaging.Threading;

/// <summary>
///     Tests for the <see cref="KeyedLock" /> class.
/// </summary>
[Trait("Category", "Unit")]
public sealed class KeyedLockShould
{
	[Fact]
	public async Task AcquireLockForKey()
	{
		var sut = new KeyedLock();

		using var handle = await sut.AcquireAsync("key1", CancellationToken.None).ConfigureAwait(false);

		handle.ShouldNotBeNull();
	}

	[Fact]
	public async Task ReturnDisposableLockHandle()
	{
		var sut = new KeyedLock();

		var handle = await sut.AcquireAsync("key1", CancellationToken.None).ConfigureAwait(false);

		handle.ShouldBeAssignableTo<IDisposable>();
		handle.Dispose();
	}

	[Fact]
	public async Task AllowReacquireAfterDispose()
	{
		var sut = new KeyedLock();

		var handle1 = await sut.AcquireAsync("key1", CancellationToken.None).ConfigureAwait(false);
		handle1.Dispose();

		// Should be able to acquire again after disposing
		using var handle2 = await sut.AcquireAsync("key1", CancellationToken.None).ConfigureAwait(false);
		handle2.ShouldNotBeNull();
	}

	[Fact]
	public async Task AllowConcurrentLocksForDifferentKeys()
	{
		var sut = new KeyedLock();

		using var handle1 = await sut.AcquireAsync("key1", CancellationToken.None).ConfigureAwait(false);
		using var handle2 = await sut.AcquireAsync("key2", CancellationToken.None).ConfigureAwait(false);

		handle1.ShouldNotBeNull();
		handle2.ShouldNotBeNull();
	}

	[Fact]
	public async Task EnforceMutualExclusionForSameKey()
	{
		var sut = new KeyedLock();
		var order = new List<int>();
		var barrier = new SemaphoreSlim(0, 1);

		// Acquire the lock first
		var handle = await sut.AcquireAsync("key1", CancellationToken.None).ConfigureAwait(false);

		// Start a task that will wait for the lock
		var waitingTask = Task.Run(async () =>
		{
			barrier.Release();
			using var innerHandle = await sut.AcquireAsync("key1", CancellationToken.None).ConfigureAwait(false);
			order.Add(2);
		});

		// Wait for the second task to start waiting
		await barrier.WaitAsync(CancellationToken.None).ConfigureAwait(false);
		await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(50).ConfigureAwait(false);

		order.Add(1);
		handle.Dispose(); // Release the lock

		await waitingTask.ConfigureAwait(false);

		order.ShouldBe([1, 2]);
	}

	[Fact]
	public async Task RespectCancellationToken()
	{
		var sut = new KeyedLock();

		// Acquire the lock
		using var handle = await sut.AcquireAsync("key1", CancellationToken.None).ConfigureAwait(false);

		// Try to acquire with a cancelled token
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync().ConfigureAwait(false);

		await Should.ThrowAsync<OperationCanceledException>(async () =>
			await sut.AcquireAsync("key1", cts.Token).ConfigureAwait(false)).ConfigureAwait(false);
	}

	[Fact]
	public async Task ImplementIKeyedLock()
	{
		var sut = new KeyedLock();
		sut.ShouldBeAssignableTo<IKeyedLock>();

		// Ensure interface contract works
		IKeyedLock iface = sut;
		using var handle = await iface.AcquireAsync("test", CancellationToken.None).ConfigureAwait(false);
		handle.ShouldNotBeNull();
	}
}
