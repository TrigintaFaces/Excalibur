using System.Reflection;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.DataAccess.SqlServer.Cdc;

/// <summary>
///     Provides extension methods for registering CDC (Change Data Capture) processors and handlers in the dependency injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
	/// <summary>
	///     Registers the CDC processor and associated data change handlers in the service collection.
	/// </summary>
	/// <param name="services"> The service collection to register the CDC processor with. </param>
	/// <param name="handlerAssemblies"> A collection of assemblies to scan for implementations of <see cref="IDataChangeHandler" />. </param>
	/// <returns> The modified <see cref="IServiceCollection" />. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="handlerAssemblies" /> is null. </exception>
	/// <remarks>
	///     This method registers the following components:
	///     <list type="bullet">
	///         <item> <see cref="IDataChangeEventProcessorFactory" /> as a transient service. </item>
	///         <item> <see cref="IDataChangeHandlerRegistry" /> as a scoped service. </item>
	///         <item> All implementations of <see cref="IDataChangeHandler" /> from the provided assemblies. </item>
	///     </list>
	/// </remarks>
	public static IServiceCollection AddCdcProcessor(this IServiceCollection services, params Assembly[] handlerAssemblies)
	{
		ArgumentNullException.ThrowIfNull(handlerAssemblies);

		_ = services.AddTransient<IDataChangeEventProcessorFactory, DataChangeEventProcessorFactory>();
		_ = services.AddScoped<IDataChangeHandlerRegistry, DataChangeHandlerRegistry>();

		foreach (var assembly in handlerAssemblies)
		{
			_ = services.RegisterChangeEventHandlersFromAssembly(assembly);
		}

		return services;
	}

	/// <summary>
	///     Scans the specified assembly for implementations of <see cref="IDataChangeHandler" /> and registers them in the service collection.
	/// </summary>
	/// <param name="services"> The service collection to register the handlers with. </param>
	/// <param name="assembly"> The assembly to scan for implementations of <see cref="IDataChangeHandler" />. </param>
	/// <returns> The modified <see cref="IServiceCollection" />. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="assembly" /> is null. </exception>
	/// <remarks>
	///     This method ensures that all non-abstract, non-interface types in the specified assembly that implement
	///     <see cref="IDataChangeHandler" /> are registered as scoped services in the dependency injection container.
	/// </remarks>
	public static IServiceCollection RegisterChangeEventHandlersFromAssembly(this IServiceCollection services, Assembly assembly)
	{
		ArgumentNullException.ThrowIfNull(assembly);

		var handlerTypes = assembly.GetTypes()
			.Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(IDataChangeHandler).IsAssignableFrom(t));

		foreach (var handlerType in handlerTypes)
		{
			_ = services.AddScoped(typeof(IDataChangeHandler), handlerType);
		}

		return services;
	}
}
