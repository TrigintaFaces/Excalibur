// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.ErrorHandling;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Messaging.ErrorHandling;

/// <summary>
/// Tests for the <see cref="InMemoryDeadLetterQueue"/> class.
/// Epic 6 (bd-rj9o): Integration tests for dead letter queue.
/// </summary>
[Trait("Category", "Unit")]
public sealed class InMemoryDeadLetterQueueShould
{
	private readonly ILogger<InMemoryDeadLetterQueue> _logger;

	public InMemoryDeadLetterQueueShould()
	{
		_logger = NullLoggerFactory.Instance.CreateLogger<InMemoryDeadLetterQueue>();
	}

	private InMemoryDeadLetterQueue CreateQueue(Func<object, Task>? replayHandler = null)
	{
		return new InMemoryDeadLetterQueue(_logger, replayHandler);
	}

	#region Test Messages

	private sealed class TestMessage
	{
		public int Id { get; init; }
		public string Content { get; init; } = string.Empty;
	}

	private sealed class OrderMessage
	{
		public Guid OrderId { get; init; }
		public decimal Amount { get; init; }
	}

	#endregion

	#region Enqueue Tests

	[Fact]
	public async Task EnqueueMessageSuccessfully()
	{
		// Arrange
		var queue = CreateQueue();
		var message = new TestMessage { Id = 1, Content = "Test" };

		// Act
		var entryId = await queue.EnqueueAsync(message, DeadLetterReason.MaxRetriesExceeded).ConfigureAwait(false);

		// Assert
		entryId.ShouldNotBe(Guid.Empty);
	}

	[Fact]
	public async Task StoreMessageTypeCorrectly()
	{
		// Arrange
		var queue = CreateQueue();
		var message = new TestMessage { Id = 1, Content = "Test" };

		// Act
		var entryId = await queue.EnqueueAsync(message, DeadLetterReason.MaxRetriesExceeded).ConfigureAwait(false);
		var entry = await queue.GetEntryAsync(entryId).ConfigureAwait(false);

		// Assert
		entry.ShouldNotBeNull();
		entry.MessageType.ShouldContain("TestMessage");
	}

	[Fact]
	public async Task StoreReasonCorrectly()
	{
		// Arrange
		var queue = CreateQueue();
		var message = new TestMessage { Id = 1, Content = "Test" };

		// Act
		var entryId = await queue.EnqueueAsync(message, DeadLetterReason.CircuitBreakerOpen).ConfigureAwait(false);
		var entry = await queue.GetEntryAsync(entryId).ConfigureAwait(false);

		// Assert
		entry.ShouldNotBeNull();
		entry.Reason.ShouldBe(DeadLetterReason.CircuitBreakerOpen);
	}

