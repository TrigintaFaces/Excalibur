// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Excalibur.Data.DataProcessing;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering data processing services with the dependency injection container.
/// </summary>
public static class DataProcessingServiceCollectionExtensions
{
	/// <summary>
	/// Cached record handler factory delegates, keyed by record type.
	/// Populated at <c>AddRecordHandler&lt;THandler, TRecord&gt;</c> registration time.
	/// </summary>
	internal static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, Func<IServiceProvider, object>>
		RecordHandlerFactories = new();

	/// <summary>
	/// Registers a data processor implementation with the dependency injection container.
	/// </summary>
	/// <typeparam name="TProcessor">The data processor type to register.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <returns>The updated <see cref="IServiceCollection"/> instance.</returns>
	/// <remarks>
	/// This is the AOT-safe alternative to assembly scanning via
	/// <see cref="AddDataProcessing(IServiceCollection, Func{IDbConnection}, IConfiguration, string, Assembly[])"/>.
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
	/// Registers a data processor implementation with configuration options.
	/// </summary>
	/// <typeparam name="TProcessor">The data processor type to register.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The data processing configuration.</param>
	/// <returns>The updated <see cref="IServiceCollection"/> instance.</returns>
	/// <remarks>
	/// <para>
	/// This overload registers <see cref="DataProcessingOptions"/> via the standard
	/// <c>IOptions&lt;T&gt;</c> pattern, eliminating the need to manually wrap in <c>Options.Create()</c>.
	/// Configuration is validated at startup via <c>ValidateDataAnnotations</c> and <c>ValidateOnStart</c>.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddDataProcessor&lt;MyProcessor&gt;(new DataProcessingOptions
	/// {
	///     QueueSize = 128,
	///     ProducerBatchSize = 50,
	///     ConsumerBatchSize = 20
	/// });
	/// </code>
	/// </example>
	public static IServiceCollection AddDataProcessor<TProcessor>(
		this IServiceCollection services,
		DataProcessingOptions configuration)
		where TProcessor : class, IDataProcessor
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		services.TryAddSingleton(Options.Options.Create(configuration));
		services.AddOptions<DataProcessingOptions>()
			.ValidateDataAnnotations()
			.ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<DataProcessingOptions>, DataProcessingOptionsValidator>());
		services.AddScoped<TProcessor>();
		services.AddScoped<IDataProcessor, TProcessor>();

		return services;
	}

	/// <summary>
	/// Registers a data processor implementation with configuration bound from an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <typeparam name="TProcessor">The data processor type to register.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The application configuration.</param>
	/// <param name="sectionPath">The configuration section path to bind (e.g., "DataProcessing").</param>
	/// <returns>The updated <see cref="IServiceCollection"/> instance.</returns>
	/// <remarks>
	/// <para>
	/// This is the AOT-safe, appsettings-driven alternative. Uses
	/// <c>OptionsBuilder&lt;T&gt;.BindConfiguration()</c> with <c>ValidateDataAnnotations</c>
	/// and <c>ValidateOnStart</c> for fail-fast validation.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// // appsettings.json:
	/// // { "DataProcessing": { "QueueSize": 128, "ProducerBatchSize": 50 } }
	///
	/// services.AddDataProcessor&lt;MyProcessor&gt;(configuration, "DataProcessing");
	/// </code>
	/// </example>
	public static IServiceCollection AddDataProcessor<TProcessor>(
		this IServiceCollection services,
		IConfiguration configuration,
		string sectionPath)
		where TProcessor : class, IDataProcessor
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);
		ArgumentException.ThrowIfNullOrWhiteSpace(sectionPath);

		services.AddOptions<DataProcessingOptions>()
			.BindConfiguration(sectionPath)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<DataProcessingOptions>, DataProcessingOptionsValidator>());

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
	/// <see cref="AddDataProcessing(IServiceCollection, Func{IDbConnection}, IConfiguration, string, Assembly[])"/>.
	/// </remarks>
	public static IServiceCollection AddRecordHandler<THandler, TRecord>(this IServiceCollection services)
		where THandler : class, IRecordHandler<TRecord>
	{
		ArgumentNullException.ThrowIfNull(services);

		services.AddScoped<IRecordHandler<TRecord>, THandler>();
		RecordHandlerFactories.TryAdd(typeof(TRecord), sp => sp.GetRequiredService<THandler>());

		return services;
	}

	/// <summary>
	/// Registers a record handler implementation with configuration options.
	/// </summary>
	/// <typeparam name="THandler">The record handler type to register.</typeparam>
	/// <typeparam name="TRecord">The record type handled by the handler.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The data processing configuration.</param>
	/// <returns>The updated <see cref="IServiceCollection"/> instance.</returns>
	/// <remarks>
	/// <para>
	/// This overload registers <see cref="DataProcessingOptions"/> via the standard
	/// <c>IOptions&lt;T&gt;</c> pattern, eliminating the need to manually wrap in <c>Options.Create()</c>.
	/// Configuration is validated at startup via <c>ValidateDataAnnotations</c> and <c>ValidateOnStart</c>.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddRecordHandler&lt;MyHandler, MyRecord&gt;(new DataProcessingOptions
	/// {
	///     QueueSize = 128,
	///     ProducerBatchSize = 50,
	///     ConsumerBatchSize = 20
	/// });
	/// </code>
	/// </example>
	public static IServiceCollection AddRecordHandler<THandler, TRecord>(
		this IServiceCollection services,
		DataProcessingOptions configuration)
		where THandler : class, IRecordHandler<TRecord>
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		services.TryAddSingleton(Microsoft.Extensions.Options.Options.Create(configuration));
		services.AddOptions<DataProcessingOptions>()
			.ValidateDataAnnotations()
			.ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<DataProcessingOptions>, DataProcessingOptionsValidator>());
		services.AddScoped<IRecordHandler<TRecord>, THandler>();
		RecordHandlerFactories.TryAdd(typeof(TRecord), sp => sp.GetRequiredService<THandler>());

		return services;
	}

	/// <summary>
	/// Registers a record handler implementation with configuration bound from an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <typeparam name="THandler">The record handler type to register.</typeparam>
	/// <typeparam name="TRecord">The record type handled by the handler.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The application configuration.</param>
	/// <param name="sectionPath">The configuration section path to bind (e.g., "DataProcessing").</param>
	/// <returns>The updated <see cref="IServiceCollection"/> instance.</returns>
	/// <remarks>
	/// <para>
	/// This is the AOT-safe, appsettings-driven alternative. Uses
	/// <c>OptionsBuilder&lt;T&gt;.BindConfiguration()</c> with <c>ValidateDataAnnotations</c>
	/// and <c>ValidateOnStart</c> for fail-fast validation.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// // appsettings.json:
	/// // { "DataProcessing": { "QueueSize": 128, "ProducerBatchSize": 50 } }
	///
	/// services.AddRecordHandler&lt;MyHandler, MyRecord&gt;(configuration, "DataProcessing");
	/// </code>
	/// </example>
	public static IServiceCollection AddRecordHandler<THandler, TRecord>(
		this IServiceCollection services,
		IConfiguration configuration,
		string sectionPath)
		where THandler : class, IRecordHandler<TRecord>
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);
		ArgumentException.ThrowIfNullOrWhiteSpace(sectionPath);

		services.AddOptions<DataProcessingOptions>()
			.BindConfiguration(sectionPath)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<DataProcessingOptions>, DataProcessingOptionsValidator>());

		services.AddScoped<IRecordHandler<TRecord>, THandler>();
		RecordHandlerFactories.TryAdd(typeof(TRecord), sp => sp.GetRequiredService<THandler>());

		return services;
	}

	/// <summary>
	/// Adds the required services and configurations for data processing to the dependency injection container.
	/// </summary>
	/// <param name="services"> The <see cref="IServiceCollection" /> to add the services to. </param>
	/// <param name="connectionFactory"> A factory that creates database connections for data processing operations. </param>
	/// <param name="configuration"> The application configuration containing the required settings. </param>
	/// <param name="configurationSection"> The section of the configuration containing the data processing settings. </param>
	/// <param name="handlerAssemblies"> The assemblies to scan for <see cref="IDataProcessor" /> implementations. </param>
	/// <returns> The updated <see cref="IServiceCollection" /> instance. </returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown if <paramref name="connectionFactory" />, <paramref name="configuration" />, or <paramref name="handlerAssemblies" /> is <c> null </c>.
	/// </exception>
	/// <remarks>
	/// <para>
	/// The orchestration database connection factory is registered as a keyed singleton
	/// using <see cref="DataProcessingKeys.OrchestrationConnection"/>.
	/// Use <c>[FromKeyedServices(DataProcessingKeys.OrchestrationConnection)]</c>
	/// to resolve the orchestration connection.
	/// </para>
	/// </remarks>
	[RequiresUnreferencedCode("Assembly scanning may require unreferenced types for reflection-based type discovery")]
	[RequiresDynamicCode("Assembly scanning uses reflection to dynamically discover and register processor types")]
	public static IServiceCollection AddDataProcessing(
		this IServiceCollection services,
		Func<IDbConnection> connectionFactory,
		IConfiguration configuration,
		string configurationSection,
		params Assembly[] handlerAssemblies)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(connectionFactory);
		ArgumentNullException.ThrowIfNull(configuration);
		ArgumentException.ThrowIfNullOrWhiteSpace(configurationSection);
		ArgumentNullException.ThrowIfNull(handlerAssemblies);

		// Register orchestration connection as keyed singleton.
		// Use [FromKeyedServices(DataProcessingKeys.OrchestrationConnection)] to resolve.
		services.TryAddKeyedSingleton(DataProcessingKeys.OrchestrationConnection, (_, _) => connectionFactory);

		foreach (var processorType in DataProcessorDiscovery.DiscoverProcessors(handlerAssemblies))
		{
			_ = services.AddScoped(processorType);
			_ = services.AddScoped(typeof(IDataProcessor), processorType);
		}

		foreach (var (interfaceType, implementationType) in RecordHandlerDiscovery.DiscoverHandlers(handlerAssemblies))
		{
			_ = services.AddScoped(interfaceType, implementationType);
		}

		services.AddOptions<DataProcessingOptions>()
			.BindConfiguration(configurationSection)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<DataProcessingOptions>, DataProcessingOptionsValidator>());

		services.TryAddScoped<IDataProcessorRegistry>(static sp =>
		{
			var processors = sp.GetServices<IDataProcessor>() ?? [];
			return new DataProcessorRegistry(processors);
		});
		services.TryAddScoped<IDataOrchestrationManager, DataOrchestrationManager>();

		return services;
	}
}
