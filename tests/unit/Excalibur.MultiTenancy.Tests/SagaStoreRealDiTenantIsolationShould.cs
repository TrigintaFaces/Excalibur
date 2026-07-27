// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Saga.SqlServer;
using Excalibur.Saga.SqlServer.DependencyInjection;

using Microsoft.Extensions.Logging;

namespace Excalibur.MultiTenancy.Tests;

/// <summary>
/// Real-DI regression lock for bead rw2ull: the SQL Server saga store, resolved through the production
/// registration path, MUST have the ambient <see cref="ITenantContext"/> threaded into it — that field is
/// the sole switch that turns row-level tenant isolation on. The store applies its tenant predicate through
/// <c>TenantScope.FromContext(_tenantContext)</c> on every load/save: a <see langword="null"/> context
/// yields <c>TenantScope.None</c> (no predicate, no column, no parameter), so tenant B's scoped read sees
/// tenant A's saga — a cross-tenant leak — even though the provider still registers the
/// <see cref="ITenantScopingCapability{TContract}"/> capability marker attesting it is tenant-aware.
/// </summary>
/// <remarks>
/// <para>
/// The isolation itself is enforced by a row <c>TenantId</c> predicate inside the store's SQL, which cannot
/// be exercised without a live SQL Server. This lock instead asserts the wiring that gates that predicate —
/// whether the ambient context reached the resolved store — which is exactly the defect: the builder
/// registration factory constructed <see cref="SqlServerSagaStore"/> without resolving
/// <c>sp.GetService&lt;ITenantContext&gt;()</c>, so <c>_tenantContext</c> was null and every request ran
/// unscoped. No SQL Server is required: the store constructor captures the connection factory lazily.
/// </para>
/// <para>
/// Both provider registration entry points are covered against the same assertion. Against committed HEAD
/// (3d1e5f7dd) BOTH factories dropped the ambient context and the two tests are RED (verified in a throwaway
/// worktree); once the factories resolve <c>sp.GetService&lt;ITenantContext&gt;()</c> both are GREEN — a
/// non-vacuous lock (it can both fail on the inert tree and pass on the wired one):
/// <list type="bullet">
///   <item><description>
///   <b>Builder path</b> (<c>AddSagas(saga =&gt; saga.UseSqlServer(...))</c>) — the primary documented
///   consumer path, and the more important leak path.
///   </description></item>
///   <item><description>
///   <b>Standalone path</b> (<c>AddSqlServerSagaStore(...)</c>) — the direct registration entry point.
///   </description></item>
/// </list>
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class SagaStoreRealDiTenantIsolationShould
{
    // An intentionally unroutable connection string: the store never connects here — construction only
    // captures the factory. It is never opened because these tests exercise registration/wiring, not I/O.
    private const string UnusedConnectionString =
        "Server=(localdb)\\ExcaliburUnused;Database=sagas_unused;Trusted_Connection=True;";

    /// <summary>
    /// The must-have lock. Resolves the saga store through the real builder registration path the docs steer
    /// consumers to, and asserts the ambient tenant context is wired — RED on the pre-fix builder factory
    /// that omitted it (cross-tenant saga leak).
    /// </summary>
    [Fact]
    public void SagaStore_ResolvedThroughRealDI_IsolatesTenants()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        // The ambient tenant context the multi-tenant host registers; the provider factory must resolve it.
        _ = services.AddTenantContext(static o => o.RequireTenant = true);

        _ = services.AddExcalibur(excalibur => excalibur.AddSagas(saga =>
            saga.UseSqlServer(sql => sql.ConnectionString(UnusedConnectionString))));

        using var provider = services.BuildServiceProvider();

        var store = provider.GetRequiredKeyedService<ISagaStore>("sqlserver");
        var sqlStore = store.ShouldBeOfType<SqlServerSagaStore>();

        // (isolation active) The resolved provider store must carry the ambient context. Null here means
        // TenantScope.FromContext(null) == TenantScope.None on every load/save: no row predicate, so a
        // tenant-B read returns tenant-A's saga. The capability marker attests tenant-awareness the wiring
        // never delivered.
        ReadTenantContext(sqlStore).ShouldNotBeNull(
            "SqlServerSagaStore resolved through the builder path (AddSagas().UseSqlServer()) has no "
            + "ITenantContext wired: row-level tenant isolation is inert (TenantScope.None) and sagas leak "
            + "across tenants despite the registered ITenantScopingCapability<ISagaStore> attestation.");
    }

    /// <summary>
    /// Second registration entry point: the standalone <c>AddSqlServerSagaStore</c> factory must thread the
    /// ambient context too. RED against committed HEAD (the factory omitted it), GREEN once wired — the same
    /// leak on the direct registration path.
    /// </summary>
    [Fact]
    public void SagaStore_ResolvedThroughStandaloneAddPath_WiresAmbientTenantContext()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddTenantContext(static o => o.RequireTenant = true);
        // AddExcalibur supplies the serialization/dispatch primitives the saga store factory depends on.
        _ = services.AddExcalibur(_ => { });
        _ = services.AddSqlServerSagaStore(o => o.ConnectionString = UnusedConnectionString);

        using var provider = services.BuildServiceProvider();

        var sqlStore = provider.GetRequiredKeyedService<ISagaStore>("sqlserver").ShouldBeOfType<SqlServerSagaStore>();

        ReadTenantContext(sqlStore).ShouldNotBeNull(
            "SqlServerSagaStore resolved through the standalone AddSqlServerSagaStore path has no "
            + "ITenantContext wired: the direct registration factory dropped sp.GetService<ITenantContext>(), "
            + "so row-level tenant isolation is inert (TenantScope.None) and sagas leak across tenants.");
    }

    private static ITenantContext? ReadTenantContext(SqlServerSagaStore store)
    {
        var field = typeof(SqlServerSagaStore).GetField("_tenantContext", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("SqlServerSagaStore._tenantContext field not found — the wiring seam moved.");
        return (ITenantContext?)field.GetValue(store);
    }
}
