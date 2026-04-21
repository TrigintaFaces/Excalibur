// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.OpenSearch.Projections;
using Excalibur.EventSourcing.Abstractions;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using OpenSearch.Client;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering OpenSearch projection store services.
/// </summary>
/// <remarks>
/// <para>
/// Each projection type gets its own named options instance keyed by the projection type name.
/// This allows multiple projection stores to coexist with independent configurations.
/// </para>
/// </remarks>
public static class OpenSearchProjectionStoreExtensions
{
	/// <summary>
	/// Adds the OpenSearch projection store to the service collection.
	/// </summary>
	/// <typeparam name="TProjection">The projection type to store.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="configureOptions">Action to configure projection store options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddOpenSearchProjectionStore<TProjection>(
		this IServiceCollection services,
		Action<OpenSearchProjectionStoreOptions> configureOptions)
		where TProjection : class
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configureOptions);

		var optionsName = typeof(TProjection).Name;
		_ = services.Configure(optionsName, configureOptions);

		services.TryAddScoped<IProjectionStore<TProjection>>(sp =>
		{
			var optionsMonitor = sp.GetRequiredService<IOptionsMonitor<OpenSearchProjectionStoreOptions>>();
			var logger = sp.GetRequiredService<ILogger<OpenSearchProjectionStore<TProjection>>>();
			return new OpenSearchProjectionStore<TProjection>(optionsMonitor, logger);
		});

		return services;
	}

	/// <summary>
	/// Adds the OpenSearch projection store with a node URI.
	/// </summary>
	/// <typeparam name="TProjection">The projection type to store.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="nodeUri">The OpenSearch node URI.</param>
	/// <param name="configureOptions">Optional action to further configure options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddOpenSearchProjectionStore<TProjection>(
		this IServiceCollection services,
		string nodeUri,
		Action<OpenSearchProjectionStoreOptions>? configureOptions = null)
		where TProjection : class
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(nodeUri);

		return services.AddOpenSearchProjectionStore<TProjection>(options =>
		{
			options.NodeUri = nodeUri;
			configureOptions?.Invoke(options);
		});
	}

	/// <summary>
	/// Adds the OpenSearch projection store with an existing client factory.
	/// </summary>
	/// <typeparam name="TProjection">The projection type to store.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="clientFactory">Factory function that provides an OpenSearch client.</param>
	/// <param name="configureOptions">Action to configure projection store options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddOpenSearchProjectionStore<TProjection>(
		this IServiceCollection services,
		Func<IServiceProvider, OpenSearchClient> clientFactory,
		Action<OpenSearchProjectionStoreOptions> configureOptions)
		where TProjection : class
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(clientFactory);
		ArgumentNullException.ThrowIfNull(configureOptions);

		var optionsName = typeof(TProjection).Name;
		_ = services.Configure(optionsName, configureOptions);

		services.TryAddScoped<IProjectionStore<TProjection>>(sp =>
		{
			var client = clientFactory(sp);
			var optionsMonitor = sp.GetRequiredService<IOptionsMonitor<OpenSearchProjectionStoreOptions>>();
			var logger = sp.GetRequiredService<ILogger<OpenSearchProjectionStore<TProjection>>>();
			return new OpenSearchProjectionStore<TProjection>(client, optionsMonitor, logger);
		});

		return services;
	}

	/// <summary>
	/// Registers multiple OpenSearch projection stores with a shared node URI.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="nodeUri">The shared OpenSearch node URI.</param>
	/// <param name="configure">Action to register individual projection stores.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddOpenSearchProjections(
		this IServiceCollection services,
		string nodeUri,
		Action<OpenSearchProjectionRegistrar> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(nodeUri);
		ArgumentNullException.ThrowIfNull(configure);

		var registrar = new OpenSearchProjectionRegistrar(services, nodeUri);
		configure(registrar);

		return services;
	}

	/// <summary>
	/// Registers multiple OpenSearch projection stores with shared options configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configureShared">Shared options applied to all projections.</param>
	/// <param name="configure">Action to register individual projection stores.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddOpenSearchProjections(
		this IServiceCollection services,
		Action<OpenSearchProjectionStoreOptions> configureShared,
		Action<OpenSearchProjectionRegistrar> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configureShared);
		ArgumentNullException.ThrowIfNull(configure);

		var registrar = new OpenSearchProjectionRegistrar(services, configureShared);
		configure(registrar);

		return services;
	}
}
