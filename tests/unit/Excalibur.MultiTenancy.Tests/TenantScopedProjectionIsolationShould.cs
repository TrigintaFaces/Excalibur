// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using static Excalibur.MultiTenancy.Tests.TestDoubles;

namespace Excalibur.MultiTenancy.Tests;

/// <summary>
/// Emitted-behavior locks for the dep-gated tenant-scoping seam (zh70zl + 59sitk). These bind the ACTUAL
/// tenant isolation a store wired through <c>AddTenantScopedProjectionStore</c> delivers — not the mere
/// presence of a capability marker (which is now structurally un-fakeable). A do-nothing store passes the
/// safety arm vacuously, so a REAL tenant-filtering store is used and the liveness arm proves it serves the
/// owning tenant.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class TenantScopedProjectionIsolationShould
{
    // zh70zl — the isolation guarantee, both arms, against a REAL tenant-filtering store.
    [Fact]
    public async Task NotLeakOneTenantsRowToAnother_ButStillServeTheOwningTenant()
    {
        TenantFilteringProjectionStore<TestProjection>.Backing.Clear();

        var rowA = new TestProjection { Value = "tenant-A-secret" };

        // Tenant A writes its row through a store wired with tenant-A's ambient context.
        await using (var providerA = BuildTenantScopedProjectionProvider("tenant-A"))
        {
            using var scopeA = providerA.CreateScope();
            var storeA = scopeA.ServiceProvider.GetRequiredService<IProjectionStore<TestProjection>>();
            await storeA.UpsertAsync("row-1", rowA, CancellationToken.None);

            // (liveness) tenant A DOES see its own row — a store that returns nothing to anybody would
            // pass the safety arm below vacuously; this arm rejects that.
            (await storeA.GetByIdAsync("row-1", CancellationToken.None)).ShouldBe(rowA);
        }

        // Tenant B reads the SAME id through a store wired with tenant-B's ambient context.
        await using var providerB = BuildTenantScopedProjectionProvider("tenant-B");
        using var scopeB = providerB.CreateScope();
        var storeB = scopeB.ServiceProvider.GetRequiredService<IProjectionStore<TestProjection>>();

        // (safety) tenant B's scoped read does NOT see tenant A's row.
        (await storeB.GetByIdAsync("row-1", CancellationToken.None))
            .ShouldBeNull("a tenant-scoped read must not surface another tenant's row.");
    }

    // 59sitk — the coupled-emission / fail-closed guarantee: a store wired WITHOUT an ITenantContext
    // cannot be built (the dep-gated seam resolves ITenantContext with GetRequiredService → throws).
    [Fact]
    public void FailClosed_WhenTheProjectionStoreIsWiredWithoutATenantContext()
    {
        var services = new ServiceCollection();
        // Deliberately NO ITenantContext registered.
        services.AddTenantScopedProjectionStore<IProjectionStore<TestProjection>, IProjectionStore<object>>(
            (_, tenant) => new TenantFilteringProjectionStore<TestProjection>(tenant));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        // The dep-gate fails closed: without an ambient tenant context the store is never constructed,
        // so a tenant-unaware store can never slip through wearing a truthful capability marker.
        Should.Throw<InvalidOperationException>(() =>
            scope.ServiceProvider.GetRequiredService<IProjectionStore<TestProjection>>());
    }

    private static ServiceProvider BuildTenantScopedProjectionProvider(string tenantId)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITenantContext>(new TestTenantContext(tenantId));
        services.AddTenantScopedProjectionStore<IProjectionStore<TestProjection>, IProjectionStore<object>>(
            (_, tenant) => new TenantFilteringProjectionStore<TestProjection>(tenant));
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// A minimal in-memory projection store that GENUINELY scopes every read/write by the ambient tenant,
    /// so the isolation assertions are non-vacuous. The backing store is shared (static) across instances so
    /// two differently-scoped providers observe the same underlying data.
    /// </summary>
    private sealed class TenantFilteringProjectionStore<TProjection>(ITenantContext tenant) : IProjectionStore<TProjection>
        where TProjection : class
    {
        internal static readonly ConcurrentDictionary<(string Tenant, string Id), TProjection> Backing = new();

        private string Tenant => tenant.TenantId ?? throw new InvalidOperationException("No ambient tenant.");

        public Task<TProjection?> GetByIdAsync(string id, CancellationToken cancellationToken) =>
            Task.FromResult(Backing.TryGetValue((Tenant, id), out var p) ? p : null);

        public Task UpsertAsync(string id, TProjection projection, CancellationToken cancellationToken)
        {
            Backing[(Tenant, id)] = projection;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string id, CancellationToken cancellationToken)
        {
            _ = Backing.TryRemove((Tenant, id), out _);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<TProjection>> QueryAsync(
            IDictionary<string, object>? filters, QueryOptions? options, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TProjection>>(
                Backing.Where(kv => kv.Key.Tenant == Tenant).Select(kv => kv.Value).ToList());

        public Task<long> CountAsync(IDictionary<string, object>? filters, CancellationToken cancellationToken) =>
            Task.FromResult(Backing.LongCount(kv => kv.Key.Tenant == Tenant));
    }
}
