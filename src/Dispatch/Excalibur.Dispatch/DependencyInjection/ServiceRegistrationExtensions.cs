// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides common extension methods for service registration with consistent patterns.
/// </summary>
public static class ServiceRegistrationExtensions
{
	/// <summary>
	/// Adds a service only if it hasn't been registered yet.
	/// </summary>
	/// <typeparam name="TService"> The type of the service to register. </typeparam>
	/// <typeparam name="TImplementation"> The implementation type. </typeparam>
	/// <param name="services"> The service collection. </param>
	/// <param name="lifetime"> The service lifetime. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection TryAddService<TService,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>(
		this IServiceCollection services,
		ServiceLifetime lifetime = ServiceLifetime.Scoped)
		where TService : class
		where TImplementation : class, TService
	{
		ArgumentNullException.ThrowIfNull(services);

		var descriptor = new ServiceDescriptor(typeof(TService), typeof(TImplementation), lifetime);
		services.TryAdd(descriptor);
		return services;
	}

	/// <summary>
	/// Adds a singleton service only if it hasn't been registered yet.
	/// </summary>
	/// <typeparam name="TService"> The type of the service to register. </typeparam>
	/// <typeparam name="TImplementation"> The implementation type. </typeparam>
	/// <param name="services"> The service collection. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection TryAddSingletonService<TService,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>(
		this IServiceCollection services)
		where TService : class
		where TImplementation : class, TService
	{
		return services.TryAddService<TService, TImplementation>(ServiceLifetime.Singleton);
	}

	/// <summary>
	/// Adds a scoped service only if it hasn't been registered yet.
	/// </summary>
	/// <typeparam name="TService"> The type of the service to register. </typeparam>
	/// <typeparam name="TImplementation"> The implementation type. </typeparam>
	/// <param name="services"> The service collection. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection TryAddScopedService<TService,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>(
		this IServiceCollection services)
		where TService : class
		where TImplementation : class, TService
	{
		return services.TryAddService<TService, TImplementation>(ServiceLifetime.Scoped);
	}

	/// <summary>
	/// Adds a transient service only if it hasn't been registered yet.
	/// </summary>
	/// <typeparam name="TService"> The type of the service to register. </typeparam>
	/// <typeparam name="TImplementation"> The implementation type. </typeparam>
	/// <param name="services"> The service collection. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection TryAddTransientService<TService,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>(
		this IServiceCollection services)
		where TService : class
		where TImplementation : class, TService
	{
		return services.TryAddService<TService, TImplementation>(ServiceLifetime.Transient);
	}

	/// <summary>
	/// Checks if a service type has been registered.
	/// </summary>
	/// <typeparam name="TService"> The service type to check. </typeparam>
	/// <param name="services"> The service collection. </param>
	/// <returns> True if the service is registered, false otherwise. </returns>
	public static bool HasService<TService>(this IServiceCollection services)
		where TService : class
	{
		ArgumentNullException.ThrowIfNull(services);
		return services.Any(d => d.ServiceType == typeof(TService));
	}

	/// <summary>
	/// Removes all registrations for a service type.
	/// </summary>
	/// <typeparam name="TService"> The service type to remove. </typeparam>
	/// <param name="services"> The service collection. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection RemoveService<TService>(this IServiceCollection services)
		where TService : class
	{
		ArgumentNullException.ThrowIfNull(services);

		var descriptors = services.Where(d => d.ServiceType == typeof(TService)).ToList();
		foreach (var descriptor in descriptors)
		{
			_ = services.Remove(descriptor);
		}

		return services;
	}

	/// <summary>
	/// Replaces an existing service registration.
	/// </summary>
	/// <typeparam name="TService"> The type of the service to replace. </typeparam>
	/// <typeparam name="TImplementation"> The new implementation type. </typeparam>
	/// <param name="services"> The service collection. </param>
	/// <param name="lifetime"> The service lifetime. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection ReplaceService<TService,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>(
		this IServiceCollection services,
		ServiceLifetime lifetime = ServiceLifetime.Scoped)
		where TService : class
		where TImplementation : class, TService
	{
		ArgumentNullException.ThrowIfNull(services);

		var descriptor = new ServiceDescriptor(typeof(TService), typeof(TImplementation), lifetime);
		_ = services.Replace(descriptor);
		return services;
	}
}