	[Fact]
	public async Task StoreExceptionDetailsWhenProvided()
	{
		// Arrange
		var queue = CreateQueue();
		var message = new TestMessage { Id = 1, Content = "Test" };

		// Throw and catch to get a stack trace
		Exception? exception = null;
		try
		{
			throw new InvalidOperationException("Processing failed");
		}
		catch (InvalidOperationException ex)
		{
			exception = ex;
		}

		// Act
		var entryId = await queue.EnqueueAsync(
			message,
			DeadLetterReason.UnhandledException,
			exception).ConfigureAwait(false);
		var entry = await queue.GetEntryAsync(entryId).ConfigureAwait(false);

		// Assert
		entry.ShouldNotBeNull();
		entry.ExceptionMessage.ShouldBe("Processing failed");
		entry.ExceptionStackTrace.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public async Task StoreMetadataWhenProvided()
	{
		// Arrange
		var queue = CreateQueue();
		var message = new TestMessage { Id = 1, Content = "Test" };
		var metadata = new Dictionary<string, string>
		{
			["handler"] = "OrderHandler",
			["attempt"] = "5",
		};

		// Act
		var entryId = await queue.EnqueueAsync(
			message,
			DeadLetterReason.MaxRetriesExceeded,
			metadata: metadata).ConfigureAwait(false);
		var entry = await queue.GetEntryAsync(entryId).ConfigureAwait(false);

		// Assert
		entry.ShouldNotBeNull();
		entry.Metadata.ShouldNotBeNull();
		entry.Metadata["handler"].ShouldBe("OrderHandler");
		entry.Metadata["attempt"].ShouldBe("5");
	}

	[Fact]
	public async Task SetEnqueuedAtTimestamp()
	{
		// Arrange
		var queue = CreateQueue();
		var message = new TestMessage { Id = 1, Content = "Test" };
		var beforeEnqueue = DateTimeOffset.UtcNow;

		// Act
		var entryId = await queue.EnqueueAsync(message, DeadLetterReason.MaxRetriesExceeded).ConfigureAwait(false);
		var entry = await queue.GetEntryAsync(entryId).ConfigureAwait(false);

		// Assert
		entry.ShouldNotBeNull();
		entry.EnqueuedAt.ShouldBeGreaterThanOrEqualTo(beforeEnqueue);
		entry.EnqueuedAt.ShouldBeLessThanOrEqualTo(DateTimeOffset.UtcNow);
	}

	[Fact]
	public async Task GenerateUniqueIdsForEachEnqueue()
	{
		// Arrange
		var queue = CreateQueue();
		var message = new TestMessage { Id = 1, Content = "Test" };

		// Act
		var ids = new HashSet<Guid>();
		for (var i = 0; i < 100; i++)
		{
			var id = await queue.EnqueueAsync(message, DeadLetterReason.MaxRetriesExceeded).ConfigureAwait(false);
			ids.Add(id);
		}

		// Assert
		ids.Count.ShouldBe(100);
	}

	[Fact]
	public async Task SupportAllDeadLetterReasons()
	{
		// Arrange
		var queue = CreateQueue();
		var message = new TestMessage { Id = 1, Content = "Test" };

		var reasons = Enum.GetValues<DeadLetterReason>();

		// Act & Assert
		foreach (var reason in reasons)
		{
			var entryId = await queue.EnqueueAsync(message, reason).ConfigureAwait(false);
			var entry = await queue.GetEntryAsync(entryId).ConfigureAwait(false);

			entry.ShouldNotBeNull();
			entry.Reason.ShouldBe(reason);
		}
	}

	#endregion

	#region Retrieve Tests

	[Fact]
	public async Task RetrieveEntryById()
	{
		// Arrange
		var queue = CreateQueue();
		var message = new TestMessage { Id = 42, Content = "Important" };
		var entryId = await queue.EnqueueAsync(message, DeadLetterReason.MaxRetriesExceeded).ConfigureAwait(false);

		// Act
		var entry = await queue.GetEntryAsync(entryId).ConfigureAwait(false);

		// Assert
		entry.ShouldNotBeNull();
		entry.Id.ShouldBe(entryId);
	}

	[Fact]
	public async Task ReturnNullForNonExistentEntry()
	{
		// Arrange
		var queue = CreateQueue();

		// Act
		var entry = await queue.GetEntryAsync(Guid.NewGuid()).ConfigureAwait(false);

		// Assert
		entry.ShouldBeNull();
	}

	[Fact]
	public async Task GetEntriesWithoutFilter()
	{
		// Arrange
		var queue = CreateQueue();
		await queue.EnqueueAsync(new TestMessage { Id = 1 }, DeadLetterReason.MaxRetriesExceeded).ConfigureAwait(false);
		await queue.EnqueueAsync(new TestMessage { Id = 2 }, DeadLetterReason.CircuitBreakerOpen).ConfigureAwait(false);
		await queue.EnqueueAsync(new TestMessage { Id = 3 }, DeadLetterReason.ValidationFailed).ConfigureAwait(false);

		// Act
		var entries = await queue.GetEntriesAsync().ConfigureAwait(false);

		// Assert
		entries.Count.ShouldBe(3);
	}

	[Fact]
	public async Task FilterEntriesByReason()
	{
		// Arrange
		var queue = CreateQueue();
		await queue.EnqueueAsync(new TestMessage { Id = 1 }, DeadLetterReason.MaxRetriesExceeded).ConfigureAwait(false);
		await queue.EnqueueAsync(new TestMessage { Id = 2 }, DeadLetterReason.CircuitBreakerOpen).ConfigureAwait(false);
		await queue.EnqueueAsync(new TestMessage { Id = 3 }, DeadLetterReason.MaxRetriesExceeded).ConfigureAwait(false);

		// Act
		var filter = DeadLetterQueryFilter.ByReason(DeadLetterReason.MaxRetriesExceeded);
		var entries = await queue.GetEntriesAsync(filter).ConfigureAwait(false);

		// Assert
		entries.Count.ShouldBe(2);
		entries.ShouldAllBe(e => e.Reason == DeadLetterReason.MaxRetriesExceeded);
	}

	[Fact]
	public async Task FilterEntriesByMessageType()
	{
		// Arrange
		var queue = CreateQueue();
		await queue.EnqueueAsync(new TestMessage { Id = 1 }, DeadLetterReason.MaxRetriesExceeded).ConfigureAwait(false);
		await queue.EnqueueAsync(new OrderMessage { OrderId = Guid.NewGuid() }, DeadLetterReason.MaxRetriesExceeded).ConfigureAwait(false);
		await queue.EnqueueAsync(new TestMessage { Id = 2 }, DeadLetterReason.MaxRetriesExceeded).ConfigureAwait(false);

		// Act
		var filter = DeadLetterQueryFilter.ByMessageType("TestMessage");
		var entries = await queue.GetEntriesAsync(filter).ConfigureAwait(false);

		// Assert
		entries.Count.ShouldBe(2);
		entries.ShouldAllBe(e => e.MessageType.Contains("TestMessage"));
	}

	[Fact]
	public async Task RespectLimitParameter()
	{
		// Arrange
		var queue = CreateQueue();
		for (var i = 0; i < 10; i++)
		{
			await queue.EnqueueAsync(new TestMessage { Id = i }, DeadLetterReason.MaxRetriesExceeded).ConfigureAwait(false);
		}

		// Act
		var entries = await queue.GetEntriesAsync(limit: 5).ConfigureAwait(false);

		// Assert
		entries.Count.ShouldBe(5);
	}

	[Fact]
	public async Task OrderEntriesByEnqueuedAtDescending()
	{
		// Arrange
		var queue = CreateQueue();
		await queue.EnqueueAsync(new TestMessage { Id = 1, Content = "First" }, DeadLetterReason.MaxRetriesExceeded).ConfigureAwait(false);
		await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(10).ConfigureAwait(false);
		await queue.EnqueueAsync(new TestMessage { Id = 2, Content = "Second" }, DeadLetterReason.MaxRetriesExceeded).ConfigureAwait(false);
		await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(10).ConfigureAwait(false);
		await queue.EnqueueAsync(new TestMessage { Id = 3, Content = "Third" }, DeadLetterReason.MaxRetriesExceeded).ConfigureAwait(false);

		// Act
		var entries = await queue.GetEntriesAsync().ConfigureAwait(false);

		// Assert - Should be in descending order (newest first)
		entries[0].EnqueuedAt.ShouldBeGreaterThan(entries[1].EnqueuedAt);
		entries[1].EnqueuedAt.ShouldBeGreaterThan(entries[2].EnqueuedAt);
	}

	#endregion

	#region Replay Tests

	[Fact]
	public async Task ReplayEntrySuccessfully()
	{
		// Arrange
		var replayedMessages = new List<object>();
		var queue = CreateQueue(msg =>
		{
			replayedMessages.Add(msg);
			return Task.CompletedTask;
		});

		var message = new TestMessage { Id = 42, Content = "Replay me" };
		var entryId = await queue.EnqueueAsync(message, DeadLetterReason.MaxRetriesExceeded).ConfigureAwait(false);

		// Act
		var result = await queue.ReplayAsync(entryId).ConfigureAwait(false);

		// Assert
		result.ShouldBeTrue();
		replayedMessages.Count.ShouldBe(1);
	}

	[Fact]
	public async Task MarkEntryAsReplayedAfterReplay()
	{
		// Arrange
		var queue = CreateQueue(msg => Task.CompletedTask);
		var message = new TestMessage { Id = 42, Content = "Replay me" };
		var entryId = await queue.EnqueueAsync(message, DeadLetterReason.MaxRetriesExceeded).ConfigureAwait(false);

		// Act
		await queue.ReplayAsync(entryId).ConfigureAwait(false);
		var entry = await queue.GetEntryAsync(entryId).ConfigureAwait(false);

		// Assert
		entry.ShouldNotBeNull();
		entry.IsReplayed.ShouldBeTrue();
		entry.ReplayedAt.ShouldNotBeNull();
	}

	[Fact]
	public async Task ReturnFalseWhenReplayingNonExistentEntry()
	{
		// Arrange
		var queue = CreateQueue(msg => Task.CompletedTask);

		// Act
		var result = await queue.ReplayAsync(Guid.NewGuid()).ConfigureAwait(false);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task ReplayBatchByFilter()
	{
		// Arrange
		var replayCount = 0;
		var queue = CreateQueue(msg =>
		{
			Interlocked.Increment(ref replayCount);
			return Task.CompletedTask;
		});

		await queue.EnqueueAsync(new TestMessage { Id = 1 }, DeadLetterReason.MaxRetriesExceeded).ConfigureAwait(false);
		await queue.EnqueueAsync(new TestMessage { Id = 2 }, DeadLetterReason.CircuitBreakerOpen).ConfigureAwait(false);
		await queue.EnqueueAsync(new TestMessage { Id = 3 }, DeadLetterReason.MaxRetriesExceeded).ConfigureAwait(false);

		// Act
		var filter = DeadLetterQueryFilter.ByReason(DeadLetterReason.MaxRetriesExceeded);
		var count = await queue.ReplayBatchAsync(filter).ConfigureAwait(false);

		// Assert
		count.ShouldBe(2);
		replayCount.ShouldBe(2);
	}

	#endregion

	#region Purge Tests

	[Fact]
	public async Task PurgeEntrySuccessfully()
	{
		// Arrange
		var queue = CreateQueue();
		var message = new TestMessage { Id = 1, Content = "Delete me" };
		var entryId = await queue.EnqueueAsync(message, DeadLetterReason.MaxRetriesExceeded).ConfigureAwait(false);

		// Act
		var result = await queue.PurgeAsync(entryId).ConfigureAwait(false);
		var entry = await queue.GetEntryAsync(entryId).ConfigureAwait(false);

		// Assert
		result.ShouldBeTrue();
		entry.ShouldBeNull();
	}

	[Fact]
	public async Task ReturnFalseWhenPurgingNonExistentEntry()
	{
		// Arrange
		var queue = CreateQueue();

		// Act
		var result = await queue.PurgeAsync(Guid.NewGuid()).ConfigureAwait(false);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task PurgeEntriesOlderThanSpecifiedAge()
	{
		// Arrange
		var queue = CreateQueue();

		// Add entries
		await queue.EnqueueAsync(new TestMessage { Id = 1 }, DeadLetterReason.MaxRetriesExceeded).ConfigureAwait(false);
		await queue.EnqueueAsync(new TestMessage { Id = 2 }, DeadLetterReason.MaxRetriesExceeded).ConfigureAwait(false);

		// Wait a bit
		await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(100).ConfigureAwait(false);

		// Add more entries
		await queue.EnqueueAsync(new TestMessage { Id = 3 }, DeadLetterReason.MaxRetriesExceeded).ConfigureAwait(false);

		// Act - Purge entries older than 50ms
		var purgedCount = await queue.PurgeOlderThanAsync(TimeSpan.FromMilliseconds(50)).ConfigureAwait(false);

		// Assert
		purgedCount.ShouldBe(2);
		var remaining = await queue.GetCountAsync().ConfigureAwait(false);
		remaining.ShouldBe(1);
	}

	#endregion

	#region Count Tests

	[Fact]
	public async Task GetTotalCount()
	{
		// Arrange
		var queue = CreateQueue();
		for (var i = 0; i < 5; i++)
		{
			await queue.EnqueueAsync(new TestMessage { Id = i }, DeadLetterReason.MaxRetriesExceeded).ConfigureAwait(false);
		}

		// Act
		var count = await queue.GetCountAsync().ConfigureAwait(false);

		// Assert
		count.ShouldBe(5);
	}

	[Fact]
	public async Task GetFilteredCount()
	{
		// Arrange
		var queue = CreateQueue();
		await queue.EnqueueAsync(new TestMessage { Id = 1 }, DeadLetterReason.MaxRetriesExceeded).ConfigureAwait(false);
		await queue.EnqueueAsync(new TestMessage { Id = 2 }, DeadLetterReason.CircuitBreakerOpen).ConfigureAwait(false);
		await queue.EnqueueAsync(new TestMessage { Id = 3 }, DeadLetterReason.MaxRetriesExceeded).ConfigureAwait(false);

		// Act
		var filter = DeadLetterQueryFilter.ByReason(DeadLetterReason.MaxRetriesExceeded);
		var count = await queue.GetCountAsync(filter).ConfigureAwait(false);

		// Assert
		count.ShouldBe(2);
	}

	[Fact]
	public async Task ReturnZeroForEmptyQueue()
	{
		// Arrange
		var queue = CreateQueue();

		// Act
		var count = await queue.GetCountAsync().ConfigureAwait(false);

		// Assert
		count.ShouldBe(0);
	}

	#endregion

	#region Poison Message Scenario Tests

	[Fact]
	public async Task HandlePoisonMessageReason()
	{
		// Arrange
		var queue = CreateQueue();
		var message = new TestMessage { Id = 999, Content = "Poison" };
		var exception = new FormatException("Cannot deserialize malformed message");

		// Act
		var entryId = await queue.EnqueueAsync(
			message,
			DeadLetterReason.PoisonMessage,
			exception,
			new Dictionary<string, string>
			{
				["poison_detail"] = "Invalid JSON structure",
				["original_queue"] = "orders-queue",
			}).ConfigureAwait(false);

		var entry = await queue.GetEntryAsync(entryId).ConfigureAwait(false);

		// Assert
		entry.ShouldNotBeNull();
		entry.Reason.ShouldBe(DeadLetterReason.PoisonMessage);
		entry.ExceptionMessage.ShouldBe("Cannot deserialize malformed message");
		entry.Metadata.ShouldNotBeNull();
		entry.Metadata["poison_detail"].ShouldBe("Invalid JSON structure");
	}

	[Fact]
	public async Task FilterPendingEntriesOnly()
	{
		// Arrange
		var queue = CreateQueue(msg => Task.CompletedTask);

		var id1 = await queue.EnqueueAsync(new TestMessage { Id = 1 }, DeadLetterReason.MaxRetriesExceeded).ConfigureAwait(false);
		await queue.EnqueueAsync(new TestMessage { Id = 2 }, DeadLetterReason.MaxRetriesExceeded).ConfigureAwait(false);

		// Replay first entry
		await queue.ReplayAsync(id1).ConfigureAwait(false);

		// Act
		var filter = DeadLetterQueryFilter.PendingOnly();
		var pending = await queue.GetEntriesAsync(filter).ConfigureAwait(false);

		// Assert
		pending.Count.ShouldBe(1);
		pending[0].IsReplayed.ShouldBeFalse();
	}

	#endregion

	#region Concurrent Access Tests

	[Fact]
	public async Task HandleConcurrentEnqueues()
	{
		// Arrange
		var queue = CreateQueue();
		const int concurrentOperations = 100;

		// Act
		var tasks = Enumerable.Range(0, concurrentOperations)
			.Select(i => queue.EnqueueAsync(
				new TestMessage { Id = i },
				DeadLetterReason.MaxRetriesExceeded))
			.ToList();

		var ids = await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert
		ids.Distinct().Count().ShouldBe(concurrentOperations);
		var count = await queue.GetCountAsync().ConfigureAwait(false);
		count.ShouldBe(concurrentOperations);
	}

	[Fact]
	public async Task HandleConcurrentPurges()
	{
		// Arrange
		var queue = CreateQueue();
		var ids = new List<Guid>();
		for (var i = 0; i < 50; i++)
		{
			var id = await queue.EnqueueAsync(
				new TestMessage { Id = i },
				DeadLetterReason.MaxRetriesExceeded).ConfigureAwait(false);
			ids.Add(id);
		}

		// Act - Concurrently purge all
		var tasks = ids.Select(id => queue.PurgeAsync(id)).ToList();
		var results = await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert
		results.Count(r => r).ShouldBe(50);
		var count = await queue.GetCountAsync().ConfigureAwait(false);
		count.ShouldBe(0);
	}

	#endregion
}
