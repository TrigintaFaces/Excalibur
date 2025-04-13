using System.Reflection;

using Dapper;

using Excalibur.DataAccess.SqlServer.Cdc;
using Excalibur.DataAccess.SqlServer.TypeHandlers;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.DataAccess.SqlServer;

/// <summary>
///     Provides extension methods for configuring SQL services with Dapper type handlers.
/// </summary>
public static class ServiceCollectionExtensions
{
	/// <summary>
	///     Adds Excalibur SQL services to the dependency injection container and configures Dapper with custom type handlers.
	/// </summary>
	/// <param name="services"> The service collection to configure. </param>
	/// <param name="additionalTypeHandlers"> Optional additional Dapper type handlers to register. Handlers must inherit from <see cref="SqlMapper.TypeHandler{T}" />. </param>
	/// <returns> The updated service collection. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="services" /> is null. </exception>
	public static IServiceCollection AddExcaliburSqlServices(
		this IServiceCollection services,
		params SqlMapper.ITypeHandler[] additionalTypeHandlers)
	{
		ArgumentNullException.ThrowIfNull(services, nameof(services));

		_ = services.AddSingleton<IDataAccessPolicyFactory, SqlDataAccessPolicyFactory>();

		ConfigureDapper(additionalTypeHandlers ?? []);
		return services;
	}

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
	///         <item> <see cref="IDataChangeHandlerFactory" /> as a scoped service. </item>
	///         <item> All implementations of <see cref="IDataChangeHandler" /> from the provided assemblies. </item>
	///     </list>
	/// </remarks>
	public static IServiceCollection AddCdcProcessor(this IServiceCollection services, params Assembly[] handlerAssemblies)
	{
		ArgumentNullException.ThrowIfNull(handlerAssemblies);

		_ = services.AddTransient<IDataChangeEventProcessorFactory, DataChangeEventProcessorFactory>();

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

		var handlerTypes = assembly.GetTypes().Where(
			(Type t) => t is { IsAbstract: false, IsInterface: false } && typeof(IDataChangeHandler).IsAssignableFrom(t));

		foreach (var handlerType in handlerTypes)
		{
			_ = services.AddScoped(typeof(IDataChangeHandler), handlerType);
		}

		return services;
	}

	/// <summary>
	///     Configures Dapper with predefined and custom type handlers.
	/// </summary>
	/// <param name="additionalTypeHandlers"> The additional type handlers to register with Dapper. Handlers must inherit from <see cref="SqlMapper.TypeHandler{T}" />. </param>
	private static void ConfigureDapper(IEnumerable<SqlMapper.ITypeHandler> additionalTypeHandlers)
	{
		SqlMapper.AddTypeHandler(new MoneyTypeHandler());
		SqlMapper.AddTypeHandler(new NullableMoneyTypeHandler());
		SqlMapper.AddTypeHandler(new NullableDateOnlyTypeHandler());
		SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());
		SqlMapper.AddTypeHandler(new NullableTimeOnlyTypeHandler());
		SqlMapper.AddTypeHandler(new TimeOnlyTypeHandler());

		foreach (var typeHandler in additionalTypeHandlers)
		{
			RegisterTypeHandler(typeHandler);
		}
	}

	/// <summary>
	///     Registers a custom Dapper type handler.
	/// </summary>
	/// <param name="handler"> The type handler to register. </param>
	private static void RegisterTypeHandler(object handler)
	{
		var handlerType = handler.GetType();
		var handledType = (handlerType.BaseType?.GenericTypeArguments.FirstOrDefault()) ?? throw new InvalidOperationException(
			$"The handler of type '{handlerType.FullName}' does not specify a generic type and cannot be registered.");

		var addHandlerMethod =
			typeof(SqlMapper).GetMethods()
				.FirstOrDefault((MethodInfo m) => m is { Name: nameof(SqlMapper.AddTypeHandler), IsGenericMethod: true })
			?? throw new InvalidOperationException("Could not locate the generic AddTypeHandler method on SqlMapper.");

		var genericAddHandlerMethod = addHandlerMethod.MakeGenericMethod(handledType);
		_ = genericAddHandlerMethod.Invoke(null, [handler]);
	}
}
