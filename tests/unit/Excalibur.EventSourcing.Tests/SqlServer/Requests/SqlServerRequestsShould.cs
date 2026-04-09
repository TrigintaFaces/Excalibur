// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Excalibur.Domain.Model;
using Excalibur.EventSourcing.SqlServer.Requests;

namespace Excalibur.EventSourcing.Tests.SqlServer.Requests;

/// <summary>
/// Unit tests for SQL Server Request classes in Excalibur.EventSourcing.SqlServer.
/// Validates constructor argument validation, command creation, SQL structure,
/// parameter setup, table names, and DataRequestBase property behavior for all request types.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
[Trait("Feature", "SqlServer")]
public sealed class SqlServerRequestsShould
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
		request.Command.CommandText.ShouldContain("INSERT INTO [dbo].[EventStoreEvents]");
		request.Command.CommandText.ShouldContain("OUTPUT INSERTED.Position");
	}

	[Fact]
	public void InsertEventRequest_ContainCorrectSqlStructure()
	{
		var request = new InsertEventRequest(
			"event-1", "agg-1", "OrderAggregate", "OrderCreated",
			new byte[] { 1, 2, 3 }, new byte[] { 4, 5 }, 1, DateTimeOffset.UtcNow, null, Ct);

		request.Command.CommandText.ShouldContain("EventId");
		request.Command.CommandText.ShouldContain("AggregateId");
		request.Command.CommandText.ShouldContain("AggregateType");
		request.Command.CommandText.ShouldContain("EventType");
		request.Command.CommandText.ShouldContain("EventData");
		request.Command.CommandText.ShouldContain("Metadata");
		request.Command.CommandText.ShouldContain("Version");
		request.Command.CommandText.ShouldContain("Timestamp");
	}

	[Fact]
	public void InsertEventRequest_SetParameterNames()
	{
		var request = new InsertEventRequest(
			"event-1", "agg-1", "OrderAggregate", "OrderCreated",
			new byte[] { 1, 2, 3 }, null, 1, DateTimeOffset.UtcNow, null, Ct);

		var paramNames = request.Parameters.ParameterNames.ToList();
		paramNames.ShouldContain("EventId");
		paramNames.ShouldContain("AggregateId");
		paramNames.ShouldContain("AggregateType");
		paramNames.ShouldContain("EventType");
		paramNames.ShouldContain("EventData");
		paramNames.ShouldContain("Metadata");
		paramNames.ShouldContain("Version");
		paramNames.ShouldContain("Timestamp");
	}

	[Fact]
	public void InsertEventRequest_PropagateTransaction()
	{
		var transaction = A.Fake<IDbTransaction>();

		var request = new InsertEventRequest(
			"event-1", "agg-1", "OrderAggregate", "OrderCreated",
			new byte[] { 1, 2, 3 }, null, 1, DateTimeOffset.UtcNow, transaction, Ct);

		request.Command.Transaction.ShouldBeSameAs(transaction);
	}

	[Fact]
	public void InsertEventRequest_AcceptNullTransaction()
	{
		var request = new InsertEventRequest(
			"event-1", "agg-1", "OrderAggregate", "OrderCreated",
			new byte[] { 1, 2, 3 }, null, 1, DateTimeOffset.UtcNow, null, Ct);

		request.Command.Transaction.ShouldBeNull();
	}

	[Fact]
	public void InsertEventRequest_HaveCorrectRequestType()
	{
		var request = new InsertEventRequest(
			"event-1", "agg-1", "OrderAggregate", "OrderCreated",
			new byte[] { 1, 2, 3 }, null, 1, DateTimeOffset.UtcNow, null, Ct);

		request.RequestType.ShouldBe("InsertEventRequest");
	}

	[Fact]
	public void InsertEventRequest_HaveResolveAsync()
	{
		var request = new InsertEventRequest(
			"event-1", "agg-1", "OrderAggregate", "OrderCreated",
			new byte[] { 1, 2, 3 }, null, 1, DateTimeOffset.UtcNow, null, Ct);

		request.ResolveAsync.ShouldNotBeNull();
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

	[Fact]
	public void InsertEventRequest_AcceptNullMetadata()
	{
		var request = new InsertEventRequest(
			"event-1", "agg-1", "OrderAggregate", "OrderCreated",
			new byte[] { 1, 2, 3 }, null, 1, DateTimeOffset.UtcNow, null, Ct);

		request.ShouldNotBeNull();
	}

	[Fact]
	public void InsertEventRequest_AcceptNonNullMetadata()
	{
		var request = new InsertEventRequest(
			"event-1", "agg-1", "OrderAggregate", "OrderCreated",
			new byte[] { 1, 2, 3 }, new byte[] { 10, 20 }, 1, DateTimeOffset.UtcNow, null, Ct);

		request.ShouldNotBeNull();
	}

	#endregion

	#region LoadEventsRequest

	[Fact]
	public void LoadEventsRequest_CreateSuccessfully_WithValidParameters()
	{
		var request = new LoadEventsRequest("agg-1", "OrderAggregate", -1, Ct);

		request.ShouldNotBeNull();
		request.Command.CommandText.ShouldContain("SELECT");
		request.Command.CommandText.ShouldContain("FROM [dbo].[EventStoreEvents]");
	}

	[Fact]
	public void LoadEventsRequest_ContainCorrectWhereClause()
	{
		var request = new LoadEventsRequest("agg-1", "OrderAggregate", -1, Ct);

		request.Command.CommandText.ShouldContain("AggregateId = @AggregateId");
		request.Command.CommandText.ShouldContain("AggregateType = @AggregateType");
		request.Command.CommandText.ShouldContain("Version > @FromVersion");
	}

	[Fact]
	public void LoadEventsRequest_OrderByVersionAsc()
	{
		var request = new LoadEventsRequest("agg-1", "OrderAggregate", -1, Ct);

		request.Command.CommandText.ShouldContain("ORDER BY Version ASC");
	}

	[Fact]
	public void LoadEventsRequest_SetParameterNames()
	{
		var request = new LoadEventsRequest("agg-1", "OrderAggregate", 5, Ct);

		var paramNames = request.Parameters.ParameterNames.ToList();
		paramNames.ShouldContain("AggregateId");
		paramNames.ShouldContain("AggregateType");
		paramNames.ShouldContain("FromVersion");
	}

	[Fact]
	public void LoadEventsRequest_HaveCorrectRequestType()
	{
		var request = new LoadEventsRequest("agg-1", "OrderAggregate", -1, Ct);

		request.RequestType.ShouldBe("LoadEventsRequest");
	}

	[Fact]
	public void LoadEventsRequest_HaveResolveAsync()
	{
		var request = new LoadEventsRequest("agg-1", "OrderAggregate", -1, Ct);

		request.ResolveAsync.ShouldNotBeNull();
	}

	[Fact]
	public void LoadEventsRequest_SelectCorrectColumns()
	{
		var request = new LoadEventsRequest("agg-1", "OrderAggregate", -1, Ct);

		request.Command.CommandText.ShouldContain("EventId");
		request.Command.CommandText.ShouldContain("EventData");
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

	[Fact]
	public void LoadEventsRequest_AcceptNegativeFromVersion()
	{
		var request = new LoadEventsRequest("agg-1", "OrderAggregate", -1, Ct);

		request.ShouldNotBeNull();
	}

	[Fact]
	public void LoadEventsRequest_AcceptZeroFromVersion()
	{
		var request = new LoadEventsRequest("agg-1", "OrderAggregate", 0, Ct);

		request.ShouldNotBeNull();
	}

	[Fact]
	public void LoadEventsRequest_AcceptPositiveFromVersion()
	{
		var request = new LoadEventsRequest("agg-1", "OrderAggregate", 100, Ct);

		request.ShouldNotBeNull();
	}

	#endregion

	#region GetCurrentVersionRequest

	[Fact]
	public void GetCurrentVersionRequest_CreateSuccessfully_WithValidParameters()
	{
		var request = new GetCurrentVersionRequest("agg-1", "OrderAggregate", null, Ct);

		request.ShouldNotBeNull();
		request.Command.CommandText.ShouldContain("ISNULL(MAX(Version), -1)");
	}

	[Fact]
	public void GetCurrentVersionRequest_TargetEventStoreEventsTable()
	{
		var request = new GetCurrentVersionRequest("agg-1", "OrderAggregate", null, Ct);

		request.Command.CommandText.ShouldContain("FROM [dbo].[EventStoreEvents]");
	}

	[Fact]
	public void GetCurrentVersionRequest_SetParameterNames()
	{
		var request = new GetCurrentVersionRequest("agg-1", "OrderAggregate", null, Ct);

		var paramNames = request.Parameters.ParameterNames.ToList();
		paramNames.ShouldContain("AggregateId");
		paramNames.ShouldContain("AggregateType");
	}

	[Fact]
	public void GetCurrentVersionRequest_PropagateTransaction()
	{
		var transaction = A.Fake<IDbTransaction>();

		var request = new GetCurrentVersionRequest("agg-1", "OrderAggregate", transaction, Ct);

		request.Command.Transaction.ShouldBeSameAs(transaction);
	}

	[Fact]
	public void GetCurrentVersionRequest_AcceptNullTransaction()
	{
		var request = new GetCurrentVersionRequest("agg-1", "OrderAggregate", null, Ct);

		request.Command.Transaction.ShouldBeNull();
	}

	[Fact]
	public void GetCurrentVersionRequest_HaveCorrectRequestType()
	{
		var request = new GetCurrentVersionRequest("agg-1", "OrderAggregate", null, Ct);

		request.RequestType.ShouldBe("GetCurrentVersionRequest");
	}

	[Fact]
	public void GetCurrentVersionRequest_HaveResolveAsync()
	{
		var request = new GetCurrentVersionRequest("agg-1", "OrderAggregate", null, Ct);

		request.ResolveAsync.ShouldNotBeNull();
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
		request.Command.CommandText.ShouldContain("[dbo].[EventStoreSnapshots]");
	}

	[Fact]
	public void GetLatestSnapshotRequest_SelectCorrectColumns()
	{
		var request = new GetLatestSnapshotRequest("agg-1", "OrderAggregate", Ct);

		request.Command.CommandText.ShouldContain("SnapshotId");
		request.Command.CommandText.ShouldContain("AggregateId");
		request.Command.CommandText.ShouldContain("AggregateType");
		request.Command.CommandText.ShouldContain("Version");
		request.Command.CommandText.ShouldContain("Data");
		request.Command.CommandText.ShouldContain("CreatedAt");
	}

	[Fact]
	public void GetLatestSnapshotRequest_SetParameterNames()
	{
		var request = new GetLatestSnapshotRequest("agg-1", "OrderAggregate", Ct);

		var paramNames = request.Parameters.ParameterNames.ToList();
		paramNames.ShouldContain("AggregateId");
		paramNames.ShouldContain("AggregateType");
	}

	[Fact]
	public void GetLatestSnapshotRequest_HaveCorrectRequestType()
	{
		var request = new GetLatestSnapshotRequest("agg-1", "OrderAggregate", Ct);

		request.RequestType.ShouldBe("GetLatestSnapshotRequest");
	}

	[Fact]
	public void GetLatestSnapshotRequest_HaveResolveAsync()
	{
		var request = new GetLatestSnapshotRequest("agg-1", "OrderAggregate", Ct);

		request.ResolveAsync.ShouldNotBeNull();
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

	#region DeleteSnapshotsRequest

	[Fact]
	public void DeleteSnapshotsRequest_CreateSuccessfully_WithValidParameters()
	{
		var request = new DeleteSnapshotsRequest("agg-1", "OrderAggregate", Ct);

		request.ShouldNotBeNull();
		request.Command.CommandText.ShouldContain("DELETE FROM [dbo].[EventStoreSnapshots]");
	}

	[Fact]
	public void DeleteSnapshotsRequest_FilterByAggregateIdAndType()
	{
		var request = new DeleteSnapshotsRequest("agg-1", "OrderAggregate", Ct);

		request.Command.CommandText.ShouldContain("AggregateId = @AggregateId");
		request.Command.CommandText.ShouldContain("AggregateType = @AggregateType");
	}

	[Fact]
	public void DeleteSnapshotsRequest_SetParameterNames()
	{
		var request = new DeleteSnapshotsRequest("agg-1", "OrderAggregate", Ct);

		var paramNames = request.Parameters.ParameterNames.ToList();
		paramNames.ShouldContain("AggregateId");
		paramNames.ShouldContain("AggregateType");
	}

	[Fact]
	public void DeleteSnapshotsRequest_HaveCorrectRequestType()
	{
		var request = new DeleteSnapshotsRequest("agg-1", "OrderAggregate", Ct);

		request.RequestType.ShouldBe("DeleteSnapshotsRequest");
	}

	[Fact]
	public void DeleteSnapshotsRequest_HaveResolveAsync()
	{
		var request = new DeleteSnapshotsRequest("agg-1", "OrderAggregate", Ct);

		request.ResolveAsync.ShouldNotBeNull();
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
		request.Command.CommandText.ShouldContain("Version < @Version");
	}

	[Fact]
	public void DeleteSnapshotsOlderThanRequest_TargetEventStoreSnapshotsTable()
	{
		var request = new DeleteSnapshotsOlderThanRequest("agg-1", "OrderAggregate", 5, Ct);

		request.Command.CommandText.ShouldContain("DELETE FROM [dbo].[EventStoreSnapshots]");
	}

	[Fact]
	public void DeleteSnapshotsOlderThanRequest_FilterByAggregateIdAndTypeAndVersion()
	{
		var request = new DeleteSnapshotsOlderThanRequest("agg-1", "OrderAggregate", 5, Ct);

		request.Command.CommandText.ShouldContain("AggregateId = @AggregateId");
		request.Command.CommandText.ShouldContain("AggregateType = @AggregateType");
		request.Command.CommandText.ShouldContain("Version < @Version");
	}

	[Fact]
	public void DeleteSnapshotsOlderThanRequest_SetParameterNames()
	{
		var request = new DeleteSnapshotsOlderThanRequest("agg-1", "OrderAggregate", 5, Ct);

		var paramNames = request.Parameters.ParameterNames.ToList();
		paramNames.ShouldContain("AggregateId");
		paramNames.ShouldContain("AggregateType");
		paramNames.ShouldContain("Version");
	}

	[Fact]
	public void DeleteSnapshotsOlderThanRequest_HaveCorrectRequestType()
	{
		var request = new DeleteSnapshotsOlderThanRequest("agg-1", "OrderAggregate", 5, Ct);

		request.RequestType.ShouldBe("DeleteSnapshotsOlderThanRequest");
	}

	[Fact]
	public void DeleteSnapshotsOlderThanRequest_HaveResolveAsync()
	{
		var request = new DeleteSnapshotsOlderThanRequest("agg-1", "OrderAggregate", 5, Ct);

		request.ResolveAsync.ShouldNotBeNull();
	}

	[Fact]
	public void DeleteSnapshotsOlderThanRequest_AcceptZeroVersion()
	{
		var request = new DeleteSnapshotsOlderThanRequest("agg-1", "OrderAggregate", 0, Ct);

		request.ShouldNotBeNull();
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
		var snapshot = CreateFakeSnapshot();

		var request = new SaveSnapshotRequest(snapshot, Ct);

		request.ShouldNotBeNull();
		request.Command.CommandText.ShouldContain("MERGE INTO [dbo].[EventStoreSnapshots] WITH (ROWLOCK, UPDLOCK)");
	}

	[Fact]
	public void SaveSnapshotRequest_ContainUpsertLogic()
	{
		var snapshot = CreateFakeSnapshot();

		var request = new SaveSnapshotRequest(snapshot, Ct);

		request.Command.CommandText.ShouldContain("WHEN MATCHED THEN");
		request.Command.CommandText.ShouldContain("UPDATE SET");
		request.Command.CommandText.ShouldContain("WHEN NOT MATCHED THEN");
		request.Command.CommandText.ShouldContain("INSERT");
	}

	[Fact]
	public void SaveSnapshotRequest_SetParameterNames()
	{
		var snapshot = CreateFakeSnapshot();

		var request = new SaveSnapshotRequest(snapshot, Ct);

		var paramNames = request.Parameters.ParameterNames.ToList();
		paramNames.ShouldContain("SnapshotId");
		paramNames.ShouldContain("AggregateId");
		paramNames.ShouldContain("AggregateType");
		paramNames.ShouldContain("Version");
		paramNames.ShouldContain("Data");
		paramNames.ShouldContain("CreatedAt");
	}

	[Fact]
	public void SaveSnapshotRequest_HaveCorrectRequestType()
	{
		var snapshot = CreateFakeSnapshot();

		var request = new SaveSnapshotRequest(snapshot, Ct);

		request.RequestType.ShouldBe("SaveSnapshotRequest");
	}

	[Fact]
	public void SaveSnapshotRequest_HaveResolveAsync()
	{
		var snapshot = CreateFakeSnapshot();

		var request = new SaveSnapshotRequest(snapshot, Ct);

		request.ResolveAsync.ShouldNotBeNull();
	}

	[Fact]
	public void SaveSnapshotRequest_ThrowOnNullSnapshot()
	{
		Should.Throw<ArgumentNullException>(() =>
			new SaveSnapshotRequest(null!, Ct));
	}

	#endregion

	// Outbox request regions removed -- types consolidated to Excalibur.Outbox packages
	// (AddOutboxMessageRequest, GetPendingOutboxMessagesRequest, DeletePublishedOutboxMessagesRequest,
	//  IncrementOutboxRetryCountRequest, MarkOutboxMessagePublishedRequest)

	#region DataRequestBase Properties

	[Fact]
	public void AllRequests_HaveUniqueRequestId()
	{
		var request1 = new LoadEventsRequest("agg-1", "OrderAggregate", -1, Ct);
		var request2 = new LoadEventsRequest("agg-1", "OrderAggregate", -1, Ct);

		request1.RequestId.ShouldNotBeNullOrEmpty();
		request2.RequestId.ShouldNotBeNullOrEmpty();
		request1.RequestId.ShouldNotBe(request2.RequestId);
	}

	[Fact]
	public void AllRequests_HaveValidGuidRequestId()
	{
		var request = new LoadEventsRequest("agg-1", "OrderAggregate", -1, Ct);

		Guid.TryParse(request.RequestId, out _).ShouldBeTrue();
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
		var assertionUpperBound1 = DateTimeOffset.UtcNow;
		request.CreatedAt.ShouldBeLessThanOrEqualTo(assertionUpperBound1);
	}

	[Fact]
	public void AllRequests_HaveResolveAsyncDelegate()
	{
		var request = new LoadEventsRequest("agg-1", "OrderAggregate", -1, Ct);

		request.ResolveAsync.ShouldNotBeNull();
	}

	[Fact]
	public void AllRequests_HaveNullCorrelationIdByDefault()
	{
		var request = new LoadEventsRequest("agg-1", "OrderAggregate", -1, Ct);

		request.CorrelationId.ShouldBeNull();
	}

	[Fact]
	public void AllRequests_AllowSettingCorrelationId()
	{
		var request = new LoadEventsRequest("agg-1", "OrderAggregate", -1, Ct);
		var correlationId = Guid.NewGuid().ToString();

		request.CorrelationId = correlationId;

		request.CorrelationId.ShouldBe(correlationId);
	}

	[Fact]
	public void AllRequests_HaveNullMetadataByDefault()
	{
		var request = new LoadEventsRequest("agg-1", "OrderAggregate", -1, Ct);

		request.Metadata.ShouldBeNull();
	}

	[Fact]
	public void AllRequests_AllowSettingMetadata()
	{
		var request = new LoadEventsRequest("agg-1", "OrderAggregate", -1, Ct);

		request.Metadata = new Dictionary<string, object> { ["key"] = "value" };

		request.Metadata.ShouldNotBeNull();
		request.Metadata["key"].ShouldBe("value");
	}

	#endregion

	#region Helpers

	private static ISnapshot CreateFakeSnapshot()
	{
		var snapshot = A.Fake<ISnapshot>();
		A.CallTo(() => snapshot.SnapshotId).Returns("snap-1");
		A.CallTo(() => snapshot.AggregateId).Returns("agg-1");
		A.CallTo(() => snapshot.AggregateType).Returns("OrderAggregate");
		A.CallTo(() => snapshot.Version).Returns(5);
		A.CallTo(() => snapshot.Data).Returns(new byte[] { 1, 2, 3 });
		A.CallTo(() => snapshot.CreatedAt).Returns(DateTimeOffset.UtcNow);
		return snapshot;
	}

	#endregion
}
