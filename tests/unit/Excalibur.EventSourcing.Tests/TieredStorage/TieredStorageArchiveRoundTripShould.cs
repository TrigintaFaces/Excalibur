// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.TieredStorage;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using IEventStore = Excalibur.EventSourcing.IEventStore;

namespace Excalibur.EventSourcing.Tests.TieredStorage;

/// <summary>
/// The LIVENESS twin the cold-store durability criterion lacks. That criterion states only the safety
/// direction — the hot tier is not trimmed unless the cold write is confirmed — which is fully satisfied by
/// an archive path that durably writes cold, deletes hot, and is then unable to serve those events back.
/// Silent history loss passes a safety-only criterion perfectly.
/// </summary>
/// <remarks>
/// <para>
/// <b>What these arms prove.</b> After a real archive (cold write) and a real hot-tier trim, the event store
/// resolved the way a consumer resolves it returns the aggregate's history <b>complete and in version
/// order</b> — across a full trim, a <i>partial</i> trim (the merge path), and the <c>fromVersion</c>
/// overload. The partial-trim merge is the untested shape: prior coverage exercised only the hot-EMPTY case,
/// where a merge-order or off-by-one defect cannot surface because there is nothing to merge.
/// </para>
/// <para>
/// <b>Real stores, not mocks.</b> The hot and cold tiers here are hand-written in-memory implementations of
/// <see cref="IEventStore"/>/<see cref="IEventStoreArchive"/> and <see cref="IColdEventStore"/> with real
/// append/trim/read semantics. A configured mock would return whatever it was told and could not expose an
/// ordering or boundary defect in the decorator's merge. Neither fixture inherits a first-party base that
/// supplies the members under test.
/// </para>
/// <para>
/// <b>Non-vacuity (RED condition).</b> <see cref="ReturnIncompleteHistory_WhenTheReadPathIsNotBound"/> runs
/// the identical archive + trim against wiring that never re-binds the read path, and pins that it returns
/// the trimmed remainder — so the liveness arms above are RED exactly when the read path is unbound, which
/// is the failure this lock exists to catch.
/// </para>
/// <para>
/// <b>Read directions.</b> <see cref="IEventStore"/> is three members; both read overloads are exercised
/// here. The capability interfaces an event store may additionally implement are not DI-registered and are
/// reached by casting the resolved store — that direction is pinned by
/// <see cref="NotSilentlyClaimErasure_WhileTheColdTierIsOutsideItsReach"/>.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Feature", "TieredStorage")]
public sealed class TieredStorageArchiveRoundTripShould
{
    private const string AggregateId = "agg-round-trip";
    private const string AggregateType = "Order";

    [Fact]
    public async Task ReturnCompleteHistory_AfterPartialArchiveAndHotTrim_ThroughTheResolvedEventStore()
    {
        // Versions 1-3 archived to cold and trimmed from hot; 4-6 remain hot. The read must MERGE the tiers.
        var (provider, hot, cold) = BuildTieredWorld();
        using (provider)
        {
            SeedHot(hot, 1, 2, 3, 4, 5, 6);
            await ArchiveAndTrimAsync(hot, cold, throughVersion: 3);

            hot.Versions.ShouldBe([4, 5, 6], "the trim must have actually removed the archived versions from the hot tier — otherwise this arm proves nothing about read-through.");

            var history = await provider.GetRequiredKeyedService<IEventStore>("default")
                .LoadAsync(AggregateId, AggregateType, CancellationToken.None);

            Versions(history).ShouldBe(
                [1, 2, 3, 4, 5, 6],
                "after archival trims the hot tier, the resolved consumer read path must return the COMPLETE history in version order — the archived range from cold merged ahead of the hot remainder.");
        }
    }

    [Fact]
    public async Task ReturnCompleteHistory_AfterFullArchiveAndHotTrim_ThroughTheResolvedEventStore()
    {
        // Every version archived and trimmed: the hot tier is empty and cold holds the entire history.
        var (provider, hot, cold) = BuildTieredWorld();
        using (provider)
        {
            SeedHot(hot, 1, 2, 3);
            await ArchiveAndTrimAsync(hot, cold, throughVersion: 3);

            hot.Versions.ShouldBeEmpty("the full trim must leave the hot tier empty.");

            var history = await provider.GetRequiredKeyedService<IEventStore>("default")
                .LoadAsync(AggregateId, AggregateType, CancellationToken.None);

            Versions(history).ShouldBe(
                [1, 2, 3],
                "with the whole history archived and the hot tier empty, the resolved read path must serve the complete history from cold — never an empty result, which is what a silently unbound read path returns.");
        }
    }

