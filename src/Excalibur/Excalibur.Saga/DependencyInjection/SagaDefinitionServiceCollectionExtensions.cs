// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Excalibur.Saga.Abstractions;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering saga definitions from assemblies.
/// </summary>
public static class SagaDefinitionServiceCollectionExtensions
{
	/// <summary>
	/// Scans the specified assembly for classes implementing <see cref="ISagaDefinition{TSagaData}"/>
	/// and registers them in the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="assembly">The assembly to scan for saga definition implementations.</param>
	/// <param name="lifetime">The service lifetime for registered definitions. Default is <see cref="ServiceLifetime.Singleton"/>.</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="services"/> or <paramref name="assembly"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// This method discovers all concrete (non-abstract, non-interface) classes that implement
	/// any closed form of <see cref="ISagaDefinition{TSagaData}"/> and registers them with the DI container.
	/// Each definition is registered both as its concrete type and as each closed
	/// <c>ISagaDefinition&lt;T&gt;</c> interface it implements.
	/// </para>
	/// <para>
	/// Uses <c>TryAdd</c> semantics — if a definition is already registered, it will not be replaced.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// // Register all saga definitions from the current assembly
	/// services.AddSagaDefinitionsFromAssembly(typeof(Program).Assembly);
	///
	/// // Register with transient lifetime
	/// services.AddSagaDefinitionsFromAssembly(typeof(Program).Assembly, ServiceLifetime.Transient);
	/// </code>
	/// </example>
	[RequiresUnreferencedCode("Assembly scanning uses reflection to discover types implementing ISagaDefinition<T>.")]
	public static IServiceCollection AddSagaDefinitionsFromAssembly(
		this IServiceCollection services,
		Assembly assembly,
		ServiceLifetime lifetime = ServiceLifetime.Singleton)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(assembly);

		var definitionTypes = assembly.GetTypes()
			.Where(static type => type is { IsClass: true, IsAbstract: false, IsInterface: false }
				&& ImplementsSagaDefinition(type));

		foreach (var definitionType in definitionTypes)
		{
			// Register as concrete type for direct injection
			services.TryAdd(new ServiceDescriptor(definitionType, definitionType, lifetime));

			// Register as each closed ISagaDefinition<T> interface for generic resolution
			var sagaInterfaces = definitionType.GetInterfaces()
				.Where(static i => i.IsGenericType
					&& i.GetGenericTypeDefinition() == typeof(ISagaDefinition<>));

			foreach (var sagaInterface in sagaInterfaces)
			{
				services.TryAddEnumerable(new ServiceDescriptor(
					sagaInterface, definitionType, lifetime));
			}
		}

		return services;
	}

	private static bool ImplementsSagaDefinition(Type type) =>
		type.GetInterfaces().Any(static i =>
			i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISagaDefinition<>));
}
