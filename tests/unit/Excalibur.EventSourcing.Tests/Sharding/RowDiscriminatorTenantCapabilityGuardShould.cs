// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Sharding;
using Excalibur.MultiTenancy;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.EventSourcing.Tests.Sharding;

/// <summary>
/// Regression lock for bd-b1g4o0 — the reachable cross-tenant leak in the row-discriminator multi-tenancy
/// keystone. Before the fix, <c>AddMultiTenancy(RowDiscriminator)</c> wrapped ANY registered
/// <see cref="IEventStore"/> with <see cref="TenantScopedEventStore"/> on an "a store exists" guard alone;
/// the decorator enforces tenant <em>presence</em> (<c>RequireTenant</c>) but NOT inner-store
/// <em>capability</em>, so a tenant-unaware provider (e.g. Oracle, which ignores the ambient tenant) got
/// wrapped, passed the presence check, and leaked every tenant's events.
/// </summary>
/// <remarks>
/// The structural fix (enforce-invariants-structurally): a provider must PROVE tenant-scoping capability by
/// registering an <see cref="ITenantScopingCapability{TContract}"/> marker; <c>ApplyRowDiscriminator</c>
/// fails fast at registration when the marker is absent. These locks bind that guard:
/// <list type="bullet">
/// <item>Lock A (leak-closer): an unmarked store + RowDiscriminator throws at registration. RED on the
/// pre-fix code, which silently decorated the tenant-unaware store.</item>
/// <item>Lock B (positive): a marked (capable) store + RowDiscriminator resolves the fail-closed decorator.</item>
/// </list>
/// <see cref="ISagaStore"/> flows through the identical shared <c>RequireTenantScopingCapability&lt;TContract&gt;</c>
/// guard; <see cref="IProjectionStore{TProjection}"/> through its family-token variant.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class RowDiscriminatorTenantCapabilityGuardShould
{
    [Fact]
    public void Throw_WhenRowDiscriminatorWrapsATenantUnawareEventStore()
    {
        var services = new ServiceCollection();

        // A registered but tenant-UNAWARE event store: no ITenantScopingCapability<IEventStore> marker,
        // exactly as a provider that ignores the ambient tenant (e.g. Oracle) would register.
        services.AddSingleton<IEventStore>(new TenantUnawareEventStore(A.Fake<ITenantContext>()));

        var ex = Should.Throw<InvalidOperationException>(() =>
            services.AddMultiTenancy(o => o.Strategy = TenantIsolationStrategy.RowDiscriminator));

        // RED on pre-fix: pre-fix ApplyRowDiscriminator had no capability check → no throw → silent leak.
        ex.Message.ShouldContain("not tenant-scoping-capable");
        ex.Message.ShouldContain(nameof(TenantIsolationStrategy.RowDiscriminator));
    }

    [Fact]
    public void ResolveTheFailClosedDecorator_WhenTheEventStoreProvesTenantCapability()
    {
        var services = new ServiceCollection();

        // A tenant-CAPABLE store: the real marker is emitted via the dep-gated seam (the bare fake is now
        // structurally unimplementable). The AddSingleton<IEventStore> is the interface the decorator wraps.
        services.AddSingleton<IEventStore>(new TenantUnawareEventStore(A.Fake<ITenantContext>()));
        services.AddTenantAwareStore<IEventStore, TenantUnawareEventStore>(
            sp => new TenantUnawareEventStore(sp.GetRequiredService<ITenantContext>()));

        services.AddMultiTenancy(o => o.Strategy = TenantIsolationStrategy.RowDiscriminator);

        using var provider = services.BuildServiceProvider();

        // The primary IEventStore resolution is the fail-closed tenant-scoping decorator.
        provider.GetRequiredService<IEventStore>().ShouldBeOfType<TenantScopedEventStore>();
    }

    /// <summary>A minimal event store that ignores the ambient tenant — the leak vector under test.</summary>
    private sealed class TenantUnawareEventStore(ITenantContext tenantContext) : IEventStore
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