    [Fact]
    public async Task ReturnCompleteHistoryFromVersion_AfterPartialArchiveAndHotTrim()
    {
        // The fromVersion overload is a second reachable read direction and needs its own liveness arm:
        // a decorator that reads through on one overload and not the other loses history on that path only.
        var (provider, hot, cold) = BuildTieredWorld();
        using (provider)
        {
            SeedHot(hot, 1, 2, 3, 4, 5, 6);
            await ArchiveAndTrimAsync(hot, cold, throughVersion: 3);

            var history = await provider.GetRequiredKeyedService<IEventStore>("default")
                .LoadAsync(AggregateId, AggregateType, fromVersion: 1, CancellationToken.None);

            Versions(history).ShouldBe(
                [2, 3, 4, 5, 6],
                "the fromVersion read path must also merge the archived range from cold — every version after the requested one, complete and in order.");
        }
    }

    [Fact]
    public async Task ReturnIncompleteHistory_WhenTheReadPathIsNotBound()
    {
        // NON-VACUITY. Identical archive + trim, but the consumer resolves a store with no read-through
        // binding. It returns the trimmed remainder and reports no error — the exact silent history loss the
        // arms above forbid. If this arm ever returns the complete history, the arms above have stopped
        // discriminating and must be re-derived before being trusted.
        var hot = new InMemoryHotEventStore();
        var cold = new InMemoryColdEventStore();

        var services = new ServiceCollection();
        _ = services.AddKeyedSingleton<IEventStore>("default", hot);
        services.TryAddSingleton<IEventStore>(sp => sp.GetRequiredKeyedService<IEventStore>("default"));
        using var provider = services.BuildServiceProvider();

        SeedHot(hot, 1, 2, 3, 4, 5, 6);
        await ArchiveAndTrimAsync(hot, cold, throughVersion: 3);

        var history = await provider.GetRequiredService<IEventStore>()
            .LoadAsync(AggregateId, AggregateType, CancellationToken.None);

        Versions(history).ShouldBe(
            [4, 5, 6],
            "an unbound read path returns the post-trim remainder with no error — this arm pins the RED condition the liveness arms detect.");
    }

    [Fact]
    public void NotSilentlyClaimErasure_WhileTheColdTierIsOutsideItsReach()
    {
        // The third read direction is a CAST over the resolved store, not a DI resolve: consumers probe
        // `resolved is IEventStoreErasure`. The hot store here IS erasure-capable, so before tiering the probe
        // succeeded. Once tiering owns the read path, the consumer-facing store must NOT answer that probe
        // affirmatively while it can only erase the hot tier — a truthful strip fails loudly at the consumer's
        // `?? throw`, whereas a forwarded capability would tombstone hot and leave the archived copies intact.
        var (provider, _, _) = BuildTieredWorld();
        using (provider)
        {
            var consumerFacing = provider.GetRequiredKeyedService<IEventStore>("default");

            _ = consumerFacing.ShouldBeAssignableTo<TieredEventStoreDecorator>(
                "the consumer read path must be the read-through decorator.");

            (consumerFacing as IEventStoreErasure).ShouldBeNull(
                "the tiering wrapper must not answer the erasure capability probe while the cold tier is outside its erase reach — the strip must be observable at the probe, not silent.");
        }
    }

    // ---- the world under test: real wiring, real in-memory tiers ----

    private static (ServiceProvider Provider, InMemoryHotEventStore Hot, InMemoryColdEventStore Cold) BuildTieredWorld()
    {
        var hot = new InMemoryHotEventStore();
        var cold = new InMemoryColdEventStore();

        var services = new ServiceCollection();
        _ = services.AddKeyedSingleton<IEventStore>("default", hot);
        _ = services.AddSingleton<IColdEventStore>(cold);
        _ = services.AddSingleton<ILogger<TieredEventStoreDecorator>>(NullLogger<TieredEventStoreDecorator>.Instance);
        // Verbatim shape of the core registration: the non-keyed store forwards to the keyed "default".
        services.TryAddSingleton<IEventStore>(sp => sp.GetRequiredKeyedService<IEventStore>("default"));

        _ = new ExcaliburEventSourcingBuilder(services).UseTieredStorage(_ => { });

        return (services.BuildServiceProvider(), hot, cold);
    }

    /// <summary>
    /// Runs the archive contract the way the archive service does: confirm the cold write first, then trim the
    /// hot tier only up to the watermark cold actually confirmed.
    /// </summary>
    private static async Task ArchiveAndTrimAsync(
        InMemoryHotEventStore hot,
        InMemoryColdEventStore cold,
        long throughVersion)
    {
        var toArchive = hot.Snapshot().Where(e => e.Version <= throughVersion).ToList();

        var watermark = await cold.WriteAsync(
            KeyedTenantPartition.Untenanted, AggregateId, toArchive, CancellationToken.None);

        watermark.ShouldBe(
            throughVersion,
            "the cold tier must confirm the archived range before any hot event is deleted.");

        _ = await ((IEventStoreArchive)hot).DeleteEventsUpToVersionAsync(
            KeyedTenantPartition.Untenanted, AggregateId, AggregateType, watermark, CancellationToken.None);
    }

    private static void SeedHot(InMemoryHotEventStore hot, params long[] versions)
    {
        foreach (var version in versions)
        {
            hot.Add(NewEvent(version));
        }
    }

