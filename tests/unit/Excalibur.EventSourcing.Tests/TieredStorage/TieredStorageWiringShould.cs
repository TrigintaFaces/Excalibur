// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.TieredStorage;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using IEventStore = Excalibur.EventSourcing.IEventStore;

namespace Excalibur.EventSourcing.Tests.TieredStorage;

/// <summary>
/// l31hx7 (ADR-336 wired-AND-tested) — author≠impl lock (TestsDeveloper) for the tiered-storage DI wiring
/// (<c>UseTieredStorage</c>). The default <see cref="IEventStoreArchive"/> is the REGISTERED hot store itself
/// (never a fabricated default — SA seam): it resolves to the hot store when that store implements
/// <see cref="IEventStoreArchive"/>, <b>fails fast</b> when it does not, yields to a consumer-supplied
/// override (<c>TryAdd</c>), and the <c>EventArchiveService</c> background service is registered as an
/// <see cref="IHostedService"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Feature", "TieredStorage")]
public sealed class TieredStorageWiringShould
{
    [Fact]
    public void ResolveDefaultArchiveToTheHotStore_WhenHotStoreImplementsIEventStoreArchive()
    {
        // The hot store implements BOTH IEventStore and IEventStoreArchive → the default archive IS it.
        var archivableHotStore = A.Fake<IEventStore>(x => x.Implements<IEventStoreArchive>());
        using var provider = BuildTieredProvider(archivableHotStore);

        provider.GetRequiredService<IEventStoreArchive>()
            .ShouldBeSameAs(archivableHotStore, "l31hx7: the default IEventStoreArchive must be the registered hot store itself.");
    }

    [Fact]
    public void FailFast_WhenHotStoreDoesNotImplementIEventStoreArchive()
    {
        // A hot store that does NOT implement IEventStoreArchive must fail fast — never a silent no-op archive.
        var nonArchivableHotStore = A.Fake<IEventStore>();
        using var provider = BuildTieredProvider(nonArchivableHotStore);

        _ = Should.Throw<InvalidOperationException>(
            () => provider.GetRequiredService<IEventStoreArchive>(),
            "l31hx7: tiered storage never fabricates a default archive — a non-archivable hot store fails fast.");
    }

    [Fact]
    public void YieldToConsumerSuppliedArchiveOverride_WhenRegisteredBeforeUseTieredStorage()
    {
        // A consumer override registered BEFORE UseTieredStorage wins (TryAddSingleton yields to it),
        // even though the hot store itself could archive.
        var archivableHotStore = A.Fake<IEventStore>(x => x.Implements<IEventStoreArchive>());
        var consumerArchive = A.Fake<IEventStoreArchive>();
        using var provider = BuildTieredProvider(archivableHotStore, s => s.AddSingleton(consumerArchive));

        provider.GetRequiredService<IEventStoreArchive>()
            .ShouldBeSameAs(consumerArchive, "l31hx7: a consumer-supplied IEventStoreArchive override must win over the default hot-store archive.");
    }

    [Fact]
    public void RegisterEventArchiveService_AsHostedService()
    {
        var services = NewTieredServices(A.Fake<IEventStore>(x => x.Implements<IEventStoreArchive>()));
        _ = new ExcaliburEventSourcingBuilder(services).UseTieredStorage(_ => { });

        services.ShouldContain(
            d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(EventArchiveService),
            "l31hx7: EventArchiveService must be registered as an IHostedService so archiving actually runs.");
    }

