// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Sharding;
using Excalibur.MultiTenancy;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.EventSourcing.Tests.Sharding;

/// <summary>
/// B3 (S894 REVIEW_CODE) — safety+liveness lock for the cold-tier fail-fast capability-gate. Row-discriminator
/// multi-tenancy MUST fail fast when a cold tier (<see cref="IColdEventStore"/>) is registered without proving
/// tenant-scoping capability, because a cold store reads purely by aggregate id with no tenant awareness (blob
/// key <c>{aggregateId}.json.gz</c>) — under row-discriminator MT a hot-miss read-through would return another
/// tenant's archived events (the cross-tenant cold-read leak, tracked <c>7iu2xc</c>/<c>e6t62k</c>). The gate
/// (<c>MultiTenancyServiceCollectionExtensions</c>: <c>RequireTenantScopingCapability&lt;IColdEventStore&gt;</c>,
/// composed on the same S886 <c>rw2ull</c> machinery) makes the unsafe (row-discriminator MT + non-tenant-aware
/// cold) triple inexpressible at startup rather than a silent runtime leak.
/// </summary>
/// <remarks>
/// <b>NON-VACUOUS.</b>
/// <list type="bullet">
/// <item><b>SAFETY</b> — the unsafe triple throws at registration. RED if the cold gate is removed (no
/// capability check → the tenant-unaware cold store is silently accepted → cross-tenant cold-read leak). A gate
/// asserted only on this half would be satisfied by one that rejects EVERY configuration, so both liveness arms
/// are required (<c>testing-patterns</c> §3).</item>
/// <item><b>LIVENESS (single-tenant)</b> — a cold tier registered WITHOUT multi-tenancy still starts (the gate
/// lives inside the row-discriminator path; a non-MT tiered+cold app is unaffected).</item>
/// <item><b>LIVENESS (MT-without-cold)</b> — row-discriminator multi-tenancy with a capability-proving store and
/// NO cold tier still starts and resolves the fail-closed decorator (the cold gate fires only when an
/// <see cref="IColdEventStore"/> is registered).</item>
/// </list>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class RowDiscriminatorColdStoreCapabilityGuardShould
{
    [Fact]
    public void Throw_WhenRowDiscriminatorRegistersANonTenantAwareColdStore()
    {
        var services = new ServiceCollection();

        // A registered but tenant-UNAWARE cold store: no ITenantScopingCapability<IColdEventStore> marker.
        // Cold stores (AzureBlob/S3/GCS) key by aggregate id with zero tenant awareness — the leak vector.
        services.AddSingleton<IColdEventStore>(new TenantUnawareColdEventStore());

        var ex = Should.Throw<InvalidOperationException>(() =>
            services.AddMultiTenancy(o => o.Strategy = TenantIsolationStrategy.RowDiscriminator));

        // RED if the cold gate is removed: without RequireTenantScopingCapability<IColdEventStore> the unsafe
        // triple is silently accepted and a hot-miss read-through leaks another tenant's archived events.
        ex.Message.ShouldContain("not tenant-scoping-capable");
        ex.Message.ShouldContain(nameof(IColdEventStore));
    }

    [Fact]
    public void Start_WhenAColdStoreIsRegisteredWithoutMultiTenancy()
    {
        // LIVENESS (single-tenant): a cold tier with NO AddMultiTenancy. The gate lives inside the
        // row-discriminator path, so a non-MT app with tiered+cold storage must start unaffected.
        var services = new ServiceCollection();
        services.AddSingleton<IColdEventStore>(new TenantUnawareColdEventStore());

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IColdEventStore>().ShouldBeOfType<TenantUnawareColdEventStore>();
    }

    [Fact]
    public void Start_WhenRowDiscriminatorIsRegisteredWithoutAColdStore()
    {
        // LIVENESS (MT-without-cold): row-discriminator multi-tenancy with a capability-proving event store and
        // NO cold tier. The cold gate is triggered only when an IColdEventStore is registered, so this safe
        // configuration must start and resolve the fail-closed tenant-scoping decorator.
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(new TenantUnawareEventStore());
        services.AddTenantScopedStore<IEventStore, TenantUnawareEventStore>((_, _) => new TenantUnawareEventStore());

        Should.NotThrow(() =>
            services.AddMultiTenancy(o => o.Strategy = TenantIsolationStrategy.RowDiscriminator));

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IEventStore>().ShouldBeOfType<TenantScopedEventStore>();
    }

    [Fact]
    public void Start_WhenRowDiscriminatorRegistersACapabilityProvingColdStore()
    {
        // LIVENESS (positive capability): a cold tier that PROVES tenant-scoping capability via the dep-gated
        // AddTenantScopedStore seam (the S886 inseparable marker) IS accepted by the gate — it starts. This is
        // the "permitted thing happens" arm: the gate is not reject-everything; a genuinely tenant-scoping cold
        // store passes. (No tenant-aware cold impl ships yet — that is the S895 e6t62k fix — so this uses the
        // sanctioned marker seam to stand in for a future capable cold store, as Frontend's B3 arm does.)
        var services = new ServiceCollection();

        // A realistic tiered + multi-tenant host: a PRIMARY tenant-owned hot store (RowDiscriminator requires
        // at least one of IEventStore/IProjectionStore/ISagaStore/IInboxStore/IOutboxStore) AND a cold tier —
        // BOTH capability-proving. The hot store is registered exactly as the MT-without-cold arm does.
        services.AddSingleton<IEventStore>(new TenantUnawareEventStore());
        services.AddTenantScopedStore<IEventStore, TenantUnawareEventStore>((_, _) => new TenantUnawareEventStore());

        // Register the cold tier through the dep-gated seam with the CONTRACT itself as TStore, so the
        // IColdEventStore service type IS registered (the gate at line 180 fires on
        // `services.Any(ServiceType == IColdEventStore)`) AND the ITenantScopingCapability<IColdEventStore>
        // marker is emitted inseparably (S886) — the cold gate then fires and PASSES. This is the exact seam a
        // real tenant-aware cold provider uses (mirrors TenantScopedTieredReadThroughShould:165).
        services.AddTenantScopedStore<IColdEventStore, IColdEventStore>(
            (_, _) => new TenantUnawareColdEventStore());

        Should.NotThrow(() =>
            services.AddMultiTenancy(o => o.Strategy = TenantIsolationStrategy.RowDiscriminator));

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IColdEventStore>().ShouldNotBeNull(
            "a capability-proving cold store must pass the gate and resolve");
    }

    /// <summary>A minimal cold store that ignores the ambient tenant — the cross-tenant cold-read leak vector.</summary>
    private sealed class TenantUnawareColdEventStore : IColdEventStore
    {
        // NOTE: the tenant term is accepted and deliberately IGNORED. That is the point of this fixture —
        // it is the tenant-unaware leak vector the guard must reject. Accepting the parameter keeps it a
        // compiling implementation of the current contract; honouring it would defeat the test.
        public Task<long> WriteAsync(
            KeyedTenantPartition tenant, string aggregateId, IReadOnlyList<StoredEvent> events,
            CancellationToken cancellationToken) =>
            Task.FromResult(events.Count > 0 ? events[^1].Version : -1);

        public Task<IReadOnlyList<StoredEvent>> ReadAsync(
            KeyedTenantPartition tenant, string aggregateId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<StoredEvent>>([]);

        public Task<IReadOnlyList<StoredEvent>> ReadAsync(
            KeyedTenantPartition tenant, string aggregateId, long fromVersion,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<StoredEvent>>([]);

        public Task<bool> HasArchivedEventsAsync(
            KeyedTenantPartition tenant, string aggregateId, CancellationToken cancellationToken) =>
            Task.FromResult(false);
    }

    /// <summary>A minimal event store used only to satisfy the IEventStore gate in the MT-without-cold liveness arm.</summary>
    private sealed class TenantUnawareEventStore : IEventStore
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
}
