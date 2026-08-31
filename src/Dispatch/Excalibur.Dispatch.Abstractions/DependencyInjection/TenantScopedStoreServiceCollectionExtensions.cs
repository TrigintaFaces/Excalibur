// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Excalibur.Dispatch;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration helper that emits a persistence store <em>and</em> the correct tenancy capability
/// marker (<see cref="ITenantScopingCapability{TContract}"/> or
/// <see cref="ITenantPartitionedCapability{TContract}"/>) as a single, dep-gated act.
/// </summary>
/// <remarks>
/// <para>
/// A store's tenancy mechanism — ambient (reads <see cref="ITenantContext"/>) or row-partitioned (carries
/// the tenant on the row) — is a fact about <c>TStore</c> itself, not a decision the registration author
/// holds information to make. <see cref="AddTenantAwareStore{TContract, TStore}(IServiceCollection)"/>
/// and its factory overload are the ONLY sanctioned path that emits either capability marker: every
/// provider deletes its standalone <c>TryAddSingleton&lt;ITenantScopingCapability&lt;TContract&gt;&gt;</c>
/// or <c>TryAddSingleton&lt;ITenantPartitionedCapability&lt;TContract&gt;&gt;</c> and registers its store
/// through this seam instead. The seam derives which marker to emit from <c>TStore</c> itself — the
/// caller never names which one applies — but the two mechanisms are not symmetric in how they are
/// derived: the ambient case is <em>proven</em> from the constructor signature; the row-partitioned case
/// requires <c>TStore</c> to <em>declare</em> it via <see cref="ITenantPartitionedStore"/>, because "this
/// constructor does not read an ambient tenant" is not evidence that the store carries the tenant on the
/// row instead — it is equally consistent with the store implementing no tenancy mechanism at all.
/// </para>
/// <para>
/// The two underlying mechanisms remain exactly as separate as before this collapse: an ambient store's
/// registration resolves <see cref="ITenantContext"/> fail-closed
/// (<see cref="ServiceProviderServiceExtensions.GetRequiredService{T}"/>) before the store can be built, so
/// a truthful-looking <see cref="ITenantScopingCapability{TContract}"/> marker cannot co-exist with a store
/// built without the tenant context. A row-partitioned store's registration offers no
/// <see cref="ITenantContext"/> at all, so there is nothing for a factory to be handed and silently
/// discard — and its <see cref="ITenantPartitionedCapability{TContract}"/> marker is never emitted unless
/// the store itself claims the mechanism. What collapsed is only the caller's obligation to name the
/// mechanism by calling a different method — the mechanism itself is derived from <c>TStore</c>,
/// mechanically, every time.
/// </para>
/// <para>
/// The zero-argument overload goes further: it constructs <c>TStore</c> via
/// <see cref="ActivatorUtilities.CreateInstance{T}(IServiceProvider, object[])"/>, which binds
/// <see cref="ITenantContext"/> directly from the constructor signature when the store needs it. There is
/// no factory parameter for a caller to bind to a discard on that path at all — "handed the context,
/// discards it anyway" is inexpressible, not merely discouraged. Registration sites that need a
/// non-default connection or a keyed dependency use the factory overload instead, which preserves the
/// same fail-closed resolution ahead of the factory call, independent of what the factory body does with
/// it.
/// </para>
/// </remarks>
public static class TenantScopedStoreServiceCollectionExtensions
{
	/// <summary>
	/// Registers the tenant-aware store <typeparamref name="TStore"/>, constructed via
	/// <see cref="ActivatorUtilities.CreateInstance{T}(IServiceProvider, object[])"/>, and, inseparably,
	/// the capability marker matching the mechanism <typeparamref name="TStore"/>'s constructor actually
	/// requires.
	/// </summary>
	/// <remarks>
	/// Use this overload when <typeparamref name="TStore"/> can be built entirely from services already
	/// registered in the container. When a registration site needs a non-default connection, a keyed
	/// dependency, or any value not resolvable from <see cref="IServiceProvider"/> alone, use the
	/// <see cref="AddTenantAwareStore{TContract, TStore}(IServiceCollection, Func{IServiceProvider, TStore})"/>
	/// overload instead.
	/// </remarks>
	/// <typeparam name="TContract">The store contract the capability applies to.</typeparam>
	/// <typeparam name="TStore">The concrete store implementation to construct and register.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
	/// <exception cref="InvalidOperationException">
	/// <typeparamref name="TStore"/> has no public instance constructor; or it declares more than one public
	/// constructor accepting an <see cref="ITenantContext"/> without exactly one of them being marked
	/// <see cref="ActivatorUtilitiesConstructorAttribute"/>, which would leave construction dependent on
	/// which unrelated services the host happens to have registered. Both are raised here, while the
	/// registration is being built, rather than on the consumer's first resolve.
	/// </exception>
	public static IServiceCollection AddTenantAwareStore<TContract, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TStore>(
		this IServiceCollection services)
		where TContract : class
		where TStore : class, TContract
	{
		ArgumentNullException.ThrowIfNull(services);

		// Strict: this overload constructs TStore itself via ActivatorUtilities, which requires at least
		// one public constructor to exist at all — unlike the factory overload, "no constructor" is not a
		// graceful "no requirement" here, because there is nothing this overload could have built.
		var mechanism = DetermineTenantMechanism(typeof(TStore), requireConstructor: true);

		// When TStore reads the ambient tenant, the resolved context is handed to ActivatorUtilities as an
		// EXPLICIT argument rather than leaving it to pick a constructor unaided. ActivatorUtilities selects
		// a constructor able to consume every supplied argument, so "built through a constructor that omits
		// the tenant context, while this registration emits a scoped capability marker" is inexpressible on
		// this path — including for a store whose widest constructor is not the tenant-aware one.
		Func<IServiceProvider, TStore> storeFactory = mechanism == TenantMechanism.Scoped
			? static sp => ActivatorUtilities.CreateInstance<TStore>(sp, sp.GetRequiredService<ITenantContext>())
			: static sp => ActivatorUtilities.CreateInstance<TStore>(sp);

		return services.RegisterTenantAwareStore<TContract, TStore>(storeFactory, mechanism);
	}