    [Fact]
    public void ReadThroughDecorateBothKeyedDefaultAndNonKeyedIEventStore_WhileArchiveKeepsRawHot()
    {
        // jqk80w (REVIEW_CODE BLOCKING) — UseTieredStorage must RE-BIND IEventStore to the
        // TieredEventStoreDecorator so cold read-through runs. Registering the decorator as an orphaned
        // concrete type leaves IEventStore mapped to the BARE hot store; since EventArchiveService trims the
        // hot tier after copying to cold, early events live ONLY in cold and a hot-store LoadAsync rehydrates
        // INCOMPLETE history (data-loss shaped). SA seam (jqk80w): decorate the KEYED "default" (the
        // load-bearing repository/time-travel/notification path) — splitting the raw hot onto keyed
        // "tiered-hot" for the archive machinery. Both the keyed "default" AND the non-keyed delegator must
        // read-through; the archive path must still resolve the RAW hot (never the decorator).
        // NON-VACUITY: on the current orphaned wiring the keyed "default" + non-keyed resolve to the bare
        // hot store (not a TieredEventStoreDecorator) → RED. GREEN once the keyed re-bind lands.
        var hotStore = A.Fake<IEventStore>(x => x.Implements<IEventStoreArchive>());
        var services = NewTieredServices(hotStore);

        // Mirror EventSourcingServiceCollectionExtensions:72 — the non-keyed IEventStore delegates to keyed "default".
        services.TryAddSingleton<IEventStore>(sp => sp.GetRequiredKeyedService<IEventStore>("default"));

        _ = new ExcaliburEventSourcingBuilder(services).UseTieredStorage(_ => { });
        using var provider = services.BuildServiceProvider();

        // (1) the KEYED "default" (repositories / time-travel / notification resolve this) reads through.
        _ = provider.GetRequiredKeyedService<IEventStore>("default").ShouldBeAssignableTo<TieredEventStoreDecorator>(
            "jqk80w: the keyed \"default\" IEventStore (the repository load path) must be read-through-decorated, not the bare hot store.");

        // (2) the non-keyed delegator (which forwards to keyed "default") also reads through.
        _ = provider.GetRequiredService<IEventStore>().ShouldBeAssignableTo<TieredEventStoreDecorator>(
            "jqk80w: the non-keyed IEventStore must also resolve to the read-through decorator.");

        // (3) the archive machinery must still resolve the RAW hot store (it trims the hot tier and must
        //     never read-through cold) — decorating keyed "default" must NOT break this.
        provider.GetRequiredService<IEventStoreArchive>().ShouldBeSameAs(
            hotStore,
            "jqk80w: the IEventStoreArchive default must remain the RAW hot store (never the decorator) so trim enumerates only the hot tier.");
    }

    [Fact]
    public async Task WiredKeyedDefaultDecorator_ReadsThroughToCold_OnHotMiss_EndToEnd()
    {
        // jqk80w e2e — the DI-resolved keyed "default" decorator must actually READ THROUGH to cold on a hot
        // miss (not merely be the right type). Proves the wired path end-to-end: hot returns no events → the
        // decorator surfaces the archived events from cold. (The read-through LOGIC is unit-tested directly in
        // TieredEventStoreDecoratorShould; this proves the WIRED instance exercises it — closing the
        // advertised-but-unwired read gap the structural facts above catch.)
        var hotStore = A.Fake<IEventStore>(x => x.Implements<IEventStoreArchive>());
        var coldStore = A.Fake<IColdEventStore>();
        _ = A.CallTo(() => hotStore.LoadAsync("agg-1", "Order", A<CancellationToken>._)).Returns(new List<StoredEvent>());
        _ = A.CallTo(() => coldStore.HasArchivedEventsAsync(A<KeyedTenantPartition>._, "agg-1", A<CancellationToken>._)).Returns(true);
        _ = A.CallTo(() => coldStore.ReadAsync(A<KeyedTenantPartition>._, "agg-1", A<CancellationToken>._)).Returns(CreateEvents("agg-1", 1, 2, 3));

        var services = new ServiceCollection();
        _ = services.AddKeyedSingleton("default", hotStore);
        _ = services.AddSingleton(coldStore);
        _ = services.AddSingleton<ILogger<TieredEventStoreDecorator>>(NullLogger<TieredEventStoreDecorator>.Instance);
        services.TryAddSingleton<IEventStore>(sp => sp.GetRequiredKeyedService<IEventStore>("default"));
        _ = new ExcaliburEventSourcingBuilder(services).UseTieredStorage(_ => { });

        using var provider = services.BuildServiceProvider();
        var wired = provider.GetRequiredKeyedService<IEventStore>("default");

        var result = await wired.LoadAsync("agg-1", "Order", CancellationToken.None);

        result.Count.ShouldBe(
            3,
            "jqk80w: the wired keyed-\"default\" decorator must read through to cold on a hot miss, surfacing the archived events (not the hot store's empty result).");
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

    private static ServiceProvider BuildTieredProvider(
        IEventStore hotStore, Action<IServiceCollection>? preRegister = null)
    {
        var services = NewTieredServices(hotStore);
        preRegister?.Invoke(services);
        _ = new ExcaliburEventSourcingBuilder(services).UseTieredStorage(_ => { });
        return services.BuildServiceProvider();
    }

    private static IServiceCollection NewTieredServices(IEventStore hotStore)
    {
        var services = new ServiceCollection();
        _ = services.AddKeyedSingleton("default", hotStore);
        _ = services.AddSingleton(A.Fake<IColdEventStore>());
        _ = services.AddSingleton<ILogger<TieredEventStoreDecorator>>(NullLogger<TieredEventStoreDecorator>.Instance);
        return services;
    }
}
