// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Sharding;
using Excalibur.EventSourcing.TieredStorage;
using Excalibur.MultiTenancy;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using IEventStore = Excalibur.EventSourcing.IEventStore;

namespace Excalibur.EventSourcing.Tests.TieredStorage;

/// <summary>
/// 7iu2xc (author≠impl regression lock) — tenant scoping must be the OUTER decorator of tiered storage. After
/// <c>UseTieredStorage(...)</c> then <c>AddMultiTenancy(RowDiscriminator)</c>, the keyed <c>"default"</c>
/// <see cref="IEventStore"/> — the load-bearing repository / time-travel / notification read path — must resolve
/// to <c>TenantScoped(Tiered(rawHot, rawCold))</c>, and the archive machinery's private hot key
/// (<c>tiered-hot</c>) must stay the RAW hot store (never tenant-scoped) so its intentionally cross-tenant trim
/// enumerates only the hot tier.
/// </summary>
/// <remarks>
/// <para>
/// <b>The fix.</b> <c>ApplyRowDiscriminator</c> passes the private hot key as a <c>reservedKeys</c> argument to
/// <c>DecorateKeyedStores&lt;IEventStore&gt;</c>. The reserved key is excluded from Rule 1 (decorate non-default
/// keyed terminals), so decoration falls through to Rule 2 and wraps the <c>"default"</c> Tiered store itself —
/// the OUTER position — leaving the inner hot leg raw for the archive service.
/// </para>
/// <para>
/// <b>What these arms prove — and what they do NOT.</b> They lock the DI <b>topology</b> (outer = TenantScoped,
/// inner hot key = raw) and that read-through and the archive path still work — for the <b>SUPPORTED</b> config
/// (a tenant-aware cold tier: the helper registers the cold store's <c>ITenantScopingCapability&lt;IColdEventStore&gt;</c>
/// marker through the dep-gated seam so it builds past the tiered/cold fail-fast gate). They do <b>NOT</b> prove
/// cross-tenant COLD-read isolation, and this fix does not provide it: <see cref="TenantScopedEventStore"/>
/// enforces tenant PRESENCE (<c>RequireTenant</c>, fail-closed) then delegates WITHOUT a predicate — isolation
/// lives in the inner store's query, and the cold leg (<see cref="IColdEventStore"/>, keyed by aggregate id, no
/// tenant awareness) has none. A caller with a different ambient tenant can still read another tenant's archived
/// (cold) events. That is a KNOWN OPEN, proven cross-tenant isolation gap: the (row-discriminator MT + tiered +
/// non-tenant-aware cold) combination is NOT currently safe, and is gated UNSUPPORTED at startup — a cold store
/// with no tenant-scoping marker fails fast (that fail-fast is covered by a separate independent lock, not here).
/// The real-infra cold-isolation lock lands with the tenant-partitioned cold fix; it is deliberately <b>not
/// asserted here</b> rather than asserted vacuously.
/// </para>
/// <para>
/// <b>Non-vacuity (pre-fix RED).</b> Before the fix, <c>DecorateKeyedStores</c> had no <c>reservedKeys</c>
/// parameter, so Rule 1 wrapped the non-default <c>tiered-hot</c> key and left keyed <c>"default"</c> a BARE
/// <see cref="TieredEventStoreDecorator"/> (NOT tenant-scoped): the topology arm's
/// <c>ShouldBeAssignableTo&lt;TenantScopedEventStore&gt;</c> on <c>"default"</c> is RED, and the raw-hot arm's
/// <c>ShouldNotBeAssignableTo&lt;TenantScopedEventStore&gt;</c> / <c>ShouldBeSameAs(hotStore)</c> on
/// <c>tiered-hot</c> (which was tenant-scoped pre-fix) is RED. Both GREEN once the reserved-key re-bind lands.
/// A no-ambient-tenant read fails closed <em>both</em> before and after the fix (the inner hot decorator already
/// threw pre-fix), so it does not distinguish the wiring and is intentionally not asserted as a non-vacuity arm.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Feature", "TieredStorage")]
public sealed class TenantScopedTieredReadThroughShould
{
    // ---- Topology (structural safety + liveness pair) ----

