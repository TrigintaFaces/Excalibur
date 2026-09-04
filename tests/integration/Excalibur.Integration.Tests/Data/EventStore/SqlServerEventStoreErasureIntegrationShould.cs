// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Xunit;

namespace Excalibur.Integration.Tests.Data.EventStore;

/// <summary>
/// Real-infrastructure coverage of the GDPR erase path on SQL Server — the sibling of the Postgres lock,
/// written separately because the two providers fail differently and a shared assumption would hide that.
/// </summary>
/// <remarks>
/// <para>
/// Postgres rejected its erase outright: the statement cast the metadata to <c>jsonb</c> and assigned it to
/// a <c>BYTEA</c> column, and the engine refused the whole UPDATE — taking the payload deletion with it.
/// SQL Server's erase binds the same JSON as an <b>unparameterised string</b> into a <c>VARBINARY(MAX)</c>
/// column while the insert path binds <c>DbType.Binary</c> from a <c>byte[]</c>. Implicit
/// <c>nvarchar</c>→<c>varbinary</c> conversion exists on this engine, so the statement plausibly executes —
/// which would make the payload deletion succeed while the metadata is written in a different encoding
/// from every row the insert path produced.
/// </para>
/// <para>
/// <b>That is a prediction, and predictions about server-side coercion are exactly what this test exists to
/// replace.</b> The assertions below bind the guarantee — the payload is gone — and let the engine decide
/// whether the metadata stamp is a second defect or a non-issue.
/// </para>
/// <para>
/// <b>Both arms (testing-patterns §3):</b> SAFETY — the erased aggregate's payload is unreadable, verified
/// straight from the engine rather than through the store's own reader. LIVENESS — an untouched aggregate
/// in the same table still round-trips and still reports not-erased, so an erase that destroyed everything
/// could not pass.
/// </para>
/// </remarks>
[Collection(SqlServerEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerEventStoreErasureIntegrationShould
{
	private const string AggregateType = "Order";

	private readonly SqlServerEventStoreContainerFixture _fixture;

	public SqlServerEventStoreErasureIntegrationShould(SqlServerEventStoreContainerFixture fixture) =>
		_fixture = fixture;

	private SqlServerEventStore Store() =>
		new(
			() => _fixture.CreateConnection(),
			NullLogger<SqlServerEventStore>.Instance,
			schema: _fixture.SchemaName,
			table: _fixture.TableName,
			tenantContext: UntenantedTestTenantContext.Instance);

[MessageName("Test.SqlServerEventStoreErasureIntegration.OrderPlaced")]
private sealed record OrderPlaced(string AggregateId, long Version) : IDomainEvent
	{
		public string EventId { get; init; } = Guid.NewGuid().ToString();
		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
		public IDictionary<string, object>? Metadata { get; init; }
	}

	/// <summary>
	/// Counts surviving payloads directly against the engine. Reading through the store would let a defect
	/// in the store's own interpretation of its tombstone conceal a payload that is still present.
	/// </summary>
	private async Task<long> SurvivingPayloadCountAsync(string aggregateId)
	{
		await using var connection = (SqlConnection)_fixture.CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		// CA2100: the only interpolated elements are the fixture's constant schema/table names; the
		// aggregate id — the sole value that could carry input — is a bound parameter.
#pragma warning disable CA2100
		await using var command = new SqlCommand(
			$"SELECT COUNT_BIG(*) FROM [{_fixture.SchemaName}].[{_fixture.TableName}] "
			+ "WHERE AggregateId = @aggId AND EventData IS NOT NULL",
			connection);
#pragma warning restore CA2100
		_ = command.Parameters.AddWithValue("@aggId", aggregateId);

		return (long)(await command.ExecuteScalarAsync().ConfigureAwait(false))!;
	}

	[Fact]
	public async Task DestroyEveryPayloadForTheErasedAggregate_OnRealSqlServer()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"GDPR erasure is a legal obligation — this real-SQL-Server lock must never be skipped");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		var erased = "agg-" + Guid.NewGuid().ToString("N");
		var untouched = "agg-" + Guid.NewGuid().ToString("N");
		var store = Store();

		_ = await store.AppendAsync(
			erased, AggregateType,
			new IDomainEvent[] { new OrderPlaced(erased, 0), new OrderPlaced(erased, 1) },
			-1, CancellationToken.None).ConfigureAwait(false);

		_ = await store.AppendAsync(
			untouched, AggregateType,
			new IDomainEvent[] { new OrderPlaced(untouched, 0) },
			-1, CancellationToken.None).ConfigureAwait(false);

		var count = await store.EraseEventsAsync(
			erased, AggregateType, Guid.NewGuid(), CancellationToken.None).ConfigureAwait(false);

		count.ShouldBe(2, "both events of the erased aggregate should be reported erased");

		// SAFETY — the personal data is gone.
		(await SurvivingPayloadCountAsync(erased).ConfigureAwait(false))
			.ShouldBe(0, "no payload may survive an erasure request for the erased aggregate");

		(await store.IsErasedAsync(erased, AggregateType, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeTrue("the aggregate must report itself erased after erasure");

		// LIVENESS — erasure is targeted, not indiscriminate.
		(await SurvivingPayloadCountAsync(untouched).ConfigureAwait(false))
			.ShouldBe(1, "an unrelated aggregate's payload must survive another aggregate's erasure");

		(await store.LoadAsync(untouched, AggregateType, -1, CancellationToken.None).ConfigureAwait(false))
			.Count.ShouldBe(1, "the untouched aggregate still loads its own event");

		(await store.IsErasedAsync(untouched, AggregateType, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeFalse("an unrelated aggregate must not report itself erased");
	}
}
