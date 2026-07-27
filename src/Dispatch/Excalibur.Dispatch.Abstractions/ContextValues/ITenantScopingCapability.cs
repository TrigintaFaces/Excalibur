// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch;

/// <summary>
/// Registration-time capability marker declaring that a persistence store contract
/// (<typeparamref name="TContract"/>) has been wired by a provider that honors the ambient
/// tenant discriminator on every operation.
/// </summary>
/// <typeparam name="TContract">
/// The store contract the capability applies to (for example, an event store, saga store, or the
/// projection-store family). For the generic projection-store family the type argument is a family
/// discriminator (a single closed marker such as the projection-store contract closed over
/// <see cref="object"/>), not the shape of any individual store.
/// </typeparam>
/// <remarks>
/// <para>
/// This marker exists so that row-discriminator multi-tenancy can fail closed at registration when a
/// tenant-<em>unaware</em> provider store is present. The tenant-scoping decorators are applied through
/// factory registrations and telemetry wrappers, so the wrapped store's implementation type cannot be
/// inspected reliably; the presence of this marker in the service collection is the authoritative,
/// registration-time signal that the wrapped store actually enforces the tenant predicate.
/// </para>
/// <para>
/// Providers that thread the ambient tenant through their store implementation register this marker
/// through the dep-gated seams on <c>TenantScopedStoreServiceCollectionExtensions</c>
/// (<c>AddTenantScopedStore</c> / <c>AddTenantScopedProjectionStore</c>), co-located with the store
/// registration. A provider that does not honor the tenant discriminator simply does not register it,
/// causing row-discriminator wiring to reject the configuration rather than silently returning
/// cross-tenant data.
/// </para>
/// <para>
/// <b>Structural lock.</b> The <see cref="AssertWiredThroughDepGatedSeam"/> member is
/// <see langword="internal"/>, so this interface can only be implemented from within
/// <c>Excalibur.Dispatch.Abstractions</c> (and its <c>InternalsVisibleTo</c> friends). Its sole
/// implementation is the internal <c>TenantScopingCapabilityMarker&lt;TContract&gt;</c>, emitted only by
/// the dep-gated seams above. A provider assembly outside the friend set therefore <em>cannot</em>
/// register a cloned bare marker beside a store built without <see cref="ITenantContext"/> — the clone
/// fails to compile. This makes the "marker without the tenant dependency it attests" leak
/// (a truthful-looking capability on a cross-tenant-leaking store) structurally inexpressible for those
/// providers, rather than merely discouraged.
/// </para>
/// </remarks>
public interface ITenantScopingCapability<TContract>
{
    /// <summary>
    /// Structural-lock member: exists only to make this interface implementable exclusively from within
    /// <c>Excalibur.Dispatch.Abstractions</c>. The single implementation
    /// (<c>TenantScopingCapabilityMarker&lt;TContract&gt;</c>) provides a no-op body; the type-level
    /// unimplementability outside the assembly is the mechanism, not any behaviour of this method. It is
    /// never invoked — the capability is consumed purely as a registration-time presence signal.
    /// </summary>
    internal void AssertWiredThroughDepGatedSeam();
}
