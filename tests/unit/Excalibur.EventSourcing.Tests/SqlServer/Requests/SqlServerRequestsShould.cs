// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Excalibur.Domain.Model;
using Excalibur.Dispatch;
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

	#region LoadEventsRequest

	[Fact]
	public void LoadEventsRequest_CreateSuccessfully_WithValidParameters()
	{
		var request = new LoadEventsRequest("agg-1", "OrderAggregate", -1, TenantScope.Scoped("tenant-1"), Ct);

		request.ShouldNotBeNull();
		request.Command.CommandText.ShouldContain("SELECT");
		request.Command.CommandText.ShouldContain("FROM [dbo].[EventStoreEvents]");
	}

	[Fact]
	public void LoadEventsRequest_ContainCorrectWhereClause()
	{
		var request = new LoadEventsRequest("agg-1", "OrderAggregate", -1, TenantScope.Scoped("tenant-1"), Ct);

		request.Command.CommandText.ShouldContain("AggregateId = @AggregateId");
		request.Command.CommandText.ShouldContain("AggregateType = @AggregateType");
		request.Command.CommandText.ShouldContain("Version > @FromVersion");
	}

	[Fact]
	public void LoadEventsRequest_OrderByVersionAsc()
	{
		var request = new LoadEventsRequest("agg-1", "OrderAggregate", -1, TenantScope.Scoped("tenant-1"), Ct);

		request.Command.CommandText.ShouldContain("ORDER BY Version ASC");
	}

	[Fact]
	public void LoadEventsRequest_SetParameterNames()
	{
		var request = new LoadEventsRequest("agg-1", "OrderAggregate", 5, TenantScope.Scoped("tenant-1"), Ct);

		var paramNames = request.Parameters.ParameterNames.ToList();
		paramNames.ShouldContain("AggregateId");
		paramNames.ShouldContain("AggregateType");
		paramNames.ShouldContain("FromVersion");
	}

	[Fact]
	public void LoadEventsRequest_HaveCorrectRequestType()
	{
		var request = new LoadEventsRequest("agg-1", "OrderAggregate", -1, TenantScope.Scoped("tenant-1"), Ct);

		request.RequestType.ShouldBe("LoadEventsRequest");
	}

	[Fact]
	public void LoadEventsRequest_HaveResolveAsync()
	{
		var request = new LoadEventsRequest("agg-1", "OrderAggregate", -1, TenantScope.Scoped("tenant-1"), Ct);

		request.ResolveAsync.ShouldNotBeNull();
	}

	[Fact]
	public void LoadEventsRequest_SelectCorrectColumns()
	{
		var request = new LoadEventsRequest("agg-1", "OrderAggregate", -1, TenantScope.Scoped("tenant-1"), Ct);

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
			new LoadEventsRequest(aggregateId, "Agg", -1, TenantScope.Untenanted, Ct));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void LoadEventsRequest_ThrowOnInvalidAggregateType(string? aggregateType)
	{
		Should.Throw<ArgumentException>(() =>
			new LoadEventsRequest("agg-1", aggregateType, -1, TenantScope.Untenanted, Ct));
	}

	[Fact]
	public void LoadEventsRequest_AcceptNegativeFromVersion()
	{
		var request = new LoadEventsRequest("agg-1", "OrderAggregate", -1, TenantScope.Scoped("tenant-1"), Ct);

		request.ShouldNotBeNull();
	}

	[Fact]
	public void LoadEventsRequest_AcceptZeroFromVersion()
	{
		var request = new LoadEventsRequest("agg-1", "OrderAggregate", 0, TenantScope.Scoped("tenant-1"), Ct);

		request.ShouldNotBeNull();
	}

	[Fact]
	public void LoadEventsRequest_AcceptPositiveFromVersion()
	{
		var request = new LoadEventsRequest("agg-1", "OrderAggregate", 100, TenantScope.Scoped("tenant-1"), Ct);

		request.ShouldNotBeNull();
	}

	#endregion

	#region GetCurrentVersionRequest

	[Fact]
	public void GetCurrentVersionRequest_CreateSuccessfully_WithValidParameters()
	{
		var request = new GetCurrentVersionRequest("agg-1", "OrderAggregate", null, TenantScope.Scoped("tenant-1"), Ct);

		request.ShouldNotBeNull();
		request.Command.CommandText.ShouldContain("ISNULL(MAX(Version), -1)");
	}

	[Fact]
	public void GetCurrentVersionRequest_TargetEventStoreEventsTable()
	{
		var request = new GetCurrentVersionRequest("agg-1", "OrderAggregate", null, TenantScope.Scoped("tenant-1"), Ct);

		request.Command.CommandText.ShouldContain("FROM [dbo].[EventStoreEvents]");
	}

	[Fact]
	public void GetCurrentVersionRequest_SetParameterNames()
	{
		var request = new GetCurrentVersionRequest("agg-1", "OrderAggregate", null, TenantScope.Scoped("tenant-1"), Ct);

		var paramNames = request.Parameters.ParameterNames.ToList();
		paramNames.ShouldContain("AggregateId");
		paramNames.ShouldContain("AggregateType");
	}

	[Fact]
	public void GetCurrentVersionRequest_PropagateTransaction()
	{
		var transaction = A.Fake<IDbTransaction>();

		var request = new GetCurrentVersionRequest("agg-1", "OrderAggregate", transaction, TenantScope.Scoped("tenant-1"), Ct);

		request.Command.Transaction.ShouldBeSameAs(transaction);
	}

	[Fact]
	public void GetCurrentVersionRequest_AcceptNullTransaction()
	{
		var request = new GetCurrentVersionRequest("agg-1", "OrderAggregate", null, TenantScope.Scoped("tenant-1"), Ct);

		request.Command.Transaction.ShouldBeNull();
	}

	[Fact]
	public void GetCurrentVersionRequest_HaveCorrectRequestType()
	{
		var request = new GetCurrentVersionRequest("agg-1", "OrderAggregate", null, TenantScope.Scoped("tenant-1"), Ct);

		request.RequestType.ShouldBe("GetCurrentVersionRequest");
	}

	[Fact]
	public void GetCurrentVersionRequest_HaveResolveAsync()
	{
		var request = new GetCurrentVersionRequest("agg-1", "OrderAggregate", null, TenantScope.Scoped("tenant-1"), Ct);

		request.ResolveAsync.ShouldNotBeNull();
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void GetCurrentVersionRequest_ThrowOnInvalidAggregateId(string? aggregateId)
	{
		Should.Throw<ArgumentException>(() =>
			new GetCurrentVersionRequest(aggregateId, "Agg", null, TenantScope.Untenanted, Ct));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void GetCurrentVersionRequest_ThrowOnInvalidAggregateType(string? aggregateType)
	{
		Should.Throw<ArgumentException>(() =>
			new GetCurrentVersionRequest("agg-1", aggregateType, null, TenantScope.Untenanted, Ct));
	}

	#endregion

	#region GetLatestSnapshotRequest

	[Fact]
	public void GetLatestSnapshotRequest_CreateSuccessfully_WithValidParameters()
	{
		var request = new GetLatestSnapshotRequest("agg-1", "OrderAggregate", TenantScope.Untenanted, Ct);

		request.ShouldNotBeNull();
		request.Command.CommandText.ShouldContain("[dbo].[EventStoreSnapshots]");
	}

	[Fact]
	public void GetLatestSnapshotRequest_SelectCorrectColumns()
	{
		var request = new GetLatestSnapshotRequest("agg-1", "OrderAggregate", TenantScope.Untenanted, Ct);

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
		var request = new GetLatestSnapshotRequest("agg-1", "OrderAggregate", TenantScope.Untenanted, Ct);

		var paramNames = request.Parameters.ParameterNames.ToList();
		paramNames.ShouldContain("AggregateId");
		paramNames.ShouldContain("AggregateType");
	}

	[Fact]
	public void GetLatestSnapshotRequest_HaveCorrectRequestType()
	{
		var request = new GetLatestSnapshotRequest("agg-1", "OrderAggregate", TenantScope.Untenanted, Ct);

		request.RequestType.ShouldBe("GetLatestSnapshotRequest");
	}

	[Fact]
	public void GetLatestSnapshotRequest_HaveResolveAsync()
	{
		var request = new GetLatestSnapshotRequest("agg-1", "OrderAggregate", TenantScope.Untenanted, Ct);

		request.ResolveAsync.ShouldNotBeNull();
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void GetLatestSnapshotRequest_ThrowOnInvalidAggregateId(string? aggregateId)
	{
		Should.Throw<ArgumentException>(() =>
			new GetLatestSnapshotRequest(aggregateId, "Agg", TenantScope.Untenanted, Ct));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void GetLatestSnapshotRequest_ThrowOnInvalidAggregateType(string? aggregateType)
	{
		Should.Throw<ArgumentException>(() =>
			new GetLatestSnapshotRequest("agg-1", aggregateType, TenantScope.Untenanted, Ct));
	}

	#endregion

	#region DeleteSnapshotsRequest

	[Fact]
	public void DeleteSnapshotsRequest_CreateSuccessfully_WithValidParameters()
	{
		var request = new DeleteSnapshotsRequest("agg-1", "OrderAggregate", TenantScope.Untenanted, Ct);

		request.ShouldNotBeNull();
		request.Command.CommandText.ShouldContain("DELETE FROM [dbo].[EventStoreSnapshots]");
	}

	[Fact]
	public void DeleteSnapshotsRequest_FilterByAggregateIdAndType()
	{
		var request = new DeleteSnapshotsRequest("agg-1", "OrderAggregate", TenantScope.Untenanted, Ct);

		request.Command.CommandText.ShouldContain("AggregateId = @AggregateId");
		request.Command.CommandText.ShouldContain("AggregateType = @AggregateType");
	}

	[Fact]
	public void DeleteSnapshotsRequest_SetParameterNames()
	{
		var request = new DeleteSnapshotsRequest("agg-1", "OrderAggregate", TenantScope.Untenanted, Ct);

		var paramNames = request.Parameters.ParameterNames.ToList();
		paramNames.ShouldContain("AggregateId");
		paramNames.ShouldContain("AggregateType");
	}

	[Fact]
	public void DeleteSnapshotsRequest_HaveCorrectRequestType()
	{
		var request = new DeleteSnapshotsRequest("agg-1", "OrderAggregate", TenantScope.Untenanted, Ct);

		request.RequestType.ShouldBe("DeleteSnapshotsRequest");
	}

	[Fact]
	public void DeleteSnapshotsRequest_HaveResolveAsync()
	{
		var request = new DeleteSnapshotsRequest("agg-1", "OrderAggregate", TenantScope.Untenanted, Ct);

		request.ResolveAsync.ShouldNotBeNull();
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void DeleteSnapshotsRequest_ThrowOnInvalidAggregateId(string? aggregateId)
	{
		Should.Throw<ArgumentException>(() =>
			new DeleteSnapshotsRequest(aggregateId, "Agg", TenantScope.Untenanted, Ct));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void DeleteSnapshotsRequest_ThrowOnInvalidAggregateType(string? aggregateType)
	{
		Should.Throw<ArgumentException>(() =>
			new DeleteSnapshotsRequest("agg-1", aggregateType, TenantScope.Untenanted, Ct));
	}

	#endregion

	#region DeleteSnapshotsOlderThanRequest

	[Fact]
	public void DeleteSnapshotsOlderThanRequest_CreateSuccessfully_WithValidParameters()
	{
		var request = new DeleteSnapshotsOlderThanRequest("agg-1", "OrderAggregate", 5, TenantScope.Untenanted, Ct);

		request.ShouldNotBeNull();
		request.Command.CommandText.ShouldContain("Version < @Version");
	}

	[Fact]
	public void DeleteSnapshotsOlderThanRequest_TargetEventStoreSnapshotsTable()
	{
		var request = new DeleteSnapshotsOlderThanRequest("agg-1", "OrderAggregate", 5, TenantScope.Untenanted, Ct);

		request.Command.CommandText.ShouldContain("DELETE FROM [dbo].[EventStoreSnapshots]");
	}

	[Fact]
	public void DeleteSnapshotsOlderThanRequest_FilterByAggregateIdAndTypeAndVersion()
	{
		var request = new DeleteSnapshotsOlderThanRequest("agg-1", "OrderAggregate", 5, TenantScope.Untenanted, Ct);

		request.Command.CommandText.ShouldContain("AggregateId = @AggregateId");
		request.Command.CommandText.ShouldContain("AggregateType = @AggregateType");
		request.Command.CommandText.ShouldContain("Version < @Version");
	}

	[Fact]
	public void DeleteSnapshotsOlderThanRequest_SetParameterNames()
	{
		var request = new DeleteSnapshotsOlderThanRequest("agg-1", "OrderAggregate", 5, TenantScope.Untenanted, Ct);

		var paramNames = request.Parameters.ParameterNames.ToList();
		paramNames.ShouldContain("AggregateId");
		paramNames.ShouldContain("AggregateType");
		paramNames.ShouldContain("Version");
	}

	[Fact]
	public void DeleteSnapshotsOlderThanRequest_HaveCorrectRequestType()
	{
		var request = new DeleteSnapshotsOlderThanRequest("agg-1", "OrderAggregate", 5, TenantScope.Untenanted, Ct);

		request.RequestType.ShouldBe("DeleteSnapshotsOlderThanRequest");
	}

	[Fact]
	public void DeleteSnapshotsOlderThanRequest_HaveResolveAsync()
	{
		var request = new DeleteSnapshotsOlderThanRequest("agg-1", "OrderAggregate", 5, TenantScope.Untenanted, Ct);

		request.ResolveAsync.ShouldNotBeNull();
	}

	[Fact]
	public void DeleteSnapshotsOlderThanRequest_AcceptZeroVersion()
	{
		var request = new DeleteSnapshotsOlderThanRequest("agg-1", "OrderAggregate", 0, TenantScope.Untenanted, Ct);

		request.ShouldNotBeNull();
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void DeleteSnapshotsOlderThanRequest_ThrowOnInvalidAggregateId(string? aggregateId)
	{
		Should.Throw<ArgumentException>(() =>
			new DeleteSnapshotsOlderThanRequest(aggregateId, "Agg", 5, TenantScope.Untenanted, Ct));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void DeleteSnapshotsOlderThanRequest_ThrowOnInvalidAggregateType(string? aggregateType)
	{
		Should.Throw<ArgumentException>(() =>
			new DeleteSnapshotsOlderThanRequest("agg-1", aggregateType, 5, TenantScope.Untenanted, Ct));
	}

	#endregion

	#region SaveSnapshotRequest

	[Fact]
	public void SaveSnapshotRequest_TargetTheSnapshotTable()
	{
		var snapshot = CreateFakeSnapshot();

		var request = new SaveSnapshotRequest(snapshot, TenantScope.Untenanted, Ct);

		request.ShouldNotBeNull();
		request.Command.CommandText.ShouldContain("[dbo].[EventStoreSnapshots]");

		// The upsert must NOT be a MERGE. A MERGE here took key-range locks on a clustered index keyed
		// (AggregateType, TenantId), so concurrent saves for DIFFERENT aggregates deadlocked and nothing
		// retried them. A revert to that shape must go red here.
		request.Command.CommandText.ShouldNotContain("MERGE", Case.Insensitive);
	}

	[Fact]
	public void SaveSnapshotRequest_ContainUpsertLogic()
	{
		var snapshot = CreateFakeSnapshot();

		var request = new SaveSnapshotRequest(snapshot, TenantScope.Untenanted, Ct);

		var sql = request.Command.CommandText;

		// Monotonicity: a lower-versioned snapshot must not overwrite a higher one.
		sql.ShouldContain("[Version] < @Version");

		// Insert only when the update matched nothing, and only when no row is already there.
		sql.ShouldContain("UPDATE");
		sql.ShouldContain("@@ROWCOUNT = 0");
		sql.ShouldContain("NOT EXISTS");
		sql.ShouldContain("INSERT INTO");

		// The concurrent-insert race converges by re-running the guarded update on a duplicate key.
		sql.ShouldContain("2627");
		sql.ShouldContain("2601");
	}

	[Fact]
	public void SaveSnapshotRequest_SetParameterNames()
	{
		var snapshot = CreateFakeSnapshot();

		var request = new SaveSnapshotRequest(snapshot, TenantScope.Untenanted, Ct);

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

		var request = new SaveSnapshotRequest(snapshot, TenantScope.Untenanted, Ct);

		request.RequestType.ShouldBe("SaveSnapshotRequest");
	}

	[Fact]
	public void SaveSnapshotRequest_HaveResolveAsync()
	{
		var snapshot = CreateFakeSnapshot();

		var request = new SaveSnapshotRequest(snapshot, TenantScope.Untenanted, Ct);

		request.ResolveAsync.ShouldNotBeNull();
	}

	[Fact]
	public void SaveSnapshotRequest_ThrowOnNullSnapshot()
	{
		Should.Throw<ArgumentNullException>(() =>
			new SaveSnapshotRequest(null!, TenantScope.Untenanted, Ct));
	}

	#endregion

	// Outbox request regions removed -- types consolidated to Excalibur.Outbox packages
	// (AddOutboxMessageRequest, GetPendingOutboxMessagesRequest, DeletePublishedOutboxMessagesRequest,
	//  IncrementOutboxRetryCountRequest, MarkOutboxMessagePublishedRequest)

	#region DataRequestBase Properties

	[Fact]
	public void AllRequests_HaveUniqueRequestId()
	{
		var request1 = new LoadEventsRequest("agg-1", "OrderAggregate", -1, TenantScope.Scoped("tenant-1"), Ct);
		var request2 = new LoadEventsRequest("agg-1", "OrderAggregate", -1, TenantScope.Scoped("tenant-1"), Ct);

		request1.RequestId.ShouldNotBeNullOrEmpty();
		request2.RequestId.ShouldNotBeNullOrEmpty();
		request1.RequestId.ShouldNotBe(request2.RequestId);
	}

	[Fact]
	public void AllRequests_HaveValidGuidRequestId()
	{
		var request = new LoadEventsRequest("agg-1", "OrderAggregate", -1, TenantScope.Scoped("tenant-1"), Ct);

		Guid.TryParse(request.RequestId, out _).ShouldBeTrue();
	}

	[Fact]
	public void AllRequests_HaveRequestType()
	{
		var request = new LoadEventsRequest("agg-1", "OrderAggregate", -1, TenantScope.Scoped("tenant-1"), Ct);

		request.RequestType.ShouldBe("LoadEventsRequest");
	}

	[Fact]
	public void AllRequests_HaveCreatedAtTimestamp()
	{
		var before = DateTimeOffset.UtcNow;
		var request = new LoadEventsRequest("agg-1", "OrderAggregate", -1, TenantScope.Scoped("tenant-1"), Ct);

		request.CreatedAt.ShouldBeGreaterThanOrEqualTo(before);
		var assertionUpperBound1 = DateTimeOffset.UtcNow;
		request.CreatedAt.ShouldBeLessThanOrEqualTo(assertionUpperBound1);
	}

	[Fact]
	public void AllRequests_HaveResolveAsyncDelegate()
	{
		var request = new LoadEventsRequest("agg-1", "OrderAggregate", -1, TenantScope.Scoped("tenant-1"), Ct);

		request.ResolveAsync.ShouldNotBeNull();
	}

	[Fact]
	public void AllRequests_HaveNullCorrelationIdByDefault()
	{
		var request = new LoadEventsRequest("agg-1", "OrderAggregate", -1, TenantScope.Scoped("tenant-1"), Ct);

		request.CorrelationId.ShouldBeNull();
	}

	[Fact]
	public void AllRequests_AllowSettingCorrelationId()
	{
		var request = new LoadEventsRequest("agg-1", "OrderAggregate", -1, TenantScope.Scoped("tenant-1"), Ct);
		var correlationId = Guid.NewGuid().ToString();

		request.CorrelationId = correlationId;

		request.CorrelationId.ShouldBe(correlationId);
	}

	[Fact]
	public void AllRequests_HaveNullMetadataByDefault()
	{
		var request = new LoadEventsRequest("agg-1", "OrderAggregate", -1, TenantScope.Scoped("tenant-1"), Ct);

		request.Metadata.ShouldBeNull();
	}

	[Fact]
	public void AllRequests_AllowSettingMetadata()
	{
		var request = new LoadEventsRequest("agg-1", "OrderAggregate", -1, TenantScope.Scoped("tenant-1"), Ct);

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
