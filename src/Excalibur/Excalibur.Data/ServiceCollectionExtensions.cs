// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Dapper;

using Excalibur.Data.Persistence;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Serialization;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides extension methods for configuring Excalibur data services and repositories in an application.
/// </summary>
public static class ExcaliburDataServiceCollectionExtensions
{
	/// <summary>
	/// Configures Excalibur data services including Dapper and JSON serialization.
	/// </summary>
	/// <param name="services"> The <see cref="IServiceCollection" /> to configure. </param>
	/// <returns> The updated <see cref="IServiceCollection" />. </returns>
	/// <remarks>
	/// For event sourcing repository registration, use AddExcaliburEventSourcing()
	/// from Excalibur.EventSourcing.DependencyInjection.
	/// </remarks>
	public static IServiceCollection AddExcaliburDataServices(this IServiceCollection services)
	{
		ConfigureDapper();
		ConfigureJsonSerialization();

		services.TryAddSingleton<IJsonSerializer, DispatchJsonSerializer>();

		return services;
	}

	/// <summary>
	/// Configures Excalibur data services with unified persistence configuration.
	/// </summary>
	/// <param name="services"> The <see cref="IServiceCollection" /> to configure. </param>
	/// <param name="configuration"> The configuration section for persistence. </param>
	/// <returns> The updated <see cref="IServiceCollection" />. </returns>
	[RequiresUnreferencedCode("This method uses reflection and may not work correctly with trimming")]
	[RequiresDynamicCode("This method uses dynamic code generation and may not work correctly with AOT")]
	public static IServiceCollection AddExcaliburDataServicesWithPersistence(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(configuration);

		// Configure basic data services
		_ = services.AddExcaliburDataServices();

		// Configure persistence from configuration
		_ = services.AddPersistence(configuration.GetSection("Persistence"));

		return services;
	}

	/// <summary>
	/// Configures Excalibur data services with unified persistence configuration.
	/// </summary>
	/// <param name="services"> The <see cref="IServiceCollection" /> to configure. </param>
	/// <param name="configurePersistence"> Action to configure persistence. </param>
	/// <returns> The updated <see cref="IServiceCollection" />. </returns>
	public static IServiceCollection AddExcaliburDataServicesWithPersistence(
		this IServiceCollection services,
		Action<PersistenceConfiguration> configurePersistence)
	{
		ArgumentNullException.ThrowIfNull(configurePersistence);

		// Configure basic data services
		_ = services.AddExcaliburDataServices();

		// Configure persistence directly
		_ = services.AddPersistence(configurePersistence);

		return services;
	}

	/// <summary>
	/// Configures Dapper with custom type handlers and naming conventions.
	/// </summary>
	private static void ConfigureDapper() => DefaultTypeMap.MatchNamesWithUnderscores = true;

	/// <summary>
	/// Configures global JSON serialization settings for System.Text.Json.
	/// </summary>
	/// <remarks>
	/// Note: System.Text.Json does not have a global default settings mechanism like Newtonsoft.Json. Applications should configure
	/// JsonSerializerOptions per service or use DI to inject configured options.
	/// </remarks>
	private static void ConfigureJsonSerialization()
	{
		// System.Text.Json doesn't have global defaults like Newtonsoft.Json Configuration should be done per service using ExcaliburJsonSerializerOptions.Default
	}
}
