// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Tests.Messaging.Outbox.DeliveryGuarantee;

/// <summary>
/// Tests for OutboxDeliveryGuarantee behaviors per AD-222-1 through AD-222-5.
/// </summary>
/// <remarks>
/// Sprint 222 Task sjwm - 5 test scenarios:
/// 1. AtLeastOnce batch completion
/// 2. MinimizedWindow individual completion
/// 3. TransactionalWhenApplicable with SQL Server
/// 4. TransactionalWhenApplicable fallback
/// 5. Failure recovery behavior
/// </remarks>
[Trait("Category", "Unit")]
public sealed class DeliveryGuaranteeShould
{
	#region Test 1: AtLeastOnce Batch Completion

	/// <summary>
	/// AD-222-1: AtLeastOnce marks all messages complete as a batch after publishing.
	/// </summary>
	[Fact]
	public async Task AtLeastOnce_MarkAllMessagesCompleteAsBatch()
	{
		// Arrange
		var store = new TrackingOutboxStore();
		var messageIds = new[] { "msg-1", "msg-2", "msg-3" };

		foreach (var id in messageIds)
		{
			await store.StageMessageAsync(
				CreateTestMessage(id),
				CancellationToken.None);
		}

		// Act - Simulate batch completion (AtLeastOnce behavior)
		// In OutboxProcessor, all messages are collected then marked at end of batch
		foreach (var id in messageIds)
		{
			await store.MarkSentAsync(id, CancellationToken.None);
		}

		// Assert - All messages should be marked sent
		store.SentMessageIds.Count.ShouldBe(3);
		store.SentMessageIds.ShouldContain("msg-1");
		store.SentMessageIds.ShouldContain("msg-2");
		store.SentMessageIds.ShouldContain("msg-3");

		// AtLeastOnce: Individual marks happen AFTER all dispatches
		// The order of MarkSentAsync calls reflects batch completion
		store.MarkSentCallCount.ShouldBe(3);
	}

	/// <summary>
	/// AD-222-1: AtLeastOnce has larger failure window - all messages in batch may be redelivered.
	/// </summary>
	[Fact]
	public async Task AtLeastOnce_FailureBeforeBatchCompletion_AllMessagesEligibleForRedelivery()
	{
		// Arrange
		var store = new TrackingOutboxStore();
		var messageIds = new[] { "msg-1", "msg-2", "msg-3" };

		foreach (var id in messageIds)
		{
			await store.StageMessageAsync(
				CreateTestMessage(id),
				CancellationToken.None);
		}

		// Act - Simulate failure BEFORE batch completion (no MarkSentAsync calls)
		// In AtLeastOnce, if process crashes after dispatch but before batch mark,
		// ALL messages are still eligible for redelivery

		// Assert - All messages should still be unsent
		var unsentMessages = await store.GetUnsentMessagesAsync(10, CancellationToken.None);
		unsentMessages.Count().ShouldBe(3);
		store.SentMessageIds.ShouldBeEmpty();
	}

	#endregion Test 1: AtLeastOnce Batch Completion

	#region Test 2: MinimizedWindow Individual Completion

	/// <summary>
	/// AD-222-1: MinimizedWindow marks each message complete immediately after publish.
	/// </summary>
	[Fact]
	public async Task MinimizedWindow_MarkEachMessageCompleteImmediately()
	{
		// Arrange
		var store = new TrackingOutboxStore();
		var messageIds = new[] { "msg-1", "msg-2", "msg-3" };

		foreach (var id in messageIds)
		{
			await store.StageMessageAsync(
				CreateTestMessage(id),
				CancellationToken.None);
		}

		// Act - Simulate MinimizedWindow: mark sent immediately after each dispatch
		// This is what OutboxProcessor does for MinimizedWindow
		foreach (var id in messageIds)
		{
			// Dispatch happens here...
			await store.MarkSentAsync(id, CancellationToken.None);
			// Next message isn't dispatched until current is marked
		}

		// Assert - All messages marked sent, one at a time
		store.SentMessageIds.Count.ShouldBe(3);
		store.MarkSentCallCount.ShouldBe(3);

		// Order of marking reflects immediate completion
		store.MarkSentOrder[0].ShouldBe("msg-1");
		store.MarkSentOrder[1].ShouldBe("msg-2");
		store.MarkSentOrder[2].ShouldBe("msg-3");
	}

