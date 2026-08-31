// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance;
using Excalibur.Dispatch.ErrorHandling;

namespace Excalibur.MultiTenancy.Tests;

/// <summary>
/// Author-independent lock on the coverage ORACLE that decides which contracts row-discriminator
/// multi-tenancy must gate.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect.</b> Coverage was a hardcoded closed set. The gates named eight contracts and nothing
/// detected a registered store outside them, so a consumer selecting row-discriminator multi-tenancy received
/// an unscoped audit store, compliance store, data-inventory store, dead-letter queue and snapshot store, and
/// no error at all. The composition check reported success over contracts it had never been told about. The
/// gate file said so itself: the only thing standing between a new tenant-owned contract and a silent
/// cross-tenant leak was a person reading an array, and that is not a control.
/// </para>
/// <para>
/// <b>The fix under test.</b> The oracle is inverted. The tenant-owned attribute is declared on the contract
/// interface, and the gate enumerates the registration, so coverage is derived from what is actually present
/// rather than from a list somebody has to remember to extend. A contract nobody named is caught.
/// </para>
/// <para>
/// <b>Both arms, deliberately.</b> A gate that rejects everything satisfies every safety assertion here
/// perfectly, so the safety arms alone would be passed by a total outage. The liveness arms register a
/// correctly-capable store through the real seam, resolve it from a real container, and read through it — and
/// one arm asserts an ordinary un-owned contract is still accepted with no marker at all.
/// </para>
/// <para>
/// <b>Non-vacuity.</b> Every safety arm fails on the pre-fix gate, because that gate raised no error for any
/// of these contracts. <see cref="RejectAConsumersOwnTenantOwnedContract_ThatNoFrameworkListCouldName"/> is
/// the arm no hand-maintained manifest can be edited to pass.
/// </para>
/// </remarks>
/// <summary>A tenant-owned contract declared OUTSIDE the framework, to prove the oracle is open-world.</summary>
[TenantOwned]
public interface IConsumerTenantOwnedStore
{
    Task<string?> ReadAsync();
}

/// <summary>Extends a tenant-owned contract without repeating the attribute.</summary>
public interface IExtendedConsumerStore : IConsumerTenantOwnedStore;

