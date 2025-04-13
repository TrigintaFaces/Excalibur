using System.Collections.Concurrent;
using System.Reflection;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Core;

/// <summary>
///     Provides extension methods for registering implementations of interfaces in an assembly into an <see cref="IServiceCollection" />.
/// </summary>
public static class ServiceCollectionExtensions
{
	private static readonly ConcurrentDictionary<Assembly, IEnumerable<Type>> CachedTypes = new();

	/// <summary>
	///     Registers all implementations of a specified interface in the given assembly into the service collection.
	/// </summary>
	/// <typeparam name="TInterface"> The interface type whose implementations will be registered. </typeparam>
	/// <param name="services"> The service collection to register the services into. </param>
	/// <param name="assembly"> The assembly to scan for implementations. </param>
	/// <param name="lifetime"> The service lifetime (Transient, Scoped, or Singleton). </param>
	/// <param name="registerImplementingType">
	///     If <c> true </c>, the concrete implementation type will also be registered in addition to the interface.
	/// </param>
	/// <returns> The updated <see cref="IServiceCollection" />. </returns>
	public static IServiceCollection AddImplementations<TInterface>(
		this IServiceCollection services,
		Assembly assembly,
		ServiceLifetime lifetime,
		bool registerImplementingType = false) =>
		services.AddImplementations(assembly, typeof(TInterface), lifetime, registerImplementingType);

	/// <summary>
	///     Registers all implementations of a specified interface type in the given assembly into the service collection.
	/// </summary>
	/// <param name="services"> The service collection to register the services into. </param>
	/// <param name="assembly"> The assembly to scan for implementations. </param>
	/// <param name="interfaceType"> The interface type whose implementations will be registered. </param>
	/// <param name="lifetime"> The service lifetime (Transient, Scoped, or Singleton). </param>
	/// <param name="registerImplementingType">
	///     If <c> true </c>, the concrete implementation type will also be registered in addition to the interface.
	/// </param>
	/// <returns> The updated <see cref="IServiceCollection" />. </returns>
	public static IServiceCollection AddImplementations(
		this IServiceCollection services,
		Assembly assembly,
		Type interfaceType,
		ServiceLifetime lifetime,
		bool registerImplementingType = false)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(assembly);
		ArgumentNullException.ThrowIfNull(interfaceType);

		if (!CachedTypes.TryGetValue(assembly, out var types))
		{
			types = assembly.GetExportedTypes();
			CachedTypes[assembly] = types;
		}

		foreach (var implementation in types)
		{
			if (implementation.IsAbstract || implementation.IsGenericTypeDefinition)
			{
				continue;
			}

			var matchingInterfaces = implementation.GetInterfaces()
				.Where((Type i) => i == interfaceType || (i.IsGenericType && i.GetGenericTypeDefinition() == interfaceType)).ToList();

			foreach (var @interface in matchingInterfaces)
			{
				services.Add(new ServiceDescriptor(@interface, implementation, lifetime));
			}

			if (registerImplementingType)
			{
				services.Add(new ServiceDescriptor(implementation, implementation, lifetime));
			}
		}

		return services;
	}
}
