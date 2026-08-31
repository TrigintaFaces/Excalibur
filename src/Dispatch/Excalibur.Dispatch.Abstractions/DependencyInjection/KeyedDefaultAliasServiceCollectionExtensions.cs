// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the non-keyed convenience alias that forwards a store contract to its keyed <c>"default"</c>
/// registration, as a descriptor that can be told apart from a real store registration.
/// </summary>
/// <remarks>
/// <para>
/// The alias exists so a consumer can inject <c>IEventStore</c> (or any other aliased contract) without
/// writing <c>[FromKeyedServices("default")]</c>. It is registered by the core <c>Add...</c> call, which runs
/// <em>before</em> the provider that supplies the keyed <c>"default"</c> store: registering a store after
/// adding the subsystem is a supported, tested ordering, so at alias-registration time the collection cannot
/// yet say whether a store will exist. The alias is therefore unconditional by necessity, and a host that
/// never registers a store ends up holding an alias that resolves to nothing.
/// </para>
/// <para>
/// That is a problem for any registration-time gate that asks "is this contract registered?", because a
/// forwarder <em>promises</em> the contract without <em>providing</em> it: a plain
/// <c>services.Any(d =&gt; d.ServiceType == typeof(TContract))</c> counts the alias and concludes a store is
/// present when none is. A fail-closed gate then demands guarantees of a store that does not exist and
/// refuses a correctly-configured host.
/// </para>
/// <para>
/// So the alias is made self-describing. Its implementation factory is bound to
/// <see cref="KeyedDefaultAliasForwarder{TContract}"/>, whose non-generic marker interface
/// <see cref="IKeyedDefaultAliasForwarder"/> a gate reads straight off the delegate's
/// <see cref="Delegate.Target"/> - no reflection, nothing to trim, and contract-agnostic, so one predicate
/// serves every contract including those a gate discovers dynamically.
/// </para>
/// <para>
/// <b>Why excluding these descriptors cannot weaken a gate.</b> The marker interface is <c>internal</c>, so
/// no descriptor outside this assembly can present it: the exclusion is bounded, by the type system, to
/// descriptors this seam itself created as pure forwarders. And a forwarder is resolvable only when a keyed
/// <c>"default"</c> descriptor for the same contract exists - which is itself a descriptor the gate still
/// counts. Excluding forwarders therefore removes exactly the registrations that provide nothing, and cannot
/// hide a store that a gate would otherwise have caught.
/// </para>
/// </remarks>
internal static class KeyedDefaultAliasServiceCollectionExtensions
{
	/// <summary>The service key every provider registers its store under for the alias to forward to.</summary>
	internal const string DefaultServiceKey = "default";

	/// <summary>
	/// Adds the non-keyed alias forwarding <typeparamref name="TContract"/> to its keyed
	/// <see cref="DefaultServiceKey"/> registration, using <c>TryAdd</c> semantics so a real non-keyed
	/// registration made first always wins.
	/// </summary>
	/// <typeparam name="TContract">The store contract to alias.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <returns>The same <paramref name="services"/> instance, for chaining.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
	internal static IServiceCollection AddKeyedDefaultAlias<TContract>(this IServiceCollection services)
		where TContract : class
	{
		ArgumentNullException.ThrowIfNull(services);

		// A method group on an instance of the forwarder, not a lambda: the delegate's Target is then the
		// forwarder itself, which is what IsKeyedDefaultForwardingAlias reads. A lambda would compile to a
		// closure class and carry no such identity.
		var forwarder = new KeyedDefaultAliasForwarder<TContract>();

		services.TryAdd(
			ServiceDescriptor.Describe(typeof(TContract), forwarder.Resolve, ServiceLifetime.Singleton));

		return services;
	}

