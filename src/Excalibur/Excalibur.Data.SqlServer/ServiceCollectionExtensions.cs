// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;

using Dapper;

using Excalibur.Data;
using Excalibur.Data.SqlServer;
using Excalibur.Data.SqlServer.Cdc;
using Excalibur.Data.SqlServer.TypeHandlers;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides extension methods for configuring SQL services with Dapper type handlers.
/// </summary>
public static class ExcaliburDataSqlServerServiceCollectionExtensions
{
	/// <summary>
	/// Adds Excalibur SQL services to the dependency injection container and configures Dapper with custom type handlers.
	/// </summary>
	/// <param name="services"> The service collection to configure. </param>
	/// <param name="additionalTypeHandlers"> Optional additional Dapper type handlers to register. Handlers must inherit from <see cref="SqlMapper.TypeHandler{T}" />. </param>
	/// <returns> The updated service collection. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="services" /> is null. </exception>
	public static IServiceCollection AddExcaliburSqlServices(
		this IServiceCollection services,
		params SqlMapper.ITypeHandler[]? additionalTypeHandlers)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton<IDataAccessPolicyFactory, SqlDataAccessPolicyFactory>();

		ConfigureDapper(additionalTypeHandlers ?? []);
		return services;
	}

	/// <summary>
	/// Registers the CDC processor and associated data change handlers in the service collection.
	/// </summary>
	/// <param name="services"> The service collection to register the CDC processor with. </param>
	/// <param name="handlerAssemblies"> A collection of assemblies to scan for implementations of <see cref="IDataChangeHandler" />. </param>
	/// <returns> The modified <see cref="IServiceCollection" />. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="handlerAssemblies" /> is null. </exception>
	/// <remarks>
	/// This method registers the following components:
	/// <list type="bullet">
	/// <item> <see cref="IDataChangeEventProcessorFactory" /> as a transient service. </item>
	/// <item> All implementations of <see cref="IDataChangeHandler" /> from the provided assemblies. </item>
	/// </list>
	/// </remarks>
	public static IServiceCollection AddCdcProcessor(this IServiceCollection services, params Assembly[] handlerAssemblies)
	{
		ArgumentNullException.ThrowIfNull(handlerAssemblies);

		RegisterCdcStateStoreOptions(services);

		services.TryAddTransient<IDataChangeEventProcessorFactory, DataChangeEventProcessorFactory>();

		foreach (var assembly in handlerAssemblies)
		{
			_ = services.AddImplementations<IDataChangeHandler>(assembly, ServiceLifetime.Scoped);
		}

		return services;
	}

	/// <summary>
	/// Configures Dapper with predefined and custom type handlers.
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

	private static void RegisterCdcStateStoreOptions(IServiceCollection services)
	{
		_ = services.AddOptions<SqlServerCdcStateStoreOptions>()
			.ValidateDataAnnotations()
			.ValidateOnStart();
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<SqlServerCdcStateStoreOptions>, SqlServerCdcStateStoreOptionsValidator>());
	}

	/// <summary>
	/// Registers a custom Dapper type handler.
	/// </summary>
	/// <param name="handler"> The type handler to register. </param>
	/// <exception cref="InvalidOperationException"> </exception>
	[RequiresDynamicCode("Calls System.Reflection.MethodInfo.MakeGenericMethod(params Type[])")]
	private static void RegisterTypeHandler(object handler)
	{
		var handlerType = handler.GetType();
		var handledType = handlerType.BaseType?.GenericTypeArguments.FirstOrDefault() ?? throw new InvalidOperationException(
			string.Format(CultureInfo.InvariantCulture, "Handler type {0} does not specify generic type.", handlerType.FullName));

		var addHandlerMethod =
			typeof(SqlMapper).GetMethods()
				.FirstOrDefault(static m => m is { Name: nameof(SqlMapper.AddTypeHandler), IsGenericMethod: true })
			?? throw new InvalidOperationException("Could not locate generic AddTypeHandler method.");

		var genericAddHandlerMethod = addHandlerMethod.MakeGenericMethod(handledType);
		_ = genericAddHandlerMethod.Invoke(null, [handler]);
	}
}