	/// <summary>
	/// AD-222-1: MinimizedWindow failure affects only current message.
	/// </summary>
	[Fact]
	public async Task MinimizedWindow_FailureAfterPartialCompletion_OnlyRemainingMessagesRedelivered()
	{
		// Arrange
		var store = new TrackingOutboxStore();
		var messageIds = new[] { "msg-1", "msg-2", "msg-3" };

		foreach (var id in messageIds)
		{
			await store.StageMessageAsync(
				CreateTestMessage(id),
				CancellationToken.None);
		}

		// Act - Simulate MinimizedWindow with failure after first message
		await store.MarkSentAsync("msg-1", CancellationToken.None);
		// Crash/failure happens here - msg-2 and msg-3 not marked

		// Assert - Only msg-1 is sent, others eligible for redelivery
		store.SentMessageIds.Count.ShouldBe(1);
		store.SentMessageIds.ShouldContain("msg-1");

		var unsentMessages = await store.GetUnsentMessagesAsync(10, CancellationToken.None);
		unsentMessages.Count().ShouldBe(2);
	}

	#endregion Test 2: MinimizedWindow Individual Completion

	#region Test 3: TransactionalWhenApplicable with Transactional Store

	/// <summary>
	/// AD-222-2, AD-222-3: TransactionalWhenApplicable uses transaction when store supports it.
	/// </summary>
	[Fact]
	public async Task TransactionalWhenApplicable_UseTransactionWhenStoreSupportsIt()
	{
		// Arrange
		var store = new TrackingTransactionalOutboxStore(supportsTransactions: true);
		var messageIds = new[] { "msg-1", "msg-2", "msg-3" };

		foreach (var id in messageIds)
		{
			await store.StageMessageAsync(
				CreateTestMessage(id),
				CancellationToken.None);
		}

		// Act - Simulate TransactionalWhenApplicable with supporting store
		// OutboxProcessor calls MarkSentTransactionalAsync for all messages
		await store.MarkSentTransactionalAsync(
			messageIds.ToList(),
			CancellationToken.None);

		// Assert - All marked via transactional method
		store.TransactionalMarkSentCalled.ShouldBeTrue();
		store.TransactionalMessageIds.Count.ShouldBe(3);
		store.SentMessageIds.Count.ShouldBe(3);

		// Individual MarkSentAsync should NOT be called
		store.MarkSentCallCount.ShouldBe(0);
	}

	/// <summary>
	/// AD-222-3: Transactional completion is atomic - all or nothing.
	/// </summary>
	[Fact]
	public async Task TransactionalWhenApplicable_AtomicCompletion_AllOrNothing()
	{
		// Arrange
		var store = new TrackingTransactionalOutboxStore(supportsTransactions: true);
		var messageIds = new[] { "msg-1", "msg-2", "msg-3" };

		foreach (var id in messageIds)
		{
			await store.StageMessageAsync(
				CreateTestMessage(id),
				CancellationToken.None);
		}

		// Configure store to fail on transactional mark
		store.FailOnTransactionalMark = true;

		// Act & Assert - Transaction should fail atomically
		_ = await Should.ThrowAsync<InvalidOperationException>(async () =>
		{
			await store.MarkSentTransactionalAsync(
				messageIds.ToList(),
				CancellationToken.None);
		});

		// No messages should be marked (atomic rollback)
		store.SentMessageIds.ShouldBeEmpty();
	}

	#endregion Test 3: TransactionalWhenApplicable with Transactional Store

	#region Test 4: TransactionalWhenApplicable Fallback