    [Fact]
    public void ResolveKeyedDefaultToTenantScopedOuter_AndKeepTieredHotRaw()
    {
        var hotStore = A.Fake<IEventStore>(x => x.Implements<IEventStoreArchive>());
        using var provider = BuildTieredTenantProvider(hotStore, A.Fake<IColdEventStore>());

        // (topology) the OUTER type on the consumer read path is TenantScoped, wrapping the Tiered store (this
        // asserts the DECORATION ORDER only — not cold-read tenant isolation, see the class remarks). RED pre-fix:
        // keyed "default" was a bare TieredEventStoreDecorator.
        _ = provider.GetRequiredKeyedService<IEventStore>("default").ShouldBeAssignableTo<TenantScopedEventStore>(
            "7iu2xc: after UseTieredStorage + AddMultiTenancy(RowDiscriminator) the keyed \"default\" IEventStore "
            + "must resolve to TenantScoped(Tiered(...)) — tenant scoping OUTER of tiered storage.");

        // (safety) the private hot key the archive service trims stays RAW — never tenant-scoped. It is the exact
        // instance the hot descriptor was registered with. RED pre-fix: Rule 1 wrapped "tiered-hot" in TenantScoped.
        var rawHot = provider.GetRequiredKeyedService<IEventStore>(EventArchiveService.RawHotEventStoreKey);
        rawHot.ShouldNotBeAssignableTo<TenantScopedEventStore>(
            "7iu2xc: the archive machinery's raw hot store (\"tiered-hot\") must NOT be tenant-scoped — its trim is "
            + "intentionally cross-tenant and must enumerate only the hot tier.");
        rawHot.ShouldBeSameAs(
            hotStore,
            "7iu2xc: keyed \"tiered-hot\" must resolve the RAW registered hot store itself, undecorated.");
    }

    // NOTE: there is deliberately NO "fail-closed / never-query-cold" arm here. A no-ambient-tenant LoadAsync
    // throws before cold BOTH pre- and post-fix (pre-fix the inner hot TenantScoped decorator already threw), so
    // such an arm passes vacuously and would manufacture false cross-tenant-isolation confidence. The real
    // cross-tenant COLD-read isolation is a known, separately-tracked gap (see the class remarks); its real-infra
    // isolation lock lands with the tenant-partitioned cold fix, not here.

    // ---- Behavioral liveness (read-through still works with a valid tenant) ----

    [Fact]
    public async Task ReadThroughToCold_OnHotMiss_WhenAmbientTenantPresent()
    {
        var hotStore = A.Fake<IEventStore>(x => x.Implements<IEventStoreArchive>());
        var coldStore = A.Fake<IColdEventStore>();
        _ = A.CallTo(() => hotStore.LoadAsync("agg-1", "Order", A<CancellationToken>._)).Returns(new List<StoredEvent>());
        _ = A.CallTo(() => coldStore.HasArchivedEventsAsync(A<KeyedTenantPartition>._, "agg-1", A<CancellationToken>._)).Returns(true);
        _ = A.CallTo(() => coldStore.ReadAsync(A<KeyedTenantPartition>._, "agg-1", A<CancellationToken>._)).Returns(CreateEvents("agg-1", 1, 2, 3));

        using var provider = BuildTieredTenantProvider(hotStore, coldStore);
        var wired = provider.GetRequiredKeyedService<IEventStore>("default");

        // A valid ambient tenant lets RequireTenant() pass, and the OUTER TenantScoped delegates to the Tiered
        // store, which reads through to cold on the hot miss. Liveness: scoping does not block the legitimate read.
        using (TenantContextHolder.BeginScope("tenant-A"))
        {
            var result = await wired.LoadAsync("agg-1", "Order", CancellationToken.None);

            result.Count.ShouldBe(
                3,
                "7iu2xc: with a valid ambient tenant the OUTER TenantScoped(Tiered) read path must still surface the "
                + "archived cold events on a hot miss — tenant scoping tightens, it does not disable read-through.");
        }
    }

    // ---- Archive path unchanged (mirrors TieredStorageWiringShould) ----

    [Fact]
    public void ResolveDefaultArchiveToTheRawHotStore_WhenTenantScopingIsOuter()
    {
        var hotStore = A.Fake<IEventStore>(x => x.Implements<IEventStoreArchive>());
        using var provider = BuildTieredTenantProvider(hotStore, A.Fake<IColdEventStore>());

        // The default IEventStoreArchive remains the RAW hot store — adding the outer tenant decorator on "default"
        // must not divert the archive path onto the decorator (which would read through cold during trim).
        provider.GetRequiredService<IEventStoreArchive>().ShouldBeSameAs(
            hotStore,
            "7iu2xc: the default IEventStoreArchive must stay the RAW hot store; the tenant decorator wraps only the "
            + "consumer-facing \"default\" read path, never the archive machinery.");
    }