	/// <summary>
	/// Registers the tenant-aware store <typeparamref name="TStore"/> built by
	/// <paramref name="storeFactory"/> and, inseparably, the capability marker matching the mechanism
	/// <typeparamref name="TStore"/>'s constructor actually requires.
	/// </summary>
	/// <remarks>
	/// <para>
	/// When <typeparamref name="TStore"/>'s public constructor(s) declare an <see cref="ITenantContext"/>
	/// parameter, this seam resolves it via <see cref="ServiceProviderServiceExtensions.GetRequiredService{T}"/>
	/// (fail-closed) <em>before</em> invoking <paramref name="storeFactory"/> and emits
	/// <see cref="ITenantScopingCapability{TContract}"/>. That resolution happens whether or not
	/// <paramref name="storeFactory"/> itself resolves the context again to construct the store — the
	/// registration cannot succeed with a missing tenant context regardless of what the factory body does.
	/// </para>
	/// <para>
	/// When <typeparamref name="TStore"/>'s constructor(s) declare no <see cref="ITenantContext"/>
	/// parameter, the seam does not infer row-partitioning from that absence — the absence of the ambient
	/// claim is not evidence for a different, equally affirmative claim. It emits
	/// <see cref="ITenantPartitionedCapability{TContract}"/> only when <typeparamref name="TStore"/> also
	/// implements <see cref="ITenantPartitionedStore"/>, the store's own explicit declaration that it
	/// carries the tenant discriminator on the row. A store that neither takes an
	/// <see cref="ITenantContext"/> constructor parameter nor implements
	/// <see cref="ITenantPartitionedStore"/> is registered with <em>no</em> tenancy capability marker —
	/// the same outcome as before this seam existed, and the multi-tenancy gate fails closed on it exactly
	/// as it does for any other unattested provider.
	/// </para>
	/// </remarks>
	/// <typeparam name="TContract">The store contract the capability applies to.</typeparam>
	/// <typeparam name="TStore">The concrete store implementation registered by the factory.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="storeFactory">Factory that builds the store from the service provider.</param>
	/// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="services"/> or <paramref name="storeFactory"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	/// <typeparamref name="TStore"/> has no public instance constructor and does not implement
	/// <see cref="ITenantPartitionedStore"/>, so its tenancy mechanism cannot be derived.
	/// </exception>
	public static IServiceCollection AddTenantAwareStore<TContract, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TStore>(
		this IServiceCollection services,
		Func<IServiceProvider, TStore> storeFactory)
		where TContract : class
		where TStore : class, TContract
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(storeFactory);

