// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch;

/// <summary>
/// Registration-time capability marker declaring that a persistence store contract
/// (<typeparamref name="TContract"/>) keeps tenant rows separated by <em>carrying the tenant
/// discriminator on the row</em> rather than by reading an ambient tenant.
/// </summary>
/// <typeparam name="TContract">
/// The store contract the capability applies to (the outbox store being the canonical case).
/// </typeparam>
/// <remarks>
/// <para>
/// <b>What this attests, and how it differs from <see cref="ITenantScopingCapability{TContract}"/>.</b>
/// The scoping capability attests that the store applies the <em>ambient</em> tenant discriminator to every
/// operation. This one attests a different — and, for some contracts, the only correct — mechanism: the
/// store persists the tenant discriminator on every row it writes and hands that value back on read, so the
/// owning tenant is re-established <em>from the row</em>. Its reads are deliberately estate-wide.
/// </para>
/// <para>
/// The outbox is why this exists. One drain pass carries every tenant's messages, and the processor
/// establishes a per-message tenant scope from the discriminator the row carries. A store that instead
/// filtered on the ambient tenant would read it as absent at drain time, claim the empty set, and stall the
/// drain permanently — while still satisfying any test that only asserts one tenant cannot see another's
/// rows. Attesting ambient scoping for such a store is therefore not merely inaccurate; it describes
/// behaviour that would be a defect if it were true.
/// </para>
/// <para>
/// <b>In the framework's tenancy vocabulary.</b> Every tenancy-bearing operation is exactly one of three
/// kinds, and the kind is a property of the operation rather than a provider's choice. A
/// <em>tenant-confined</em> operation takes its partition as an explicit argument and fails closed without
/// one. A <em>deliberately estate-wide</em> operation takes nothing and applies no tenant term, and states
/// its reason where it is declared. An <em>identity-addressed</em> operation is reached by an identifier the
/// caller already holds, and adding a tenant term to it can only turn the correct row into no row. A store
/// presenting this capability is built from the second and third kinds: it stamps the partition it is given
/// on write, and re-establishes it from the row on read. <b>It never infers a tenant from ambient state</b>
/// — which is why the seam that emits this marker offers no ambient context to infer one from.
/// </para>
/// <para>
/// <b>Structural lock.</b> <see cref="AssertWiredThroughPartitionedSeam"/> is <see langword="internal"/>, so
/// this interface can only be implemented from within <c>Excalibur.Dispatch.Abstractions</c> (and its
/// <c>InternalsVisibleTo</c> friends). Its sole implementation is the internal
/// <c>TenantPartitionedCapabilityMarker&lt;TContract&gt;</c>, emitted only by
/// <c>TenantScopedStoreServiceCollectionExtensions.AddTenantAwareStore</c>. A provider assembly outside
/// the friend set cannot register a cloned bare marker beside a store that carries no discriminator — the
/// clone fails to compile.
/// </para>
/// <para>
/// <b>What earns this marker, stated precisely.</b> The absence of an
/// <see cref="ITenantContext"/> constructor parameter is NOT, by itself, sufficient — that absence is
/// equally consistent with a store implementing no tenancy mechanism at all, and the two are
/// indistinguishable by constructor shape alone. The seam emits this marker only when the store ALSO
/// implements <see cref="ITenantPartitionedStore"/>: the store's own explicit declaration of the mechanism, made
/// by the provider that wrote it, not inferred from what its constructor happens to omit. A store with
/// neither an <see cref="ITenantContext"/> parameter nor an <see cref="ITenantPartitionedStore"/>
/// implementation receives no capability marker at all.
/// </para>
/// <para>
/// <b>What the lock does NOT prove, stated rather than implied.</b> The seam emitting this marker takes no
/// <see cref="ITenantContext"/>, so the "handed a dependency and silently discarded it" leak is
/// inexpressible on this path. It does not, and at registration time cannot, prove that the store's writes
/// actually populate the discriminator column: that is behaviour observable only against real
/// infrastructure, and it is the conformance suite's round-trip — write under one tenant, drain, and read
/// the owning tenant back off the message — that holds it, not this marker.
/// </para>
/// </remarks>
public interface ITenantPartitionedCapability<TContract>
{
    /// <summary>
    /// Structural-lock member: exists only to make this interface implementable exclusively from within
    /// <c>Excalibur.Dispatch.Abstractions</c>. The single implementation
    /// (<c>TenantPartitionedCapabilityMarker&lt;TContract&gt;</c>) provides a no-op body; the type-level
    /// unimplementability outside the assembly is the mechanism, not any behaviour of this method. It is
    /// never invoked — the capability is consumed purely as a registration-time presence signal.
    /// </summary>
    internal void AssertWiredThroughPartitionedSeam();
}
