// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing.InMemory;
using Excalibur.EventSourcing.Queries;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

namespace Excalibur.EventSourcing.Tests.InMemory;

/// <summary>
/// The in-memory event store exposes a readable global stream: every appended event carries a global
/// position, the stream reads back in position order across aggregates, and the query resolves from DI
/// against the SAME store instance the event store contract resolves to.
/// </summary>
/// <remarks>
/// <para>
/// Until this landed, <see cref="InMemoryEventStore"/> built every <see cref="StoredEvent"/> through the
/// positional constructor, and <c>GlobalPosition</c> is an <c>init</c> property declared OUTSIDE that
/// constructor. Every stored event therefore carried position 0 in silence, and the in-memory provider
/// could not host a global-stream projection, a materialized view, a projection rebuild or the lag
/// read-model at all -- while appearing to work, because nothing read the value back.
/// </para>
/// <para>
/// <b>Both arms (testing-patterns section 3).</b> SAFETY -- positions are distinct, gapless and ordered,
/// so a subscriber advancing a high-water mark can never skip an event. LIVENESS -- the stream actually
/// returns the appended events and the paging cursor makes progress, so a query that returned nothing
/// (trivially gapless and trivially ordered) fails these arms rather than passing them.
/// </para>
/// <para>
/// <b>RED-on-mutant.</b> Remove <c>GlobalPosition = nextPosition++</c> from the store's append and every
/// event reads back as 0: <see cref="AssignEveryAppendedEventADistinctAscendingPosition"/> goes RED on
/// distinctness, and <see cref="PageThroughTheStreamWithoutSkippingOrRepeating"/> goes RED because the
/// cursor cannot advance.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class InMemoryGlobalStreamQueryShould
{
	private const string AggregateType = "Order";

	[MessageName("Test.InMemoryGlobalStream.OrderPlaced")]
	private sealed record OrderPlaced(string AggregateId) : IDomainEvent
	{
		public string EventId { get; init; } = Guid.NewGuid().ToString();

		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

		public IDictionary<string, object>? Metadata { get; init; }
	}

	/// <summary>Builds a provider through the real registration path, not by hand-constructing.</summary>
	/// <remarks>
	/// The store is resolved through the KEYED "default" registration because that is how the provider
	/// publishes it: AddTenantAwareStore registers the concrete type plus a capability marker, and the
	/// keyed entries are what a consumer binds to. Resolving the bare contract would test a registration
	/// that does not exist.
	/// </remarks>
	private static ServiceProvider BuildProvider()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddInMemoryEventStore();

		return services.BuildServiceProvider();
	}

	/// <summary>Appends <paramref name="count"/> events, continuing from the stream's current version.</summary>
	/// <returns>The stream's version after the appends, so a caller can append to it again.</returns>
	private static async Task<long> AppendAsync(
		IEventStore store,
		string aggregateId,
		int count,
		long fromVersion = -1)
	{
		var version = fromVersion;
		for (var i = 0; i < count; i++)
		{
			var result = await store.AppendAsync(
					aggregateId,
					AggregateType,
					new IDomainEvent[] { new OrderPlaced(aggregateId) },
					version,
					CancellationToken.None)
				.ConfigureAwait(false);

			result.Success.ShouldBeTrue("the append must succeed for the stream to contain anything");
			version++;
		}

		return version;
	}

	[Fact]
	public async Task ResolveFromDiAgainstTheSameStoreTheEventStoreContractUses()
	{
		await using var provider = BuildProvider();

		// LIVENESS — the registration exists at all. A provider that cannot resolve this hosts no
		// projections, which is the state every non-SQL-Server provider was in.
		var query = provider.GetService<IGlobalStreamQuery>();
		query.ShouldNotBeNull("UseInMemory must register a global stream query");

		var store = provider.GetRequiredKeyedService<IEventStore>("default");
		_ = await AppendAsync(store, "agg-" + Guid.NewGuid().ToString("N"), 2).ConfigureAwait(false);

		// SAFETY — the query reads the store the event store contract writes to. A second store instance
		// would carry its own events and its own position counter, and this would read empty.
		var read = await query!.ReadAllAsync(GlobalStreamPosition.Start, 100, CancellationToken.None)
			.ConfigureAwait(false);

		read.Count.ShouldBe(2, "the query must read the same store instance the event store wrote to");
	}

	[Fact]
	public async Task AssignEveryAppendedEventADistinctAscendingPosition()
	{
		await using var provider = BuildProvider();
		var store = provider.GetRequiredKeyedService<IEventStore>("default");
		var query = provider.GetRequiredService<IGlobalStreamQuery>();

		// Interleaved across two aggregates: the global stream is cross-aggregate, so a per-stream
		// counter would pass a single-aggregate test and fail here.
		var first = "agg-" + Guid.NewGuid().ToString("N");
		var second = "agg-" + Guid.NewGuid().ToString("N");
		var firstVersion = await AppendAsync(store, first, 2).ConfigureAwait(false);
		_ = await AppendAsync(store, second, 2).ConfigureAwait(false);

		// Back to the FIRST aggregate, after the second one has written. Its events must land at the
		// HIGHEST positions: the counter is global, not per stream.
		_ = await AppendAsync(store, first, 1, firstVersion).ConfigureAwait(false);

		var events = await query.ReadAllAsync(GlobalStreamPosition.Start, 100, CancellationToken.None)
			.ConfigureAwait(false);

		// LIVENESS — every appended event is present.
		events.Count.ShouldBe(5, "the global stream must carry every appended event, across aggregates");

		var positions = events.Select(static e => e.GlobalPosition).ToList();

		// SAFETY — distinct. Before the fix every event carried 0, so this is the arm that catches it.
		positions.Distinct().Count().ShouldBe(5, "every event must carry its own global position");

		// SAFETY — ascending, and contiguous. A subscriber advances its high-water mark past the highest
		// position it has seen, so a gap would be an event it can never come back for.
		positions.ShouldBe(positions.OrderBy(static p => p).ToList(), "the stream must read in position order");
		(positions[^1] - positions[0] + 1).ShouldBe(
			positions.Count,
			"committed positions must be contiguous — a hole is an event a subscriber would stall on or skip");
	}

	[Fact]
	public async Task PageThroughTheStreamWithoutSkippingOrRepeating()
	{
		await using var provider = BuildProvider();
		var store = provider.GetRequiredKeyedService<IEventStore>("default");
		var query = provider.GetRequiredService<IGlobalStreamQuery>();

		_ = await AppendAsync(store, "agg-" + Guid.NewGuid().ToString("N"), 5).ConfigureAwait(false);

		// Page two at a time, exactly as the projection hosts do: read, then resume FROM the last
		// position seen -- the read is exclusive, so nothing is added.
		var seen = new List<long>();
		var cursor = GlobalStreamPosition.Start;
		for (var page = 0; page < 10 && seen.Count < 5; page++)
		{
			var batch = await query.ReadAllAsync(cursor, 2, CancellationToken.None).ConfigureAwait(false);
			if (batch.Count == 0)
			{
				break;
			}

			seen.AddRange(batch.Select(static e => e.GlobalPosition));
			// The cursor IS the last delivered position and the read is exclusive of it -- no arithmetic.
			cursor = new GlobalStreamPosition(batch[^1].GlobalPosition, batch[^1].Timestamp);
		}

		// LIVENESS — paging terminates having seen everything. If positions were all 0 the cursor would
		// never advance and this loop would spin on the same page until its bound.
		seen.Count.ShouldBe(5, "paging must reach every event");

		// SAFETY — nothing delivered twice.
		seen.Distinct().Count().ShouldBe(5, "paging must not repeat an event across batches");
	}

	[Fact]
	public async Task ReadByEventTypeWithoutLosingGlobalOrder()
	{
		await using var provider = BuildProvider();
		var store = provider.GetRequiredKeyedService<IEventStore>("default");
		var query = provider.GetRequiredService<IGlobalStreamQuery>();

		_ = await AppendAsync(store, "agg-" + Guid.NewGuid().ToString("N"), 3).ConfigureAwait(false);

		var typed = await query.ReadByEventTypeAsync(
				"Test.InMemoryGlobalStream.OrderPlaced", GlobalStreamPosition.Start, 100, CancellationToken.None)
			.ConfigureAwait(false);

		// LIVENESS — the filter matches the type that was actually written. A filter keyed on the CLR
		// name rather than the message name would return nothing here and still look like a clean pass.
		typed.Count.ShouldBe(3, "the type filter must match the stored event type name");

		// SAFETY — a filtered read is still globally ordered.
		var positions = typed.Select(static e => e.GlobalPosition).ToList();
		positions.ShouldBe(positions.OrderBy(static p => p).ToList());

		var head = await query.GetHeadPositionAsync(CancellationToken.None).ConfigureAwait(false);
		head.ShouldBe(positions[^1], "the head position must be the highest committed position");
	}
}
