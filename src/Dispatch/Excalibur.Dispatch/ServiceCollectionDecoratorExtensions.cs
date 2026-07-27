// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides helper methods for decorating registered services.
/// </summary>
public static class ServiceCollectionDecoratorExtensions
{
	/// <summary>
	/// Decorates an existing service registration with a decorator type.
	/// </summary>
	/// <typeparam name="TService"> Service contract being decorated. </typeparam>
	/// <typeparam name="TDecorator"> Decorator implementation. </typeparam>
	/// <param name="services"> The service collection. </param>
	/// <returns> The modified <see cref="IServiceCollection" />. </returns>
	/// <exception cref="InvalidOperationException"> Thrown when the service type is not registered. </exception>
	public static IServiceCollection Decorate<TService,
	[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	TDecorator>(this IServiceCollection services)
		where TService : class
		where TDecorator : class, TService
	{
		ArgumentNullException.ThrowIfNull(services);

		var matches = services.Where(sd => sd.ServiceType == typeof(TService)).ToList();
		if (matches.Count == 0)
		{
			throw new InvalidOperationException($"Service type {typeof(TService)} not registered");
		}

		// A generic Decorate<T> decorates EXACTLY ONE registration and cannot safely disambiguate when a
		// service type has several — so when it finds more than one, it REFUSES rather than silently guessing.
		//
		//   * Several providers register the same service type more than once (a provider-specific key
		//     alongside a keyed "default"), and the core registers a NON-KEYED FORWARDING ALIAS that resolves
		//     the keyed "default" store. Decorating every match would wrap the alias AND the store it forwards
		//     to, producing DOUBLE DECORATION — harmless-looking for tenant scoping, corrupting for an
		//     encrypting or telemetry decorator.
		//
		//   * Decorating only one (the old last-wins behaviour) leaves its siblings RAW. A consumer resolving
		//     an undecorated sibling gets the service with none of the behaviour the decorator exists to add;
		//     for a tenant-scoping decorator that is a cross-tenant leak.
		//
		// Neither is acceptable, and a generic Decorate<T> cannot distinguish a forwarding alias from a real
		// registration. The correct seam is a KEY-TARGETED decoration chosen by the caller, which knows which
		// registration is canonical (or decoration at the registration site). Silent last-wins is the
		// cross-tenant-leak defect this guard closes — fail fast at composition instead.
		if (matches.Count > 1)
		{
			throw new InvalidOperationException(
				$"Service type {typeof(TService)} has {matches.Count} registrations; the generic Decorate<T> "
				+ "decorates exactly one and cannot disambiguate multiple registrations (for example a "
				+ "provider-keyed store, a keyed \"default\" alias, and a non-keyed forwarding alias). Decorating "
				+ "one leaves its siblings undecorated (a cross-tenant leak for a tenant-scoping decorator); "
				+ "decorating all double-decorates the forwarding alias. Decorate the canonical registration "
				+ "explicitly by key, or decorate at the registration site.");
		}

		var descriptor = matches[0];
		_ = services.Remove(descriptor);

		// Read implementation members through the keyed-safe accessors (raw reads throw on keyed descriptors
		// on .NET 8+).
		//
		// AND RE-REGISTER WITH THE SAME KEY. A keyed descriptor satisfies the ServiceType predicate above, so
		// re-adding it through the non-keyed ServiceDescriptor constructor DESTROYS its ServiceKey: the keyed
		// registration disappears, GetRequiredKeyedService<TService>(key) can no longer resolve it, and the
		// decorated instance becomes reachable only without a key. A decorator that drops the key strips it
		// exactly as a decorator that omits an interface strips a capability — the wrapper must preserve
		// everything the wrapped registration advertised, including how it is addressed.
		var originalFactory = BuildOriginalFactory(descriptor)
							  ?? throw new InvalidOperationException(
								  $"Service type {typeof(TService)} is registered in a form "
								  + $"{nameof(Decorate)} cannot wrap.");

		services.Add(descriptor.IsKeyedService
			? ServiceDescriptor.DescribeKeyed(
				typeof(TService),
				descriptor.ServiceKey,
				(sp, _) => ActivatorUtilities.CreateInstance<TDecorator>(sp, (TService)originalFactory(sp)),
				descriptor.Lifetime)
			: ServiceDescriptor.Describe(
				typeof(TService),
				sp => ActivatorUtilities.CreateInstance<TDecorator>(sp, (TService)originalFactory(sp)),
				descriptor.Lifetime));

		return services;
	}

	/// <summary>
	/// Produces a factory for the undecorated service from whichever registration form the descriptor uses.
	/// </summary>
	/// <param name="descriptor">The registration being decorated.</param>
	/// <returns>A factory for the original service, or <see langword="null"/> when the form is unsupported.</returns>
	private static Func<IServiceProvider, object>? BuildOriginalFactory(ServiceDescriptor descriptor)
	{
		var implementationType = descriptor.GetImplementationType();
		if (implementationType is not null)
		{
			return sp => ActivatorUtilities.CreateInstance(sp, implementationType);
		}

		if (descriptor.GetImplementationFactory() is { } implementationFactory)
		{
			return implementationFactory;
		}

		return descriptor.GetImplementationInstance() is { } instance ? _ => instance : null;
	}
}
