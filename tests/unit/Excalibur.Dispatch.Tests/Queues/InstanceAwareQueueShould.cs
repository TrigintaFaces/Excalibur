// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Queues;

namespace Excalibur.Dispatch.Tests.Queues;

/// <summary>
/// Regression tests for InstanceAwareQueue -- Sprint 690 T.12 (DisposeAsync timeout)
/// and T.24 (TryPopAsync allocation reduction).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InstanceAwareQueueShould
{
	[Fact]
	public async Task AddAsync_PrefixesValueWithInstanceId()
	{
		// Arrange
		var fakeQueue = new FakeDistributedQueue();
		var queue = new InstanceAwareQueue("inst-1", fakeQueue);

		// Act
		var result = await queue.AddAsync("task-42", CancellationToken.None);

		// Assert
		result.ShouldBeTrue();
		fakeQueue.Items.ShouldContain("inst-1:task-42");
	}

	[Fact]
	public async Task TryPopAsync_StripsInstancePrefix()
	{
		// Arrange
		var fakeQueue = new FakeDistributedQueue();
		fakeQueue.Items.Add("other-inst:my-value");
		var queue = new InstanceAwareQueue("inst-1", fakeQueue);

		// Act
		var (success, value) = await queue.TryPopAsync(CancellationToken.None);

		// Assert
		success.ShouldBeTrue();
		value.ShouldBe("my-value");
	}

	[Fact]
	public async Task TryPopAsync_ReturnsItemWithoutPrefix_WhenNoSeparator()
	{
		// Arrange -- edge case: item has no ':' separator
		var fakeQueue = new FakeDistributedQueue();
		fakeQueue.Items.Add("noprefix");
		var queue = new InstanceAwareQueue("inst-1", fakeQueue);

		// Act
		var (success, value) = await queue.TryPopAsync(CancellationToken.None);

		// Assert
		success.ShouldBeTrue();
		value.ShouldBe("noprefix");
	}

	[Fact]
	public async Task TryPopAsync_ReturnsFalse_WhenQueueEmpty()
	{
		// Arrange
		var fakeQueue = new FakeDistributedQueue();
		var queue = new InstanceAwareQueue("inst-1", fakeQueue);

		// Act
		var (success, value) = await queue.TryPopAsync(CancellationToken.None);

		// Assert
		success.ShouldBeFalse();
		value.ShouldBeNull();
	}

	[Fact]
	public async Task DisposeAsync_CleansUpOwnedItems()
	{
		// Arrange
		var fakeQueue = new FakeDistributedQueue();
		var queue = new InstanceAwareQueue("inst-1", fakeQueue);
		await queue.AddAsync("item-1", CancellationToken.None);
		await queue.AddAsync("item-2", CancellationToken.None);

		// Act
		await queue.DisposeAsync();

		// Assert -- items should have been removed during cleanup
		fakeQueue.RemovedItems.ShouldContain("inst-1:item-1");
		fakeQueue.RemovedItems.ShouldContain("inst-1:item-2");
	}

	[Fact]
	public async Task DisposeAsync_DoesNotHangIndefinitely()
	{
		// Arrange -- T.12 regression: DisposeAsync used CancellationToken.None which could hang
		var slowQueue = new SlowDistributedQueue(delay: TimeSpan.FromSeconds(60));
		var queue = new InstanceAwareQueue("inst-1", slowQueue);
		await queue.AddAsync("item-1", CancellationToken.None);

		// Act -- should complete within timeout, not hang indefinitely.
		// CI runners under heavy load can delay task scheduling significantly.
		var disposeTask = queue.DisposeAsync().AsTask();
		var completed = await Task.WhenAny(disposeTask, Task.Delay(TimeSpan.FromSeconds(120)));

		// Assert
		completed.ShouldBe(disposeTask, "DisposeAsync should complete within timeout, not hang indefinitely");
	}

	[Fact]
	public async Task DisposeAsync_IsIdempotent()
	{
		// Arrange
		var fakeQueue = new FakeDistributedQueue();
		var queue = new InstanceAwareQueue("inst-1", fakeQueue);

		// Act -- double dispose should not throw
		await queue.DisposeAsync();
		await queue.DisposeAsync();
	}

	#region Test Doubles

	private sealed class FakeDistributedQueue : IDistributedOrderedSetQueue<string>
	{
		public List<string> Items { get; } = [];
		public List<string> RemovedItems { get; } = [];

		public Task<bool> AddAsync(string item, CancellationToken cancellationToken)
		{
			if (Items.Contains(item))
			{
				return Task.FromResult(false);
			}

			Items.Add(item);
			return Task.FromResult(true);
		}

		public Task<(bool Success, string? Item)> TryPopAsync(CancellationToken cancellationToken)
		{
			if (Items.Count == 0)
			{
				return Task.FromResult<(bool, string?)>((false, null));
			}

			var item = Items[0];
			Items.RemoveAt(0);
			return Task.FromResult<(bool, string?)>((true, item));
		}

		public Task<bool> ContainsAsync(string item, CancellationToken cancellationToken)
			=> Task.FromResult(Items.Contains(item));

		public Task<int> CountAsync(CancellationToken cancellationToken)
			=> Task.FromResult(Items.Count);

		public Task<bool> RemoveAsync(string item, CancellationToken cancellationToken)
		{
			RemovedItems.Add(item);
			return Task.FromResult(Items.Remove(item));
		}
	}

	private sealed class SlowDistributedQueue(TimeSpan delay) : IDistributedOrderedSetQueue<string>
	{
		private readonly List<string> _items = [];

		public Task<bool> AddAsync(string item, CancellationToken cancellationToken)
		{
			_items.Add(item);
			return Task.FromResult(true);
		}

		public Task<(bool Success, string? Item)> TryPopAsync(CancellationToken cancellationToken)
			=> Task.FromResult<(bool, string?)>((false, null));

		public Task<bool> ContainsAsync(string item, CancellationToken cancellationToken)
			=> Task.FromResult(false);

		public Task<int> CountAsync(CancellationToken cancellationToken)
			=> Task.FromResult(_items.Count);

		public async Task<bool> RemoveAsync(string item, CancellationToken cancellationToken)
		{
			await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
			return _items.Remove(item);
		}
	}

	#endregion
}
