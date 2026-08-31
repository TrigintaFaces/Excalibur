// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using static Excalibur.MultiTenancy.Tests.TestDoubles;

namespace Excalibur.MultiTenancy.Tests;

/// <summary>
/// Coverage for bead f1vlvs (umbrella): <c>AddMultiTenancy(RowDiscriminator)</c> real-DI resolve and the
/// registration-time capability fail-fast for the SAGA and PROJECTION store paths. The event-store path is
/// already covered by RowDiscriminatorTenantCapabilityGuardShould; this adds the sibling contracts and the
/// no-store guard.
/// </summary>
/// <remarks>
/// Capability fail-fast is asserted in BOTH arms per contract: a tenant-UNAWARE store (no capability marker) is
/// rejected at registration (safety), and a tenant-CAPABLE store resolves the fail-closed decorator through the
/// real container (liveness). A guard that rejected every store would fail the liveness arm.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class MultiTenancyDecoratorResolveShould
{
    // ---- Saga store path ----

    [Fact]
    public void Throw_WhenRowDiscriminatorWrapsATenantUnawareSagaStore()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISagaStore>(new NoopSagaStore(A.Fake<ITenantContext>())); // no capability marker → tenant-unaware provider

        var ex = Should.Throw<InvalidOperationException>(() =>
            services.AddMultiTenancy(static o => o.Strategy = TenantIsolationStrategy.RowDiscriminator));

        // (safety) the leak vector is refused at composition, not silently decorated.
        ex.Message.ShouldContain("not tenant-scoping-capable");
        ex.Message.ShouldContain(nameof(TenantIsolationStrategy.RowDiscriminator));
    }

    [Fact]
    public void ResolveTheFailClosedSagaDecorator_WhenTheSagaStoreProvesTenantCapability()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISagaStore>(new NoopSagaStore(A.Fake<ITenantContext>()));
        // Real marker via the dep-gated seam (the bare fake is now structurally unimplementable).
        services.AddTenantAwareStore<ISagaStore, NoopSagaStore>(sp => new NoopSagaStore(sp.GetRequiredService<ITenantContext>()));

        services.AddMultiTenancy(static o => o.Strategy = TenantIsolationStrategy.RowDiscriminator);

        using var provider = services.BuildServiceProvider();

        // (liveness) the real container resolves the fail-closed decorator for the capable saga store.
        _ = provider.GetRequiredService<ISagaStore>().ShouldBeOfType<TenantScopedSagaStore>();
    }

    // ---- Projection store path (family capability marker: ITenantScopingCapability<IProjectionStore<object>>) ----

    [Fact]
    public void Throw_WhenRowDiscriminatorWrapsATenantUnawareProjectionStore()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IProjectionStore<TestProjection>>(new NoopProjectionStore<TestProjection>());

        var ex = Should.Throw<InvalidOperationException>(() =>
            services.AddMultiTenancy(static o => o.Strategy = TenantIsolationStrategy.RowDiscriminator));

        // (safety) a tenant-unaware projection provider is rejected before it can leak cross-tenant rows.
        ex.Message.ShouldContain("not tenant-scoping-capable");
    }

    [Fact]
    public void ResolveTheFailClosedProjectionDecorator_WhenTheProjectionStoreProvesTenantCapability()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITenantContext>(new TestTenantContext("tenant-A"));
        // The projection seam registers the interface (scoped) AND the family marker together, dep-gating
        // ITenantContext — the real production wiring (the bare fake is now structurally unimplementable).
        // The store type is the seam's evidence: the marker is emitted because THIS type's constructor
        // requires an ITenantContext, so a store that ignores the tenant cannot be registered here at all.
        services.AddTenantScopedProjectionStore<IProjectionStore<TestProjection>, TenantReadingProjectionStore<TestProjection>, IProjectionStore<object>>(
            sp => new TenantReadingProjectionStore<TestProjection>(sp.GetRequiredService<ITenantContext>()));

        services.AddMultiTenancy(static o => o.Strategy = TenantIsolationStrategy.RowDiscriminator);

        using var provider = services.BuildServiceProvider();

        // (liveness) the real container resolves the fail-closed decorator for the capable projection store.
        // The projection registration is scoped, so resolve within a scope (A3).
        using var scope = provider.CreateScope();
        _ = scope.ServiceProvider.GetRequiredService<IProjectionStore<TestProjection>>()
            .ShouldBeOfType<TenantScopedProjectionStore<TestProjection>>();
    }

    // ---- No decoratable store at all ----

    [Fact]
    public void Throw_WhenRowDiscriminatorIsSelectedButNoTenantOwnedStoreIsRegistered()
    {
        var services = new ServiceCollection();

        // (safety) RowDiscriminator with nothing to scope fails fast rather than composing a no-op isolation.
        var ex = Should.Throw<InvalidOperationException>(() =>
            services.AddMultiTenancy(static o => o.Strategy = TenantIsolationStrategy.RowDiscriminator));
        ex.Message.ShouldContain("no tenant-owned store");
    }
}
