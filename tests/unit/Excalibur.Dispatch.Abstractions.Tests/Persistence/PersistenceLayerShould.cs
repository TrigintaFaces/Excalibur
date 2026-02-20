// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Snapshots;

namespace Excalibur.Dispatch.Abstractions.Tests.Persistence;

/// <summary>
/// Tests for persistence layer types and behaviors.
/// </summary>
[Trait("Category", "Unit")]
public sealed class PersistenceLayerShould
{
	#region IntervalSnapshotStrategy Tests

	[Fact]
	public void IntervalSnapshotStrategyDefaultsToHundred()
	{
		// Arrange
		var strategy = new IntervalSnapshotStrategy();
		var aggregate100 = A.Fake<IAggregateRoot>();
		var aggregate99 = A.Fake<IAggregateRoot>();
		_ = A.CallTo(() => aggregate100.Version).Returns(100);
		_ = A.CallTo(() => aggregate99.Version).Returns(99);

		// Act & Assert - should trigger at version 100
		strategy.ShouldCreateSnapshot(aggregate100).ShouldBeTrue();
		strategy.ShouldCreateSnapshot(aggregate99).ShouldBeFalse();
	}

	[Fact]
	public void IntervalSnapshotStrategyReturnsFalseForVersionZero()
	{
		// Arrange
		var strategy = new IntervalSnapshotStrategy(10);
		var aggregate = A.Fake<IAggregateRoot>();
		_ = A.CallTo(() => aggregate.Version).Returns(0);

		// Act
		var result = strategy.ShouldCreateSnapshot(aggregate);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void IntervalSnapshotStrategyTriggersAtInterval()
	{
		// Arrange
		var strategy = new IntervalSnapshotStrategy(10);
		var aggregate9 = A.Fake<IAggregateRoot>();
		var aggregate10 = A.Fake<IAggregateRoot>();
		var aggregate20 = A.Fake<IAggregateRoot>();
		_ = A.CallTo(() => aggregate9.Version).Returns(9);
		_ = A.CallTo(() => aggregate10.Version).Returns(10);
		_ = A.CallTo(() => aggregate20.Version).Returns(20);

		// Act & Assert
		strategy.ShouldCreateSnapshot(aggregate9).ShouldBeFalse();
		strategy.ShouldCreateSnapshot(aggregate10).ShouldBeTrue();
		strategy.ShouldCreateSnapshot(aggregate20).ShouldBeTrue();
	}

	#endregion IntervalSnapshotStrategy Tests

	#region ISnapshotStore Interface Tests

	[Fact]
	public async Task SnapshotStoreReturnsNullForNonExistent()
	{
		// Arrange
		var store = A.Fake<ISnapshotStore>();
		_ = A.CallTo(() => store.GetLatestSnapshotAsync("non-existent", "Order", A<CancellationToken>._))
			.Returns(new ValueTask<ISnapshot?>((ISnapshot?)null));

		// Act
		var snapshot = await store.GetLatestSnapshotAsync("non-existent", "Order", CancellationToken.None).ConfigureAwait(false);

		// Assert
		snapshot.ShouldBeNull();
	}

	[Fact]
	public async Task SnapshotStoreSavesSnapshot()
	{
		// Arrange
		var store = A.Fake<ISnapshotStore>();
		var snapshot = A.Fake<ISnapshot>();

		// Act
		await store.SaveSnapshotAsync(snapshot, CancellationToken.None).ConfigureAwait(false);

		// Assert
		_ = A.CallTo(() => store.SaveSnapshotAsync(snapshot, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task SnapshotStoreDeletesAllSnapshots()
	{
		// Arrange
		var store = A.Fake<ISnapshotStore>();

		// Act
		await store.DeleteSnapshotsAsync("agg", "Order", CancellationToken.None).ConfigureAwait(false);

		// Assert
		_ = A.CallTo(() => store.DeleteSnapshotsAsync("agg", "Order", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task SnapshotStoreDeletesOldSnapshots()
	{
		// Arrange
		var store = A.Fake<ISnapshotStore>();

		// Act
		await store.DeleteSnapshotsOlderThanAsync("agg", "Order", 50, CancellationToken.None).ConfigureAwait(false);

		// Assert
		_ = A.CallTo(() => store.DeleteSnapshotsOlderThanAsync("agg", "Order", 50, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task SnapshotStoreRetrievesLatest()
	{
		// Arrange
		var store = A.Fake<ISnapshotStore>();
		var snapshot = A.Fake<ISnapshot>();
		_ = A.CallTo(() => snapshot.Version).Returns(100);
		_ = A.CallTo(() => store.GetLatestSnapshotAsync("agg", "Order", A<CancellationToken>._))
			.Returns(new ValueTask<ISnapshot?>(snapshot));

		// Act
		var result = await store.GetLatestSnapshotAsync("agg", "Order", CancellationToken.None).ConfigureAwait(false);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Version.ShouldBe(100);
	}

	[Fact]
	public async Task SnapshotStoreSupportsCancellation()
	{
		// Arrange
		var store = A.Fake<ISnapshotStore>();
		using var cts = new CancellationTokenSource();
		cts.Cancel();

		_ = A.CallTo(() => store.GetLatestSnapshotAsync("agg", "Order", cts.Token))
			.ThrowsAsync(new OperationCanceledException());

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(async () =>
			await store.GetLatestSnapshotAsync("agg", "Order", cts.Token).ConfigureAwait(false)).ConfigureAwait(false);
	}

	[Fact]
	public async Task SnapshotStoreHandlesDifferentAggregateTypes()
	{
		// Arrange
		var store = A.Fake<ISnapshotStore>();
		var orderSnapshot = A.Fake<ISnapshot>();
		var customerSnapshot = A.Fake<ISnapshot>();
		_ = A.CallTo(() => orderSnapshot.Version).Returns(10);
		_ = A.CallTo(() => customerSnapshot.Version).Returns(20);

		_ = A.CallTo(() => store.GetLatestSnapshotAsync("agg", "Order", A<CancellationToken>._))
			.Returns(new ValueTask<ISnapshot?>(orderSnapshot));
		_ = A.CallTo(() => store.GetLatestSnapshotAsync("agg", "Customer", A<CancellationToken>._))
			.Returns(new ValueTask<ISnapshot?>(customerSnapshot));

		// Act
		var order = await store.GetLatestSnapshotAsync("agg", "Order", CancellationToken.None).ConfigureAwait(false);
		var customer = await store.GetLatestSnapshotAsync("agg", "Customer", CancellationToken.None).ConfigureAwait(false);

		// Assert
		order.Version.ShouldBe(10);
		customer.Version.ShouldBe(20);
	}

	#endregion ISnapshotStore Interface Tests

	#region IOutboxStore Interface Tests

	[Fact]
	public async Task OutboxStoreStagesMessage()
	{
		// Arrange
		var store = A.Fake<IOutboxStore>();
		var message = new OutboundMessage("TestMessage", [1, 2, 3], "destination");

		// Act
		await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

		// Assert
		_ = A.CallTo(() => store.StageMessageAsync(message, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task OutboxStoreGetUnsentRespectsBatchSize()
	{
		// Arrange
		var store = A.Fake<IOutboxStore>();
		var messages = Enumerable.Range(1, 5).Select(i => new OutboundMessage($"Msg{i}", [1], "dest")).ToList();
		_ = A.CallTo(() => store.GetUnsentMessagesAsync(5, A<CancellationToken>._))
			.Returns(new ValueTask<IEnumerable<OutboundMessage>>(messages));

		// Act
		var result = await store.GetUnsentMessagesAsync(5, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Count().ShouldBe(5);
	}

	[Fact]
	public async Task OutboxStoreMarksSentCorrectly()
	{
		// Arrange
		var store = A.Fake<IOutboxStore>();
		var messageId = "msg-123";

		// Act
		await store.MarkSentAsync(messageId, CancellationToken.None).ConfigureAwait(false);

		// Assert
		_ = A.CallTo(() => store.MarkSentAsync(messageId, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task OutboxStoreMarksFailedWithError()
	{
		// Arrange
		var store = A.Fake<IOutboxStore>();
		var messageId = "msg-123";
		var error = "Network timeout";

		// Act
		await store.MarkFailedAsync(messageId, error, 1, CancellationToken.None).ConfigureAwait(false);

		// Assert
		_ = A.CallTo(() => store.MarkFailedAsync(messageId, error, 1, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task OutboxStoreGetFailedFiltersByRetry()
	{
		// Arrange
		var store = A.Fake<IOutboxStoreAdmin>();
		var messages = new List<OutboundMessage> { new OutboundMessage("FailedMsg", [1], "dest") };
		_ = A.CallTo(() => store.GetFailedMessagesAsync(3, A<DateTimeOffset?>._, 100, A<CancellationToken>._))
			.Returns(new ValueTask<IEnumerable<OutboundMessage>>(messages));

		// Act
		var result = await store.GetFailedMessagesAsync(3, null, 100, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Count().ShouldBe(1);
	}

	[Fact]
	public async Task OutboxStoreGetScheduledFiltersByTime()
	{
		// Arrange
		var store = A.Fake<IOutboxStoreAdmin>();
		var scheduledBefore = DateTimeOffset.UtcNow;
		var messages = new List<OutboundMessage> { new OutboundMessage("ScheduledMsg", [1], "dest") };
		_ = A.CallTo(() => store.GetScheduledMessagesAsync(scheduledBefore, 100, A<CancellationToken>._))
			.Returns(new ValueTask<IEnumerable<OutboundMessage>>(messages));

		// Act
		var result = await store.GetScheduledMessagesAsync(scheduledBefore, 100, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Count().ShouldBe(1);
	}

	[Fact]
	public async Task OutboxStoreCleanupRemovesOld()
	{
		// Arrange
		var store = A.Fake<IOutboxStoreAdmin>();
		var olderThan = DateTimeOffset.UtcNow.AddDays(-7);
		_ = A.CallTo(() => store.CleanupSentMessagesAsync(olderThan, 1000, A<CancellationToken>._))
			.Returns(new ValueTask<int>(50));

		// Act
		var removed = await store.CleanupSentMessagesAsync(olderThan, 1000, CancellationToken.None).ConfigureAwait(false);

		// Assert
		removed.ShouldBe(50);
	}

	[Fact]
	public async Task OutboxStoreReturnsStatistics()
	{
		// Arrange
		var store = A.Fake<IOutboxStoreAdmin>();
		var stats = new OutboxStatistics();
		_ = A.CallTo(() => store.GetStatisticsAsync(A<CancellationToken>._))
			.Returns(new ValueTask<OutboxStatistics>(stats));

		// Act
		var result = await store.GetStatisticsAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		_ = result.ShouldNotBeNull();
	}

	[Fact]
	public async Task OutboxStoreEnqueuesWithContext()
	{
		// Arrange
		var store = A.Fake<IOutboxStore>();
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();

		// Act
		await store.EnqueueAsync(message, context, CancellationToken.None).ConfigureAwait(false);

		// Assert
		_ = A.CallTo(() => store.EnqueueAsync(message, context, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task OutboxStoreSupportsCancellation()
	{
		// Arrange
		var store = A.Fake<IOutboxStore>();
		using var cts = new CancellationTokenSource();
		cts.Cancel();

		_ = A.CallTo(() => store.GetUnsentMessagesAsync(100, cts.Token))
			.ThrowsAsync(new OperationCanceledException());

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(async () =>
			await store.GetUnsentMessagesAsync(100, cts.Token).ConfigureAwait(false)).ConfigureAwait(false);
	}

	#endregion IOutboxStore Interface Tests

	#region IInboxStore Interface Tests

	[Fact]
	public async Task InboxStoreCreatesEntry()
	{
		// Arrange
		var store = A.Fake<IInboxStore>();
		var entry = new InboxEntry("msg-1", "OrderHandler", "OrderCreated", [1, 2, 3]);
		_ = A.CallTo(() => store.CreateEntryAsync("msg-1", "OrderHandler", "OrderCreated", A<byte[]>._, A<IDictionary<string, object>>._, A<CancellationToken>._))
			.Returns(new ValueTask<InboxEntry>(entry));

		// Act
		var result = await store.CreateEntryAsync("msg-1", "OrderHandler", "OrderCreated", [], new Dictionary<string, object>(), CancellationToken.None).ConfigureAwait(false);

		// Assert
		_ = result.ShouldNotBeNull();
		result.MessageId.ShouldBe("msg-1");
	}

	[Fact]
	public async Task InboxStoreMarksProcessed()
	{
		// Arrange
		var store = A.Fake<IInboxStore>();

		// Act
		await store.MarkProcessedAsync("msg-1", "TestHandler", CancellationToken.None).ConfigureAwait(false);

		// Assert
		_ = A.CallTo(() => store.MarkProcessedAsync("msg-1", "TestHandler", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task InboxStoreChecksAlreadyProcessed()
	{
		// Arrange
		var store = A.Fake<IInboxStore>();
		_ = A.CallTo(() => store.IsProcessedAsync("msg-1", "TestHandler", A<CancellationToken>._))
			.Returns(new ValueTask<bool>(true));
		_ = A.CallTo(() => store.IsProcessedAsync("msg-2", "TestHandler", A<CancellationToken>._))
			.Returns(new ValueTask<bool>(false));

		// Act
		var processed = await store.IsProcessedAsync("msg-1", "TestHandler", CancellationToken.None).ConfigureAwait(false);
		var notProcessed = await store.IsProcessedAsync("msg-2", "TestHandler", CancellationToken.None).ConfigureAwait(false);

		// Assert
		processed.ShouldBeTrue();
		notProcessed.ShouldBeFalse();
	}

	[Fact]
	public async Task InboxStoreGetsFailed()
	{
		// Arrange
		var store = A.Fake<IInboxStore>();
		var entry = new InboxEntry("msg-1", "FailedHandler", "FailedCommand", [1, 2, 3]);
		entry.MarkFailed("Test error");
		var entries = new List<InboxEntry> { entry };
		_ = A.CallTo(() => store.GetFailedEntriesAsync(3, A<DateTimeOffset?>._, 100, A<CancellationToken>._))
			.Returns(new ValueTask<IEnumerable<InboxEntry>>(entries));

		// Act
		var result = await store.GetFailedEntriesAsync(3, null, 100, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Count().ShouldBe(1);
	}

	[Fact]
	public async Task InboxStoreCleansUp()
	{
		// Arrange
		var store = A.Fake<IInboxStore>();
		var retentionPeriod = TimeSpan.FromDays(30);
		_ = A.CallTo(() => store.CleanupAsync(retentionPeriod, A<CancellationToken>._))
			.Returns(new ValueTask<int>(100));

		// Act
		var removed = await store.CleanupAsync(retentionPeriod, CancellationToken.None).ConfigureAwait(false);

		// Assert
		removed.ShouldBe(100);
	}

	#endregion IInboxStore Interface Tests

	#region InboxEntry Behavior Tests

	[Fact]
	public void InboxEntryMarksProcessing()
	{
		// Arrange
		var entry = new InboxEntry("msg-1", "TestHandler", "TestCommand", [1, 2, 3]);

		// Act
		entry.MarkProcessing();

		// Assert
		entry.Status.ShouldBe(InboxStatus.Processing);
		_ = entry.LastAttemptAt.ShouldNotBeNull();
	}

	[Fact]
	public void InboxEntryMarksProcessed()
	{
		// Arrange
		var entry = new InboxEntry("msg-1", "TestHandler", "TestCommand", [1, 2, 3]);
		entry.MarkProcessing();

		// Act
		entry.MarkProcessed();

		// Assert
		entry.Status.ShouldBe(InboxStatus.Processed);
		_ = entry.ProcessedAt.ShouldNotBeNull();
		entry.LastError.ShouldBeNull();
	}

	[Fact]
	public void InboxEntryMarksFailed()
	{
		// Arrange
		var entry = new InboxEntry("msg-1", "TestHandler", "TestCommand", [1, 2, 3]);

		// Act
		entry.MarkFailed("Test error");

		// Assert
		entry.Status.ShouldBe(InboxStatus.Failed);
		entry.LastError.ShouldBe("Test error");
		entry.RetryCount.ShouldBe(1);
	}

	[Fact]
	public void InboxEntryChecksRetryEligibility()
	{
		// Arrange
		var entry = new InboxEntry("msg-1", "TestHandler", "TestCommand", [1, 2, 3]);

		// Not failed - not eligible
		entry.IsEligibleForRetry().ShouldBeFalse();

		// Failed once - eligible
		entry.MarkFailed("Error 1");
		entry.IsEligibleForRetry(maxRetries: 3, retryDelayMinutes: 0).ShouldBeTrue();

		// Max retries reached - not eligible
		entry.MarkFailed("Error 2");
		entry.MarkFailed("Error 3");
		entry.IsEligibleForRetry(maxRetries: 3).ShouldBeFalse();
	}

	#endregion InboxEntry Behavior Tests
}