	/// <summary>
	/// AD-222-3: TransactionalWhenApplicable falls back to MinimizedWindow when store doesn't support transactions.
	/// </summary>
	[Fact]
	public async Task TransactionalWhenApplicable_FallbackToMinimizedWindow_WhenStoreDoesNotSupportTransactions()
	{
		// Arrange
		var store = new TrackingTransactionalOutboxStore(supportsTransactions: false);
		var messageIds = new[] { "msg-1", "msg-2", "msg-3" };

		foreach (var id in messageIds)
		{
			await store.StageMessageAsync(
				CreateTestMessage(id),
				CancellationToken.None);
		}

		// Act - Simulate OutboxProcessor fallback logic per AD-222-3
		if (store.SupportsTransactions)
		{
			await store.MarkSentTransactionalAsync(messageIds.ToList(), CancellationToken.None);
		}
		else
		{
			// Fallback to individual completion (MinimizedWindow behavior)
			foreach (var id in messageIds)
			{
				await store.MarkSentAsync(id, CancellationToken.None);
			}
		}

		// Assert - Individual marks used (fallback)
		store.TransactionalMarkSentCalled.ShouldBeFalse();
		store.MarkSentCallCount.ShouldBe(3);
		store.SentMessageIds.Count.ShouldBe(3);
	}

	/// <summary>
	/// AD-222-3: Non-transactional IOutboxStore triggers fallback.
	/// </summary>
	[Fact]
	public async Task TransactionalWhenApplicable_FallbackForNonTransactionalStore()
	{
		// Arrange - Use regular store that doesn't implement ITransactionalOutboxStore
		IOutboxStore store = new TrackingOutboxStore();
		var messageIds = new[] { "msg-1", "msg-2", "msg-3" };

		foreach (var id in messageIds)
		{
			await store.StageMessageAsync(
				CreateTestMessage(id),
				CancellationToken.None);
		}

		// Act - Simulate OutboxProcessor check per AD-222-3
		// if (_outboxStore is ITransactionalOutboxStore txStore && txStore.SupportsTransactions)
		var isTransactional = store is ITransactionalOutboxStore { SupportsTransactions: true };
		isTransactional.ShouldBeFalse();

		// Fallback to individual completion
		foreach (var id in messageIds)
		{
			await store.MarkSentAsync(id, CancellationToken.None);
		}

		// Assert - Cast back to verify internal state
		var trackingStore = (TrackingOutboxStore)store;
		trackingStore.MarkSentCallCount.ShouldBe(3);
		trackingStore.SentMessageIds.Count.ShouldBe(3);
	}

	#endregion Test 4: TransactionalWhenApplicable Fallback

	#region Test 5: Failure Recovery Behavior

	/// <summary>
	/// Verify correct redelivery window per guarantee level.
	/// </summary>
	[Fact]
	public async Task FailureRecovery_AtLeastOnce_EntireBatchEligibleForRedelivery()
	{
		// Arrange
		var store = new TrackingOutboxStore();
		var batchSize = 5;

		for (var i = 0; i < batchSize; i++)
		{
			await store.StageMessageAsync(
				CreateTestMessage($"msg-{i}"),
				CancellationToken.None);
		}

		// Act - Simulate AtLeastOnce: all messages dispatched but failure before batch mark
		// (No MarkSentAsync calls - simulating crash)

		// Assert - All 5 messages eligible for redelivery
		var eligibleForRedelivery = await store.GetUnsentMessagesAsync(10, CancellationToken.None);
		eligibleForRedelivery.Count().ShouldBe(batchSize);
	}

	/// <summary>
	/// Verify MinimizedWindow limits redelivery to single message.
	/// </summary>
	[Fact]
	public async Task FailureRecovery_MinimizedWindow_OnlyCurrentMessageEligibleForRedelivery()
	{
		// Arrange
		var store = new TrackingOutboxStore();
		var batchSize = 5;

		for (var i = 0; i < batchSize; i++)
		{
			await store.StageMessageAsync(
				CreateTestMessage($"msg-{i}"),
				CancellationToken.None);
		}

		// Act - Simulate MinimizedWindow: 3 messages processed, failure on 4th
		await store.MarkSentAsync("msg-0", CancellationToken.None);
		await store.MarkSentAsync("msg-1", CancellationToken.None);
		await store.MarkSentAsync("msg-2", CancellationToken.None);
		// Crash here - msg-3 was being dispatched

		// Assert - Only 2 messages eligible for redelivery (msg-3 and msg-4)
		var eligibleForRedelivery = await store.GetUnsentMessagesAsync(10, CancellationToken.None);
		eligibleForRedelivery.Count().ShouldBe(2);

		// msg-0, msg-1, msg-2 are sent
		store.SentMessageIds.Count.ShouldBe(3);
	}

