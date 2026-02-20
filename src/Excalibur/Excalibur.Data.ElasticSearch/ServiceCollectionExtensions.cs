// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Elastic.Clients.Elasticsearch;
using Elastic.Transport;

using Excalibur.Data.ElasticSearch;
using Excalibur.Data.ElasticSearch.IndexManagement;
using Excalibur.Data.ElasticSearch.Monitoring;
using Excalibur.Data.ElasticSearch.Projections;
using Excalibur.Data.ElasticSearch.Resilience;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides extension methods for configuring Elasticsearch-related services in the application's dependency injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
	/// <summary>
	/// Registers Elasticsearch services using a preconfigured <see cref="ElasticsearchClient" />.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="client"> The preconfigured <see cref="ElasticsearchClient" />. </param>
	/// <param name="registry"> A delegate to register additional services related to Elasticsearch. </param>
	/// <returns> The updated <see cref="IServiceCollection" />. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="client" /> is null. </exception>
	public static IServiceCollection AddElasticsearchServices(
		this IServiceCollection services,
		ElasticsearchClient client,
		Action<IServiceCollection>? registry)
	{
		ArgumentNullException.ThrowIfNull(client);

		services.TryAddSingleton(client);
		services.TryAddSingleton<IIndexInitializer, IndexInitializer>();
		services.TryAddSingleton<IElasticsearchHealthClient, ElasticsearchHealthClient>();

		registry?.Invoke(services);

		return services;
	}

	/// <summary>
	/// Registers the Elasticsearch client and related services with the dependency injection container.
	/// </summary>
	/// <param name="services"> The <see cref="IServiceCollection" /> to which services will be added. </param>
	/// <param name="configuration"> The application's <see cref="IConfiguration" /> containing the Elasticsearch configuration settings. </param>
	/// <param name="registry"> A delegate to register additional services related to Elasticsearch. </param>
	/// <param name="configureSettings">
	/// An optional delegate to further configure the <see cref="ElasticsearchClientSettings" /> before creating the client.
	/// </param>
	/// <returns> The updated <see cref="IServiceCollection" /> with Elasticsearch services registered. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="configuration" /> is <c> null </c>. </exception>
	[RequiresUnreferencedCode("Configuration binding may require unreferenced types for reflection-based operations")]
	[RequiresDynamicCode("Configuration binding uses reflection to dynamically access and populate configuration types")]
	public static IServiceCollection AddElasticsearchServices(
		this IServiceCollection services,
		IConfiguration configuration,
		Action<IServiceCollection>? registry,
		Action<ElasticsearchClientSettings>? configureSettings = null)
	{
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.Configure<ElasticsearchConfigurationOptions>(configuration.GetSection("ElasticSearch"));

		services.TryAddSingleton<IElasticsearchHealthClient, ElasticsearchHealthClient>();

		services.TryAddSingleton(sp =>
		{
			var elasticConfig = sp.GetRequiredService<IOptions<ElasticsearchConfigurationOptions>>().Value;
			var settings = CreateElasticsearchClientSettings(elasticConfig);

			configureSettings?.Invoke(settings);

			return new ElasticsearchClient(settings);
		});

		services.TryAddSingleton<IIndexInitializer, IndexInitializer>();

		registry?.Invoke(services);

		return services;
	}

	/// <summary>
	/// Registers resilient Elasticsearch services with retry policies, circuit breaker, and dead letter handling.
	/// </summary>
	/// <param name="services"> The <see cref="IServiceCollection" /> to which services will be added. </param>
	/// <param name="configuration"> The application's <see cref="IConfiguration" /> containing the Elasticsearch configuration settings. </param>
	/// <param name="registry"> A delegate to register additional services related to Elasticsearch. </param>
	/// <param name="configureSettings">
	/// An optional delegate to further configure the <see cref="ElasticsearchClientSettings" /> before creating the client.
	/// </param>
	/// <returns> The updated <see cref="IServiceCollection" /> with resilient Elasticsearch services registered. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="configuration" /> is <c> null </c>. </exception>
	[RequiresUnreferencedCode("This method uses reflection and may not work correctly with trimming")]
	[RequiresDynamicCode("This method uses dynamic code generation and may not work correctly with AOT")]
	public static IServiceCollection AddResilientElasticsearchServices(
		this IServiceCollection services,
		IConfiguration configuration,
		Action<IServiceCollection>? registry = null,
		Action<ElasticsearchClientSettings>? configureSettings = null)
	{
		ArgumentNullException.ThrowIfNull(configuration);

		// Register base Elasticsearch services
		_ = services.AddElasticsearchServices(configuration, registry, configureSettings);

		// Register resilience components
		services.TryAddSingleton<IElasticsearchRetryPolicy, ElasticsearchRetryPolicy>();
		services.TryAddSingleton<IElasticsearchCircuitBreaker, ElasticsearchCircuitBreaker>();

		// Register resilient client as primary client interface
		services.TryAddSingleton<IResilientElasticsearchClient, ResilientElasticsearchClient>();

		return services;
	}

	/// <summary>
	/// Registers comprehensive monitoring and diagnostics services for Elasticsearch operations.
	/// </summary>
	/// <param name="services"> The <see cref="IServiceCollection" /> to which services will be added. </param>
	/// <param name="configuration"> The application's <see cref="IConfiguration" /> containing the monitoring configuration settings. </param>
	/// <returns> The updated <see cref="IServiceCollection" /> with monitoring services registered. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="configuration" /> is <c> null </c>. </exception>
	[RequiresUnreferencedCode("Configuration binding may require unreferenced types for reflection-based operations")]
	[RequiresDynamicCode("Configuration binding uses reflection to dynamically access and populate configuration types")]
	public static IServiceCollection AddElasticsearchMonitoring(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(configuration);

		// Configure monitoring settings
		_ = services.Configure<ElasticsearchMonitoringOptions>(configuration.GetSection("ElasticSearch:Monitoring"));

		// Register monitoring services
		services.TryAddSingleton(static sp =>
		{
			var options = sp.GetRequiredService<IOptions<ElasticsearchMonitoringOptions>>();
			var settings = options.Value;
			return new ElasticsearchMetrics(
				settings.Tracing.ActivitySourceName,
				version: null,
				settings.Metrics.DurationHistogramBuckets);
		});

		services.TryAddSingleton(static sp =>
		{
			var options = sp.GetRequiredService<IOptions<ElasticsearchMonitoringOptions>>();
			var settings = options.Value;
			return new ElasticsearchActivitySource(settings.Tracing.ActivitySourceName);
		});

		services.TryAddSingleton<ElasticsearchRequestLogger>();
		services.TryAddSingleton<ElasticsearchPerformanceDiagnostics>();
		services.TryAddSingleton<ElasticsearchMonitoringService>();

		// Register health monitor as hosted service
		services.TryAddSingleton<ElasticsearchHealthMonitor>();
		_ = services.AddHostedService(static sp => sp.GetRequiredService<ElasticsearchHealthMonitor>());

		return services;
	}

	/// <summary>
	/// Registers monitored resilient Elasticsearch services with comprehensive observability features.
	/// </summary>
	/// <param name="services"> The <see cref="IServiceCollection" /> to which services will be added. </param>
	/// <param name="configuration"> The application's <see cref="IConfiguration" /> containing the Elasticsearch configuration settings. </param>
	/// <param name="registry"> A delegate to register additional services related to Elasticsearch. </param>
	/// <param name="configureSettings">
	/// An optional delegate to further configure the <see cref="ElasticsearchClientSettings" /> before creating the client.
	/// </param>
	/// <returns> The updated <see cref="IServiceCollection" /> with monitored resilient Elasticsearch services registered. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="configuration" /> is <c> null </c>. </exception>
	[RequiresUnreferencedCode("This method uses reflection and may not work correctly with trimming")]
	[RequiresDynamicCode("This method uses dynamic code generation and may not work correctly with AOT")]
	public static IServiceCollection AddMonitoredResilientElasticsearchServices(
		this IServiceCollection services,
		IConfiguration configuration,
		Action<IServiceCollection>? registry = null,
		Action<ElasticsearchClientSettings>? configureSettings = null)
	{
		ArgumentNullException.ThrowIfNull(configuration);

		// Register base Elasticsearch services
		_ = services.AddElasticsearchServices(configuration, registry, configureSettings);

		// Register monitoring services
		_ = services.AddElasticsearchMonitoring(configuration);

		// Register resilience components
		services.TryAddSingleton<IElasticsearchRetryPolicy, ElasticsearchRetryPolicy>();
		services.TryAddSingleton<IElasticsearchCircuitBreaker, ElasticsearchCircuitBreaker>();

		// Register monitored resilient client as primary client interface
		services.TryAddSingleton<IResilientElasticsearchClient, MonitoredResilientElasticsearchClient>();

		return services;
	}

	/// <summary>
	/// Registers resilient Elasticsearch services using a preconfigured <see cref="ElasticsearchClient" />.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="client"> The preconfigured <see cref="ElasticsearchClient" />. </param>
	/// <param name="resilienceOptions"> The resilience configuration options. </param>
	/// <param name="registry"> A delegate to register additional services related to Elasticsearch. </param>
	/// <returns> The updated <see cref="IServiceCollection" />. </returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown if <paramref name="client" /> or <paramref name="resilienceOptions" /> is null.
	/// </exception>
	public static IServiceCollection AddResilientElasticsearchServices(
		this IServiceCollection services,
		ElasticsearchClient client,
		ElasticsearchConfigurationOptions resilienceOptions,
		Action<IServiceCollection>? registry = null)
	{
		ArgumentNullException.ThrowIfNull(client);
		ArgumentNullException.ThrowIfNull(resilienceOptions);

		// Register base Elasticsearch services
		_ = services.AddElasticsearchServices(client, registry);

		// Register resilience configuration Configure resilience settings through the configuration builder rather than modifying the
		// already-created options object
		_ = services.AddSingleton<IPostConfigureOptions<ElasticsearchConfigurationOptions>>(sp =>
			new ElasticsearchConfigurationPostConfigure(
				sp.GetService<ILogger<ElasticsearchConfigurationOptions>>()));

		// Register resilience components
		services.TryAddSingleton<IElasticsearchRetryPolicy, ElasticsearchRetryPolicy>();
		services.TryAddSingleton<IElasticsearchCircuitBreaker, ElasticsearchCircuitBreaker>();

		// Register resilient client as primary client interface
		services.TryAddSingleton<IResilientElasticsearchClient, ResilientElasticsearchClient>();

		return services;
	}

	/// <summary>
	/// Registers an Elasticsearch repository in the dependency injection container.
	/// </summary>
	/// <typeparam name="TRepositoryInterface"> The interface type of the repository. </typeparam>
	/// <typeparam name="TRepository"> The concrete implementation type of the repository, which also implements <see cref="IInitializeElasticIndex" />. </typeparam>
	/// <param name="services"> The <see cref="IServiceCollection" /> to which the repository will be added. </param>
	/// <returns> The updated <see cref="IServiceCollection" /> with the repository registered. </returns>
	public static IServiceCollection AddRepository<TRepositoryInterface,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TRepository>(this IServiceCollection services)
		where TRepositoryInterface : class
		where TRepository : class, TRepositoryInterface, IInitializeElasticIndex
	{
		services.TryAddScoped<TRepositoryInterface, TRepository>();
		services.TryAddSingleton<IInitializeElasticIndex, TRepository>();

		return services;
	}

	/// <summary>
	/// Registers comprehensive index management services including templates, lifecycle, and operations.
	/// </summary>
	/// <param name="services"> The <see cref="IServiceCollection" /> to which services will be added. </param>
	/// <param name="configuration">
	/// The application's <see cref="IConfiguration" /> containing the index management configuration settings.
	/// </param>
	/// <returns> The updated <see cref="IServiceCollection" /> with index management services registered. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="configuration" /> is <c> null </c>. </exception>
	[RequiresUnreferencedCode("Configuration binding may require unreferenced types for reflection-based operations")]
	[RequiresDynamicCode("Configuration binding uses reflection to dynamically access and populate configuration types")]
	public static IServiceCollection AddElasticsearchIndexManagement(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(configuration);

		// Configure index management settings
		_ = services.Configure<IndexManagementOptions>(
			configuration.GetSection("ElasticSearch:IndexManagement"));

		// Register index management services
		services.TryAddSingleton<IIndexTemplateManager, IndexTemplateManager>();
		services.TryAddSingleton<IIndexLifecycleManager, IndexLifecycleManager>();
		services.TryAddSingleton<IIndexOperationsManager, IndexOperationsManager>();
		services.TryAddSingleton<IIndexAliasManager, IndexAliasManager>();

		return services;
	}

	/// <summary>
	/// Registers projection management services for ElasticSearch read models.
	/// </summary>
	/// <param name="services"> The <see cref="IServiceCollection" /> to which services will be added. </param>
	/// <param name="configuration"> The application's <see cref="IConfiguration" /> containing the projection configuration settings. </param>
	/// <returns> The updated <see cref="IServiceCollection" /> with projection services registered. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="configuration" /> is <c> null </c>. </exception>
	[RequiresUnreferencedCode("Configuration binding may require unreferenced types for reflection-based operations")]
	[RequiresDynamicCode("Configuration binding uses reflection to dynamically access and populate configuration types")]
	public static IServiceCollection AddElasticsearchProjections(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(configuration);

		// Configure projection settings
		_ = services.Configure<ProjectionOptions>(configuration.GetSection("ElasticSearch:Projections"));

		// Register cross-property validator
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<ProjectionOptions>, ProjectionOptionsValidator>());

		// Register projection error handler
		services.TryAddSingleton<IProjectionErrorHandler, ProjectionErrorHandler>();

		// Register projection rebuild manager
		services.TryAddSingleton<IProjectionRebuildManager, ProjectionRebuildManager>();

		// Register eventual consistency tracker
		services.TryAddSingleton<IEventualConsistencyTracker, EventualConsistencyTracker>();

		// Register schema evolution handler
		services.TryAddSingleton<ISchemaEvolutionHandler, SchemaEvolutionHandler>();
		return services;
	}

	/// <summary>
	/// Creates and configures an <see cref="ElasticsearchClientSettings" /> instance based on the provided configuration.
	/// </summary>
	/// <param name="config"> The Elasticsearch configuration settings. </param>
	/// <returns> A configured <see cref="ElasticsearchClientSettings" /> instance. </returns>
	/// <exception cref="InvalidOperationException"> Thrown when no valid connection configuration is provided. </exception>
	private static ElasticsearchClientSettings CreateElasticsearchClientSettings(ElasticsearchConfigurationOptions config)
	{
		ElasticsearchClientSettings settings;

		// Configure connection based on available options
		if (!string.IsNullOrWhiteSpace(config.CloudId))
		{
			// Use Elastic Cloud configuration
			settings = new ElasticsearchClientSettings(new Uri(config.CloudId));
		}
		else if (config.Urls?.Any() == true)
		{
			// Use cluster configuration with multiple nodes
			NodePool nodePool = config.ConnectionPoolType switch
			{
				ConnectionPoolType.Sniffing => new SniffingNodePool(config.Urls),
				ConnectionPoolType.Static => new StaticNodePool(config.Urls),
				_ => new StaticNodePool(config.Urls),
			};

			settings = new ElasticsearchClientSettings(nodePool);

			// Configure sniffing options if using sniffing pool
			if (config is { ConnectionPoolType: ConnectionPoolType.Sniffing, EnableSniffing: true })
			{
				// Sniffing configuration would be set here if needed
				// Note: v9 API may have different sniffing configuration method
			}
		}
		else if (config.Url is not null)
		{
			// Use single-node configuration
			settings = new ElasticsearchClientSettings(config.Url);
		}
		else
		{
			throw new InvalidOperationException(
				"Elasticsearch configuration must specify either CloudId, Urls (for cluster), or Url (for single node).");
		}

		// Configure authentication
		settings = ConfigureAuthentication(settings, config);

		// Configure SSL/TLS
		settings = ConfigureSslSettings(settings, config);

		// Configure timeouts and connection limits
		settings = ConfigureConnectionSettings(settings, config);

		return settings;
	}

	/// <summary>
	/// Configures authentication settings for the Elasticsearch client.
	/// </summary>
	/// <param name="settings"> The Elasticsearch client settings to configure. </param>
	/// <param name="config"> The configuration containing authentication options. </param>
	private static ElasticsearchClientSettings ConfigureAuthentication(
			ElasticsearchClientSettings settings,
			ElasticsearchConfigurationOptions config)
	{
		if (!string.IsNullOrWhiteSpace(config.Base64ApiKey))
		{
			return settings.Authentication(new Base64ApiKey(config.Base64ApiKey));
		}

		if (!string.IsNullOrWhiteSpace(config.ApiKey))
		{
			return settings.Authentication(new ApiKey(config.ApiKey));
		}

		if (!string.IsNullOrWhiteSpace(config.Username) && !string.IsNullOrWhiteSpace(config.Password))
		{
			return settings.Authentication(new BasicAuthentication(config.Username, config.Password));
		}

		return settings;
	}

	/// <summary>
	/// Configures SSL/TLS settings for the Elasticsearch client.
	/// </summary>
	/// <param name="settings"> The Elasticsearch client settings to configure. </param>
	/// <param name="config"> The configuration containing SSL/TLS options. </param>
	private static ElasticsearchClientSettings ConfigureSslSettings(
			ElasticsearchClientSettings settings,
			ElasticsearchConfigurationOptions config)
	{
		if (!string.IsNullOrWhiteSpace(config.CertificateFingerprint))
		{
			settings = settings.CertificateFingerprint(config.CertificateFingerprint);
		}

		if (config.DisableCertificateValidation)
		{
			settings = settings.ServerCertificateValidationCallback((_, _, _, _) => true);
		}

		return settings;
	}

	/// <summary>
	/// Configures connection settings such as timeouts and connection limits.
	/// </summary>
	/// <param name="settings"> The Elasticsearch client settings to configure. </param>
	/// <param name="config"> The configuration containing connection options. </param>
	private static ElasticsearchClientSettings ConfigureConnectionSettings(
			ElasticsearchClientSettings settings,
			ElasticsearchConfigurationOptions config)
	{
		settings = settings
				.RequestTimeout(config.RequestTimeout)
				.PingTimeout(config.PingTimeout)
				.ConnectionLimit(config.MaximumConnectionsPerNode);

		if (config.ConnectionPoolType == ConnectionPoolType.Sniffing && config.EnableSniffing)
		{
			settings = settings
					.SniffOnStartup(true)
					.SniffOnConnectionFault(true)
					.SniffLifeSpan(config.SniffingInterval);
		}

		return settings;
	}
}

/// <summary>
/// Post-configures Elasticsearch settings to warn about missing resilience configuration.
/// </summary>
internal sealed class ElasticsearchConfigurationPostConfigure(
	ILogger<ElasticsearchConfigurationOptions>? logger) : IPostConfigureOptions<ElasticsearchConfigurationOptions>
{
	/// <inheritdoc/>
	public void PostConfigure(string? name, ElasticsearchConfigurationOptions options)
	{
		if (options.Resilience == null)
		{
			logger?.LogWarning("Elasticsearch resilience settings are not configured. Using defaults from resilience options.");
		}
	}
}
