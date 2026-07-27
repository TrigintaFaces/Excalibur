// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration helper that emits a persistence store <em>and</em> its
/// <see cref="ITenantScopingCapability{TContract}"/> capability marker as a single, dep-gated act.
/// </summary>
/// <remarks>
/// <para>
/// The capability marker attests that the store honors the ambient tenant discriminator on every
/// operation. Row-discriminator multi-tenancy (<c>RequireTenantScopingCapability&lt;TContract&gt;</c>)
/// fails closed at registration when the marker is absent. The integrity risk this type removes is a
/// marker registered <em>without</em> the tenant dependency the store needs to honor it: a store built
/// WITHOUT the ambient <see cref="ITenantContext"/>, yet carrying a truthful-looking capability marker,
/// would pass the gate and then leak cross-tenant rows at runtime.
/// </para>
/// <para>
/// <see cref="AddTenantScopedStore{TContract, TStore}"/> is the ONLY sanctioned path that emits
/// <see cref="ITenantScopingCapability{TContract}"/>: every provider deletes its standalone
/// <c>TryAddSingleton&lt;ITenantScopingCapability&lt;TContract&gt;&gt;</c> and registers its store through
/// this method instead. The seam resolves the ambient <see cref="ITenantContext"/> itself and <em>hands
/// it to the factory</em>, so a store registered through this seam is structurally incapable of being
/// built without the tenant context — <see cref="ServiceProviderServiceExtensions.GetRequiredService{T}"/>
/// fails closed when no <see cref="ITenantContext"/> is registered. A truthful-looking marker therefore
/// cannot co-exist with a store the factory built without the context (dep-gated, not merely co-located).
/// </para>
/// <para>
/// The residual gap C# cannot close — a factory that <em>receives</em> the context and ignores it — is
/// left to the behavioural regression lock (a tenant-B scoped read must not see a tenant-A row). This seam
/// removes the "never handed the dependency at all" path, which is the structural half of the leak.
/// </para>
/// </remarks>
public static class TenantScopedStoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the tenant-honoring store <typeparamref name="TStore"/> built by
    /// <paramref name="storeFactory"/> — which is <em>handed</em> the resolved ambient
    /// <see cref="ITenantContext"/> — and, inseparably, the
    /// <see cref="ITenantScopingCapability{TContract}"/> marker attesting it honors the ambient tenant.
    /// <typeparamref name="TContract"/> is the contract the multi-tenancy gate inspects (for example
    /// <c>IOutboxStore</c>, <c>IInboxStore</c>, <c>ISagaStore</c>, or <c>IEventStore</c>); the provider
    /// keeps its own keyed/admin registrations that resolve <typeparamref name="TStore"/>, so this call
    /// does not change how the contract is selected.
    /// </summary>
    /// <typeparam name="TContract">The store contract the capability applies to.</typeparam>
    /// <typeparam name="TStore">The concrete store implementation registered by the factory.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="storeFactory">
    /// Factory that builds the tenant-honoring store from the service provider and the resolved ambient
    /// <see cref="ITenantContext"/>. Because the context is a required factory argument resolved via
    /// <see cref="ServiceProviderServiceExtensions.GetRequiredService{T}"/>, a store built without it is
    /// inexpressible through this seam and its registration fails closed when no
    /// <see cref="ITenantContext"/> is registered.
    /// </param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/> or <paramref name="storeFactory"/> is <see langword="null"/>.
    /// </exception>
    public static IServiceCollection AddTenantScopedStore<TContract, TStore>(
        this IServiceCollection services,
        Func<IServiceProvider, ITenantContext, TStore> storeFactory)
        where TContract : class
        where TStore : class, TContract
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(storeFactory);

        // Dep-gated: the tenant context is resolved HERE (fail-closed) and threaded into construction, so
        // the store cannot be built without it. The marker is emitted only alongside this registration.
        services.TryAddSingleton<TStore>(sp => storeFactory(sp, sp.GetRequiredService<ITenantContext>()));
        AddTenantScopingCapability<TContract>(services);

        return services;
    }

    /// <summary>
    /// Registers a <em>scoped</em>, tenant-honoring store as its service type <typeparamref name="TService"/>
    /// — built by <paramref name="storeFactory"/>, which is <em>handed</em> the resolved ambient
    /// <see cref="ITenantContext"/> — and, inseparably, the <see cref="ITenantScopingCapability{TContract}"/>
    /// marker for the capability family <typeparamref name="TCapabilityFamily"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the scoped, family-token counterpart of <see cref="AddTenantScopedStore{TContract, TStore}"/>.
    /// It exists for store families whose gate contract is a single family discriminator distinct from the
    /// per-instance service type — the projection-store family being the canonical case: many closed-generic
    /// <c>IProjectionStore&lt;TProjection&gt;</c> services are registered at <see cref="ServiceLifetime.Scoped"/>,
    /// while the multi-tenancy gate requires one family marker
    /// (<c>ITenantScopingCapability&lt;IProjectionStore&lt;object&gt;&gt;</c>). <typeparamref name="TService"/>
    /// is the scoped service registered; <typeparamref name="TCapabilityFamily"/> is the family the marker
    /// attests. Neither is named here (both are supplied by the caller), so this abstraction takes no
    /// dependency on any concrete store contract.
    /// </para>
    /// <para>
    /// Dep-gated identically to <see cref="AddTenantScopedStore{TContract, TStore}"/>: the ambient
    /// <see cref="ITenantContext"/> is resolved HERE via
    /// <see cref="ServiceProviderServiceExtensions.GetRequiredService{T}"/> (fail-closed) and threaded into
    /// construction, so the store cannot be built without it, and the family marker is emitted only alongside
    /// this registration. A truthful-looking family marker therefore cannot co-exist with a store the factory
    /// built without the context.
    /// </para>
    /// </remarks>
    /// <typeparam name="TService">The scoped service type registered (for example <c>IProjectionStore&lt;TProjection&gt;</c>).</typeparam>
    /// <typeparam name="TCapabilityFamily">
    /// The capability family the emitted marker attests (for example <c>IProjectionStore&lt;object&gt;</c>), which
    /// the multi-tenancy gate inspects for the whole family.
    /// </typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="storeFactory">
    /// Factory that builds the tenant-honoring store from the service provider and the resolved ambient
    /// <see cref="ITenantContext"/>. Because the context is a required factory argument, a store built without
    /// it is inexpressible through this seam and its registration fails closed when no
    /// <see cref="ITenantContext"/> is registered.
    /// </param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/> or <paramref name="storeFactory"/> is <see langword="null"/>.
    /// </exception>
    public static IServiceCollection AddTenantScopedProjectionStore<TService, TCapabilityFamily>(
        this IServiceCollection services,
        Func<IServiceProvider, ITenantContext, TService> storeFactory)
        where TService : class
        where TCapabilityFamily : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(storeFactory);

        // Dep-gated: the tenant context is resolved HERE (fail-closed) and threaded into construction, so the
        // store cannot be built without it — this closes the projection DOA (a store built with a null context
        // throws on every operation). The family marker is emitted only alongside this registration.
        services.TryAddScoped<TService>(sp => storeFactory(sp, sp.GetRequiredService<ITenantContext>()));
        AddTenantScopingCapability<TCapabilityFamily>(services);

        return services;
    }

    /// <summary>
    /// Emits the <see cref="ITenantScopingCapability{TContract}"/> marker using the sole canonical
    /// implementation. Kept internal so the marker is emittable ONLY from within this class — via the
    /// dep-gated <see cref="AddTenantScopedStore{TContract, TStore}"/> and
    /// <see cref="AddTenantScopedProjectionStore{TService, TCapabilityFamily}"/> seams, or from an
    /// <c>InternalsVisibleTo</c> friend co-locating the emission with the tenant wiring it attests (the
    /// event-store erasure capability). No provider outside the friend set can register a bare marker.
    /// </summary>
    internal static void AddTenantScopingCapability<TContract>(IServiceCollection services)
        where TContract : class
    {
        services.TryAddSingleton<ITenantScopingCapability<TContract>>(
            static _ => new TenantScopingCapabilityMarker<TContract>());
    }
}

/// <summary>
/// Shared registration-time implementation of <see cref="ITenantScopingCapability{TContract}"/>. Emitted
/// only via <see cref="TenantScopedStoreServiceCollectionExtensions"/>, co-located with the dep-gated store
/// wiring so the marker cannot exist independently of the factory that builds the store it attests.
/// </summary>
/// <typeparam name="TContract">The store contract the capability applies to.</typeparam>
internal sealed class TenantScopingCapabilityMarker<TContract> : ITenantScopingCapability<TContract>
    where TContract : class
{
    /// <inheritdoc/>
    void ITenantScopingCapability<TContract>.AssertWiredThroughDepGatedSeam()
    {
        // No-op. The structural lock is the TYPE-level unimplementability of the internal member outside
        // this assembly; this body exists only to satisfy the contract. The capability is consumed as a
        // registration-time presence signal and this method is never invoked.
    }
}
