// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance;

namespace Excalibur.MultiTenancy.Tests;

/// <summary>
/// Locks the tenant-scoping gate on <see cref="IComplianceStore"/> -- consent records and subject-access
/// request tracking -- in both directions: an unattested or wrongly-attested provider is refused, and a
/// correctly wired one still starts and resolves.
/// </summary>
/// <remarks>
/// <para>
/// <b>What was actually missing, stated precisely.</b> This contract carries <c>[TenantOwned]</c>, so the
/// attribute sweep already refused a provider presenting NO capability at all -- that refusal is held by
/// the coverage-oracle lock and was never absent. What was missing is the contract's own gate block: the
/// sweep is a floor that accepts <em>either</em> capability, and for this contract only one of the two can
/// be true. Both shipped providers take the ambient <c>ITenantContext</c> and bind its term on every
/// tenant-facing statement, so a provider presenting the row-partitioned marker would satisfy the sweep
/// while attesting the opposite mechanism -- that it re-establishes the tenant from the row and never
/// infers it from ambient state. The row-partitioned arm below is what proves the specific gate does
/// something the sweep does not: it passes the floor and must still be refused.
/// </para>
/// <para>
/// <b>Why the marker cannot be a separate registration.</b> The capability is emitted only by
/// <c>AddTenantAwareStore</c>, which derives the mechanism from the store's own constructor and emits the
/// marker in the same act as the wiring. A store that was never handed a tenant context therefore cannot
/// carry a truthful-looking attestation: the plain-registration arm shows a bare registration produces no
/// marker, and the row-partitioned arm shows a hand-registered marker of the wrong kind buys no admission.
/// A lying marker is not discouraged here, it is unreachable through the seam.
/// </para>
/// <para>
/// <b>Both halves, deliberately.</b> A suite of refusals is structurally incapable of noticing a gate that
/// rejects correct hosts, which is the failure this contract's siblings shipped. The liveness arms build a
/// real <see cref="ServiceProvider"/> through the same seam a provider package uses and resolve the store
/// from it, rather than asserting a descriptor: a registration that emits the marker and no longer
/// produces a usable store satisfies a descriptor scan and still fails the consumer at startup.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class ComplianceStoreTenantAttestationShould
{
    // ---- SAFETY --------------------------------------------------------------------------------------

    [Fact]
    public void RefuseAComplianceStoreThatPresentsNoCapability()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton(A.Fake<IComplianceStore>());

        var thrown = Should.Throw<InvalidOperationException>(
            () => services.AddMultiTenancy(static o => o.Strategy = TenantIsolationStrategy.RowDiscriminator));

        thrown.Message.ShouldContain(
            nameof(IComplianceStore),
            customMessage: "The refusal must name the contract. A consumer holding a hundred registrations "
            + "cannot act on a message saying only that some store is unscoped.");

        thrown.Message.ShouldContain(
            "not tenant-scoping-capable",
            customMessage: "This must be the CONTRACT'S OWN gate firing, not the generic attribute sweep. "
            + "The sweep's message describes either capability; these providers read the ambient tenant, "
            + "and a message that left the mechanism open would send a consumer to build a row-partitioned "
            + "compliance store the framework does not want.");
    }

    [Fact]
    public void RefuseAComplianceStoreThatAttestsRowPartitioningInstead()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton(A.Fake<IComplianceStore>());

        // This registration PASSES the attribute sweep, which accepts either capability. That is the whole
        // reason the explicit gate block earns its lines: delete the block and the sweep admits this host,
        // with a store attesting that it never infers a tenant from ambient state while both shipped
        // providers do exactly that. The attestation would be false and nothing downstream could tell.
        _ = services.AddSingleton(A.Fake<ITenantPartitionedCapability<IComplianceStore>>());

        var thrown = Should.Throw<InvalidOperationException>(
            () => services.AddMultiTenancy(static o => o.Strategy = TenantIsolationStrategy.RowDiscriminator),
            "A compliance store attesting row-partitioned tenancy claims a mechanism neither shipped "
            + "provider implements. Accepting it would let a store satisfy the gate by describing itself "
            + "wrongly, which is the lying-marker failure in its quietest form.");

        thrown.Message.ShouldContain(nameof(IComplianceStore));
    }

    [Fact]
    public void EmitNoCapabilityForAPlainlyRegisteredComplianceStore()
    {
        // The structural half: the marker is an OUTPUT of the seam, so a bare registration cannot produce
        // one. If this ever finds a marker, some path is registering an attestation independently of the
        // wiring it attests, and every arm above becomes satisfiable by a store that isolates nothing.
        var services = new ServiceCollection();
        _ = services.AddSingleton(A.Fake<IComplianceStore>());

        services.ShouldNotContain(
            descriptor => descriptor.ServiceType == typeof(ITenantScopingCapability<IComplianceStore>),
            "A plain registration must emit no attestation. A marker reachable without the wiring is the "
            + "lying-marker defect this seam exists to make inexpressible.");
    }

    // ---- LIVENESS ------------------------------------------------------------------------------------

    [Fact]
    public void AdmitAComplianceStoreWiredThroughTheTenantAwareSeam()
    {
        var services = BuildSeamRegisteredHost();

        // Reaching past this line is the assertion. A gate that refused here would leave a consumer no way
        // to run compliance storage under row-discriminator multi-tenancy at all.
        Should.NotThrow(
            () => services.AddMultiTenancy(static o => o.Strategy = TenantIsolationStrategy.RowDiscriminator),
            "RowDiscriminator must ADMIT a store registered through the tenant-aware seam. Rejecting it is "
            + "the gate refusing a correct host, and no refusal arm in this file could see that.");
    }

    [Fact]
    public void AttestTenantScopingFromTheSeamThatWiredIt()
    {
        using var provider = BuildSeamRegisteredHost().BuildServiceProvider();

        _ = provider.GetRequiredService<ITenantScopingCapability<IComplianceStore>>().ShouldNotBeNull(
            "The seam derives the mechanism from the store's constructor and emits the capability in the "
            + "same act as the wiring, so the attestation cannot outlive the registration it describes.");
    }

    [Fact]
    public void ResolveTheContractAndTheConcreteStoreToOneInstance()
    {
        var services = BuildSeamRegisteredHost();
        _ = services.AddMultiTenancy(static o => o.Strategy = TenantIsolationStrategy.RowDiscriminator);

        using var provider = services.BuildServiceProvider();

        // Resolved through the real container rather than read off a descriptor: a registration that
        // cannot be constructed satisfies a scan and still fails the consumer at startup.
        var byContract = provider.GetRequiredService<IComplianceStore>();
        var byConcreteType = provider.GetRequiredService<AmbientScopedComplianceStore>();

        byContract.ShouldBeSameAs(
            byConcreteType,
            "The seam registers the CONCRETE type, because that is the instance the marker is bound to. If "
            + "the contract forwarded to a different construction, the attestation would describe an "
            + "object the application never uses.");

        _ = byContract.ShouldBeOfType<AmbientScopedComplianceStore>(
            "The store must resolve undecorated. This contract is gated and deliberately not wrapped: each "
            + "provider binds the ambient tenant inside its own store, so a decorator would add a second "
            + "filter over one that already filters without repairing the first.");
    }

    /// <summary>
    /// Registers a compliance store the way a provider package does: through the real seam, which requires
    /// a resolvable <see cref="ITenantContext"/> and emits the capability as part of the same act.
    /// </summary>
    private static ServiceCollection BuildSeamRegisteredHost()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<ITenantContext>(new TestDoubles.TestTenantContext("tenant-a"));
        _ = services.AddTenantAwareStore<IComplianceStore, AmbientScopedComplianceStore>();

        // The seam registers the CONCRETE store, so the marker binds to a real instance; the contract is
        // forwarded onto that same singleton. Both shipped provider packages do exactly this, and without
        // the forward the contract does not resolve at all -- so omitting it here would test a registration
        // shape no consumer ever gets.
        Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions
            .TryAddSingleton<IComplianceStore>(
                services,
                static sp => sp.GetRequiredService<AmbientScopedComplianceStore>());
        return services;
    }

    /// <summary>
    /// Mirrors the shipped providers' shape: a REQUIRED <see cref="ITenantContext"/> constructor parameter,
    /// which is what the seam's probe reads. An optional one would let the store be built having been
    /// handed nothing and still be registered through the seam -- the marker would then attest a scoping
    /// that is not happening, which is worse than an absent marker.
    /// </summary>
    private sealed class AmbientScopedComplianceStore(ITenantContext tenantContext) : IComplianceStore
    {
        private readonly ITenantContext _tenantContext =
            tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));

        public Task StoreConsentAsync(ConsentRecord record, CancellationToken cancellationToken)
        {
            _ = _tenantContext;
            return Task.CompletedTask;
        }

        public Task<ConsentRecord?> GetConsentAsync(
            string subjectId,
            string purpose,
            CancellationToken cancellationToken) => Task.FromResult<ConsentRecord?>(null);

        public Task StoreErasureLogAsync(
            string subjectId,
            string details,
            DateTimeOffset erasedAt,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StoreSubjectAccessRequestAsync(
            SubjectAccessResult result,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
