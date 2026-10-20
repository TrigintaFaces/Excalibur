// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Configuration;
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
	private readonly IConfiguration? _section;

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

	internal OpenSearchProjectionRegistrar(IServiceCollection services, IConfiguration section)
	{
		_services = services;
		_section = section;
	}

	/// <summary>
	/// Adds a projection store for the specified type using the shared configuration.
	/// </summary>
	/// <typeparam name="TProjection">The projection type to store.</typeparam>
	/// <param name="configureOptions">Optional action to override per-projection options.</param>
	/// <returns>This registrar for fluent chaining.</returns>
	[UnconditionalSuppressMessage("Trimming", "IL2026",
		Justification = "Only the configuration-bound branch reaches the reflective binder, and the annotated public entry point that selects that branch already warns the consumer. The node-uri and shared-action branches bind nothing.")]
	[UnconditionalSuppressMessage("AOT", "IL3050",
		Justification = "Only the configuration-bound branch reaches the reflective binder, and the annotated public entry point that selects that branch already warns the consumer. The node-uri and shared-action branches bind nothing.")]
	public OpenSearchProjectionRegistrar Add<TProjection>(
		Action<OpenSearchProjectionStoreOptions>? configureOptions = null)
		where TProjection : class
	{
		if (_section is not null)
		{
			// The section binds into this projection's own named options, then the per-projection action
			// overrides it -- the same precedence as the single-projection overload.
			_ = _services.AddOpenSearchProjectionStore<TProjection>(_section, configureOptions);
			return this;
		}

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
