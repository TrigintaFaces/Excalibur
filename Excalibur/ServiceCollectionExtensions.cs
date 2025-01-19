using System.Reflection;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur;

/// <summary>
///     Provides extension methods for registering implementations of interfaces in an assembly into an <see cref="IServiceCollection" />.
/// </summary>
public static class ServiceCollectionExtensions
{
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
		ArgumentNullException.ThrowIfNull(assembly);
		ArgumentNullException.ThrowIfNull(interfaceType);

		return services.AddImplementations(assembly, Predicate, lifetime, registerImplementingType);

		bool Predicate(Type i) => i.IsGenericType ? i.GetGenericTypeDefinition() == interfaceType : i == interfaceType;
	}

	/// <summary>
	///     Registers all types in the specified assembly that match the provided interface predicate into the service collection.
	/// </summary>
	/// <param name="services"> The service collection to register the services into. </param>
	/// <param name="assembly"> The assembly to scan for matching types. </param>
	/// <param name="interfacePredicate"> A predicate to identify matching interfaces. </param>
	/// <param name="lifetime"> The service lifetime (Transient, Scoped, or Singleton). </param>
	/// <param name="registerImplementingType">
	///     If <c> true </c>, the concrete implementation type will also be registered in addition to the interface.
	/// </param>
	/// <returns> The updated <see cref="IServiceCollection" />. </returns>
	private static IServiceCollection AddImplementations(
		this IServiceCollection services,
		Assembly assembly,
		Func<Type, bool> interfacePredicate,
		ServiceLifetime lifetime,
		bool registerImplementingType)
	{
		IEnumerable<(Type Interface, Type Implementation)> matches = from type in assembly.ExportedTypes
																	 where !type.IsAbstract && !type.IsGenericTypeDefinition
																	 let interfaces = type.GetInterfaces().Where(interfacePredicate)
																	 let matchingInterface = interfaces.FirstOrDefault()
																	 where matchingInterface != null
																	 select (matchingInterface, type);

		foreach (var (@interface, implementation) in matches)
		{
			switch (lifetime)
			{
				case ServiceLifetime.Transient:

					_ = services.AddTransient(@interface, implementation);

					if (registerImplementingType)
					{
						_ = services.AddTransient(implementation, implementation);
					}

					break;

				case ServiceLifetime.Scoped:

					_ = services.AddScoped(@interface, implementation);

					if (registerImplementingType)
					{
						_ = services.AddScoped(implementation, implementation);
					}

					break;

				case ServiceLifetime.Singleton:

					_ = services.AddSingleton(@interface, implementation);

					if (registerImplementingType)
					{
						_ = services.AddSingleton(implementation, implementation);
					}

					break;

				default:
					throw new ArgumentException("The ServiceLifetime is invalid.", nameof(lifetime));
			}
		}

		return services;
	}
}
