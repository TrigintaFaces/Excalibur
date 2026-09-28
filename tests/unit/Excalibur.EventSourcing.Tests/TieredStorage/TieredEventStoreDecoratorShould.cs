// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing.TieredStorage;

using FakeItEasy;

using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Xunit;

namespace Excalibur.EventSourcing.Tests.TieredStorage;

/// <summary>
/// The tiered decorator restores archived payloads from cold storage and otherwise leaves the hot
/// stream exactly as it found it.
/// </summary>
/// <remarks>
/// <para>
/// <b>What changed, and why these arms look different from the ones they replace.</b> Archival used to
/// DELETE the archived rows from the hot store, so the decorator had to infer what had happened from
/// what was missing: if the hot stream did not start at version 1 it assumed a gap, consulted a
/// snapshot to decide whether the gap was benign, and concatenated cold events in front of hot ones.
/// Every one of those steps was an inference about absence.
/// </para>
/// <para>
/// Archival now TOMBSTONES instead: the payload moves to cold and the row stays, carrying its version,
/// its global position and an <see cref="StoredEvent.ArchivedAt"/> stamp. The hot read therefore already
/// returns the whole stream in order, and the decorator's only remaining job is to put the payloads
/// back. Gap detection, snapshot-gap coverage and the cold/hot merge are gone -- not relaxed, but
/// unnecessary, because the condition they detected can no longer arise.
/// </para>
/// <para>
/// <b>Both arms (testing-patterns section 3).</b> SAFETY -- an erased payload is never resurrected, and
/// cold storage is not consulted when nothing is archived. LIVENESS -- archived payloads really are
/// restored, so a decorator that returned the hot rows untouched (trivially safe) fails rather than
/// passes.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class TieredEventStoreDecoratorShould
{
	private const string AggregateType = "Order";

	private readonly IEventStore _hotStore = A.Fake<IEventStore>();
	private readonly IColdEventStore _coldStore = A.Fake<IColdEventStore>();
	private readonly TieredEventStoreDecorator _decorator;

	public TieredEventStoreDecoratorShould() =>
		_decorator = new TieredEventStoreDecorator(
			_hotStore,
			_coldStore,
			NullLogger<TieredEventStoreDecorator>.Instance,
			tenantContext: TestTenantContext.SingleTenantDefault);

	private static StoredEvent Event(
		string aggregateId,
		long version,
		byte[]? payload,
		DateTimeOffset? archivedAt = null) =>
		new(
			EventId: $"evt-{aggregateId}-{version}",
			AggregateId: aggregateId,
			AggregateType: AggregateType,
			EventType: "OrderPlaced",
			EventData: payload,
			Metadata: null,
			Version: version,
			Timestamp: DateTimeOffset.UnixEpoch.AddSeconds(version))
		{
			GlobalPosition = version,
			ArchivedAt = archivedAt,
		};

	private void HotReturns(string aggregateId, params StoredEvent[] events) =>
		A.CallTo(() => _hotStore.LoadAsync(aggregateId, AggregateType, A<CancellationToken>._))
			.Returns(events);

	private void ColdReturns(string aggregateId, params StoredEvent[] events) =>
		A.CallTo(() => _coldStore.ReadAsync(A<KeyedTenantPartition>._, aggregateId, A<CancellationToken>._))
			.Returns(events);

	[Fact]
	public async Task RouteAppendToHotStore()
	{
		var events = new List<IDomainEvent>();
		_ = await _decorator.AppendAsync("agg-1", AggregateType, events, -1, CancellationToken.None);

		A.CallTo(() => _hotStore.AppendAsync("agg-1", AggregateType, events, -1, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ReturnHotEventsUntouchedAndNotTouchColdWhenNothingIsArchived()
	{
		HotReturns("agg-1", Event("agg-1", 1, [1]), Event("agg-1", 2, [2]));

		var result = await _decorator.LoadAsync("agg-1", AggregateType, CancellationToken.None);

		// LIVENESS -- the stream comes back.
		result.Count.ShouldBe(2);
		result[0].EventData.ShouldBe(new byte[] { 1 });

		// SAFETY -- cold storage is a network call. Consulting it on every read of an unarchived stream
		// would be a per-read cost paid by every consumer who never enabled archival.
		A.CallTo(() => _coldStore.ReadAsync(A<KeyedTenantPartition>._, A<string>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task RestoreArchivedPayloadsFromColdStorage()
	{
		var archivedAt = DateTimeOffset.UnixEpoch.AddDays(1);
		HotReturns(
			"agg-1",
			Event("agg-1", 1, payload: null, archivedAt),
			Event("agg-1", 2, payload: null, archivedAt),
			Event("agg-1", 3, [33]));

		ColdReturns("agg-1", Event("agg-1", 1, [11]), Event("agg-1", 2, [22]));

		var result = await _decorator.LoadAsync("agg-1", AggregateType, CancellationToken.None);

		// LIVENESS -- the archived payloads are actually back. This is the arm a decorator that simply
		// returned the hot rows would fail.
		result.Count.ShouldBe(3);
		result[0].EventData.ShouldBe(new byte[] { 11 });
		result[1].EventData.ShouldBe(new byte[] { 22 });

		// SAFETY -- the un-archived event is untouched, and versions and positions survive hydration, so
		// the stream a caller sees is still ordered and contiguous.
		result[2].EventData.ShouldBe(new byte[] { 33 });
		result.Select(static e => e.Version).ShouldBe(new long[] { 1, 2, 3 });
		result.Select(static e => e.GlobalPosition).ShouldBe(new long[] { 1, 2, 3 });
	}

	[Fact]
	public async Task NotResurrectAnErasedPayload()
	{
		// An ERASED event also has a null payload -- and must stay that way. The discriminator is the
		// archive stamp, which erasure does not set. Inferring "archived" from "payload is missing" is
		// exactly the mistake this arm exists to prevent: it would hand back data a data-subject request
		// removed, from a cold copy that predates the erasure.
		HotReturns(
			"agg-1",
			Event("agg-1", 1, payload: null, archivedAt: null),
			Event("agg-1", 2, payload: null, archivedAt: DateTimeOffset.UnixEpoch.AddDays(1)));

		ColdReturns("agg-1", Event("agg-1", 1, [11]), Event("agg-1", 2, [22]));

		var result = await _decorator.LoadAsync("agg-1", AggregateType, CancellationToken.None);

		// SAFETY -- the erased entry keeps its absent payload even though cold storage holds a copy.
		result[0].EventData.ShouldBeNull(
			"an erased payload must never be restored from a cold copy taken before the erasure");

		// LIVENESS -- the genuinely archived entry beside it is still hydrated, so the arm above is not
		// passing merely because nothing was restored at all.
		result[1].EventData.ShouldBe(new byte[] { 22 });
	}

	[Fact]
	public async Task ReadColdStorageOnceForAStreamWithManyArchivedEvents()
	{
		var archivedAt = DateTimeOffset.UnixEpoch.AddDays(1);
		var hot = Enumerable.Range(1, 25)
			.Select(v => Event("agg-1", v, payload: null, archivedAt))
			.ToArray();
		var cold = Enumerable.Range(1, 25)
			.Select(v => Event("agg-1", v, [(byte)v]))
			.ToArray();

		HotReturns("agg-1", hot);
		ColdReturns("agg-1", cold);

		var result = await _decorator.LoadAsync("agg-1", AggregateType, CancellationToken.None);

		result.Count.ShouldBe(25);
		result[24].EventData.ShouldBe(new byte[] { 25 });

		// SAFETY -- one read for the stream, not one per archived entry. Cold storage is blob storage;
		// a per-entry read turns a 25-event load into 25 network round-trips.
		A.CallTo(() => _coldStore.ReadAsync(A<KeyedTenantPartition>._, "agg-1", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task LeaveAnArchivedEntryInPlaceWhenColdHasNoPayloadForIt()
	{
		var archivedAt = DateTimeOffset.UnixEpoch.AddDays(1);
		HotReturns(
			"agg-1",
			Event("agg-1", 1, payload: null, archivedAt),
			Event("agg-1", 2, [22]));

		ColdReturns("agg-1");

		var result = await _decorator.LoadAsync("agg-1", AggregateType, CancellationToken.None);

		// SAFETY -- a missing cold payload does not take down the read of the whole aggregate, and the
		// entry keeps its version and position so the stream stays contiguous. The caller sees an event
		// with no payload, which is a shape it must already tolerate for erased events.
		result.Count.ShouldBe(2);
		result[0].EventData.ShouldBeNull();
		result[0].Version.ShouldBe(1);

		// LIVENESS -- the rest of the stream is unaffected.
		result[1].EventData.ShouldBe(new byte[] { 22 });
	}

	[Fact]
	public async Task RestoreArchivedPayloadsOnTheFromVersionOverloadToo()
	{
		var archivedAt = DateTimeOffset.UnixEpoch.AddDays(1);
		A.CallTo(() => _hotStore.LoadAsync("agg-1", AggregateType, 1L, A<CancellationToken>._))
			.Returns(new[] { Event("agg-1", 2, payload: null, archivedAt) });
		ColdReturns("agg-1", Event("agg-1", 2, [22]));

		var result = await _decorator.LoadAsync("agg-1", AggregateType, 1, CancellationToken.None);

		// Both overloads must hydrate. A decorator that fixed only the parameterless one would leave every
		// snapshot-resumed load reading empty payloads -- the half-wired shape.
		result.Count.ShouldBe(1);
		result[0].EventData.ShouldBe(new byte[] { 22 });
	}

	[Fact]
	public void ThrowOnNullHotStore() =>
		Should.Throw<ArgumentNullException>(() => new TieredEventStoreDecorator(
			null!, _coldStore, NullLogger<TieredEventStoreDecorator>.Instance,
			TestTenantContext.SingleTenantDefault));

	[Fact]
	public void ThrowOnNullColdStore() =>
		Should.Throw<ArgumentNullException>(() => new TieredEventStoreDecorator(
			_hotStore, null!, NullLogger<TieredEventStoreDecorator>.Instance,
			TestTenantContext.SingleTenantDefault));

	[Fact]
	public void ThrowOnNullLogger() =>
		Should.Throw<ArgumentNullException>(() => new TieredEventStoreDecorator(
			_hotStore, _coldStore, null!, TestTenantContext.SingleTenantDefault));
}