    private static long[] Versions(IReadOnlyList<StoredEvent> events) => events.Select(e => e.Version).ToArray();

    private static StoredEvent NewEvent(long version) => new(
        EventId: $"evt-{version}",
        AggregateId: AggregateId,
        AggregateType: AggregateType,
        EventType: "TestEvent",
        EventData: Array.Empty<byte>(),
        Metadata: null,
        Version: version,
        Timestamp: DateTimeOffset.UtcNow);

    /// <summary>
    /// A hot tier with real append/read/trim semantics, implementing both interfaces directly (no first-party
    /// base supplies any member under test). It is erasure-capable, which is what makes the capability arm
    /// discriminate: the strip must come from the tiering wrapper, not from an incapable inner store.
    /// </summary>
    private sealed class InMemoryHotEventStore : IEventStore, IEventStoreArchive, IEventStoreErasure
    {
        private readonly List<StoredEvent> _events = [];

        internal long[] Versions => _events.OrderBy(e => e.Version).Select(e => e.Version).ToArray();

        internal void Add(StoredEvent stored) => _events.Add(stored);

        internal IReadOnlyList<StoredEvent> Snapshot() => _events.OrderBy(e => e.Version).ToList();

        public ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
            string aggregateId, string aggregateType, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<StoredEvent>>(
                _events.Where(e => e.AggregateId == aggregateId).OrderBy(e => e.Version).ToList());

        public ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
            string aggregateId, string aggregateType, long fromVersion, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<StoredEvent>>(
                _events.Where(e => e.AggregateId == aggregateId && e.Version > fromVersion)
                    .OrderBy(e => e.Version).ToList());

        public ValueTask<AppendResult> AppendAsync(
            string aggregateId, string aggregateType, IEnumerable<IDomainEvent> events,
            long expectedVersion, CancellationToken cancellationToken) =>
            ValueTask.FromResult(AppendResult.CreateSuccess(_events.Count, null));

        public Task<IReadOnlyList<ArchiveCandidate>> GetArchiveCandidatesAsync(
            ArchivePolicy policy, int batchSize, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ArchiveCandidate>>([]);

        public Task<int> DeleteEventsUpToVersionAsync(
            KeyedTenantPartition tenant, string aggregateId, string aggregateType, long toVersion,
            CancellationToken cancellationToken)
        {
            var removed = _events.RemoveAll(e => e.AggregateId == aggregateId && e.Version <= toVersion);
            return Task.FromResult(removed);
        }

        // Erasure is implemented only so this store ANSWERS the capability probe: the capability arm needs an
        // erasure-capable inner store, otherwise a null probe result would prove nothing about the wrapper.
        public Task<int> EraseEventsAsync(
            string aggregateId, string aggregateType, Guid erasureRequestId, CancellationToken cancellationToken) =>
            Task.FromResult(_events.RemoveAll(e => e.AggregateId == aggregateId));

        public Task<bool> IsErasedAsync(
            string aggregateId, string aggregateType, CancellationToken cancellationToken) =>
            Task.FromResult(!_events.Exists(e => e.AggregateId == aggregateId));
    }

    /// <summary>
    /// A cold tier with real write/read semantics. <c>WriteAsync</c> returns the durable watermark defined by
    /// the contract (<c>-1</c> when nothing was added), so the archive helper trims only what cold confirmed.
    /// </summary>
    private sealed class InMemoryColdEventStore : IColdEventStore
    {
        private readonly Dictionary<string, List<StoredEvent>> _archived = [];

        public Task<long> WriteAsync(
            KeyedTenantPartition tenant, string aggregateId, IReadOnlyList<StoredEvent> events,
            CancellationToken cancellationToken)
        {
            if (events.Count == 0)
            {
                return Task.FromResult(-1L);
            }

            if (!_archived.TryGetValue(aggregateId, out var bucket))
            {
                bucket = [];
                _archived[aggregateId] = bucket;
            }

            foreach (var stored in events.Where(e => bucket.TrueForAll(existing => existing.Version != e.Version)))
            {
                bucket.Add(stored);
            }

            return Task.FromResult(events.Max(e => e.Version));
        }

        public Task<IReadOnlyList<StoredEvent>> ReadAsync(
            KeyedTenantPartition tenant, string aggregateId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<StoredEvent>>(Bucket(aggregateId).OrderBy(e => e.Version).ToList());

        public Task<IReadOnlyList<StoredEvent>> ReadAsync(
            KeyedTenantPartition tenant, string aggregateId, long fromVersion, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<StoredEvent>>(
                Bucket(aggregateId).Where(e => e.Version > fromVersion).OrderBy(e => e.Version).ToList());

        public Task<bool> HasArchivedEventsAsync(
            KeyedTenantPartition tenant, string aggregateId, CancellationToken cancellationToken) =>
            Task.FromResult(Bucket(aggregateId).Count > 0);

        private List<StoredEvent> Bucket(string aggregateId) =>
            _archived.TryGetValue(aggregateId, out var bucket) ? bucket : [];
    }
}
