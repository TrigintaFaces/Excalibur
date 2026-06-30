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

		var descriptor = services.LastOrDefault(sd => sd.ServiceType == typeof(TService)) ??
						 throw new InvalidOperationException($"Service type {typeof(TService)} not registered");

		_ = services.Remove(descriptor);

		// ybem93: read implementation members through the keyed-safe accessors (raw reads throw on
		// keyed descriptors on .NET 8+). Only handle ImplementationType for simplicity.
		var implementationType = descriptor.GetImplementationType();
		if (implementationType is not null)
		{
			services.Add(new ServiceDescriptor(typeof(TService), sp =>
			{
				var original = (TService)ActivatorUtilities.CreateInstance(sp, implementationType);
				return ActivatorUtilities.CreateInstance<TDecorator>(sp, original);
			}, descriptor.Lifetime));
		}
		else if (descriptor.GetImplementationFactory() is { } implementationFactory)
		{
			services.Add(new ServiceDescriptor(typeof(TService), sp =>
			{
				var original = (TService)implementationFactory(sp);
				return ActivatorUtilities.CreateInstance<TDecorator>(sp, original);
			}, descriptor.Lifetime));
		}
		else if (descriptor.GetImplementationInstance() is { } implementationInstance)
		{
			var originalInstance = (TService)implementationInstance;
			services.Add(new ServiceDescriptor(typeof(TService), sp =>
				ActivatorUtilities.CreateInstance<TDecorator>(sp, originalInstance), descriptor.Lifetime));
		}

		return services;
	}
}
