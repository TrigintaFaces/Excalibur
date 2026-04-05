// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.ErrorHandling;

namespace Excalibur.Dispatch.Tests.Messaging.ErrorHandling;

/// <summary>
/// Unit tests for <see cref="NullDeadLetterQueue"/>.
/// </summary>
/// <remarks>
/// Sprint 745 ISP: ReplayBatchAsync, PurgeAsync, PurgeOlderThanAsync moved to IDeadLetterQueueAdmin.
/// NullDeadLetterQueue only implements IDeadLetterQueue, so those tests were removed.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class NullDeadLetterQueueShould
{
	[Fact]
	public void ProvideStaticInstance()
	{
		// Act
		var instance = NullDeadLetterQueue.Instance;

		// Assert
		instance.ShouldNotBeNull();
	}

	[Fact]
	public void ReturnSameSingletonInstance()
	{
		// Act
		var instance1 = NullDeadLetterQueue.Instance;
		var instance2 = NullDeadLetterQueue.Instance;

		// Assert
		instance1.ShouldBeSameAs(instance2);
	}

	[Fact]
	public void ImplementIDeadLetterQueue()
	{
		// Act
		var instance = NullDeadLetterQueue.Instance;

		// Assert
		instance.ShouldBeAssignableTo<IDeadLetterQueue>();
	}

	[Fact]
	public async Task EnqueueAsyncReturnsEmptyGuid()
	{
		// Arrange
		var queue = NullDeadLetterQueue.Instance;
		var message = new TestMessage { Value = "test" };

		// Act
		var result = await queue.EnqueueAsync(
			message,
			DeadLetterReason.MaxRetriesExceeded,
			CancellationToken.None);

		// Assert
		result.ShouldBe(Guid.Empty);
	}

	[Fact]
	public async Task EnqueueAsyncReturnsEmptyGuidWithException()
	{
		// Arrange
		var queue = NullDeadLetterQueue.Instance;
		var message = new TestMessage { Value = "test" };
		var exception = new InvalidOperationException("Test error");

		// Act
		var result = await queue.EnqueueAsync(
			message,
			DeadLetterReason.UnhandledException,
			CancellationToken.None,
			exception);

		// Assert
		result.ShouldBe(Guid.Empty);
	}

	[Fact]
	public async Task EnqueueAsyncReturnsEmptyGuidWithMetadata()
	{
		// Arrange
		var queue = NullDeadLetterQueue.Instance;
		var message = new TestMessage { Value = "test" };
		var metadata = new Dictionary<string, string> { ["key"] = "value" };

		// Act
		var result = await queue.EnqueueAsync(
			message,
			DeadLetterReason.ValidationFailed,
			CancellationToken.None,
			metadata: metadata);

		// Assert
		result.ShouldBe(Guid.Empty);
	}

	[Fact]
	public async Task GetEntriesAsyncReturnsEmptyList()
	{
		// Arrange
		var queue = NullDeadLetterQueue.Instance;

		// Act
		var result = await queue.GetEntriesAsync(CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task GetEntriesAsyncReturnsEmptyListWithFilter()
	{
		// Arrange
		var queue = NullDeadLetterQueue.Instance;
		var filter = DeadLetterQueryFilter.ByReason(DeadLetterReason.MaxRetriesExceeded);

		// Act
		var result = await queue.GetEntriesAsync(CancellationToken.None, filter, limit: 50);

		// Assert
		result.ShouldNotBeNull();
		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task GetEntryAsyncReturnsNull()
	{
		// Arrange
		var queue = NullDeadLetterQueue.Instance;
		var entryId = Guid.NewGuid();

		// Act
		var result = await queue.GetEntryAsync(entryId, CancellationToken.None);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task GetEntryAsyncReturnsNullForEmptyGuid()
	{
		// Arrange
		var queue = NullDeadLetterQueue.Instance;

		// Act
		var result = await queue.GetEntryAsync(Guid.Empty, CancellationToken.None);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task ReplayAsyncReturnsFalse()
	{
		// Arrange
		var queue = NullDeadLetterQueue.Instance;
		var entryId = Guid.NewGuid();

		// Act
		var result = await queue.ReplayAsync(entryId, CancellationToken.None);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task ReplayAsyncReturnsFalseForEmptyGuid()
	{
		// Arrange
		var queue = NullDeadLetterQueue.Instance;

		// Act
		var result = await queue.ReplayAsync(Guid.Empty, CancellationToken.None);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task GetCountAsyncReturnsZero()
	{
		// Arrange
		var queue = NullDeadLetterQueue.Instance;

		// Act
		var result = await queue.GetCountAsync(CancellationToken.None);

		// Assert
		result.ShouldBe(0L);
	}

	[Fact]
	public async Task GetCountAsyncReturnsZeroWithFilter()
	{
		// Arrange
		var queue = NullDeadLetterQueue.Instance;
		var filter = DeadLetterQueryFilter.ByMessageType("OrderCreated");

		// Act
		var result = await queue.GetCountAsync(CancellationToken.None, filter);

		// Assert
		result.ShouldBe(0L);
	}

	[Fact]
	public async Task AllMethodsAreCompletedSynchronously()
	{
		// Arrange
		var queue = NullDeadLetterQueue.Instance;
		var message = new TestMessage { Value = "test" };
		var filter = new DeadLetterQueryFilter();

		// Act & Assert - All tasks should be completed immediately
		var enqueueTask = queue.EnqueueAsync(message, DeadLetterReason.Unknown, CancellationToken.None);
		enqueueTask.IsCompleted.ShouldBeTrue();

		var getEntriesTask = queue.GetEntriesAsync(CancellationToken.None, filter);
		getEntriesTask.IsCompleted.ShouldBeTrue();

		var getEntryTask = queue.GetEntryAsync(Guid.NewGuid(), CancellationToken.None);
		getEntryTask.IsCompleted.ShouldBeTrue();

		var replayTask = queue.ReplayAsync(Guid.NewGuid(), CancellationToken.None);
		replayTask.IsCompleted.ShouldBeTrue();

		var countTask = queue.GetCountAsync(CancellationToken.None);
		countTask.IsCompleted.ShouldBeTrue();

		// Await to avoid warnings
		await Task.WhenAll(
			enqueueTask,
			getEntriesTask,
			getEntryTask,
			replayTask,
			countTask);
	}

	[Fact]
	public async Task SimulateTypicalNullObjectUsage()
	{
		// Arrange - Use null queue when DLQ is not configured
		IDeadLetterQueue queue = NullDeadLetterQueue.Instance;
		var message = new TestMessage { Value = "important-order" };

		// Act - Try to enqueue a failed message
		var entryId = await queue.EnqueueAsync(
			message,
			DeadLetterReason.MaxRetriesExceeded,
			CancellationToken.None,
			new TimeoutException("Connection timed out"));

		// Assert - Operation completes without error but returns empty
		entryId.ShouldBe(Guid.Empty);

		// Act - Try to get count
		var count = await queue.GetCountAsync(CancellationToken.None);

		// Assert - Count is always zero
		count.ShouldBe(0L);
	}

	private sealed class TestMessage
	{
		public required string Value { get; init; }
	}
}
