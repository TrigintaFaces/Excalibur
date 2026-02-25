// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Excalibur.Domain.Model;
using Excalibur.EventSourcing.Outbox;
using Excalibur.EventSourcing.Postgres.Requests;

namespace Excalibur.EventSourcing.Tests.Postgres.Requests;

/// <summary>
/// Unit tests for Postgres Request classes.
/// Validates constructor argument validation and command creation for all 14 request types.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
[Trait("Feature", "Postgres")]
public sealed class PostgresRequestsShould
{
	private static readonly CancellationToken Ct = CancellationToken.None;

	#region InsertEventRequest

	[Fact]
	public void InsertEventRequest_CreateSuccessfully_WithValidParameters()
	{
		var request = new InsertEventRequest(
			"event-1", "agg-1", "OrderAggregate", "OrderCreated",
			new byte[] { 1, 2, 3 }, null, 1, DateTimeOffset.UtcNow, null, Ct);

		request.ShouldNotBeNull();
		request.Command.CommandText.ShouldContain("INSERT INTO events");
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void InsertEventRequest_ThrowOnInvalidEventId(string? eventId)
	{
		Should.Throw<ArgumentException>(() =>
			new InsertEventRequest(eventId, "agg-1", "Agg", "Evt", new byte[] { 1 }, null, 1, DateTimeOffset.UtcNow, null, Ct));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void InsertEventRequest_ThrowOnInvalidAggregateId(string? aggregateId)
	{
		Should.Throw<ArgumentException>(() =>
			new InsertEventRequest("e1", aggregateId, "Agg", "Evt", new byte[] { 1 }, null, 1, DateTimeOffset.UtcNow, null, Ct));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void InsertEventRequest_ThrowOnInvalidAggregateType(string? aggregateType)
	{
		Should.Throw<ArgumentException>(() =>
			new InsertEventRequest("e1", "agg-1", aggregateType, "Evt", new byte[] { 1 }, null, 1, DateTimeOffset.UtcNow, null, Ct));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void InsertEventRequest_ThrowOnInvalidEventType(string? eventType)
	{
		Should.Throw<ArgumentException>(() =>
			new InsertEventRequest("e1", "agg-1", "Agg", eventType, new byte[] { 1 }, null, 1, DateTimeOffset.UtcNow, null, Ct));
	}

	[Fact]
	public void InsertEventRequest_ThrowOnNullEventData()
	{
		Should.Throw<ArgumentNullException>(() =>
			new InsertEventRequest("e1", "agg-1", "Agg", "Evt", null!, null, 1, DateTimeOffset.UtcNow, null, Ct));
	}

	#endregion

	#region LoadEventsRequest

	[Fact]
	public void LoadEventsRequest_CreateSuccessfully_WithValidParameters()
	{
		var request = new LoadEventsRequest("agg-1", "OrderAggregate", -1, Ct);

		request.ShouldNotBeNull();
		request.Command.CommandText.ShouldContain("SELECT");
		request.Command.CommandText.ShouldContain("FROM events");
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void LoadEventsRequest_ThrowOnInvalidAggregateId(string? aggregateId)
	{
		Should.Throw<ArgumentException>(() =>
			new LoadEventsRequest(aggregateId, "Agg", -1, Ct));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void LoadEventsRequest_ThrowOnInvalidAggregateType(string? aggregateType)
	{
		Should.Throw<ArgumentException>(() =>
			new LoadEventsRequest("agg-1", aggregateType, -1, Ct));
	}

	#endregion

	#region GetCurrentVersionRequest

	[Fact]
	public void GetCurrentVersionRequest_CreateSuccessfully_WithValidParameters()
	{
		var request = new GetCurrentVersionRequest("agg-1", "OrderAggregate", null, Ct);

		request.ShouldNotBeNull();
		request.Command.CommandText.ShouldContain("MAX(version)");
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void GetCurrentVersionRequest_ThrowOnInvalidAggregateId(string? aggregateId)
	{
		Should.Throw<ArgumentException>(() =>
			new GetCurrentVersionRequest(aggregateId, "Agg", null, Ct));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void GetCurrentVersionRequest_ThrowOnInvalidAggregateType(string? aggregateType)
	{
		Should.Throw<ArgumentException>(() =>
			new GetCurrentVersionRequest("agg-1", aggregateType, null, Ct));
	}

	#endregion

	#region GetLatestSnapshotRequest

	[Fact]
	public void GetLatestSnapshotRequest_CreateSuccessfully_WithValidParameters()
	{
		var request = new GetLatestSnapshotRequest("agg-1", "OrderAggregate", Ct);

		request.ShouldNotBeNull();
		request.Command.CommandText.ShouldContain("event_store_snapshots");
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void GetLatestSnapshotRequest_ThrowOnInvalidAggregateId(string? aggregateId)
	{
		Should.Throw<ArgumentException>(() =>
			new GetLatestSnapshotRequest(aggregateId, "Agg", Ct));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void GetLatestSnapshotRequest_ThrowOnInvalidAggregateType(string? aggregateType)
	{
		Should.Throw<ArgumentException>(() =>
			new GetLatestSnapshotRequest("agg-1", aggregateType, Ct));
	}

	#endregion

	#region MarkEventDispatchedRequest

	[Fact]
	public void MarkEventDispatchedRequest_CreateSuccessfully_WithValidParameters()
	{
		var request = new MarkEventDispatchedRequest("event-1", Ct);

		request.ShouldNotBeNull();
		request.Command.CommandText.ShouldContain("is_dispatched = true");
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void MarkEventDispatchedRequest_ThrowOnInvalidEventId(string? eventId)
	{
		Should.Throw<ArgumentException>(() =>
			new MarkEventDispatchedRequest(eventId, Ct));
	}

	#endregion

	#region DeleteSnapshotsRequest

	[Fact]
	public void DeleteSnapshotsRequest_CreateSuccessfully_WithValidParameters()
	{
		var request = new DeleteSnapshotsRequest("agg-1", "OrderAggregate", Ct);

		request.ShouldNotBeNull();
		request.Command.CommandText.ShouldContain("DELETE FROM snapshots");
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void DeleteSnapshotsRequest_ThrowOnInvalidAggregateId(string? aggregateId)
	{
		Should.Throw<ArgumentException>(() =>
			new DeleteSnapshotsRequest(aggregateId, "Agg", Ct));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void DeleteSnapshotsRequest_ThrowOnInvalidAggregateType(string? aggregateType)
	{
		Should.Throw<ArgumentException>(() =>
			new DeleteSnapshotsRequest("agg-1", aggregateType, Ct));
	}

	#endregion

	#region DeleteSnapshotsOlderThanRequest

	[Fact]
	public void DeleteSnapshotsOlderThanRequest_CreateSuccessfully_WithValidParameters()
	{
		var request = new DeleteSnapshotsOlderThanRequest("agg-1", "OrderAggregate", 5, Ct);

		request.ShouldNotBeNull();
		request.Command.CommandText.ShouldContain("version < @Version");
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void DeleteSnapshotsOlderThanRequest_ThrowOnInvalidAggregateId(string? aggregateId)
	{
		Should.Throw<ArgumentException>(() =>
			new DeleteSnapshotsOlderThanRequest(aggregateId, "Agg", 5, Ct));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void DeleteSnapshotsOlderThanRequest_ThrowOnInvalidAggregateType(string? aggregateType)
	{
		Should.Throw<ArgumentException>(() =>
			new DeleteSnapshotsOlderThanRequest("agg-1", aggregateType, 5, Ct));
	}

	#endregion

	#region SaveSnapshotRequest

	[Fact]
	public void SaveSnapshotRequest_CreateSuccessfully_WithValidParameters()
	{
		var snapshot = A.Fake<ISnapshot>();
		A.CallTo(() => snapshot.SnapshotId).Returns("snap-1");
		A.CallTo(() => snapshot.AggregateId).Returns("agg-1");
		A.CallTo(() => snapshot.AggregateType).Returns("OrderAggregate");
		A.CallTo(() => snapshot.Version).Returns(5);
		A.CallTo(() => snapshot.Data).Returns(new byte[] { 1, 2, 3 });
		A.CallTo(() => snapshot.CreatedAt).Returns(DateTimeOffset.UtcNow);

		var request = new SaveSnapshotRequest(snapshot, Ct);

		request.ShouldNotBeNull();
		request.Command.CommandText.ShouldContain("INSERT INTO event_store_snapshots");
		request.Command.CommandText.ShouldContain("ON CONFLICT");
	}

	[Fact]
	public void SaveSnapshotRequest_ThrowOnNullSnapshot()
	{
		Should.Throw<ArgumentNullException>(() =>
			new SaveSnapshotRequest(null!, Ct));
	}

	#endregion

	#region AddOutboxMessageRequest

	[Fact]
	public void AddOutboxMessageRequest_CreateSuccessfully_WithValidParameters()
	{
		var message = new OutboxMessage
		{
			Id = Guid.NewGuid(),
			AggregateId = "agg-1",
			AggregateType = "OrderAggregate",
			EventType = "OrderCreated",
			EventData = "{}",
			CreatedAt = DateTimeOffset.UtcNow,
			RetryCount = 0,
			MessageType = "DomainEvent"
		};
		var transaction = A.Fake<IDbTransaction>();

		var request = new AddOutboxMessageRequest(message, transaction, Ct);

		request.ShouldNotBeNull();
		request.Command.CommandText.ShouldContain("INSERT INTO event_sourced_outbox");
	}

	[Fact]
	public void AddOutboxMessageRequest_ThrowOnNullMessage()
	{
		var transaction = A.Fake<IDbTransaction>();

		Should.Throw<ArgumentNullException>(() =>
			new AddOutboxMessageRequest(null!, transaction, Ct));
	}

	[Fact]
	public void AddOutboxMessageRequest_ThrowOnNullTransaction()
	{
		var message = new OutboxMessage
		{
			Id = Guid.NewGuid(),
			AggregateId = "agg-1",
			AggregateType = "OrderAggregate",
			EventType = "OrderCreated",
			EventData = "{}",
			CreatedAt = DateTimeOffset.UtcNow,
			RetryCount = 0,
			MessageType = "DomainEvent"
		};

		Should.Throw<ArgumentNullException>(() =>
			new AddOutboxMessageRequest(message, null!, Ct));
	}

	#endregion

	#region GetPendingOutboxMessagesRequest

	[Fact]
	public void GetPendingOutboxMessagesRequest_CreateSuccessfully_WithValidParameters()
	{
		var request = new GetPendingOutboxMessagesRequest(100, Ct);

		request.ShouldNotBeNull();
		request.Command.CommandText.ShouldContain("published_at IS NULL");
		request.Command.CommandText.ShouldContain("LIMIT @BatchSize");
	}

	#endregion

	#region GetUndispatchedEventsRequest

	[Fact]
	public void GetUndispatchedEventsRequest_CreateSuccessfully_WithValidParameters()
	{
		var request = new GetUndispatchedEventsRequest(50, Ct);

		request.ShouldNotBeNull();
		request.Command.CommandText.ShouldContain("is_dispatched = false");
		request.Command.CommandText.ShouldContain("LIMIT @BatchSize");
	}

	#endregion

	#region DeletePublishedOutboxMessagesRequest

	[Fact]
	public void DeletePublishedOutboxMessagesRequest_CreateSuccessfully_WithValidParameters()
	{
		var request = new DeletePublishedOutboxMessagesRequest(TimeSpan.FromDays(7), Ct);

		request.ShouldNotBeNull();
		request.Command.CommandText.ShouldContain("DELETE FROM event_sourced_outbox");
		request.Command.CommandText.ShouldContain("published_at < @CutoffDate");
	}

	#endregion

	#region IncrementOutboxRetryCountRequest

	[Fact]
	public void IncrementOutboxRetryCountRequest_CreateSuccessfully_WithValidParameters()
	{
		var request = new IncrementOutboxRetryCountRequest(Guid.NewGuid(), Ct);

		request.ShouldNotBeNull();
		request.Command.CommandText.ShouldContain("retry_count = retry_count + 1");
	}

	#endregion

	#region MarkOutboxMessagePublishedRequest

	[Fact]
	public void MarkOutboxMessagePublishedRequest_CreateSuccessfully_WithValidParameters()
	{
		var request = new MarkOutboxMessagePublishedRequest(Guid.NewGuid(), null, Ct);

		request.ShouldNotBeNull();
		request.Command.CommandText.ShouldContain("published_at = @PublishedAt");
	}

	#endregion

	#region DataRequestBase Properties

	[Fact]
	public void AllRequests_HaveRequestId()
	{
		var request = new LoadEventsRequest("agg-1", "OrderAggregate", -1, Ct);

		request.RequestId.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public void AllRequests_HaveRequestType()
	{
		var request = new LoadEventsRequest("agg-1", "OrderAggregate", -1, Ct);

		request.RequestType.ShouldBe("LoadEventsRequest");
	}

	[Fact]
	public void AllRequests_HaveCreatedAtTimestamp()
	{
		var before = DateTimeOffset.UtcNow;
		var request = new LoadEventsRequest("agg-1", "OrderAggregate", -1, Ct);

		request.CreatedAt.ShouldBeGreaterThanOrEqualTo(before);
		request.CreatedAt.ShouldBeLessThanOrEqualTo(DateTimeOffset.UtcNow);
	}

	[Fact]
	public void AllRequests_HaveResolveAsyncDelegate()
	{
		var request = new LoadEventsRequest("agg-1", "OrderAggregate", -1, Ct);

		request.ResolveAsync.ShouldNotBeNull();
	}

	#endregion
}
