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

		// Configure options
		_ = services.Configure(configureOptions);

		// Register projection store
		services.TryAddScoped<IProjectionStore<TProjection>>(sp =>
		{
			var options = sp.GetRequiredService<IOptions<ElasticSearchProjectionStoreOptions>>();
			var logger = sp.GetRequiredService<ILogger<ElasticSearchProjectionStore<TProjection>>>();

			return new ElasticSearchProjectionStore<TProjection>(options, logger);
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

		// Configure options
		_ = services.Configure(configureOptions);

		// Register projection store with client factory
		services.TryAddScoped<IProjectionStore<TProjection>>(sp =>
		{
			var client = clientFactory(sp);
			var options = sp.GetRequiredService<IOptions<ElasticSearchProjectionStoreOptions>>();
			var logger = sp.GetRequiredService<ILogger<ElasticSearchProjectionStore<TProjection>>>();

			return new ElasticSearchProjectionStore<TProjection>(client, options, logger);
		});

		return services;
	}
}
