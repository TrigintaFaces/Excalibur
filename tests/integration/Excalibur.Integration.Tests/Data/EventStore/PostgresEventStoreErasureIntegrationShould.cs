// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Postgres;

using Microsoft.Extensions.Logging.Abstractions;

using Npgsql;

using Shouldly;

using Xunit;

namespace Excalibur.Integration.Tests.Data.EventStore;

/// <summary>
/// First real-infrastructure coverage of the GDPR erase path on any provider.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists:</b> every existing test touching <c>EraseEventsAsync</c> is unit or in-memory, so the
/// provider's erase behaviour had only ever been established by reading the SQL. Four seats read the same
/// statement and reached four different confidence levels about whether it works. The statement sets
/// <c>event_data = NULL</c>, stamps a tombstone <c>event_type</c>, and writes <c>metadata =
/// @ErasureMetadata::jsonb</c> — into a column the schema declares <c>BYTEA</c>. Only the real engine can
/// settle whether that executes, and what it leaves behind.
/// </para>
/// <para>
/// <b>What is asserted is the GUARANTEE, not the mechanism.</b> The GDPR-critical fact is that the payload
/// is gone after an erasure request — not which column layout or cast the provider chose to get there. A
/// test written against the expected mechanism would pass on a provider that satisfies it a different way,
/// and fail on one that is merely different rather than broken.
/// </para>
/// <para>
/// <b>Both arms (testing-patterns §3):</b> SAFETY — after erasure the payload is unreadable and the
/// aggregate reports erased. LIVENESS — an untouched aggregate in the same table still round-trips, so an
/// erase that destroyed everything (or a store that returned nothing to anybody) cannot pass.
/// </para>
/// <para>
/// NON-SKIPPED (<c>DockerAvailable.ShouldBeTrue</c>): a skip-gated compliance lock is the gap that let this
/// path go unmeasured in the first place.
/// </para>
/// </remarks>
[Collection(PostgresEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Postgres")]
public sealed class PostgresEventStoreErasureIntegrationShould
{
	private const string AggregateType = "Order";

	private readonly PostgresEventStoreContainerFixture _fixture;

	public PostgresEventStoreErasureIntegrationShould(PostgresEventStoreContainerFixture fixture) =>
		_fixture = fixture;

	private PostgresEventStore Store() =>
		new(
			NpgsqlDataSource.Create(_fixture.ConnectionString),
			NullLogger<PostgresEventStore>.Instance,
			schema: "public",
			table: _fixture.TableName,
			tenantContext: UntenantedTestTenantContext.Instance);

	private sealed record OrderPlaced(string AggregateId, long Version) : IDomainEvent
	{
		public string EventId { get; init; } = Guid.NewGuid().ToString();
		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
		public string EventType { get; init; } = nameof(OrderPlaced);
		public IDictionary<string, object>? Metadata { get; init; }
	}

	/// <summary>
	/// Reads the raw payload column straight from the engine, bypassing the store's own read path. If the
	/// store both wrote and interpreted the tombstone, a defect in that interpretation would be invisible.
	/// </summary>
	private async Task<bool> AllPayloadsErasedAsync(string aggregateId)
	{
		await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync().ConfigureAwait(false);

		// CA2100: the only interpolated element is the fixture's constant table name; the aggregate id — the
		// sole value that could carry input — is a bound parameter.
#pragma warning disable CA2100
		await using var command = new NpgsqlCommand(
			$"SELECT COUNT(*) FROM public.{_fixture.TableName} "
			+ "WHERE aggregate_id = @aggId AND event_data IS NOT NULL",
			connection);
#pragma warning restore CA2100
		_ = command.Parameters.AddWithValue("aggId", aggregateId);

		var remaining = (long)(await command.ExecuteScalarAsync().ConfigureAwait(false))!;
		return remaining == 0;
	}

	[Fact]
	public async Task DestroyEveryPayloadForTheErasedAggregate_OnRealPostgres()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"GDPR erasure is a legal obligation — this real-Postgres lock must never be skipped");
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

		// The call under measurement. If the provider's statement cannot execute against the real engine,
		// this throws and the erasure guarantee is unmet — which is the finding, not a test defect.
		var count = await store.EraseEventsAsync(
			erased, AggregateType, Guid.NewGuid(), CancellationToken.None).ConfigureAwait(false);

		count.ShouldBe(2, "both events of the erased aggregate should be reported erased");

		// SAFETY — the personal data is gone, verified against the engine rather than the store's own reader.
		(await AllPayloadsErasedAsync(erased).ConfigureAwait(false))
			.ShouldBeTrue("no payload may survive an erasure request for the erased aggregate");

		(await store.IsErasedAsync(erased, AggregateType, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeTrue("the aggregate must report itself erased after erasure");

		// LIVENESS — erasure is targeted. A store that wiped the table, or returned nothing to anybody,
		// would satisfy the safety arm alone.
		(await AllPayloadsErasedAsync(untouched).ConfigureAwait(false))
			.ShouldBeFalse("an unrelated aggregate's payload must survive another aggregate's erasure");

		(await store.LoadAsync(untouched, AggregateType, -1, CancellationToken.None).ConfigureAwait(false))
			.Count.ShouldBe(1, "the untouched aggregate still loads its own event");

		(await store.IsErasedAsync(untouched, AggregateType, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeFalse("an unrelated aggregate must not report itself erased");
	}
}