		// Structural, not caller-supplied: derived from TStore's own shape, once, at registration time —
		// never from which overload or delegate shape the caller happened to write. Lenient about
		// CONSTRUCTION, because the caller supplies it here; still strict about CLASSIFICATION, because
		// a store the factory builds through a non-public constructor gives the probe nothing to read,
		// and must declare its mechanism rather than be silently recorded as having none.
		var mechanism = DetermineTenantMechanism(typeof(TStore), requireConstructor: false);

		return services.RegisterTenantAwareStore<TContract, TStore>(storeFactory, mechanism);
	}

	/// <summary>
	/// Shared registration logic for both <see cref="AddTenantAwareStore{TContract, TStore}(IServiceCollection)"/>
	/// overloads, once <paramref name="mechanism"/> has been derived.
	/// </summary>
	private static IServiceCollection RegisterTenantAwareStore<TContract, TStore>(
		this IServiceCollection services,
		Func<IServiceProvider, TStore> storeFactory,
		TenantMechanism mechanism)
		where TContract : class
		where TStore : class, TContract
	{
		var requiresTenantContext = mechanism == TenantMechanism.Scoped;

		services.TryAddSingleton(sp =>
		{
			if (requiresTenantContext)
			{
				// Dep-gated exactly as before this collapse: resolved HERE (fail-closed) before the
				// factory runs, so a store this seam has determined needs the ambient tenant cannot be
				// constructed through this registration when none is available — independent of whether
				// storeFactory itself also resolves (or ignores) ITenantContext.
				_ = sp.GetRequiredService<ITenantContext>();
			}

			return storeFactory(sp);
		});

		switch (mechanism)
		{
			case TenantMechanism.Scoped:
				AddTenantScopingCapability<TContract>(services);
				break;
			case TenantMechanism.Partitioned:
				AddTenantPartitionedCapability<TContract>(services);
				break;
			case TenantMechanism.None:
				// No capability marker: TStore neither takes ITenantContext nor implements
				// ITenantPartitionedStore, so neither claim is warranted. The multi-tenancy gate fails
				// closed on this registration if the contract requires a marker — matching the outcome
				// for any provider that predates this seam.
				break;
			default:
				throw new UnreachableException($"Unhandled {nameof(TenantMechanism)} value: {mechanism}.");
		}

		return services;
	}

	/// <summary>
	/// The tenancy mechanism a store implements, as derived by <see cref="DetermineTenantMechanism"/>.
	/// </summary>
	private enum TenantMechanism
	{
		/// <summary>No tenancy capability marker is warranted: neither claim is evidenced.</summary>
		None,

		/// <summary>The store's constructor requires <see cref="ITenantContext"/> — ambient scoping.</summary>
		Scoped,

		/// <summary>The store declares <see cref="ITenantPartitionedStore"/> — row-carried tenancy.</summary>
		Partitioned,
	}

	/// <summary>
	/// Registers a <em>scoped</em>, tenant-honoring store <typeparamref name="TStore"/> as its service type
	/// <typeparamref name="TService"/> and, inseparably, the <see cref="ITenantScopingCapability{TContract}"/>
	/// marker for the capability family <typeparamref name="TCapabilityFamily"/>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is the scoped, family-token counterpart used by store families whose gate contract is a single
	/// family discriminator distinct from the per-instance service type — the projection-store family
	/// being the canonical case: many closed-generic <c>IProjectionStore&lt;TProjection&gt;</c> services
	/// are registered at <see cref="ServiceLifetime.Scoped"/>, while the multi-tenancy gate requires one
	/// family marker (<c>ITenantScopingCapability&lt;IProjectionStore&lt;object&gt;&gt;</c>).
	/// <typeparamref name="TService"/> is the scoped service registered; <typeparamref name="TCapabilityFamily"/>
	/// is the family the marker attests. Neither is named here (both are supplied by the caller), so this
	/// abstraction takes no dependency on any concrete store contract.
	/// </para>
	/// <para>
	/// <b>The marker is earned by <typeparamref name="TStore"/>'s constructor, not by what the caller
	/// wrote.</b> Until this seam took <typeparamref name="TStore"/>, its factory received the resolved
	/// <see cref="ITenantContext"/> as a second parameter — which a call site could bind to a discard and
	/// still emit a marker attesting a discipline the store never honoured. The mechanism is now read from
	/// <typeparamref name="TStore"/>'s own public constructors, exactly as
	/// <see cref="AddTenantAwareStore{TContract, TStore}(IServiceCollection, Func{IServiceProvider, TStore})"/>
	/// reads it: a store whose constructors declare no <see cref="ITenantContext"/> parameter cannot be
	/// registered through this verb at all, so the marker and the requirement it attests cannot come apart.
	/// The factory takes one parameter, so there is no second formal parameter for a call site to discard —
	/// the same structural property the store seam has, rather than a convention a source scan must police.
	/// </para>
	/// <para>
	/// Dep-gated: the ambient <see cref="ITenantContext"/> is resolved inside the registration via
	/// <see cref="ServiceProviderServiceExtensions.GetRequiredService{T}"/> (fail-closed) before
	/// <paramref name="storeFactory"/> runs, so the registration cannot succeed on a host with no tenant
	/// context regardless of what the factory body does.
	/// </para>
	/// </remarks>
	/// <typeparam name="TService">The scoped service type registered (for example <c>IProjectionStore&lt;TProjection&gt;</c>).</typeparam>
	/// <typeparam name="TStore">
	/// The concrete store the factory builds. Its public constructors are read to derive the tenancy
	/// mechanism; the type itself may stay internal.
	/// </typeparam>
	/// <typeparam name="TCapabilityFamily">
	/// The capability family the emitted marker attests (for example <c>IProjectionStore&lt;object&gt;</c>), which
	/// the multi-tenancy gate inspects for the whole family.
	/// </typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="storeFactory">Factory that builds the store from the service provider.</param>
	/// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="services"/> or <paramref name="storeFactory"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	/// <typeparamref name="TStore"/> does not read the ambient tenant — no public constructor accepts an
	/// <see cref="ITenantContext"/> — so this verb would emit a tenant-scoping marker for a store that
	/// cannot honour it; or <typeparamref name="TStore"/> has no public constructor at all, leaving its
	/// mechanism underivable rather than absent.
	/// </exception>
	public static IServiceCollection AddTenantScopedProjectionStore<TService, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TStore, TCapabilityFamily>(
		this IServiceCollection services,
		Func<IServiceProvider, TStore> storeFactory)
		where TService : class
		where TStore : class, TService
		where TCapabilityFamily : class
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(storeFactory);

		// Structural, read from TStore rather than asserted by the caller. Lenient about CONSTRUCTION (the
		// factory builds the store, so a non-public constructor is fine) and strict about CLASSIFICATION.
		var mechanism = DetermineTenantMechanism(typeof(TStore), requireConstructor: false);
		if (mechanism != TenantMechanism.Scoped)
		{
			throw new InvalidOperationException(
				$"'{typeof(TStore)}' does not read the ambient tenant — none of its public constructors " +
				$"accepts an {nameof(ITenantContext)} — so registering it through " +
				"AddTenantScopedProjectionStore would emit a tenant-scoping capability marker for a store " +
				"that cannot honour it. Give the store a public constructor accepting an " +
				$"{nameof(ITenantContext)}, or register it through a verb matching the mechanism it does " +
				"implement.");
		}

		services.TryAddScoped<TService>(sp =>
		{
			// Fail-closed before the factory runs, so a missing tenant context is reported as a missing
			// tenant context rather than surfacing later as a store that silently reads every tenant.
			_ = sp.GetRequiredService<ITenantContext>();

			return storeFactory(sp);
		});

		AddTenantScopingCapability<TCapabilityFamily>(services);

		return services;
	}

	/// <summary>
	/// Determines <paramref name="storeType"/>'s tenancy mechanism — the structural fact
	/// <see cref="AddTenantAwareStore{TContract, TStore}(IServiceCollection)"/> dispatches on, in place of
	/// a caller-named verb.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>The ambient-scoped case is derived, because it is provable.</b> <paramref name="storeType"/> is
	/// ambient-scoped when <em>any</em> of its public constructors declares an
	/// <see cref="ITenantContext"/>-assignable parameter. A store that reads the ambient tenant commonly
	/// also offers a convenience constructor that omits it and delegates to the tenant-aware one — the
	/// idiomatic .NET overload shape, and the shape every erasure and legal-hold store uses. Requiring
	/// unanimity across constructors would classify that ordinary shape as a contradiction and reject
	/// correct code, so the evidence is read as it stands: one constructor accepting the context is an
	/// affirmative statement that the store reads it, and a second constructor that omits it does not
	/// retract that statement.
	/// </para>
	/// <para>
	/// This relaxation cannot yield a store built <em>without</em> the context yet marked as scoped. On the
	/// factory overload the caller constructs the store, so the probe only classifies; the seam's
	/// fail-closed <see cref="ServiceProviderServiceExtensions.GetRequiredService{T}"/> for
	/// <see cref="ITenantContext"/> runs regardless. On the constructing overload the resolved context is
	/// passed to <see cref="ActivatorUtilities"/> as an explicit argument, which constrains selection to a
	/// constructor that accepts it.
	/// </para>
	/// <para>
	/// <b>The row-partitioned case is NOT derived from the absence of the ambient case — it requires an
	/// explicit declaration.</b> A constructor with no <see cref="ITenantContext"/> parameter is evidence
	/// of exactly one fact: the store does not read an ambient tenant. It is <em>not</em> evidence that
	/// the store carries the tenant on the row instead — that is a distinct, equally affirmative claim,
	/// and a store can just as easily implement no tenancy mechanism at all. Inferring row-partitioning
	/// from what a constructor omits would let a tenancy-blind store pass the multi-tenancy gate under an
	/// attestation it does not earn. So this method returns <see cref="TenantMechanism.Partitioned"/> only
	/// when <paramref name="storeType"/> also implements <see cref="ITenantPartitionedStore"/> — the
	/// store's own explicit statement of the mechanism it implements. Absent both signals, it returns
	/// <see cref="TenantMechanism.None"/>: the same outcome a store had before either mechanism existed.
	/// </para>
	/// <para>
	/// A type with NO public instance constructor — an interface or an abstract class, used as the store
	/// type only through the factory overload as a registration key for a hand-provided instance — has
	/// nothing to reflect on for the ambient-scoped case. <paramref name="requireConstructor"/> controls
	/// what that means: the zero-argument overload passes <see langword="true"/>, because
	/// <see cref="ActivatorUtilities"/> cannot construct such a type at all and the absence must fail
	/// loudly rather than emit a marker for a store that was never built. The factory overload passes
	/// <see langword="false"/>: the caller supplies construction entirely, so "no constructor to inspect"
	/// is not a CONSTRUCTION error. It remains a CLASSIFICATION error unless the type declares
	/// <see cref="ITenantPartitionedStore"/>, because a factory can build a store whose constructors are
	/// all non-public — the shape the internal-first standard makes ordinary — and the absence of a
	/// public constructor is then evidence of nothing at all. Reading it as
	/// <see cref="TenantMechanism.None"/> would mark a genuinely tenant-aware store as having no
	/// mechanism. Both overloads therefore refuse; only the message differs.
	/// </para>
	/// </remarks>
	private static TenantMechanism DetermineTenantMechanism(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type storeType,
		bool requireConstructor)
	{
		var constructors = storeType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);

		// The constructing overload cannot proceed at all without a public constructor, so it reports
		// that first and names construction as the reason. The factory overload CAN construct such a
		// type; its refusal, below, is about the underivable mechanism, not about construction.
		if (constructors.Length == 0 && requireConstructor)
		{
			throw new InvalidOperationException(
				$"'{storeType}' has no public instance constructor, so AddTenantAwareStore cannot determine " +
				"whether it requires an ITenantContext.");
		}

		// IsAssignableFrom, not exact-type: a constructor parameter typed as a more specific interface or
		// class that itself IS an ITenantContext must still be recognised as the ambient-scoped case, or a
		// store using such a type would silently fall through toward TenantMechanism.None instead of the
		// Scoped case its constructor actually documents.
		var tenantAwareConstructors = Array.FindAll(
			constructors,
			static constructor => Array.Exists(
				constructor.GetParameters(),
				static parameter => typeof(ITenantContext).IsAssignableFrom(parameter.ParameterType)));

		if (tenantAwareConstructors.Length != 0)
		{
			if (requireConstructor)
			{
				RequireUnambiguousConstruction(storeType, tenantAwareConstructors);
			}

			return TenantMechanism.Scoped;
		}

		if (typeof(ITenantPartitionedStore).IsAssignableFrom(storeType))
		{
			return TenantMechanism.Partitioned;
		}

		// No public instance constructor, and no explicit row-partitioned declaration. There is nothing
		// here to read EITHER WAY: the factory overload constructs the store itself, so it can build a
		// type whose constructors are all non-public - the shape this project's internal-first standard
		// makes the expected one. Absence of a public constructor is therefore not evidence that the
		// store reads no ambient tenant, exactly as the absence of an ambient constructor is not
		// evidence that it partitions by row. Returning None here would silently classify a genuinely
		// tenant-aware store as having no mechanism, and carry that misclassification into the
		// capability marker the multi-tenancy floor reads. Refuse loudly, and ask the provider to state
		// its mechanism instead.
		if (constructors.Length == 0)
		{
			throw new InvalidOperationException(
				$"'{storeType}' has no public instance constructor, so AddTenantAwareStore cannot derive " +
				"which tenancy mechanism it implements, and will not guess. Give the store a public " +
				$"constructor accepting an {nameof(ITenantContext)} if it reads the ambient tenant, or " +
				$"implement {nameof(ITenantPartitionedStore)} if it carries the tenant on the row. The " +
				"store type itself may stay internal.");
		}

		return TenantMechanism.None;
	}

	/// <summary>
	/// Fails the registration when <see cref="ActivatorUtilities"/> could not pick one of
	/// <paramref name="tenantAwareConstructors"/> deterministically.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The constructing overload hands the resolved <see cref="ITenantContext"/> to
	/// <see cref="ActivatorUtilities"/> as an explicit argument. When two or more public constructors accept
	/// one, that argument no longer identifies a single constructor, and which one is selected — or whether
	/// selection fails at all — depends on which of the <em>other</em> parameters happen to be registered in
	/// the container at the time. A store can therefore resolve in one host and throw
	/// <c>"Multiple constructors accepting all given argument types have been found"</c> in another, and that
	/// message names the constructors rather than the dependency that is actually missing.
	/// </para>
	/// <para>
	/// <see cref="ActivatorUtilitiesConstructorAttribute"/> is the platform's own answer: a marked
	/// constructor is selected before any of that reasoning runs, so selection stops depending on container
	/// contents and a genuinely absent dependency reports itself as an absent dependency. This check makes
	/// the marker mandatory rather than advisory — an ambiguous store fails HERE, while its registration is
	/// being built, instead of on a consumer's first resolve.
	/// </para>
	/// </remarks>
	private static void RequireUnambiguousConstruction(Type storeType, ConstructorInfo[] tenantAwareConstructors)
	{
		if (tenantAwareConstructors.Length == 1)
		{
			return;
		}

		var preferredCount = Array.FindAll(
			tenantAwareConstructors,
			static constructor => constructor.IsDefined(typeof(ActivatorUtilitiesConstructorAttribute), inherit: false)).Length;

		if (preferredCount == 1)
		{
			return;
		}

		throw new InvalidOperationException(
			$"'{storeType}' declares {tenantAwareConstructors.Length} public constructors that accept an " +
			$"ITenantContext, and {preferredCount} of them are marked with [ActivatorUtilitiesConstructor]. " +
			"AddTenantAwareStore constructs the store by handing it the resolved ITenantContext, so exactly " +
			"one of those constructors must be marked for the selection to be deterministic. Mark the " +
			"constructor intended for dependency injection with [ActivatorUtilitiesConstructor], or register " +
			"the store through the AddTenantAwareStore overload that takes a factory and construct it there.");
	}

	/// <summary>
	/// Emits the <see cref="ITenantScopingCapability{TContract}"/> marker using the sole canonical
	/// implementation. Kept internal so the marker is emittable ONLY from within this class — via
	/// <see cref="AddTenantAwareStore{TContract, TStore}(IServiceCollection, Func{IServiceProvider, TStore})"/>
	/// and <see cref="AddTenantScopedProjectionStore{TService, TStore, TCapabilityFamily}"/> — or from an
	/// <c>InternalsVisibleTo</c> friend co-locating the emission with the tenant wiring it attests (the
	/// event-store erasure capability). No provider outside the friend set can register a bare marker.
	/// </summary>
	internal static void AddTenantScopingCapability<TContract>(IServiceCollection services)
		where TContract : class
	{
		services.TryAddSingleton<ITenantScopingCapability<TContract>>(
			static _ => new TenantScopingCapabilityMarker<TContract>());
	}

	/// <summary>
	/// Emits the <see cref="ITenantPartitionedCapability{TContract}"/> marker using the sole canonical
	/// implementation. Kept private so the marker is emittable ONLY from
	/// <see cref="AddTenantAwareStore{TContract, TStore}(IServiceCollection, Func{IServiceProvider, TStore})"/>,
	/// co-located with the store registration it attests.
	/// </summary>
	private static void AddTenantPartitionedCapability<TContract>(IServiceCollection services)
		where TContract : class
	{
		services.TryAddSingleton<ITenantPartitionedCapability<TContract>>(
			static _ => new TenantPartitionedCapabilityMarker<TContract>());
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

/// <summary>
/// Shared registration-time implementation of <see cref="ITenantPartitionedCapability{TContract}"/>. Emitted
/// only via <see cref="TenantScopedStoreServiceCollectionExtensions"/>, co-located with the store wiring so
/// the marker cannot exist independently of the registration it attests.
/// </summary>
/// <typeparam name="TContract">The store contract the capability applies to.</typeparam>
internal sealed class TenantPartitionedCapabilityMarker<TContract> : ITenantPartitionedCapability<TContract>
	where TContract : class
{
	/// <inheritdoc/>
	void ITenantPartitionedCapability<TContract>.AssertWiredThroughPartitionedSeam()
	{
		// No-op. The structural lock is the TYPE-level unimplementability of the internal member outside
		// this assembly; this body exists only to satisfy the contract. The capability is consumed as a
		// registration-time presence signal and this method is never invoked.
	}
}
