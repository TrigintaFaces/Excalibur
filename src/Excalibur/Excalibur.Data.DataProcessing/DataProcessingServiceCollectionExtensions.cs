// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Globalization;

using Excalibur.Data.DataProcessing;
using Excalibur.Data.DataProcessing.Diagnostics;
using Excalibur.Data.DataProcessing.Processing;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

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
	/// <see cref="AddDataProcessing(IServiceCollection, Func{IDbConnection}, IConfiguration, string, Assembly[])"/>.
	/// </remarks>
	public static IServiceCollection AddDataProcessor<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TProcessor>(this IServiceCollection services)
		where TProcessor : class, IDataProcessor
	{
		ArgumentNullException.ThrowIfNull(services);

		// Register both the concrete type and interface so DataProcessorRegistry
		// can resolve by concrete type within a scope (matching the builder pattern).
		services.AddScoped<TProcessor>();
		services.AddScoped<IDataProcessor, TProcessor>(sp => sp.GetRequiredService<TProcessor>());

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
	/// Configuration is validated at startup via <c>IValidateOptions&lt;T&gt;</c> and <c>ValidateOnStart</c>.
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
	public static IServiceCollection AddDataProcessor<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TProcessor>(
		this IServiceCollection services,
		DataProcessingOptions configuration)
		where TProcessor : class, IDataProcessor
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		RegisterSuppliedOptions(services, configuration);
		services.AddOptions<DataProcessingOptions>()
			.ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<DataProcessingOptions>, DataProcessingOptionsValidator>());
		// Register both the concrete type and interface so DataProcessorRegistry
		// can resolve by concrete type within a scope (matching the builder pattern).
		services.AddScoped<TProcessor>();
		services.AddScoped<IDataProcessor, TProcessor>(sp => sp.GetRequiredService<TProcessor>());

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
	/// <c>an options factory that initializes immutable properties</c> with <c>IValidateOptions&lt;T&gt;</c>
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
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddDataProcessor<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TProcessor>(
		this IServiceCollection services,
		IConfiguration configuration,
		string sectionPath)
		where TProcessor : class, IDataProcessor
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);
		ArgumentException.ThrowIfNullOrWhiteSpace(sectionPath);

        RegisterConfiguredOptions(services, _ => configuration.GetSection(sectionPath));
        services.AddOptions<DataProcessingOptions>().ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<DataProcessingOptions>, DataProcessingOptionsValidator>());

		// Register both the concrete type and interface so DataProcessorRegistry
		// can resolve by concrete type within a scope (matching the builder pattern).
		services.AddScoped<TProcessor>();
		services.AddScoped<IDataProcessor, TProcessor>(sp => sp.GetRequiredService<TProcessor>());

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
	public static IServiceCollection AddRecordHandler<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler, TRecord>(this IServiceCollection services)
		where THandler : class, IRecordHandler<TRecord>
	{
		ArgumentNullException.ThrowIfNull(services);

		services.AddScoped<IRecordHandler<TRecord>, THandler>();

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
	/// Configuration is validated at startup via <c>IValidateOptions&lt;T&gt;</c> and <c>ValidateOnStart</c>.
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
	public static IServiceCollection AddRecordHandler<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler, TRecord>(
		this IServiceCollection services,
		DataProcessingOptions configuration)
		where THandler : class, IRecordHandler<TRecord>
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		RegisterSuppliedOptions(services, configuration);
		services.AddOptions<DataProcessingOptions>()
			.ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<DataProcessingOptions>, DataProcessingOptionsValidator>());
		services.AddScoped<IRecordHandler<TRecord>, THandler>();

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
	/// <c>an options factory that initializes immutable properties</c> with <c>IValidateOptions&lt;T&gt;</c>
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
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddRecordHandler<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler, TRecord>(
		this IServiceCollection services,
		IConfiguration configuration,
		string sectionPath)
		where THandler : class, IRecordHandler<TRecord>
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);
		ArgumentException.ThrowIfNullOrWhiteSpace(sectionPath);

        RegisterConfiguredOptions(services, _ => configuration.GetSection(sectionPath));
        services.AddOptions<DataProcessingOptions>().ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<DataProcessingOptions>, DataProcessingOptionsValidator>());

		services.AddScoped<IRecordHandler<TRecord>, THandler>();

		return services;
	}

	/// <summary>
	/// Adds data processing services using the fluent builder pattern.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure the data processing builder.</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="services"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddDataProcessing(dp =&gt;
	/// {
	///     dp.ConnectionFactory(() =&gt; new SqlConnection(connectionString))
	///       .BindConfiguration("DataProcessing")
	///       .AddProcessor&lt;OrderProcessor&gt;()
	///       .AddRecordHandler&lt;OrderHandler, OrderRecord&gt;()
	///       .EnableBackgroundProcessing();
	/// });
	/// </code>
	/// </example>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Configuration binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	public static IServiceCollection AddDataProcessing(
		this IServiceCollection services,
		Action<IDataProcessingBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		var builder = new DataProcessingBuilder(services);
		configure(builder);

		// Resolve connection factory
		Func<IServiceProvider, Func<IDbConnection>> connectionFactory;
		if (builder.DependencyAwareConnectionFactory is not null)
		{
			connectionFactory = builder.DependencyAwareConnectionFactory;
		}
		else if (builder.SimpleConnectionFactory is not null)
		{
			var simpleFactory = builder.SimpleConnectionFactory;
			connectionFactory = _ => simpleFactory;
		}
		else
		{
			// No connection factory — services will fail at resolution time if needed
			connectionFactory = _ => throw new InvalidOperationException(
				"No connection configured for DataProcessing. " +
				"Call ConnectionFactory() inside AddDataProcessing().");
		}

		// Register orchestration connection as keyed singleton
		services.TryAddKeyedSingleton(DataProcessingKeys.OrchestrationConnection,
			(sp, _) => connectionFactory(sp));

		// Register BindConfiguration if set
		if (builder.BindConfigurationPath is not null)
		{
            RegisterConfiguredOptions(services, sp => sp.GetRequiredService<IConfiguration>().GetSection(builder.BindConfigurationPath));
            services.AddOptions<DataProcessingOptions>().ValidateOnStart();
			services.TryAddEnumerable(
				ServiceDescriptor.Singleton<IValidateOptions<DataProcessingOptions>, DataProcessingOptionsValidator>());
		}

		// Register core services
		services.TryAddScoped<IDataProcessorRegistry>(static sp =>
		{
			var processors = sp.GetServices<IDataProcessor>() ?? [];
			return new DataProcessorRegistry(processors);
		});
		services.TryAddScoped<IDataOrchestrationManager, DataOrchestrationManager>();

		// Register background processing if enabled
		if (builder.BackgroundProcessingEnabled)
		{
			var bgConfigure = builder.BackgroundProcessingConfigure;
			var bgOptionsBuilder = services.AddOptions<DataProcessingHostedServiceOptions>()
				.ValidateOnStart();

			if (bgConfigure is not null)
			{
				_ = bgOptionsBuilder.Configure(bgConfigure);
			}

			services.TryAddEnumerable(
				ServiceDescriptor.Singleton<IValidateOptions<DataProcessingHostedServiceOptions>,
					DataProcessingHostedServiceOptionsValidator>());
			services.TryAddSingleton<DataProcessingHealthState>();
			services.TryAddEnumerable(
				ServiceDescriptor.Singleton<IHostedService, DataProcessingHostedService>());
		}

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
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
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
			// Register both the concrete type and interface so DataProcessorRegistry
			// can resolve by concrete type within a scope (matching the builder pattern).
			_ = services.AddScoped(processorType);
			_ = services.AddScoped(typeof(IDataProcessor), sp => sp.GetRequiredService(processorType));
		}

		foreach (var (interfaceType, implementationType) in RecordHandlerDiscovery.DiscoverHandlers(handlerAssemblies))
		{
			_ = services.AddScoped(interfaceType, implementationType);
		}

        RegisterConfiguredOptions(services, _ => configuration.GetSection(configurationSection));
        services.AddOptions<DataProcessingOptions>().ValidateOnStart();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<DataProcessingOptions>, DataProcessingOptionsValidator>());

		// Assembly-scanning path: use reflection fallback for record type discovery
		// since processors discovered via assembly scanning may use runtime RecordType property
		// instead of [DataTaskRecordType] attribute.
		services.TryAddScoped<IDataProcessorRegistry>(static sp =>
		{
			var processors = sp.GetServices<IDataProcessor>() ?? [];
			return new DataProcessorRegistry(processors, useReflectionFallback: true);
		});
		services.TryAddScoped<IDataOrchestrationManager, DataOrchestrationManager>();

		return services;
	}

	/// <summary>
	/// Enables a background hosted service that polls for pending data tasks
	/// and processes them through the registered <see cref="IDataOrchestrationManager"/>.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Optional configuration action for hosted service options.</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// This is the alternative to running data processing via Quartz jobs
	/// (<c>DataProcessingJob</c>). It registers a <see cref="BackgroundService"/>
	/// that polls on a configurable interval.
	/// </para>
	/// <para>
	/// This method works with both the assembly-scanning registration path
	/// (<see cref="AddDataProcessing(IServiceCollection, Action{IDataProcessingBuilder})"/>) and the AOT-safe explicit registration
	/// path (<see cref="AddDataProcessor{TProcessor}(IServiceCollection)"/>).
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// // Enable with defaults (5s polling interval)
	/// services.EnableDataProcessingBackgroundService();
	///
	/// // Enable with custom options
	/// services.EnableDataProcessingBackgroundService(options =>
	/// {
	///     options.PollingInterval = TimeSpan.FromSeconds(10);
	///     options.DrainTimeoutSeconds = 60;
	///     options.UnhealthyThreshold = 5;
	/// });
	/// </code>
	/// </example>
	public static IServiceCollection EnableDataProcessingBackgroundService(
		this IServiceCollection services,
		Action<DataProcessingHostedServiceOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		var optionsBuilder = services.AddOptions<DataProcessingHostedServiceOptions>()
			.ValidateOnStart();

		if (configure is not null)
		{
			_ = optionsBuilder.Configure(configure);
		}

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<DataProcessingHostedServiceOptions>,
				DataProcessingHostedServiceOptionsValidator>());

		services.TryAddSingleton<DataProcessingHealthState>();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IHostedService, DataProcessingHostedService>());

		return services;
	}

	/// <summary>
	/// Enables a background hosted service with options bound from an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The application configuration.</param>
	/// <param name="sectionPath">The configuration section path to bind (e.g., "DataProcessingService").</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// This is the AOT-safe, appsettings-driven alternative. Uses
	/// <c>an options factory that initializes immutable properties</c> with <c>IValidateOptions&lt;T&gt;</c>
	/// and <c>ValidateOnStart</c> for fail-fast validation.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// // appsettings.json:
	/// // { "DataProcessingService": { "PollingInterval": "00:00:10", "DrainTimeoutSeconds": 60 } }
	///
	/// services.EnableDataProcessingBackgroundService(configuration, "DataProcessingService");
	/// </code>
	/// </example>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection EnableDataProcessingBackgroundService(
		this IServiceCollection services,
		IConfiguration configuration,
		string sectionPath)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);
		ArgumentException.ThrowIfNullOrWhiteSpace(sectionPath);

		services.AddOptions<DataProcessingHostedServiceOptions>()
			.Bind(configuration.GetSection(sectionPath))
			.ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<DataProcessingHostedServiceOptions>,
				DataProcessingHostedServiceOptionsValidator>());

		services.TryAddSingleton<DataProcessingHealthState>();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IHostedService, DataProcessingHostedService>());

		return services;
	}
    private sealed record ProcessingConfiguration(IConfiguration Section);
    private sealed record SuppliedProcessingOptions(DataProcessingOptions Value);

    private static void RegisterOptionsFactory(IServiceCollection services)
    {
        services.TryAddTransient<IOptionsFactory<DataProcessingOptions>>(sp => new SuppliedOptionsFactory(() =>
        {
            var options = sp.GetService<SuppliedProcessingOptions>()?.Value ?? new DataProcessingOptions();
            foreach (var source in sp.GetServices<ProcessingConfiguration>())
            {
                options = ReadConfiguration(source.Section, options);
            }
            return options;
        }, sp.GetServices<IConfigureOptions<DataProcessingOptions>>(),
            sp.GetServices<IPostConfigureOptions<DataProcessingOptions>>(),
            sp.GetServices<IValidateOptions<DataProcessingOptions>>()));
    }

    private static void RegisterConfiguredOptions(IServiceCollection services, Func<IServiceProvider, IConfigurationSection> sectionFactory)
    {
        RegisterOptionsFactory(services);
        services.AddSingleton(sp => new ProcessingConfiguration(sectionFactory(sp)));
        services.AddSingleton<IOptionsChangeTokenSource<DataProcessingOptions>>(sp =>
            new ConfigurationChangeTokenSource<DataProcessingOptions>(Options.Options.DefaultName, sectionFactory(sp)));
    }

    // Init-only options cannot be populated by the generated Bind(existingInstance) path.
    // Construct them explicitly so managed and Native AOT hosts use the same values.
    private static DataProcessingOptions ReadConfiguration(IConfiguration configuration, DataProcessingOptions defaults)
    {
        return new DataProcessingOptions
        {
            SchemaName = configuration[nameof(DataProcessingOptions.SchemaName)] ?? defaults.SchemaName,
            TableName = configuration[nameof(DataProcessingOptions.TableName)] ?? defaults.TableName,
            DispatcherTimeoutMilliseconds = ReadInt(configuration, nameof(DataProcessingOptions.DispatcherTimeoutMilliseconds), defaults.DispatcherTimeoutMilliseconds),
            MaxAttempts = ReadInt(configuration, nameof(DataProcessingOptions.MaxAttempts), defaults.MaxAttempts),
            QueueSize = ReadInt(configuration, nameof(DataProcessingOptions.QueueSize), defaults.QueueSize),
            ProducerBatchSize = ReadInt(configuration, nameof(DataProcessingOptions.ProducerBatchSize), defaults.ProducerBatchSize),
            ConsumerBatchSize = ReadInt(configuration, nameof(DataProcessingOptions.ConsumerBatchSize), defaults.ConsumerBatchSize),
        };
    }

    private static int ReadInt(IConfiguration configuration, string key, int fallback) =>
        configuration[key] is { } value ? int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture) : fallback;

    private static void RegisterSuppliedOptions(IServiceCollection services, DataProcessingOptions configuration)
    {
        RegisterOptionsFactory(services);
        services.TryAddSingleton(new SuppliedProcessingOptions(configuration));
    }

    private sealed class SuppliedOptionsFactory(
        Func<DataProcessingOptions> seed,
        IEnumerable<IConfigureOptions<DataProcessingOptions>> configure,
        IEnumerable<IPostConfigureOptions<DataProcessingOptions>> postConfigure,
        IEnumerable<IValidateOptions<DataProcessingOptions>> validate)
        : OptionsFactory<DataProcessingOptions>(configure, postConfigure, validate)
    {
        protected override DataProcessingOptions CreateInstance(string name)
        {
            if (name != Options.Options.DefaultName)
            {
                return new DataProcessingOptions();
            }
            var supplied = seed();
            return new DataProcessingOptions
            {
                SchemaName = supplied.SchemaName,
                TableName = supplied.TableName,
                DispatcherTimeoutMilliseconds = supplied.DispatcherTimeoutMilliseconds,
                MaxAttempts = supplied.MaxAttempts,
                QueueSize = supplied.QueueSize,
                ProducerBatchSize = supplied.ProducerBatchSize,
                ConsumerBatchSize = supplied.ConsumerBatchSize,
            };
        }
    }

}