/// <summary>Not tenant-owned: holds no tenant rows and must never be gated.</summary>
public interface IPlainNonTenantStore
{
    Task<string?> ReadAsync();
}

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class TenantOwnedCoverageOracleShould
{
    // ---- SAFETY: a registered tenant-owned contract with no capability is refused, and named ------------

    [Fact]
    public void RejectAnAuditStore_ThatPresentsNoTenantCapability() =>
        ShouldRefuseAndNameTheContract<IAuditStore>();

    [Fact]
    public void RejectAComplianceStore_ThatPresentsNoTenantCapability() =>
        ShouldRefuseAndNameTheContract<IComplianceStore>();

    [Fact]
    public void RejectADataInventoryStore_ThatPresentsNoTenantCapability() =>
        ShouldRefuseAndNameTheContract<IDataInventoryStore>();

    [Fact]
    public void RejectASnapshotStore_ThatPresentsNoTenantCapability() =>
        ShouldRefuseAndNameTheContract<ISnapshotStore>();

    [Fact]
    public void RejectADeadLetterQueue_ThatPresentsNoTenantCapability() =>
        ShouldRefuseAndNameTheContract<IDeadLetterQueue>();

    /// <summary>
    /// The arm that distinguishes an inverted oracle from a longer list: this contract is declared in the TEST
    /// assembly, so no manifest in the framework could name it, yet it is covered because it is marked.
    /// </summary>
    [Fact]
    public void RejectAConsumersOwnTenantOwnedContract_ThatNoFrameworkListCouldName() =>
        ShouldRefuseAndNameTheContract<IConsumerTenantOwnedStore>();

    /// <summary>
    /// Attributes are not inherited across interfaces, so an interface extending a tenant-owned contract would
    /// slip through a naive check on the service type alone.
    /// </summary>
    [Fact]
    public void RejectAnInterfaceExtendingATenantOwnedContract() =>
        ShouldRefuseAndNameTheContract<IExtendedConsumerStore>();

    // ---- LIVENESS: a correctly-capable configuration still registers, resolves, and READS --------------

    [Fact]
    public async Task AcceptAndServeReadsFrom_AStoreRegisteredThroughTheTenantScopedSeam()
    {
        var services = NewCollectionWithTenant("acme");
        services.AddTenantAwareStore<IConsumerTenantOwnedStore, ConsumerTenantOwnedStore>(
            static sp => new ConsumerTenantOwnedStore(sp.GetRequiredService<ITenantContext>()));
        services.AddSingleton<IConsumerTenantOwnedStore>(
            static sp => sp.GetRequiredService<ConsumerTenantOwnedStore>());

        services.AddMultiTenancy(static o => o.Strategy = TenantIsolationStrategy.RowDiscriminator);

        using var provider = services.BuildServiceProvider();

        // Not merely "did not throw": the real container resolves it and the store serves a read that
        // reflects the ambient tenant it was constructed with.
        var store = provider.GetRequiredService<IConsumerTenantOwnedStore>();
        var row = await store.ReadAsync().ConfigureAwait(true);

        row.ShouldBe(
            ConsumerTenantOwnedStore.Row,
            "the resolved store must actually serve a read, not merely resolve without throwing.");

        // And it was BUILT with the ambient context, which is what the dep-gated seam exists to guarantee.
        // Asserting the tenant VALUE here would be asserting the wrong property: AddMultiTenancy replaces
        // ITenantContext with the ambient one, which correctly reports no tenant outside a request scope, so a
        // value assertion would be testing request-scope plumbing rather than this gate.
        provider.GetRequiredService<ConsumerTenantOwnedStore>().WasGivenATenantContext.ShouldBeTrue(
            "the dep-gated seam must hand the store its tenant context, or the capability it emits is a lie.");
    }

    [Fact]
    public void AcceptAStoreRegisteredThroughTheTenantPartitionedSeam()
    {
        var services = NewCollectionWithTenant("acme");
        services.AddTenantAwareStore<IConsumerTenantOwnedStore, ConsumerTenantOwnedPartitionedStore>(
            static _ => new ConsumerTenantOwnedPartitionedStore());
        services.AddSingleton<IConsumerTenantOwnedStore>(
            static sp => sp.GetRequiredService<ConsumerTenantOwnedPartitionedStore>());

        // Either capability satisfies the floor: this one attests the tenant travels on the row. Which marker
        // a given contract must present is enforced by the per-contract gates, not here.
        Should.NotThrow(() =>
            services.AddMultiTenancy(static o => o.Strategy = TenantIsolationStrategy.RowDiscriminator));

        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<IConsumerTenantOwnedStore>().ShouldNotBeNull();
    }

    /// <summary>
    /// The arm a reject-everything gate fails. An ordinary contract that is not tenant-owned carries no
    /// marker and must still be accepted.
    /// </summary>
    [Fact]
    public void AcceptAContractThatIsNotTenantOwned_CarryingNoCapabilityAtAll()
    {
        var services = NewCollectionWithTenant("acme");
        services.AddSingleton(A.Fake<IPlainNonTenantStore>());
        services.AddTenantAwareStore<IConsumerTenantOwnedStore, ConsumerTenantOwnedStore>(
            static sp => new ConsumerTenantOwnedStore(sp.GetRequiredService<ITenantContext>()));

        Should.NotThrow(() =>
            services.AddMultiTenancy(static o => o.Strategy = TenantIsolationStrategy.RowDiscriminator));
    }

    // ---- helpers ---------------------------------------------------------------------------------------

    private static void ShouldRefuseAndNameTheContract<TContract>()
        where TContract : class
    {
        var services = NewCollectionWithTenant("acme");
        services.AddSingleton(A.Fake<TContract>());

        var error = Should.Throw<InvalidOperationException>(() =>
            services.AddMultiTenancy(static o => o.Strategy = TenantIsolationStrategy.RowDiscriminator));

        // Naming the contract is part of the contract: a consumer with a hundred registrations cannot act on
        // "some store is unscoped".
        error.Message.ShouldContain(
            typeof(TContract).Name,
            Case.Sensitive,
            "the failure must name the store the consumer has to fix.");
    }

    private static ServiceCollection NewCollectionWithTenant(string tenantId)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITenantContext>(new FixedTenantContext(tenantId));
        return services;
    }

    private sealed class ConsumerTenantOwnedStore(ITenantContext tenantContext) : IConsumerTenantOwnedStore
    {
        internal const string Row = "row";

        /// <summary>Whether the dep-gated seam supplied the ambient tenant context at construction.</summary>
        internal bool WasGivenATenantContext { get; } = tenantContext is not null;

        public Task<string?> ReadAsync() => Task.FromResult<string?>(Row);
    }

    /// <summary>
    /// The row-partitioned sibling of <see cref="ConsumerTenantOwnedStore"/>: no <see cref="ITenantContext"/>
    /// constructor parameter, and an explicit <see cref="ITenantPartitionedStore"/> declaration (Excalibur_Dispatch-uvyccs:
    /// the absence of an ambient-context constructor is not, by itself, evidence of row-partitioning — a
    /// tenancy-blind store has the identical constructor shape — so the seam requires this store to state
    /// the mechanism itself). A separate type is needed here because the probe derives the marker from
    /// <c>TStore</c>'s own shape, not from which registration call the test happens to write — reusing
    /// <see cref="ConsumerTenantOwnedStore"/>'s <see cref="ITenantContext"/>-taking constructor for both
    /// tests would derive the scoped marker for both, which is exactly the ambiguity this fixture exists
    /// to keep apart.
    /// </summary>
    private sealed class ConsumerTenantOwnedPartitionedStore : IConsumerTenantOwnedStore, ITenantPartitionedStore
    {
        internal const string Row = "row";

        public Task<string?> ReadAsync() => Task.FromResult<string?>(Row);
    }

    private sealed class FixedTenantContext(string? tenantId) : ITenantContext
    {
        public string? TenantId { get; } = tenantId;

        public bool HasTenant => !string.IsNullOrEmpty(TenantId);
    }
}