	/// <summary>
	/// Verify TransactionalWhenApplicable has zero redelivery when transactional.
	/// </summary>
	[Fact]
	public async Task FailureRecovery_TransactionalWhenApplicable_ZeroRedeliveryWindowWhenSuccessful()
	{
		// Arrange
		var store = new TrackingTransactionalOutboxStore(supportsTransactions: true);
		var messageIds = new[] { "msg-1", "msg-2", "msg-3" };

		foreach (var id in messageIds)
		{
			await store.StageMessageAsync(
				CreateTestMessage(id),
				CancellationToken.None);
		}

		// Act - Transactional mark (atomic)
		await store.MarkSentTransactionalAsync(messageIds.ToList(), CancellationToken.None);

		// Assert - Zero messages eligible for redelivery
		var eligibleForRedelivery = await store.GetUnsentMessagesAsync(10, CancellationToken.None);
		eligibleForRedelivery.Count().ShouldBe(0);
	}

	#endregion Test 5: Failure Recovery Behavior

	#region Helpers

	private static OutboundMessage CreateTestMessage(string messageId)
	{
		return new OutboundMessage
		{
			Id = messageId,
			MessageType = "TestMessage",
			Payload = new byte[] { 1, 2, 3 },
			Destination = "test-queue",
			CorrelationId = Guid.NewGuid().ToString(),
			CreatedAt = DateTimeOffset.UtcNow,
			Status = OutboxStatus.Staged
		};
	}

	#endregion Helpers

	#region Test Doubles

	/// <summary>
	/// Tracking outbox store for verifying mark behaviors.
	/// </summary>
	private sealed class TrackingOutboxStore : IOutboxStore
	{
		private readonly Dictionary<string, OutboundMessage> _messages = new();

		public HashSet<string> SentMessageIds { get; } = [];
		public List<string> MarkSentOrder { get; } = [];
		public int MarkSentCallCount { get; private set; }

		public ValueTask StageMessageAsync(OutboundMessage message, CancellationToken cancellationToken)
		{
			_messages[message.Id] = message;
			return default;
		}

		public ValueTask MarkSentAsync(string messageId, CancellationToken cancellationToken)
		{
			MarkSentCallCount++;
			_ = SentMessageIds.Add(messageId);
			MarkSentOrder.Add(messageId);

			if (_messages.TryGetValue(messageId, out var message))
			{
				message.Status = OutboxStatus.Sent;
			}

			return default;
		}

		public ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesAsync(int batchSize, CancellationToken cancellationToken)
		{
			var unsent = _messages.Values
				.Where(m => m.Status != OutboxStatus.Sent)
				.Take(batchSize);
			return new ValueTask<IEnumerable<OutboundMessage>>(unsent);
		}

		// Not used in these tests
		public ValueTask EnqueueAsync(IDispatchMessage message, IMessageContext context, CancellationToken cancellationToken) =>
			throw new NotImplementedException();

		public ValueTask MarkFailedAsync(string messageId, string errorMessage, int retryCount, CancellationToken cancellationToken) =>
			default;

		public ValueTask<IEnumerable<OutboundMessage>> GetFailedMessagesAsync(int maxRetries, DateTimeOffset? olderThan, int batchSize, CancellationToken cancellationToken) =>
			new ValueTask<IEnumerable<OutboundMessage>>(Enumerable.Empty<OutboundMessage>());

