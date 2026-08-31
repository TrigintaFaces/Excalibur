// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.MultiTenancy.Tests;

/// <summary>
/// Excalibur_Dispatch-uvyccs — author-independent lock on the fix for an affirmative-by-default
/// partitioned attestation: <c>AddTenantAwareStore</c> must not infer
/// <see cref="ITenantPartitionedCapability{TContract}"/> from the mere absence of an
/// <see cref="ITenantContext"/> constructor parameter, because that absence is equally consistent with a
/// store that implements no tenancy mechanism at all.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect, precisely.</b> A store with no <see cref="ITenantContext"/> constructor parameter used
/// to be treated as row-partitioned by default. That is an affirmative claim —
/// <see cref="ITenantPartitionedCapability{TContract}"/> attests the store persists the tenant
/// discriminator on every row and re-establishes the owning tenant from it on read — and "does not read
/// an ambient tenant" is not evidence for it. A tenancy-blind store (no mechanism at all) has the
/// IDENTICAL constructor shape as a genuinely row-partitioned one, so the seam could not tell them apart
/// and, after the one-verb collapse, started emitting a truthful-looking marker for both.
/// </para>
/// <para>
/// <b>The fix.</b> The negative case now requires an explicit, structural opt-in:
/// <see cref="ITenantPartitionedStore"/>, a marker interface the store implements to declare the
/// mechanism itself. A store with no <see cref="ITenantContext"/> constructor parameter AND no
/// <see cref="ITenantPartitionedStore"/> implementation is registered with NEITHER capability marker —
/// the same outcome it would have had before either mechanism existed, and the multi-tenancy gate fails
/// closed on it exactly as it does for any other unattested provider.
/// </para>
/// <para>
/// <b>Both arms, deliberately.</b> The safety arm (a tenancy-blind store gets no marker) is satisfied by
/// a seam that never emits the partitioned marker at all. The liveness arm (a store that legitimately
/// declares the mechanism still gets it) is satisfied by a seam that ignores the declaration. Only
/// together do they constrain the real invariant.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class TenantPartitionedMarkerRequiresDeclarationShould
{
    /// <summary>
    /// SAFETY ARM. A store with no <see cref="ITenantContext"/> constructor parameter and no
    /// <see cref="ITenantPartitionedStore"/> declaration must NOT receive
    /// <see cref="ITenantPartitionedCapability{TContract}"/> — the exact shape of the uvyccs regression:
    /// same constructor shape as a genuinely partitioned store, but no evidenced mechanism.
    /// </summary>
    [Fact]
    public void ATenancyBlindStore_ReceivesNeitherCapabilityMarker()
    {
        var services = new ServiceCollection();

        services.AddTenantAwareStore<ITenancyBlindStore, TenancyBlindStore>(
            static _ => new TenancyBlindStore());

        using var provider = services.BuildServiceProvider();

        provider.GetService<ITenantScopingCapability<ITenancyBlindStore>>().ShouldBeNull(
            "A tenancy-blind store (no ITenantContext constructor parameter, no ITenantPartitionedStore "
            + "declaration) must not carry ITenantScopingCapability — it does not read an ambient tenant.");

        provider.GetService<ITenantPartitionedCapability<ITenancyBlindStore>>().ShouldBeNull(
            "A tenancy-blind store must not carry ITenantPartitionedCapability either. The absence of an "
            + "ITenantContext constructor parameter is evidence of exactly one fact — no ambient read — "
            + "not evidence that the store carries the tenant on the row instead. This is the uvyccs "
            + "regression: before the one-verb collapse this store got no marker and the gate refused it; "
            + "inferring 'partitioned' from the omission would readmit it under a false attestation.");
    }

    /// <summary>
    /// LIVENESS ARM. A store that explicitly implements <see cref="ITenantPartitionedStore"/> still
    /// receives <see cref="ITenantPartitionedCapability{TContract}"/> — the fix closes the false-default,
    /// it does not disable the legitimate mechanism.
    /// </summary>
    [Fact]
    public void AStoreThatDeclaresItself_StillReceivesThePartitionedMarker()
    {
        var services = new ServiceCollection();

        services.AddTenantAwareStore<ITenancyBlindStore, DeclaredPartitionedStore>(
            static _ => new DeclaredPartitionedStore());

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<ITenantPartitionedCapability<ITenancyBlindStore>>().ShouldNotBeNull(
            "A store that implements ITenantPartitionedStore must still receive "
            + "ITenantPartitionedCapability — the fix must not turn the explicit declaration into "
            + "another form of inert default.");

        provider.GetService<ITenantScopingCapability<ITenancyBlindStore>>().ShouldBeNull(
            "The declared-partitioned store must not ALSO carry the scoping marker — the two mechanisms "
            + "stay mutually exclusive.");
    }

    /// <summary>
    /// A store contract used only by this lock, standing in for both the tenancy-blind and the
    /// declared-partitioned fixtures below.
    /// </summary>
    private interface ITenancyBlindStore
    {
    }

    /// <summary>
    /// No <see cref="ITenantContext"/> constructor parameter, no <see cref="ITenantPartitionedStore"/> —
    /// the exact shape a genuinely tenant-blind provider (or the uvyccs regression's false-positive) has.
    /// </summary>
    private sealed class TenancyBlindStore : ITenancyBlindStore
    {
    }

    /// <summary>
    /// Same constructor shape as <see cref="TenancyBlindStore"/> — no <see cref="ITenantContext"/>
    /// parameter — but explicitly declares <see cref="ITenantPartitionedStore"/>.
    /// </summary>
    private sealed class DeclaredPartitionedStore : ITenancyBlindStore, ITenantPartitionedStore
    {
    }
}
