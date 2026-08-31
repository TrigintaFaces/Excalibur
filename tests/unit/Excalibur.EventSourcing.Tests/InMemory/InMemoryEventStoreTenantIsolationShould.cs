// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing;

namespace Excalibur.EventSourcing.Tests.InMemory;

/// <summary>
/// Binds the tenant term in <see cref="InMemoryEventStore"/>'s stream key.
/// </summary>
/// <remarks>
/// <para>
/// One store instance serves every caller and the tenant is resolved per call from the ambient context,
/// so these tests use a single store and switch the ambient tenant between operations — the shape the
/// default registration actually produces, rather than one store per tenant, which would pass even with
/// no tenant term in the key at all.
/// </para>
/// <para>
/// Each isolation assertion is paired with a liveness assertion. "Tenant B cannot see tenant A's events"
/// is satisfied by a store that returns nothing to anybody, so every arm below also asserts that the
/// tenant which DID write its events reads them back.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InMemoryEventStoreTenantIsolationShould
{
	private const string AggregateType = "Order";

	private readonly AmbientHolderTenantContext _tenantContext = new();
	private readonly InMemoryEventStore _store;

	public InMemoryEventStoreTenantIsolationShould() => _store = new InMemoryEventStore(_tenantContext);

	/// <summary>
	/// SAFETY + LIVENESS. Two tenants writing the same aggregate id get separate streams, and each reads
	/// back exactly its own events.
	/// </summary>
	/// <remarks>
	/// Without the tenant term both tenants address one stream: tenant B's append lands in tenant A's
	/// history and both loads return the union. The liveness half is what distinguishes the fix from a
	/// store that simply stopped returning anything.
	/// </remarks>
	[Fact]
	public async Task GiveTwoTenantsSeparateStreamsForOneAggregateId()
	{
		// Arrange — one aggregate id, deliberately shared by both tenants.
		const string SharedAggregateId = "order-1";

		var tenantAEvents = CreateEvents(SharedAggregateId, 2);
		var tenantBEvents = CreateEvents(SharedAggregateId, 3);

		// Act — tenant A writes first, then tenant B writes the SAME aggregate id.
		_tenantContext.TenantId = "tenant-a";
		var appendA = await _store.AppendAsync(
			SharedAggregateId, AggregateType, tenantAEvents, -1, CancellationToken.None);

		_tenantContext.TenantId = "tenant-b";
		var appendB = await _store.AppendAsync(
			SharedAggregateId, AggregateType, tenantBEvents, -1, CancellationToken.None);

		// Assert — B's append is against an EMPTY stream of its own, so expectedVersion -1 holds. Sharing a
		// stream with A would make this a concurrency conflict against A's version instead.
		appendA.Success.ShouldBeTrue();
		appendB.Success.ShouldBeTrue(
			"Tenant B appended at expectedVersion -1. If this is a concurrency conflict, B is being " +
			"versioned against tenant A's stream — the two tenants share one key.");

		// Assert — LIVENESS: each tenant reads back its own events...
		_tenantContext.TenantId = "tenant-a";
		var loadedByA = await _store.LoadAsync(SharedAggregateId, AggregateType, CancellationToken.None);

		_tenantContext.TenantId = "tenant-b";
		var loadedByB = await _store.LoadAsync(SharedAggregateId, AggregateType, CancellationToken.None);

		loadedByA.Count.ShouldBe(2, "Tenant A wrote 2 events and must read all 2 back.");
		loadedByB.Count.ShouldBe(3, "Tenant B wrote 3 events and must read all 3 back.");

		// ...and SAFETY: only its own.
		loadedByA.Select(static e => e.EventId)
			.ShouldBe(tenantAEvents.Select(static e => e.EventId), ignoreOrder: true);
		loadedByB.Select(static e => e.EventId)
			.ShouldBe(tenantBEvents.Select(static e => e.EventId), ignoreOrder: true);

		var tenantBEventIds = tenantBEvents.Select(static e => e.EventId).ToHashSet(StringComparer.Ordinal);
		loadedByA.ShouldAllBe(e => !tenantBEventIds.Contains(e.EventId));
	}

	/// <summary>
	/// LIVENESS. A single tenant writes and reads its own events, appends again at the version it was told,
	/// and sees the full stream.
	/// </summary>
	/// <remarks>
	/// This arm fails if the tenant term is applied inconsistently between the write and read paths — a
	/// store that keys writes by tenant but reads without the term (or the reverse) loses every event
	/// silently, and the isolation arm above would still pass.
	/// </remarks>
	[Fact]
	public async Task LetOneTenantWriteAndReadItsOwnStream()
	{
		// Arrange
		_tenantContext.TenantId = "tenant-a";
		var aggregateId = Guid.NewGuid().ToString();
		var first = CreateEvents(aggregateId, 2);
		var second = CreateEvents(aggregateId, 1);

		// Act
		var firstAppend = await _store.AppendAsync(
			aggregateId, AggregateType, first, -1, CancellationToken.None);
		var secondAppend = await _store.AppendAsync(
			aggregateId, AggregateType, second, firstAppend.NextExpectedVersion.ShouldNotBeNull(), CancellationToken.None);

		var loaded = await _store.LoadAsync(aggregateId, AggregateType, CancellationToken.None);

		// Assert
		firstAppend.Success.ShouldBeTrue();
		secondAppend.Success.ShouldBeTrue();
		loaded.Count.ShouldBe(3);
		loaded.Select(static e => e.EventId)
			.ShouldBe(first.Concat(second).Select(static e => e.EventId), ignoreOrder: true);
	}

	/// <summary>
	/// LIVENESS. A deliberately untenanted caller — the single-tenant deployment — still writes and reads
	/// normally, under the reserved untenanted partition rather than an absent term.
	/// </summary>
	[Fact]
	public async Task ServeADeliberatelyUntenantedCaller()
	{
		// Arrange
		var store = new InMemoryEventStore(UntenantedContext.Instance);
		var aggregateId = Guid.NewGuid().ToString();
		var events = CreateEvents(aggregateId, 2);

		// Act
		var append = await store.AppendAsync(aggregateId, AggregateType, events, -1, CancellationToken.None);
		var loaded = await store.LoadAsync(aggregateId, AggregateType, CancellationToken.None);

		// Assert
		append.Success.ShouldBeTrue();
		loaded.Count.ShouldBe(2);
		loaded.Select(static e => e.EventId).ShouldBe(events.Select(static e => e.EventId), ignoreOrder: true);
	}

	/// <summary>
	/// SAFETY + LIVENESS. Erasing one tenant's aggregate neither tombstones another tenant's same-id
	/// aggregate nor reports it as erased.
	/// </summary>
	/// <remarks>
	/// The erased-aggregate set is keyed the same way the stream is. Keyed on the aggregate pair alone,
	/// one tenant's erasure request would report a second tenant's live aggregate as erased — suppressing
	/// a replay of data that was never subject to the request.
	/// </remarks>
	[Fact]
	public async Task ConfineErasureToTheErasingTenant()
	{
		// Arrange — both tenants hold an aggregate under the same id.
		const string SharedAggregateId = "customer-7";

		_tenantContext.TenantId = "tenant-a";
		_ = await _store.AppendAsync(
			SharedAggregateId, AggregateType, CreateEvents(SharedAggregateId, 2), -1, CancellationToken.None);

		_tenantContext.TenantId = "tenant-b";
		var tenantBEvents = CreateEvents(SharedAggregateId, 2);
		_ = await _store.AppendAsync(
			SharedAggregateId, AggregateType, tenantBEvents, -1, CancellationToken.None);

		// Act — only tenant A erases.
		_tenantContext.TenantId = "tenant-a";
		var erasedCount = await ((IEventStoreErasure)_store).EraseEventsAsync(
			SharedAggregateId, AggregateType, Guid.NewGuid(), CancellationToken.None);

		// Assert — LIVENESS: A's erasure actually happened.
		erasedCount.ShouldBe(2);
		(await ((IEventStoreErasure)_store).IsErasedAsync(
			SharedAggregateId, AggregateType, CancellationToken.None)).ShouldBeTrue();
		(await _store.LoadAsync(SharedAggregateId, AggregateType, CancellationToken.None))
			.ShouldAllBe(e => e.EventType == ErasedEventMarker.EventType);

		// Assert — SAFETY: B's same-id aggregate is untouched and not reported erased.
		_tenantContext.TenantId = "tenant-b";
		(await ((IEventStoreErasure)_store).IsErasedAsync(
			SharedAggregateId, AggregateType, CancellationToken.None)).ShouldBeFalse(
			"Tenant A's erasure must not report tenant B's same-id aggregate as erased.");

		var loadedByB = await _store.LoadAsync(SharedAggregateId, AggregateType, CancellationToken.None);
		loadedByB.Count.ShouldBe(2);
		loadedByB.ShouldAllBe(e => e.EventType != ErasedEventMarker.EventType);
		loadedByB.Select(static e => e.EventId)
			.ShouldBe(tenantBEvents.Select(static e => e.EventId), ignoreOrder: true);
	}

	private static IReadOnlyList<IDomainEvent> CreateEvents(string aggregateId, int count) =>
		[.. Enumerable.Range(0, count).Select(i => (IDomainEvent)new TenantIsolationTestDomainEvent
		{
			EventId = Guid.NewGuid().ToString(),
			AggregateId = aggregateId,
			Version = i,
			EventType = "TestEvent",
		})];

	/// <summary>
	/// An ambient tenant context whose resolved tenant can be switched between calls, so one store
	/// instance serves both tenants exactly as the singleton registration does in a running host.
	/// </summary>
	private sealed class AmbientHolderTenantContext : ITenantContext
	{
		public string? TenantId { get; set; }

		public bool HasTenant => !string.IsNullOrWhiteSpace(TenantId);
	}

	private sealed class TenantIsolationTestDomainEvent : IDomainEvent
	{
		public required string EventId { get; init; }

		public required string AggregateId { get; init; }

		public required long Version { get; init; }

		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

		public required string EventType { get; init; }

		public IDictionary<string, object>? Metadata { get; init; }
	}
}
