// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Excalibur.Data.Abstractions;
using Excalibur.Data.DataProcessing;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering data processing services with the dependency injection container.
/// </summary>
public static class DataProcessingServiceCollectionExtensions
{
	/// <summary>
	/// Registers a data processor implementation with the dependency injection container.
	/// </summary>
	/// <typeparam name="TProcessor">The data processor type to register.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <returns>The updated <see cref="IServiceCollection"/> instance.</returns>
	/// <remarks>
	/// This is the AOT-safe alternative to assembly scanning via
	/// <see cref="AddDataProcessing{TDataProcessorDb, TDataToProcessDb}"/>.
	/// </remarks>
	public static IServiceCollection AddDataProcessor<TProcessor>(this IServiceCollection services)
		where TProcessor : class, IDataProcessor
	{
		ArgumentNullException.ThrowIfNull(services);

		services.AddScoped<TProcessor>();
		services.AddScoped<IDataProcessor, TProcessor>();

		return services;
	}

	/// <summary>
	/// Registers a record handler implementation with the dependency injection container.
	/// </summary>
	/// <typeparam name="THandler">The record handler type to register.</typeparam>
	/// <typeparam name="TRecord">The record type handled by the handler.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <returns>The updated <see cref="IServiceCollection"/> instance.</returns>
	/// <remarks>
	/// This is the AOT-safe alternative to assembly scanning via
	/// <see cref="AddDataProcessing{TDataProcessorDb, TDataToProcessDb}"/>.
	/// </remarks>
	public static IServiceCollection AddRecordHandler<THandler, TRecord>(this IServiceCollection services)
		where THandler : class, IRecordHandler<TRecord>
	{
		ArgumentNullException.ThrowIfNull(services);

		services.AddScoped<IRecordHandler<TRecord>, THandler>();

		return services;
	}

	/// <summary>
	/// Adds the required services and configurations for data processing to the dependency injection container.
	/// </summary>
	/// <typeparam name="TDataProcessorDb"> The database type implementing <see cref="IDb" /> for data processor persistence. </typeparam>
	/// <typeparam name="TDataToProcessDb"> The database type implementing <see cref="IDb" /> for data to process persistence. </typeparam>
	/// <param name="services"> The <see cref="IServiceCollection" /> to add the services Excalibur.Dispatch.Transport.Aws.Sqs.LongPolling.Configuration. </param>
	/// <param name="configuration"> The application configuration containing the required settings. </param>
	/// <param name="configurationSection"> The section of the configuration containing the data processing settings. </param>
	/// <param name="handlerAssemblies"> The assemblies to scan for <see cref="IDataProcessor" /> implementations. </param>
	/// <returns> The updated <see cref="IServiceCollection" /> instance. </returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown if <paramref name="configuration" /> or <paramref name="handlerAssemblies" /> is <c> null </c>.
	/// </exception>
	[RequiresUnreferencedCode("Assembly scanning may require unreferenced types for reflection-based type discovery")]
	[RequiresDynamicCode("Assembly scanning uses reflection to dynamically discover and register processor types")]
	public static IServiceCollection AddDataProcessing<TDataProcessorDb, TDataToProcessDb>(
		this IServiceCollection services,
		IConfiguration configuration,
		string configurationSection,
		params Assembly[] handlerAssemblies)
		where TDataProcessorDb : class, IDb
		where TDataToProcessDb : class, IDb
	{
		ArgumentNullException.ThrowIfNull(configuration);
		ArgumentException.ThrowIfNullOrWhiteSpace(configurationSection);
		ArgumentNullException.ThrowIfNull(handlerAssemblies);

		services.TryAddScoped<IDataProcessorDb>(static sp => new DataProcessorDb(sp.GetRequiredService<TDataProcessorDb>()));
		services.TryAddScoped<IDataToProcessDb>(static sp => new DataToProcessDb(sp.GetRequiredService<TDataToProcessDb>()));

		foreach (var processorType in DataProcessorDiscovery.DiscoverProcessors(handlerAssemblies))
		{
			_ = services.AddScoped(processorType);
			_ = services.AddScoped(typeof(IDataProcessor), processorType);
		}

		foreach (var (interfaceType, implementationType) in RecordHandlerDiscovery.DiscoverHandlers(handlerAssemblies))
		{
			_ = services.AddScoped(interfaceType, implementationType);
		}

		_ = services.Configure<DataProcessingConfiguration>(configuration.GetSection(configurationSection));
		services.TryAddScoped<IDataProcessorRegistry>(static sp =>
		{
			var processors = sp.GetServices<IDataProcessor>() ?? [];
			return new DataProcessorRegistry(processors);
		});
		services.TryAddScoped<IDataOrchestrationManager, DataOrchestrationManager>();

		return services;
	}
}
