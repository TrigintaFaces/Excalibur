// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Shape-robust, key-targeted decoration for a store contract registered as several descriptors — a provider
/// terminal, a keyed <c>"default"</c> alias, and a non-keyed forwarding alias. It wraps every <em>terminal</em>
/// (real store) descriptor exactly once and leaves forwarding aliases untouched, so every route a consumer can
/// resolve the contract yields a decorated instance exactly once — no alias is double-wrapped, and no real
/// terminal is left raw.
/// </summary>
/// <remarks>
/// This is the seam the generic <c>ServiceCollectionDecoratorExtensions.Decorate&lt;TService, TDecorator&gt;</c>
/// documents as the correct answer when a service type has several registrations it cannot itself disambiguate
/// (decorating "the last match" would wrap a forwarding alias or leave provider terminals raw). It lives in
/// <c>Excalibur.Dispatch.Abstractions</c>, the lowest assembly the multi-tenancy and at-rest-encryption store
/// decorators both reference, and takes its decorator through a factory so it needs no reference to any concrete
/// store contract (no <c>IEventStore</c>, <c>ISagaStore</c>, or inbox/outbox dependency).
/// </remarks>
public static class KeyedStoreServiceCollectionExtensions
{
	/// <summary>
	/// The keyed alias providers register to point at their concrete store. It is a forwarding redirect, never a
	/// real implementation when a provider-keyed terminal exists, so it must not be decorated in that shape —
	/// decorating the terminal already makes the alias resolve the decorated store (a single wrap).
	/// </summary>
	private const string DefaultServiceKey = "default";

	/// <summary>
	/// Wraps every <em>terminal</em> registration of <typeparamref name="TService"/> (the real provider stores)
	/// while leaving forwarding aliases untouched, preserving each descriptor's service key and lifetime.
	/// </summary>
	/// <typeparam name="TService">The store contract being decorated.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="decoratorFactory">Builds the decorator from the resolved original store and the provider.</param>
	/// <param name="reservedKeys">
	/// Optional internal service keys that must be left <em>undecorated</em>. A reserved key marks an internal
	/// implementation registration a consumer never resolves directly (for example the tiered-storage raw hot
	/// store under its private key, which the archive service resolves for its intentionally cross-tenant trim).
	/// Reserved-keyed descriptors are excluded from every selection rule, so decoration targets the
	/// consumer-facing terminal instead — when the reserved key is the only non-<c>"default"</c> descriptor, that
	/// terminal is the <c>"default"</c> store itself (Rule 2), which is how a tenant decorator wraps the
	/// <em>outer</em> tiered store rather than the inner hot leg. Defaults to none.
	/// </param>
	/// <returns>
	/// <see langword="true"/> if at least one terminal descriptor was decorated; <see langword="false"/> when no
	/// decoratable registration of <typeparamref name="TService"/> exists (nothing to wrap).
	/// </returns>
	/// <remarks>
	/// <para>The rule is robust across the registration shapes providers actually build:</para>
	/// <list type="number">
	///   <item><description>
	///   Decorate every keyed descriptor whose service key is not <c>"default"</c> — the provider terminals (one
	///   for a single provider, several when a consumer registered more than one). The keyed <c>"default"</c>
	///   alias and any non-keyed alias forward to them, so they resolve the decorated store, scoped exactly once.
	///   </description></item>
	///   <item><description>
	///   If there is <b>no</b> non-<c>"default"</c> keyed descriptor, the <c>"default"</c> keyed descriptor is
	///   itself the real store (the core-generic registration shape) — decorate it.
	///   </description></item>
	///   <item><description>
	///   If there is <b>no</b> keyed descriptor at all, decorate the non-keyed real registration (the plain
	///   <c>AddSingleton&lt;TService&gt;</c> shape). A non-keyed forwarding alias only exists alongside a keyed
	///   <c>"default"</c>, so this branch never wraps a forwarder.
	///   </description></item>
	/// </list>
	/// <para>
	/// Implementation members are read through the keyed-safe <see cref="ServiceDescriptorExtensions"/> accessors
	/// (raw reads throw on keyed descriptors on .NET 8+). The original factory is captured before the descriptor
	/// is removed so the decorator's inner reference never re-enters the decorated registration (no resolution
	/// recursion).
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="decoratorFactory"/> is <see langword="null"/>.</exception>
	public static bool DecorateKeyedStores<TService>(
		this IServiceCollection services,
		Func<TService, IServiceProvider, TService> decoratorFactory,
		IReadOnlyCollection<object>? reservedKeys = null)
		where TService : class
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(decoratorFactory);