    // Builds the exact production wiring order: hot keyed "default" + cold + logger + non-keyed forwarder, then
    // UseTieredStorage (moves raw hot → "tiered-hot", re-binds "default" → Tiered decorator), then the dep-gated
    // capability marker (AddMultiTenancy's RowDiscriminator gate requires it), then AddMultiTenancy(RowDiscriminator)
    // — which reserves the private hot key so tenant scoping wraps the OUTER "default" Tiered store.
    private static ServiceProvider BuildTieredTenantProvider(IEventStore hotStore, IColdEventStore coldStore)
    {
        var services = new ServiceCollection();
        _ = services.AddKeyedSingleton("default", hotStore);
        _ = services.AddSingleton(coldStore);
        _ = services.AddSingleton<ILogger<TieredEventStoreDecorator>>(NullLogger<TieredEventStoreDecorator>.Instance);
        // Core AddEventSourcing's non-keyed forwarder onto the keyed "default" store (verbatim shape).
        services.TryAddSingleton<IEventStore>(sp => sp.GetRequiredKeyedService<IEventStore>("default"));

        _ = new ExcaliburEventSourcingBuilder(services).UseTieredStorage(_ => { });

        // Prove tenant-scoping capability through the sole sanctioned dep-gated seam (a bare marker is
        // structurally unimplementable). AddMultiTenancy itself registers the ambient ITenantContext.
        _ = services.AddTenantAwareStore<IEventStore, TenantAwareNoopEventStore>(
            sp => new TenantAwareNoopEventStore(sp.GetRequiredService<ITenantContext>()));

        // The tiered/cold gate in ApplyRowDiscriminator fails fast when a cold tier (IColdEventStore) is present
        // without a tenant-scoping capability marker. Emit the marker for the cold store through the same
        // dep-gated seam so this provider exercises the SUPPORTED (tenant-aware cold) config.
        // ColdStoreScopingMarkerCarrier's constructor declares ITenantContext, so the seam derives the scoped
        // marker from it; TryAddSingleton<ColdStoreScopingMarkerCarrier> registers under a key nothing else
        // resolves, so the TieredEventStoreDecorator still resolves the real, already-registered coldStore fake
        // — only the marker is added.
        _ = services.AddTenantAwareStore<IColdEventStore, ColdStoreScopingMarkerCarrier>(
            sp => new ColdStoreScopingMarkerCarrier(sp.GetRequiredService<ITenantContext>()));

        _ = services.AddMultiTenancy(static o => o.Strategy = TenantIsolationStrategy.RowDiscriminator);

        return services.BuildServiceProvider();
    }

    private static List<StoredEvent> CreateEvents(string aggregateId, params long[] versions) =>
        versions.Select(v => new StoredEvent(
            EventId: Guid.NewGuid().ToString(),
            AggregateId: aggregateId,
            AggregateType: "Order",
            EventType: "TestEvent",
            EventData: Array.Empty<byte>(),
            Metadata: null,
            Version: v,
            Timestamp: DateTimeOffset.UtcNow)).ToList();

    /// <summary>
    /// A minimal event store registered only to emit the <c>ITenantScopingCapability&lt;IEventStore&gt;</c> marker
    /// via the dep-gated <c>AddTenantAwareStore</c> seam. Its factory is never resolved by these tests.
    /// </summary>
    private sealed class TenantAwareNoopEventStore(ITenantContext tenantContext) : IEventStore
    {
        public ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
            string aggregateId, string aggregateType, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<StoredEvent>>([]);

        public ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
            string aggregateId, string aggregateType, long fromVersion, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<StoredEvent>>([]);

        public ValueTask<AppendResult> AppendAsync(
            string aggregateId, string aggregateType, IEnumerable<IDomainEvent> events,
            long expectedVersion, CancellationToken cancellationToken) =>
            ValueTask.FromResult(AppendResult.CreateSuccess(0, null));
    }

    /// <summary>
    /// A throwaway <see cref="IColdEventStore"/> implementer registered ONLY to derive the scoped marker
    /// through <c>AddTenantAwareStore</c>'s constructor-shape probe — never resolved. The real cold store
    /// under test is the FakeItEasy fake supplied by the caller and registered separately via
    /// <see cref="ServiceCollectionServiceExtensions.AddSingleton{TService}(IServiceCollection, TService)"/>;
    /// this type exists only because the probe needs a real constructor to reflect on, which an arbitrary
    /// caller-supplied fake instance does not offer.
    /// </summary>
    private sealed class ColdStoreScopingMarkerCarrier(ITenantContext tenantContext) : IColdEventStore
    {
        public Task<long> WriteAsync(
            KeyedTenantPartition tenant, string aggregateId, IReadOnlyList<StoredEvent> events,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("Unreachable — this type is never resolved.");

        public Task<IReadOnlyList<StoredEvent>> ReadAsync(
            KeyedTenantPartition tenant, string aggregateId, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Unreachable — this type is never resolved.");

        public Task<IReadOnlyList<StoredEvent>> ReadAsync(
            KeyedTenantPartition tenant, string aggregateId, long fromVersion, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Unreachable — this type is never resolved.");

        public Task<bool> HasArchivedEventsAsync(
            KeyedTenantPartition tenant, string aggregateId, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Unreachable — this type is never resolved.");
    }
}