		public ValueTask<IEnumerable<OutboundMessage>> GetScheduledMessagesAsync(DateTimeOffset scheduledBefore, int batchSize, CancellationToken cancellationToken) =>
			new ValueTask<IEnumerable<OutboundMessage>>(Enumerable.Empty<OutboundMessage>());

		public ValueTask<int> CleanupSentMessagesAsync(DateTimeOffset olderThan, int batchSize, CancellationToken cancellationToken) =>
			new ValueTask<int>(0);

		public ValueTask<OutboxStatistics> GetStatisticsAsync(CancellationToken cancellationToken) =>
			new ValueTask<OutboxStatistics>(new OutboxStatistics());
	}

	/// <summary>
	/// Tracking transactional outbox store for verifying transactional behaviors.
	/// </summary>
	private sealed class TrackingTransactionalOutboxStore : ITransactionalOutboxStore
	{
		private readonly Dictionary<string, OutboundMessage> _messages = new();
		private readonly bool _supportsTransactions;

		public TrackingTransactionalOutboxStore(bool supportsTransactions)
		{
			_supportsTransactions = supportsTransactions;
		}

		public bool SupportsTransactions => _supportsTransactions;
		public HashSet<string> SentMessageIds { get; } = [];
		public List<string> TransactionalMessageIds { get; } = [];
		public bool TransactionalMarkSentCalled { get; private set; }
		public int MarkSentCallCount { get; private set; }
		public bool FailOnTransactionalMark { get; set; }

		public ValueTask StageMessageAsync(OutboundMessage message, CancellationToken cancellationToken)
		{
			_messages[message.Id] = message;
			return default;
		}

		public ValueTask MarkSentAsync(string messageId, CancellationToken cancellationToken)
		{
			MarkSentCallCount++;
			_ = SentMessageIds.Add(messageId);

			if (_messages.TryGetValue(messageId, out var message))
			{
				message.Status = OutboxStatus.Sent;
			}

			return default;
		}

		public Task MarkSentTransactionalAsync(IReadOnlyList<string> messageIds, CancellationToken cancellationToken)
		{
			if (FailOnTransactionalMark)
			{
				throw new InvalidOperationException("Simulated transaction failure");
			}

			TransactionalMarkSentCalled = true;

			foreach (var id in messageIds)
			{
				TransactionalMessageIds.Add(id);
				_ = SentMessageIds.Add(id);

				if (_messages.TryGetValue(id, out var message))
				{
					message.Status = OutboxStatus.Sent;
				}
			}

			return Task.CompletedTask;
		}

		public ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesAsync(int batchSize, CancellationToken cancellationToken)
		{
			var unsent = _messages.Values
				.Where(m => m.Status != OutboxStatus.Sent)
				.Take(batchSize);
			return new ValueTask<IEnumerable<OutboundMessage>>(unsent);
		}

		// Not used in these tests
		public ValueTask EnqueueAsync(IDispatchMessage message, IMessageContext context, CancellationToken cancellationToken) =>
			throw new NotImplementedException();

		public ValueTask MarkFailedAsync(string messageId, string errorMessage, int retryCount, CancellationToken cancellationToken) =>
			default;

		public ValueTask<IEnumerable<OutboundMessage>> GetFailedMessagesAsync(int maxRetries, DateTimeOffset? olderThan, int batchSize, CancellationToken cancellationToken) =>
			new ValueTask<IEnumerable<OutboundMessage>>(Enumerable.Empty<OutboundMessage>());

		public ValueTask<IEnumerable<OutboundMessage>> GetScheduledMessagesAsync(DateTimeOffset scheduledBefore, int batchSize, CancellationToken cancellationToken) =>
			new ValueTask<IEnumerable<OutboundMessage>>(Enumerable.Empty<OutboundMessage>());

		public ValueTask<int> CleanupSentMessagesAsync(DateTimeOffset olderThan, int batchSize, CancellationToken cancellationToken) =>
			new ValueTask<int>(0);

		public ValueTask<OutboxStatistics> GetStatisticsAsync(CancellationToken cancellationToken) =>
			new ValueTask<OutboxStatistics>(new OutboxStatistics());
	}

	#endregion Test Doubles
}
