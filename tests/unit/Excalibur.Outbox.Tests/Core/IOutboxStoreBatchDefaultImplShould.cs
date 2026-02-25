// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Outbox.Tests.Core;

/// <summary>
/// Tests for the default (sequential fallback) implementations of
/// <see cref="IOutboxStore.MarkBatchSentAsync"/> and
/// <see cref="IOutboxStore.MarkBatchFailedAsync"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class IOutboxStoreBatchDefaultImplShould : UnitTestBase
{
	private readonly TrackingOutboxStore _store = new();

	#region MarkBatchSentAsync Default

	[Fact]
	public async Task MarkBatchSentAsync_CallsMarkSentForEachId()
	{
		// Arrange
		var ids = new List<string> { "msg-1", "msg-2", "msg-3" };

		// Act — invoke via the interface to use the default implementation
		await ((IOutboxStore)_store).MarkBatchSentAsync(ids, CancellationToken.None);

		// Assert
		_store.SentIds.ShouldBe(["msg-1", "msg-2", "msg-3"]);
	}

	[Fact]
	public async Task MarkBatchSentAsync_HandlesEmptyList()
	{
		// Arrange
		var ids = new List<string>();

		// Act
		await ((IOutboxStore)_store).MarkBatchSentAsync(ids, CancellationToken.None);

		// Assert — no individual calls made
		_store.SentIds.ShouldBeEmpty();
	}

	[Fact]
	public async Task MarkBatchSentAsync_HandlesSingleItem()
	{
		// Arrange
		var ids = new List<string> { "msg-solo" };

		// Act
		await ((IOutboxStore)_store).MarkBatchSentAsync(ids, CancellationToken.None);

		// Assert
		_store.SentIds.ShouldBe(["msg-solo"]);
	}

	[Fact]
	public async Task MarkBatchSentAsync_PreservesOrder()
	{
		// Arrange
		var ids = new List<string> { "a", "b", "c", "d", "e" };

		// Act
		await ((IOutboxStore)_store).MarkBatchSentAsync(ids, CancellationToken.None);

		// Assert — sequential fallback processes in order
		_store.SentIds.ShouldBe(["a", "b", "c", "d", "e"]);
	}

	#endregion

	#region MarkBatchFailedAsync Default

	[Fact]
	public async Task MarkBatchFailedAsync_CallsMarkFailedForEachId()
	{
		// Arrange
		var ids = new List<string> { "msg-1", "msg-2" };

		// Act
		await ((IOutboxStore)_store).MarkBatchFailedAsync(ids, "Transport timeout", CancellationToken.None);

		// Assert
		_store.FailedEntries.Count.ShouldBe(2);
		_store.FailedEntries[0].ShouldBe(("msg-1", "Transport timeout"));
		_store.FailedEntries[1].ShouldBe(("msg-2", "Transport timeout"));
	}

	[Fact]
	public async Task MarkBatchFailedAsync_HandlesEmptyList()
	{
		// Arrange
		var ids = new List<string>();

		// Act
		await ((IOutboxStore)_store).MarkBatchFailedAsync(ids, "Error", CancellationToken.None);

		// Assert — no individual calls made
		_store.FailedEntries.ShouldBeEmpty();
	}

	[Fact]
	public async Task MarkBatchFailedAsync_HandlesSingleItem()
	{
		// Arrange
		var ids = new List<string> { "msg-fail" };

		// Act
		await ((IOutboxStore)_store).MarkBatchFailedAsync(ids, "Connection reset", CancellationToken.None);

		// Assert
		_store.FailedEntries.Count.ShouldBe(1);
		_store.FailedEntries[0].ShouldBe(("msg-fail", "Connection reset"));
	}

	[Fact]
	public async Task MarkBatchFailedAsync_PassesReasonToAllMessages()
	{
		// Arrange
		var ids = new List<string> { "a", "b", "c", "d" };
		const string reason = "Kafka broker unavailable";

		// Act
		await ((IOutboxStore)_store).MarkBatchFailedAsync(ids, reason, CancellationToken.None);

		// Assert — all calls use the same reason string
		_store.FailedEntries.Count.ShouldBe(4);
		_store.FailedEntries.ShouldAllBe(e => e.Reason == reason);
	}

	#endregion

	/// <summary>
	/// Minimal IOutboxStore implementation that tracks calls to MarkSentAsync and MarkFailedAsync.
	/// Does NOT override MarkBatchSentAsync/MarkBatchFailedAsync — relies on default sequential fallback.
	/// </summary>
	private sealed class TrackingOutboxStore : IOutboxStore
	{
		public List<string> SentIds { get; } = [];
		public List<(string Id, string Reason)> FailedEntries { get; } = [];

		public ValueTask MarkSentAsync(string messageId, CancellationToken cancellationToken)
		{
			SentIds.Add(messageId);
			return ValueTask.CompletedTask;
		}

		public ValueTask MarkFailedAsync(string messageId, string errorMessage, int retryCount, CancellationToken cancellationToken)
		{
			FailedEntries.Add((messageId, errorMessage));
			return ValueTask.CompletedTask;
		}

		// Required interface members — not under test
		public ValueTask StageMessageAsync(OutboundMessage message, CancellationToken cancellationToken) => ValueTask.CompletedTask;
		public ValueTask EnqueueAsync(IDispatchMessage message, IMessageContext context, CancellationToken cancellationToken) => ValueTask.CompletedTask;
		public ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesAsync(int batchSize, CancellationToken cancellationToken) => ValueTask.FromResult(Enumerable.Empty<OutboundMessage>());
		public ValueTask<IEnumerable<OutboundMessage>> GetFailedMessagesAsync(int maxRetries, DateTimeOffset? olderThan, int batchSize, CancellationToken cancellationToken) => ValueTask.FromResult(Enumerable.Empty<OutboundMessage>());
		public ValueTask<IEnumerable<OutboundMessage>> GetScheduledMessagesAsync(DateTimeOffset scheduledBefore, int batchSize, CancellationToken cancellationToken) => ValueTask.FromResult(Enumerable.Empty<OutboundMessage>());
		public ValueTask<int> CleanupSentMessagesAsync(DateTimeOffset olderThan, int batchSize, CancellationToken cancellationToken) => ValueTask.FromResult(0);
		public ValueTask<OutboxStatistics> GetStatisticsAsync(CancellationToken cancellationToken) => ValueTask.FromResult(new OutboxStatistics());
	}
}
