// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Oracle;
using Excalibur.EventSourcing.Queries;
using Excalibur.Integration.Tests.Data.EventStore;

using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Xunit;

namespace Excalibur.Integration.Tests.EventSourcing.Oracle;

/// <summary>
/// The Oracle event store allocates gapless global positions and exposes them through a readable global
/// stream.
/// </summary>
/// <remarks>
/// <para>
/// <b>What is being protected.</b> <c>POSITION</c> was previously
/// <c>GENERATED ALWAYS AS IDENTITY</c>, which is a sequence. A sequence hands its number out at INSERT
/// and lets it escape the transaction, so an aborted append burns a value. Oracle makes this worse than
/// elsewhere in two ways: a sequence defaults to <c>CACHE 20</c>, so each SESSION draws a block and
/// issues from it -- a pooled session can commit a LOW position long after another session committed a
/// HIGHER one -- and cached values are discarded on instance restart. Either way a subscriber that has
/// passed the higher position never sees the lower one.
/// </para>
/// <para>
/// The store now allocates the position by UPDATEing a counter row inside the appending transaction.
/// <c>GENERATED ALWAYS</c> would have REJECTED that explicit insert outright, which is why the column
/// had to change rather than merely being bypassed.
/// </para>
/// <para>
/// <b>Why this arm matters more than its siblings.</b> Two Oracle-specific hazards are invisible to a
/// compiler and to any mock. ODP.NET binds parameters by POSITION rather than by name, so a query whose
/// parameter members are declared out of textual order binds the right values to the wrong placeholders.
/// And the allocation runs through a PL/SQL block with an OUT bind, which is the one allocation path in
/// this change that no other provider exercises.
/// </para>
/// <para>
/// <b>Both arms (testing-patterns section 3).</b> SAFETY -- a losing append consumes no position and the
/// committed stream stays contiguous. LIVENESS -- appends succeed, the stream reads them back with
/// correctly-mapped columns, and the head advances, so a store that refused every append (and is
/// therefore trivially gapless) fails rather than passes.
/// </para>
/// </remarks>
[Collection(OracleEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Oracle")]
public sealed class OracleGlobalStreamPositionShould
{
	private const string AggregateType = "Order";

	private readonly OracleEventStoreContainerFixture _fixture;

	public OracleGlobalStreamPositionShould(OracleEventStoreContainerFixture fixture) => _fixture = fixture;

	[MessageName("Test.OracleGlobalStreamPosition.OrderPlaced")]
	private sealed record OrderPlaced(string AggregateId) : IDomainEvent
	{
		public string EventId { get; init; } = Guid.NewGuid().ToString();

		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

		public IDictionary<string, object>? Metadata { get; init; }
	}

	private OracleEventStore Store() =>
		new(
			_fixture.CreateConnection,
			NullLogger<OracleEventStore>.Instance,
			SingleTenantTestContext.Instance,
			schema: _fixture.Schema,
			table: _fixture.TableName);

	private OracleGlobalStreamQuery Query() =>
		new(_fixture.CreateConnection, _fixture.Schema, _fixture.TableName);

	private static async Task<long> AppendAsync(OracleEventStore store, string aggregateId, long expectedVersion)
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
			"global stream ordering is a data-loss boundary -- this real-Oracle lock must never be skipped");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		var store = Store();
		var first = "agg-" + Guid.NewGuid().ToString("N");
		var second = "agg-" + Guid.NewGuid().ToString("N");

		var p1 = await AppendAsync(store, first, -1).ConfigureAwait(false);
		var p2 = await AppendAsync(store, second, -1).ConfigureAwait(false);
		var p3 = await AppendAsync(store, first, 0).ConfigureAwait(false);

		// SAFETY -- the counter is global, not per stream, and it advances by exactly one per event. A
		// CACHE-20 sequence would not produce this on a pooled connection.
		p2.ShouldBe(p1 + 1);
		p3.ShouldBe(p2 + 1);

		// Reads are EXCLUSIVE of the cursor, so starting at p1 - 1 is what includes p1. The cursor is
		// the last DELIVERED position, never the next one to fetch.
		var events = await Query()
			.ReadAllAsync(new GlobalStreamPosition(p1 - 1, DateTimeOffset.MinValue), 100, CancellationToken.None)
			.ConfigureAwait(false);

		// LIVENESS -- the query returns them, in order. This is also the first exercise of FETCH FIRST
		// with a positionally-bound :MaxCount following :Position.
		events.Count.ShouldBe(3, "the global stream must return every appended event");
		events.Select(static e => e.GlobalPosition).ShouldBe(new[] { p1, p2, p3 }, "in position order");

		// The columns are aliased and NUMBER(19) is narrowed from decimal by hand, so assert non-position
		// columns actually arrived: a mis-aliased SELECT would still produce the right count and order.
		events[0].AggregateType.ShouldBe(AggregateType, "the aliased columns must map onto StoredEvent");
		events[0].Version.ShouldBe(0, "VERSION must survive the decimal narrowing");
		events[0].EventData.ShouldNotBeNull();

		var head = await Query().GetHeadPositionAsync(CancellationToken.None).ConfigureAwait(false);
		head.ShouldBe(p3, "the head is the highest committed position");
	}

	[Fact]
	public async Task BindTheEventTypeFilterToTheRightPlaceholder()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"global stream ordering is a data-loss boundary -- this real-Oracle lock must never be skipped");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		var store = Store();
		var aggregateId = "agg-" + Guid.NewGuid().ToString("N");
		var p1 = await AppendAsync(store, aggregateId, -1).ConfigureAwait(false);
		_ = await AppendAsync(store, aggregateId, 0).ConfigureAwait(false);

		var typed = await Query()
			.ReadByEventTypeAsync(
				"Test.OracleGlobalStreamPosition.OrderPlaced",
				new GlobalStreamPosition(p1 - 1, DateTimeOffset.MinValue),
				100,
				CancellationToken.None)
			.ConfigureAwait(false);

		// LIVENESS, and the arm that catches positional-binding drift. This statement binds three
		// parameters -- :Position, :EventType, :MaxCount -- and ODP.NET binds them BY POSITION. If the
		// member order and the placeholder order disagree, the event-type string lands in the position
		// comparison and this returns nothing (or throws), rather than quietly returning the wrong rows.
		typed.Count.ShouldBe(2, "the type filter must bind to the event-type placeholder, not another one");

		// SAFETY -- a filtered read is still globally ordered.
		var positions = typed.Select(static e => e.GlobalPosition).ToList();
		positions.ShouldBe(positions.OrderBy(static p => p).ToList());
	}

	[Fact]
	public async Task NotBurnAPositionWhenAnAppendLosesAConcurrencyRace()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"global stream ordering is a data-loss boundary -- this real-Oracle lock must never be skipped");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		var store = Store();
		var contended = "agg-" + Guid.NewGuid().ToString("N");

		var seed = await AppendAsync(store, contended, -1).ConfigureAwait(false);

		var racers = await Task.WhenAll(
				Task.Run(() => store.AppendAsync(
					contended, AggregateType, new IDomainEvent[] { new OrderPlaced(contended) }, 0, CancellationToken.None).AsTask()),
				Task.Run(() => store.AppendAsync(
					contended, AggregateType, new IDomainEvent[] { new OrderPlaced(contended) }, 0, CancellationToken.None).AsTask()))
			.ConfigureAwait(false);

		// LIVENESS -- one of them committed.
		racers.Count(static r => r.Success).ShouldBe(1, "exactly one append may win a same-version race");

		var next = await AppendAsync(store, "agg-" + Guid.NewGuid().ToString("N"), -1).ConfigureAwait(false);

		// SAFETY -- the losing append consumed no position. Under an identity column the loser's value
		// would be burned and this would be seed + 3.
		next.ShouldBe(
			seed + 2,
			"an append that rolls back must not consume a global position -- the counter increment rolls " +
			"back with its transaction, unlike a sequence");
	}
}
