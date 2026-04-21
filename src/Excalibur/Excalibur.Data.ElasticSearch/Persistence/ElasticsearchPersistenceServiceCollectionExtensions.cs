// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.ElasticSearch.Persistence;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering the Elasticsearch persistence provider.
/// </summary>
public static class ElasticsearchPersistenceServiceCollectionExtensions
{
	/// <summary>
	/// Adds the Elasticsearch persistence provider to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">The configuration action for persistence options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// This registers <see cref="ElasticsearchPersistenceProvider"/> as both a concrete type
	/// and as <see cref="IPersistenceProvider"/>. The provider implements
	/// <see cref="IPersistenceProviderHealth"/> accessible via
	/// <see cref="IPersistenceProvider.GetService"/>.
	/// </para>
	/// <para>
	/// An <see cref="Elastic.Clients.Elasticsearch.ElasticsearchClient"/> must be registered
	/// separately in the service collection before calling this method.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddElasticsearchPersistence(
		this IServiceCollection services,
		Action<ElasticsearchPersistenceOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<ElasticsearchPersistenceOptions>()
			.Configure(configure)
			.ValidateOnStart();

		services.TryAddSingleton<ElasticsearchPersistenceProvider>();
		services.AddKeyedSingleton<IPersistenceProvider>("elasticsearch",
			(sp, _) => sp.GetRequiredService<ElasticsearchPersistenceProvider>());
		services.TryAddKeyedSingleton<IPersistenceProvider>("default", (sp, _) =>
			sp.GetRequiredKeyedService<IPersistenceProvider>("elasticsearch"));

		return services;
	}

	/// <summary>
	/// Adds the Elasticsearch persistence provider to the service collection using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section to bind options from.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// An <see cref="Elastic.Clients.Elasticsearch.ElasticsearchClient"/> must be registered
	/// separately in the service collection before calling this method.
	/// </para>
	/// </remarks>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddElasticsearchPersistence(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<ElasticsearchPersistenceOptions>()
			.Bind(configuration)
			.ValidateOnStart();

		services.TryAddSingleton<ElasticsearchPersistenceProvider>();
		services.AddKeyedSingleton<IPersistenceProvider>("elasticsearch",
			(sp, _) => sp.GetRequiredService<ElasticsearchPersistenceProvider>());
		services.TryAddKeyedSingleton<IPersistenceProvider>("default", (sp, _) =>
			sp.GetRequiredKeyedService<IPersistenceProvider>("elasticsearch"));

		return services;
	}
}
