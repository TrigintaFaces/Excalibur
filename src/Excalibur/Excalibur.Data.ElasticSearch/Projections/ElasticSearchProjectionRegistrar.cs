// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Configuration;
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
	private readonly IConfiguration? _section;

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
	/// Initializes a new instance of the <see cref="ElasticSearchProjectionRegistrar"/> class with a
	/// configuration section describing the shared cluster.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="section">The configuration section describing the shared cluster.</param>
	internal ElasticSearchProjectionRegistrar(IServiceCollection services, IConfiguration section)
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
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Only the configuration-bound branch binds reflectively, and the entry point that supplies the section -- AddElasticSearchProjections(IConfiguration, ...) -- carries the annotation, so a consumer has already been warned at the door they used. Annotating this method instead would warn the node-uri and action paths, which bind nothing.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "See the IL2026 justification above.")]
	public ElasticSearchProjectionRegistrar Add<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.Interfaces)] TProjection>(
		Action<ElasticSearchProjectionStoreOptions>? configureOptions = null)
		where TProjection : class
	{
		if (_section is not null)
		{
			// The section binds into this projection's own named options, then the per-projection
			// action overrides it -- the same precedence as the single-projection overload.
			_ = _services.AddElasticSearchProjectionStore<TProjection>(_section, configureOptions);
			return this;
		}

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
