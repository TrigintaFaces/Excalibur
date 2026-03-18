// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Excalibur.Dispatch.Abstractions;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering projection handlers from assemblies.
/// </summary>
public static class ProjectionHandlerServiceCollectionExtensions
{
	/// <summary>
	/// Scans the specified assembly for classes implementing <see cref="IProjectionHandler"/>
	/// and registers them in the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="assembly">The assembly to scan for projection handler implementations.</param>
	/// <param name="lifetime">The service lifetime for registered handlers. Default is <see cref="ServiceLifetime.Singleton"/>.</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="services"/> or <paramref name="assembly"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// This method discovers all concrete (non-abstract, non-interface) classes that implement
	/// <see cref="IProjectionHandler"/> and registers them with the DI container.
	/// Each handler is registered both as its concrete type and as <see cref="IProjectionHandler"/>.
	/// </para>
	/// <para>
	/// Uses <c>TryAdd</c> semantics — if a handler is already registered, it will not be replaced.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// // Register all projection handlers from the current assembly
	/// services.AddProjectionHandlersFromAssembly(typeof(Program).Assembly);
	///
	/// // Register with transient lifetime
	/// services.AddProjectionHandlersFromAssembly(typeof(Program).Assembly, ServiceLifetime.Transient);
	/// </code>
	/// </example>
	[RequiresUnreferencedCode("Assembly scanning uses reflection to discover types implementing IProjectionHandler.")]
	public static IServiceCollection AddProjectionHandlersFromAssembly(
		this IServiceCollection services,
		Assembly assembly,
		ServiceLifetime lifetime = ServiceLifetime.Singleton)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(assembly);

		var handlerTypes = assembly.GetTypes()
			.Where(static type => type is { IsClass: true, IsAbstract: false, IsInterface: false }
				&& typeof(IProjectionHandler).IsAssignableFrom(type));

		foreach (var handlerType in handlerTypes)
		{
			// Register as concrete type for direct injection
			services.TryAdd(new ServiceDescriptor(handlerType, handlerType, lifetime));

			// Register as IProjectionHandler for enumerable resolution
			services.TryAddEnumerable(new ServiceDescriptor(
				typeof(IProjectionHandler), handlerType, lifetime));
		}

		return services;
	}
}