		var decoratedAny = false;

		foreach (var descriptor in SelectDecorationTargets<TService>(services, reservedKeys))
		{
			var originalFactory = BuildOriginalFactory<TService>(descriptor);
			if (originalFactory is null)
			{
				continue;
			}

			_ = services.Remove(descriptor);

			services.Add(descriptor.IsKeyedService
				? ServiceDescriptor.DescribeKeyed(
					typeof(TService),
					descriptor.ServiceKey,
					(sp, _) => decoratorFactory(originalFactory(sp), sp),
					descriptor.Lifetime)
				: ServiceDescriptor.Describe(
					typeof(TService),
					sp => decoratorFactory(originalFactory(sp), sp),
					descriptor.Lifetime));

			decoratedAny = true;
		}

		return decoratedAny;
	}

	/// <summary>
	/// Selects the terminal (real-store) descriptors to decorate for <typeparamref name="TService"/>, applying
	/// the shape-robust rule documented on <see cref="DecorateKeyedStores{TService}"/>. Returns a materialized
	/// snapshot so the subsequent remove/add mutation does not disturb the iteration.
	/// </summary>
	private static List<ServiceDescriptor> SelectDecorationTargets<TService>(
		IServiceCollection services,
		IReadOnlyCollection<object>? reservedKeys)
		where TService : class
	{
		var all = services.Where(sd => sd.ServiceType == typeof(TService)).ToList();

		// Reserved keys are internal implementation registrations (e.g. the tiered-storage raw hot store under
		// its private key) that a consumer never resolves directly and that a decorator must NOT wrap: wrapping
		// them would decorate an internal store while leaving the consumer-facing terminal raw. Excluding them
		// from the candidate set before the rules run means a reserved-only non-"default" shape falls through to
		// Rule 2 (decorate the "default" store) — so a tenant decorator wraps the outer store, not the inner leg.
		if (reservedKeys is { Count: > 0 })
		{
			all = all
				.Where(sd => !(sd.IsKeyedService && sd.ServiceKey is { } key && reservedKeys.Contains(key)))
				.ToList();
		}

		// Rule 1 — provider terminals: keyed, key != "default". Present in the single- and multi-provider shapes.
		var nonDefaultKeyed = all
			.Where(sd => sd.IsKeyedService
						 && !string.Equals(sd.ServiceKey as string, DefaultServiceKey, StringComparison.Ordinal))
			.ToList();
		if (nonDefaultKeyed.Count > 0)
		{
			return nonDefaultKeyed;
		}

		// Rule 2 — Shape B: the "default" keyed descriptor IS the real store (core-generic registration).
		var defaultKeyed = all
			.Where(sd => sd.IsKeyedService
						 && string.Equals(sd.ServiceKey as string, DefaultServiceKey, StringComparison.Ordinal))
			.ToList();
		if (defaultKeyed.Count > 0)
		{
			return defaultKeyed;
		}

		// Rule 3 — no keyed descriptor at all: the plain non-keyed registration is the real store (no forwarding
		// alias exists without a keyed "default", so nothing here is an alias).
		return all;
	}

	/// <summary>
	/// Produces a factory for the undecorated store from whichever registration form the descriptor uses,
	/// captured from the original descriptor so the decorator's inner reference never re-enters the decorated
	/// registration (no resolution recursion).
	/// </summary>
	private static Func<IServiceProvider, TService>? BuildOriginalFactory<TService>(ServiceDescriptor descriptor)
		where TService : class
	{
		var implementationType = descriptor.GetImplementationType();
		if (implementationType is not null)
		{
			return sp => (TService)ActivatorUtilities.CreateInstance(sp, implementationType);
		}

		if (descriptor.GetImplementationFactory() is { } factory)
		{
			return sp => (TService)factory(sp);
		}

		return descriptor.GetImplementationInstance() is TService instance ? _ => instance : null;
	}
}
