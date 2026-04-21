// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Data.OpenSearch.Projections;

/// <summary>
/// Registrar for adding multiple OpenSearch projection stores that share
/// a common node URI. Used with <c>AddOpenSearchProjections</c>.
/// </summary>
public sealed class OpenSearchProjectionRegistrar
{
	private readonly IServiceCollection _services;
	private readonly string? _nodeUri;
	private readonly Action<OpenSearchProjectionStoreOptions>? _configureShared;

	internal OpenSearchProjectionRegistrar(IServiceCollection services, string nodeUri)
	{
		_services = services;
		_nodeUri = nodeUri;
	}

	internal OpenSearchProjectionRegistrar(IServiceCollection services, Action<OpenSearchProjectionStoreOptions> configureShared)
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
	public OpenSearchProjectionRegistrar Add<TProjection>(
		Action<OpenSearchProjectionStoreOptions>? configureOptions = null)
		where TProjection : class
	{
		_services.AddOpenSearchProjectionStore<TProjection>(options =>
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
