// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using Dapper;

using Excalibur.Data;
using Excalibur.Data.SqlServer;
using Excalibur.Data.SqlServer.TypeHandlers;

using Microsoft.Extensions.DependencyInjection.Extensions;

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
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "RegisterTypeHandler uses MakeGenericMethod for Dapper type handler registration. Dapper itself requires reflection.")]
	[UnconditionalSuppressMessage("AOT", "IL2060:MakeGenericMethod",
		Justification = "MakeGenericMethod is used to register Dapper type handlers dynamically. The generic type argument comes from the handler's base type.")]
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
	/// Configures Dapper with predefined and custom type handlers.
	/// </summary>
	/// <param name="additionalTypeHandlers"> The additional type handlers to register with Dapper. Handlers must inherit from <see cref="SqlMapper.TypeHandler{T}" />. </param>
	private static void ConfigureDapper(IEnumerable<SqlMapper.ITypeHandler> additionalTypeHandlers)
	{
		// REMOVED BEFORE REGISTERED, AND THE ORDER IS THE WHOLE POINT. Dapper ships BUILT-IN type maps
		// for DateOnly and TimeOnly, and a built-in map WINS over a registered TypeHandler<T>: the
		// generated materialiser casts the provider's value straight to the destination type and never
		// calls the handler at all. The handlers below are correct and fully tested in isolation, and
		// without these four lines they are dead code.
		//
		// What that looks like in production is a column whose provider value is a string -- a date
		// stored as VARCHAR, or any provider that hands dates back as text -- failing with
		// "Unable to cast object of type 'System.String' to type 'System.DateOnly'", wrapped in a
		// Dapper "Error parsing column N" whose reported column name and value are frequently not the
		// offending ones. A date that arrives as a real DateTime keeps working, so this surfaces on one
		// query and not its neighbours, long after the registration everyone would look at first.
		//
		// RemoveTypeMap only clears the built-in mapping; it does not touch the handler registry, so the
		// AddTypeHandler calls below then own both directions -- Parse on the way out, SetValue on the
		// way in. Dapper's own DateOnly/TimeOnly support has been added, withdrawn and reinstated across
		// 2.1.x releases, so this does not assume a particular version's default: it states ours.
		SqlMapper.RemoveTypeMap(typeof(DateOnly));
		SqlMapper.RemoveTypeMap(typeof(DateOnly?));
		SqlMapper.RemoveTypeMap(typeof(TimeOnly));
		SqlMapper.RemoveTypeMap(typeof(TimeOnly?));

		SqlMapper.AddTypeHandler(new MoneyTypeHandler());
		SqlMapper.AddTypeHandler(new NullableMoneyTypeHandler());
		SqlMapper.AddTypeHandler(new NullableDateOnlyTypeHandler());
		SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());
		SqlMapper.AddTypeHandler(new NullableTimeOnlyTypeHandler());
		SqlMapper.AddTypeHandler(new TimeOnlyTypeHandler());

		foreach (var typeHandler in additionalTypeHandlers)
		{
#pragma warning disable IL2026, IL3050 // RegisterTypeHandler uses reflection for Dapper type handler registration
			RegisterTypeHandler(typeHandler);
#pragma warning restore IL2026, IL3050
		}
	}

	/// <summary>
	/// Registers a custom Dapper type handler.
	/// </summary>
	/// <param name="handler"> The type handler to register. </param>
	/// <exception cref="InvalidOperationException"> </exception>
	[RequiresUnreferencedCode("Uses reflection to find and invoke generic AddTypeHandler.")]
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
