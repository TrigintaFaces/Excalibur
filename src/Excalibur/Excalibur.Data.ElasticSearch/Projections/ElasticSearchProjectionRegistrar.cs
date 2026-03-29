// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Data.ElasticSearch.Projections;

/// <summary>
/// Registrar for adding multiple ElasticSearch projection stores that share
/// a common node URI. Used with <c>AddElasticSearchProjections</c>.
/// </summary>
/// <remarks>
/// Each projection type gets its own named options instance, so per-projection
/// overrides (index prefix, shard count, etc.) are fully isolated.
/// </remarks>
public sealed class ElasticSearchProjectionRegistrar
{
	private readonly IServiceCollection _services;
	private readonly string? _nodeUri;
	private readonly Action<ElasticSearchProjectionStoreOptions>? _configureShared;

	/// <summary>
	/// Initializes a new instance of the <see cref="ElasticSearchProjectionRegistrar"/> class
	/// with an explicit node URI.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="nodeUri">The shared ElasticSearch node URI.</param>
	internal ElasticSearchProjectionRegistrar(IServiceCollection services, string nodeUri)
	{
		_services = services;
		_nodeUri = nodeUri;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ElasticSearchProjectionRegistrar"/> class
	/// with a shared options configuration action.
	/// </summary>
	internal ElasticSearchProjectionRegistrar(IServiceCollection services, Action<ElasticSearchProjectionStoreOptions> configureShared)
	{
		_services = services;
		_configureShared = configureShared;
	}

	/// <summary>
	/// Adds a projection store for the specified type using the shared configuration.
	/// </summary>
	/// <typeparam name="TProjection">The projection type to store.</typeparam>
	/// <param name="configureOptions">Optional action to override per-projection options.</param>
	/// <returns>This registrar for fluent chaining.</returns>
	public ElasticSearchProjectionRegistrar Add<TProjection>(
		Action<ElasticSearchProjectionStoreOptions>? configureOptions = null)
		where TProjection : class
	{
		_services.AddElasticSearchProjectionStore<TProjection>(options =>
		{
			if (_configureShared != null)
			{
				_configureShared(options);
			}
			else
			{
				options.NodeUri = _nodeUri!;
			}

			configureOptions?.Invoke(options);
		});

		return this;
	}
}
