// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.SqlServer;
using Excalibur.Saga.SqlServer.DependencyInjection;

namespace Excalibur.MultiTenancy.Tests;

/// <summary>
/// 15ph5g / D1 + 41dbu7 — author≠impl lock for the tenant-scoping seam's <b>dep-gate</b> and for the
/// registration mechanics the dep-gate's safety actually rests on.
/// </summary>
/// <remarks>
/// <para>
/// <b>What the seam guarantees, precisely.</b> <c>AddTenantAwareStore</c> resolves
/// <see cref="ITenantContext"/> via <c>GetRequiredService</c> inside the store factory, so a store
/// registered <i>through the seam</i> cannot be constructed unless a context was supplied — the S886
/// "lying marker" (a store advertising <see cref="ITenantScopingCapability{TContract}"/> while running
/// unscoped) is inexpressible through this path.
/// </para>
/// <para>
/// <b>What it does NOT guarantee — and why arm 1 drives the seam directly.</b> The dep-gate's throw is
/// only reachable when no <see cref="ITenantContext"/> is registered at all. Providers call
/// <c>AddDefaultTenantContext()</c>, which registers the <c>SingleTenantContext</c> Null Object, so
/// <c>GetRequiredService</c> always succeeds and the fail-closed arm never fires on those paths. The real
/// run-time protection is <c>TenantScope.FromContext</c>/<c>Scoped</c> (which throws
/// <c>TenantRequiredException</c> on a null/blank tenant), not DI resolution. Arm 1 therefore exercises
/// the seam with a <b>test-local</b> contract and store: dependency-independent, so no provider's wiring
/// can silently become its vehicle. (An earlier revision asserted this through <c>AddSqlServerSagaStore</c>
/// — it passed only because saga omitted <c>AddDefaultTenantContext()</c>, i.e. its vehicle was the 41dbu7
/// bug itself. Fixing that bug made the scenario unreachable through saga's public API, so the arm was
/// re-pointed at the seam rather than relaxed.)
/// </para>
/// <para>
/// <b>Arms (testing-patterns §3 — every safety assertion paired with liveness).</b>
/// <list type="bullet">
/// <item>SAFETY — seam with no context registered fails closed (arm 1).</item>
/// <item>LIVENESS (multi-tenant) — a context-supplied store resolves AND advertises the capability, so a
/// seam that threw unconditionally would fail (arm 2).</item>
/// <item>LIVENESS (single-tenant) — an app that registers no context of its own still resolves its store
/// (arm 3a). This is the 41dbu7 regression: its absence let a throw-for-every-single-tenant-app ship past
/// a full-coverage VERIFY, because "fails closed without a context" is equally satisfied by "throws for
/// everybody, forever".</item>
/// <item>ORDER-INDEPENDENCE — the ambient context wins over the single-tenant default regardless of
/// registration order (arm 3b), pinning the <c>Replace</c>-beats-<c>TryAdd</c> mechanic that arm 3a's
/// safety rests on.</item>
/// </list>
/// </para>
/// <para>
/// No SQL Server is required: resolution either throws (safety) or constructs the store lazily without
/// connecting (liveness). Contexts are asserted by <b>observable behavior</b> (<c>TenantId</c>) rather than
/// by concrete type — the implementations are <c>internal</c>, and a test must not widen production
/// visibility to see them.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class TenantScopingCapabilityDepGatedShould
{
    /// <summary>The tenant id the <c>SingleTenantContext</c> Null Object reports.</summary>
    private const string DefaultTenantId = "__default__";

    // Never opened: construction captures the connection string lazily; these tests exercise DI wiring only.
    private const string UnusedConnectionString =
        "Server=(localdb)\\ExcaliburUnused;Database=sagas_unused;Trusted_Connection=True;";

    /// <summary>
    /// SAFETY: a store registered through the tenant-scoping seam with NO <see cref="ITenantContext"/>
    /// registered fails closed when resolved — the store cannot be built without the dependency it needs to
    /// honor the tenant discriminator, so it can never carry a truthful capability marker.
    /// </summary>
    [Fact]
    public void TenantScopedSeam_ResolvedWithNoTenantContextRegistered_FailsClosed()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        // Deliberately NO tenant context of any kind — not the ambient one, not the single-tenant default.
        _ = services.AddTenantAwareStore<IProbeStore, ProbeStore>(
            static sp => new ProbeStore(sp.GetRequiredService<ITenantContext>()));

        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<InvalidOperationException>(
            () => provider.GetRequiredService<ProbeStore>(),
            "AddTenantAwareStore must resolve ITenantContext via GetRequiredService before the store "
            + "factory. Resolving with NO context registered did not fail closed, so a store can be built "
            + "with a null tenant context (TenantScope.Untenanted — unscoped, the S886 rw2ull cross-tenant leak) "
            + "while still advertising ITenantScopingCapability. The dep-gate must be required, not optional.");
    }

    /// <summary>
    /// LIVENESS (seam): with a tenant context registered, the seam-built store resolves AND receives that
    /// very context — proving the seam is dep-gated rather than fail-everything, and that it threads the
    /// dependency instead of merely demanding it.
    /// </summary>
    [Fact]
    public void TenantScopedSeam_ResolvedWithTenantContextRegistered_ResolvesAndThreadsTheContext()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddDefaultTenantContext();
        _ = services.AddTenantAwareStore<IProbeStore, ProbeStore>(
            static sp => new ProbeStore(sp.GetRequiredService<ITenantContext>()));

        using var provider = services.BuildServiceProvider();

        var store = provider.GetRequiredService<ProbeStore>();

        store.TenantContext.ShouldNotBeNull(
            "the seam must thread the resolved ITenantContext into the store factory; a null context here "
            + "means the store runs unscoped (TenantScope.Untenanted) while advertising tenant scoping.");
        store.TenantContext.TenantId.ShouldBe(
            DefaultTenantId,
            "the store must receive the context that is actually registered, not a substitute.");
    }

    /// <summary>
    /// LIVENESS (multi-tenant, real provider): with the ambient <see cref="ITenantContext"/> registered, a
    /// real provider's store resolves AND the provider advertises
    /// <see cref="ITenantScopingCapability{TContract}"/> — proving the marker is emitted inseparably with
    /// the (correctly wired) store.
    /// </summary>
    [Fact]
    public void SagaStore_ResolvedWithAmbientTenantContext_ResolvesAndAdvertisesCapability()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddExcalibur(_ => { });
        _ = services.AddTenantContext(static o => o.RequireTenant = true);
        _ = services.AddSqlServerSagaStore(o => o.ConnectionString = UnusedConnectionString);

        using var provider = services.BuildServiceProvider();

        var store = provider.GetRequiredKeyedService<ISagaStore>("sqlserver");
        _ = store.ShouldBeOfType<SqlServerSagaStore>();

        // The capability marker exists ONLY because AddTenantAwareStore emitted it alongside the store — so
        // a consumer's RequireTenantScopingCapability<ISagaStore> gate passes for a genuinely tenant-wired
        // store, and would be absent for a provider that never routed through the seam.
        provider.GetService<ITenantScopingCapability<ISagaStore>>().ShouldNotBeNull(
            "AddSqlServerSagaStore (via AddTenantAwareStore) must emit ITenantScopingCapability<ISagaStore> "
            + "when the store is wired with an ambient ITenantContext.");
    }

    /// <summary>
    /// LIVENESS (single-tenant, real provider) — the 41dbu7 regression lock. An app that registers NO tenant
    /// context of its own must still resolve its saga store: the provider is responsible for supplying the
    /// single-tenant default, exactly as its 11 sibling store families do.
    /// </summary>
    /// <remarks>
    /// RED before the 41dbu7 fix: the saga extensions routed through the dep-gated seam without calling
    /// <c>AddDefaultTenantContext()</c>, so <c>GetRequiredService&lt;ITenantContext&gt;</c> threw and every
    /// single-tenant app was dead at resolve. "Fails closed without a context" is equally satisfied by
    /// "throws for everybody" — this arm is what tells the two apart.
    /// </remarks>
    [Fact]
    public void SagaStore_ResolvedByASingleTenantApp_ResolvesViaTheSingleTenantDefault()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddExcalibur(_ => { });
        // A single-tenant app: no AddTenantContext, no AddMultiTenancy. The provider must supply the default.
        _ = services.AddSqlServerSagaStore(o => o.ConnectionString = UnusedConnectionString);

        using var provider = services.BuildServiceProvider();

        var store = provider.GetRequiredKeyedService<ISagaStore>("sqlserver");
        _ = store.ShouldBeOfType<SqlServerSagaStore>();

        // WHY it resolved matters: the single-tenant default must be present and must yield a real tenant
        // discriminator. A context whose TenantId were null/blank would make TenantScope.Scoped throw at
        // query time; CurrentTenantScope would silently be None — the unscoped leak.
        var tenantContext = provider.GetRequiredService<ITenantContext>();
        tenantContext.TenantId.ShouldBe(
            DefaultTenantId,
            "AddSqlServerSagaStore must register the single-tenant default (AddDefaultTenantContext), so a "
            + "single-tenant app resolves a store scoped to the default tenant rather than throwing at "
            + "resolve or running unscoped.");
        tenantContext.HasTenant.ShouldBeTrue(
            "the single-tenant default is a Null Object: it always reports a tenant, so the store is always "
            + "scoped rather than TenantScope.Untenanted.");
    }

    /// <summary>
    /// ORDER-INDEPENDENCE: the ambient, resolver-driven context must win over the single-tenant default
    /// regardless of composition order. Pins the <c>Replace</c>-beats-<c>TryAdd</c> mechanic in
    /// <c>AddTenantContext</c> that the single-tenant default's safety rests on.
    /// </summary>
    /// <remarks>
    /// Arm 3a lets providers register <c>SingleTenantContext</c> via <c>TryAdd</c>. <c>TryAdd</c> is
    /// first-wins, so that default is only safe for multi-tenant apps because <c>AddTenantContext</c> uses
    /// <c>Replace</c>, which overwrites regardless of order. Nothing else fails if that <c>Replace</c> is
    /// ever "tidied" into a <c>TryAdd</c> — at which point a multi-tenant app that registered its store
    /// first would silently resolve the single-tenant default and run every tenant against
    /// <c>"__default__"</c>. This arm is that guarantee's only structural defense.
    /// </remarks>
    [Fact]
    public void AmbientTenantContext_RegisteredAfterTheSingleTenantDefault_StillWins()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();

        // Worst-case order: the single-tenant default lands FIRST (as a provider's AddXxxStore would do)...
        _ = services.AddDefaultTenantContext();
        // ...and the multi-tenant registration arrives AFTER it.
        _ = services.AddTenantContext(static o => o.RequireTenant = true);

        using var provider = services.BuildServiceProvider();

        // Behavior, not type: the ambient context reports the AMBIENT tenant (none is set here, so null),
        // whereas the single-tenant default always reports "__default__". If the default had won, every
        // tenant of a multi-tenant app would be silently scoped to "__default__" — a cross-tenant leak.
        var tenantContext = provider.GetRequiredService<ITenantContext>();
        tenantContext.TenantId.ShouldBeNull(
            "AddTenantContext must Replace (not TryAdd) the ITenantContext registration so the ambient, "
            + "resolver-driven context wins over a previously-registered single-tenant default regardless "
            + "of composition order. Seeing \"__default__\" here means the default won and every tenant "
            + "would be scoped to it.");
    }

    /// <summary>Test-local contract — the seam's dep-gate is asserted independently of any provider.</summary>
    private interface IProbeStore
    {
        /// <summary>Gets the context the seam threaded into construction.</summary>
        ITenantContext TenantContext { get; }
    }

    /// <summary>
    /// Test-local store implementing <see cref="IProbeStore"/> DIRECTLY — no first-party base supplies the
    /// member under test, so the arms bind the seam's own contract (testing-patterns §3 fixture-shape
    /// corollary). Widens no production visibility.
    /// </summary>
    private sealed class ProbeStore : IProbeStore
    {
        public ProbeStore(ITenantContext tenantContext) => TenantContext = tenantContext;

        public ITenantContext TenantContext { get; }
    }
}
