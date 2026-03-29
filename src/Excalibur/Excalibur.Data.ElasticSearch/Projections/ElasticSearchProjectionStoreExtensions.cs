// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Elastic.Clients.Elasticsearch;

using Excalibur.Data.ElasticSearch.Projections;
using Excalibur.EventSourcing.Abstractions;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering ElasticSearch projection store services.
/// </summary>
/// <remarks>
/// <para>
/// Each projection type gets its own named options instance keyed by the projection type name.
/// This allows multiple projection stores to coexist with independent configurations
/// (different URIs, index prefixes, shard counts, etc.).
/// </para>
/// </remarks>
public static class ElasticSearchProjectionStoreExtensions
{
	/// <summary>
	/// Adds the ElasticSearch projection store to the service collection.
	/// </summary>
	/// <typeparam name="TProjection">The projection type to store.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="configureOptions">Action to configure projection store options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddElasticSearchProjectionStore<TProjection>(
		this IServiceCollection services,
		Action<ElasticSearchProjectionStoreOptions> configureOptions)
		where TProjection : class
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configureOptions);

		// Configure named options keyed by projection type name
		var optionsName = typeof(TProjection).Name;
		_ = services.Configure(optionsName, configureOptions);

		// Register projection store -- uses IOptionsMonitor to resolve named options
		services.TryAddScoped<IProjectionStore<TProjection>>(sp =>
		{
			var optionsMonitor = sp.GetRequiredService<IOptionsMonitor<ElasticSearchProjectionStoreOptions>>();
			var logger = sp.GetRequiredService<ILogger<ElasticSearchProjectionStore<TProjection>>>();

			return new ElasticSearchProjectionStore<TProjection>(optionsMonitor, logger);
		});

		return services;
	}

	/// <summary>
	/// Adds the ElasticSearch projection store to the service collection with a node URI.
	/// </summary>
	/// <typeparam name="TProjection">The projection type to store.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="nodeUri">The ElasticSearch node URI.</param>
	/// <param name="configureOptions">Optional action to further configure projection store options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddElasticSearchProjectionStore<TProjection>(
		this IServiceCollection services,
		string nodeUri,
		Action<ElasticSearchProjectionStoreOptions>? configureOptions = null)
		where TProjection : class
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(nodeUri);

		return services.AddElasticSearchProjectionStore<TProjection>(options =>
		{
			options.NodeUri = nodeUri;
			configureOptions?.Invoke(options);
		});
	}

	/// <summary>
	/// Adds the ElasticSearch projection store to the service collection with an existing client.
	/// </summary>
	/// <typeparam name="TProjection">The projection type to store.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="clientFactory">Factory function that provides an ElasticSearch client.</param>
	/// <param name="configureOptions">Action to configure projection store options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// Use this overload for advanced scenarios like shared client instances,
	/// custom connection pooling, or integration with existing ElasticSearch infrastructure.
	/// </remarks>
	public static IServiceCollection AddElasticSearchProjectionStore<TProjection>(
		this IServiceCollection services,
		Func<IServiceProvider, ElasticsearchClient> clientFactory,
		Action<ElasticSearchProjectionStoreOptions> configureOptions)
		where TProjection : class
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(clientFactory);
		ArgumentNullException.ThrowIfNull(configureOptions);

		// Configure named options keyed by projection type name
		var optionsName = typeof(TProjection).Name;
		_ = services.Configure(optionsName, configureOptions);

		// Register projection store with client factory
		services.TryAddScoped<IProjectionStore<TProjection>>(sp =>
		{
			var client = clientFactory(sp);
			var optionsMonitor = sp.GetRequiredService<IOptionsMonitor<ElasticSearchProjectionStoreOptions>>();
			var logger = sp.GetRequiredService<ILogger<ElasticSearchProjectionStore<TProjection>>>();

			return new ElasticSearchProjectionStore<TProjection>(client, optionsMonitor, logger);
		});

		return services;
	}

	/// <summary>
	/// Registers multiple ElasticSearch projection stores that share a common node URI,
	/// reducing boilerplate when multiple projections target the same cluster.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="nodeUri">The shared ElasticSearch node URI.</param>
	/// <param name="configure">Action to register individual projection stores.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <example>
	/// <code>
	/// services.AddElasticSearchProjections("https://es.example.com:9200", projections =>
	/// {
	///     projections.Add&lt;OrderSummary&gt;();
	///     projections.Add&lt;CustomerProfile&gt;(options => options.IndexPrefix = "customers");
	///     projections.Add&lt;ProductCatalog&gt;(options => options.NumberOfShards = 3);
	/// });
	/// </code>
	/// </example>
	public static IServiceCollection AddElasticSearchProjections(
		this IServiceCollection services,
		string nodeUri,
		Action<ElasticSearchProjectionRegistrar> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(nodeUri);
		ArgumentNullException.ThrowIfNull(configure);

		var registrar = new ElasticSearchProjectionRegistrar(services, nodeUri);
		configure(registrar);

		return services;
	}

	/// <summary>
	/// Registers multiple ElasticSearch projection stores with shared options configuration,
	/// allowing advanced settings like custom node URIs, index prefixes, and shard counts.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configureShared">Shared options applied to all projections before per-projection overrides.</param>
	/// <param name="configure">Action to register individual projection stores.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <example>
	/// <code>
	/// services.AddElasticSearchProjections(
	///     shared => { shared.NodeUri = "https://es.example.com:9200"; shared.NumberOfReplicas = 2; },
	///     projections =>
	///     {
	///         projections.Add&lt;OrderSummary&gt;();
	///         projections.Add&lt;CustomerProfile&gt;(options => options.IndexPrefix = "customers");
	///     });
	/// </code>
	/// </example>
	public static IServiceCollection AddElasticSearchProjections(
		this IServiceCollection services,
		Action<ElasticSearchProjectionStoreOptions> configureShared,
		Action<ElasticSearchProjectionRegistrar> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configureShared);
		ArgumentNullException.ThrowIfNull(configure);

		var registrar = new ElasticSearchProjectionRegistrar(services, configureShared);
		configure(registrar);

		return services;
	}
}