	/// <summary>
	/// Reports whether <paramref name="descriptor"/> is a forwarding alias registered by
	/// <see cref="AddKeyedDefaultAlias{TContract}"/> - a descriptor that promises a contract without
	/// providing a store for it.
	/// </summary>
	/// <param name="descriptor">The descriptor to classify.</param>
	/// <returns>
	/// <see langword="true"/> when the descriptor is one of this seam's forwarders; otherwise
	/// <see langword="false"/>, including for every registration this seam did not create.
	/// </returns>
	/// <remarks>
	/// Answers <see langword="false"/> for anything it cannot positively identify, so a gate consulting it
	/// fails closed: a misclassification leaves the gate demanding a capability, never skipping one.
	/// </remarks>
	internal static bool IsKeyedDefaultForwardingAlias(this ServiceDescriptor descriptor)
	{
		ArgumentNullException.ThrowIfNull(descriptor);

		// An alias is never keyed, so a keyed descriptor is definitively not one of ours.
		if (descriptor.IsKeyedService)
		{
			return false;
		}

		// Read through the sanctioned keyed-safe accessor rather than the raw getter. The raw
		// ImplementationFactory throws for a keyed descriptor on .NET 8.x and silently mis-reads on
		// 9 and 10, so the codebase routes every such read through one place that handles it. The
		// keyed check above makes this call safe either way; going through the accessor is what keeps
		// the hazard in a single location instead of re-deriving it at each call site.
		return descriptor.GetImplementationFactory()?.Target is IKeyedDefaultAliasForwarder;
	}

	/// <summary>
	/// Reports whether a store for <paramref name="contract"/> is registered - counting every registration
	/// except this seam's own forwarding aliases.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="contract">The store contract to look for.</param>
	/// <returns>
	/// <see langword="true"/> when at least one non-alias descriptor for <paramref name="contract"/> is
	/// present.
	/// </returns>
	internal static bool HasStoreRegistrationFor(this IServiceCollection services, Type contract)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(contract);

		for (var i = 0; i < services.Count; i++)
		{
			if (services[i].ServiceType == contract && !services[i].IsKeyedDefaultForwardingAlias())
			{
				return true;
			}
		}

		return false;
	}
}

/// <summary>
/// Non-generic handle on a keyed-default forwarding alias, so a gate can classify a descriptor without
/// knowing (or being able to name) the contract it forwards.
/// </summary>
/// <remarks>
/// Deliberately <c>internal</c>: the exclusion this marker drives is only safe because nothing outside this
/// assembly can present it, which bounds the set of skippable descriptors to the ones
/// <see cref="KeyedDefaultAliasServiceCollectionExtensions.AddKeyedDefaultAlias{TContract}"/> creates.
/// </remarks>
internal interface IKeyedDefaultAliasForwarder;

/// <summary>
/// Resolves the keyed <c>"default"</c> registration of <typeparamref name="TContract"/>. Bound as the alias
/// descriptor's implementation factory so the descriptor carries <see cref="IKeyedDefaultAliasForwarder"/>
/// on its <see cref="Delegate.Target"/>.
/// </summary>
/// <typeparam name="TContract">The aliased store contract.</typeparam>
internal sealed class KeyedDefaultAliasForwarder<TContract> : IKeyedDefaultAliasForwarder
	where TContract : class
{
	/// <summary>Resolves the keyed <c>"default"</c> store this alias forwards to.</summary>
	/// <param name="services">The resolving service provider.</param>
	/// <returns>
	/// The keyed <c>"default"</c> registration of <typeparamref name="TContract"/>, or <see langword="null"/>
	/// when no store is registered under that key.
	/// </returns>
	/// <remarks>
	/// <para>
	/// Deliberately <c>GetKeyedService</c>, not <c>GetRequiredKeyedService</c>. The alias must not change what
	/// resolving the contract means: <see cref="IServiceProvider.GetService"/> answers <see langword="null"/>
	/// for a service that is not there, and <c>GetRequiredService</c> is what turns that absence into an
	/// exception. Returning null here preserves both — a caller probing with <c>GetService</c> is told there is
	/// no store, and a caller that injects the contract or asks with <c>GetRequiredService</c> still fails at
	/// the injection site.
	/// </para>
	/// <para>
	/// Throwing from here broke the probe half of that contract, and framework code that branched on null took
	/// the wrong path: an optional-dependency check that had meant "no store is registered" became an exception
	/// a general handler swallowed, turning a log-once-and-stop into an every-interval retry that never ended.
	/// </para>
	/// <para>
	/// The <c>!</c> below is not a claim that the result is non-null. A DI implementation factory is typed
	/// <c>Func&lt;IServiceProvider, object&gt;</c> and has no way to express "may be absent", while the
	/// container handles a null result exactly as it handles an unregistered service. The suppression records
	/// that gap; it does not paper over a nullability bug.
	/// </para>
	/// </remarks>
	internal object Resolve(IServiceProvider services)
		=> services.GetKeyedService<TContract>(
			KeyedDefaultAliasServiceCollectionExtensions.DefaultServiceKey)!;
}
