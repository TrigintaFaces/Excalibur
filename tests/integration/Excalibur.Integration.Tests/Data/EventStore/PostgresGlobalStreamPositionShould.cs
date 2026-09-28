// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Postgres;
using Excalibur.EventSourcing.Postgres.DependencyInjection;
using Excalibur.EventSourcing.Queries;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Npgsql;

using Shouldly;

using Xunit;

namespace Excalibur.Integration.Tests.Data.EventStore;

/// <summary>
/// The PostgreSQL event store allocates gapless global positions and exposes them through a readable
/// global stream.
/// </summary>
/// <remarks>
/// <para>
/// <b>What is being protected.</b> <c>position</c> was previously a <c>BIGSERIAL</c>. PostgreSQL
/// advances a sequence NON-TRANSACTIONALLY by design -- that is documented behaviour, not an
/// implementation accident -- so <c>nextval()</c> is not rolled back by an aborted transaction and two
/// concurrent appends can commit in the opposite order to the positions they hold. Either one is silent,
/// permanent event loss for a subscriber tailing the stream: the event is committed and durable and
/// below a high-water mark that has already passed it.
/// </para>
/// <para>
/// The store now allocates the position by UPDATEing a counter row inside the appending transaction. The
/// row lock is released only at COMMIT and the increment rolls back with the transaction.
/// </para>
/// <para>
/// <b>verify-against-real-infra-not-mock.</b> The property under test IS PostgreSQL's locking and
/// rollback behaviour, which a mocked connection cannot reproduce -- it returns what it was told.
/// NON-SKIPPED (<c>DockerAvailable.ShouldBeTrue</c>).
/// </para>
/// <para>
/// <b>Both arms (testing-patterns section 3).</b> SAFETY -- a losing append consumes no position and the
/// committed stream stays contiguous. LIVENESS -- appends still succeed and the stream reads them back,
/// so a store that refused every append (and is therefore trivially gapless) fails rather than passes.
/// </para>
/// <para>
/// Positions are asserted RELATIVE to a seed rather than as absolute values: the fixture's cleanup
/// truncates the events table but does not reset the position counter, which is correct -- positions are
/// monotonic for the life of the database, not per test.
/// </para>
/// </remarks>
[Collection(PostgresEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Postgres")]
public sealed class PostgresGlobalStreamPositionShould
{
	private const string AggregateType = "Order";

	private readonly PostgresEventStoreContainerFixture _fixture;

	public PostgresGlobalStreamPositionShould(PostgresEventStoreContainerFixture fixture) => _fixture = fixture;

	[MessageName("Test.PostgresGlobalStreamPosition.OrderPlaced")]
	private sealed record OrderPlaced(string AggregateId) : IDomainEvent
	{
		public string EventId { get; init; } = Guid.NewGuid().ToString();

		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

		public IDictionary<string, object>? Metadata { get; init; }
	}

	private PostgresEventStore Store() =>
		new(_fixture.ConnectionString, NullLogger<PostgresEventStore>.Instance, SingleTenantTestContext.Instance);

	private PostgresGlobalStreamQuery Query(NpgsqlDataSource dataSource) =>
		new(dataSource, Options.Create(new PostgresEventSourcingOptions
		{
			EventStoreSchema = "public",
			EventStoreTable = _fixture.TableName,
		}));

	private static async Task<long> AppendAsync(PostgresEventStore store, string aggregateId, long expectedVersion)
	{
		var result = await store.AppendAsync(
				aggregateId,
				AggregateType,
				new IDomainEvent[] { new OrderPlaced(aggregateId) },
				expectedVersion,
				CancellationToken.None)
			.ConfigureAwait(false);

		result.Success.ShouldBeTrue("the append must succeed for its position to mean anything");

		return result.FirstEventPosition.ShouldNotBeNull(
			"a successful append must report the global position it was allocated");
	}

	[Fact]
	public async Task ReadTheGlobalStreamInPositionOrderAcrossAggregates()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"global stream ordering is a data-loss boundary -- this real-PostgreSQL lock must never be skipped");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		var store = Store();
		var first = "agg-" + Guid.NewGuid().ToString("N");
		var second = "agg-" + Guid.NewGuid().ToString("N");

		var p1 = await AppendAsync(store, first, -1).ConfigureAwait(false);
		var p2 = await AppendAsync(store, second, -1).ConfigureAwait(false);
		var p3 = await AppendAsync(store, first, 0).ConfigureAwait(false);

		// SAFETY -- the counter is global, not per stream: returning to the first aggregate yields the
		// highest position, not a value continuing that stream's own numbering.
		p2.ShouldBe(p1 + 1);
		p3.ShouldBe(p2 + 1);

		await using var dataSource = NpgsqlDataSource.Create(_fixture.ConnectionString);
		// Reads are EXCLUSIVE of the cursor, so starting at p1 - 1 is what includes p1. The cursor is
		// the last DELIVERED position, never the next one to fetch.
		var events = await Query(dataSource)
			.ReadAllAsync(new GlobalStreamPosition(p1 - 1, DateTimeOffset.MinValue), 100, CancellationToken.None)
			.ConfigureAwait(false);

		// LIVENESS -- the query actually returns them. Everything above would still hold if the stream
		// were unreadable, which is the state this provider was in before it had a query at all.
		events.Count.ShouldBe(3, "the global stream must return every appended event");
		events.Select(static e => e.GlobalPosition).ShouldBe(new[] { p1, p2, p3 }, "in position order");

		// The row mapping is aliased rather than conventional, so assert a non-position column actually
		// arrived: a mis-aliased SELECT would still produce the right count and order.
		events[0].AggregateType.ShouldBe(AggregateType, "the aliased columns must map onto StoredEvent");
		events[0].EventData.ShouldNotBeNull();

		var head = await Query(dataSource).GetHeadPositionAsync(CancellationToken.None).ConfigureAwait(false);
		head.ShouldBe(p3, "the head is the highest committed position");
	}

	[Fact]
	public async Task NotBurnAPositionWhenAnAppendLosesAConcurrencyRace()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"global stream ordering is a data-loss boundary -- this real-PostgreSQL lock must never be skipped");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		var store = Store();
		var contended = "agg-" + Guid.NewGuid().ToString("N");

		var seed = await AppendAsync(store, contended, -1).ConfigureAwait(false);

		// Two appends at the SAME expected version. Exactly one can win; the loser rolls back. Which one
		// wins is not deterministic, but the outcome asserted below is.
		var racers = await Task.WhenAll(
				Task.Run(() => store.AppendAsync(
					contended, AggregateType, new IDomainEvent[] { new OrderPlaced(contended) }, 0, CancellationToken.None).AsTask()),
				Task.Run(() => store.AppendAsync(
					contended, AggregateType, new IDomainEvent[] { new OrderPlaced(contended) }, 0, CancellationToken.None).AsTask()))
			.ConfigureAwait(false);

		// LIVENESS -- one of them committed. A store that failed both would satisfy the gapless assertion
		// below trivially.
		racers.Count(static r => r.Success).ShouldBe(1, "exactly one append may win a same-version race");

		var next = await AppendAsync(store, "agg-" + Guid.NewGuid().ToString("N"), -1).ConfigureAwait(false);

		// SAFETY -- the losing append consumed no position. Under BIGSERIAL the loser's nextval() would
		// be burned (sequence advancement is not transactional) and this would be seed + 3.
		next.ShouldBe(
			seed + 2,
			"an append that rolls back must not consume a global position -- the counter increment rolls " +
			"back with its transaction, unlike a sequence");
	}
}
