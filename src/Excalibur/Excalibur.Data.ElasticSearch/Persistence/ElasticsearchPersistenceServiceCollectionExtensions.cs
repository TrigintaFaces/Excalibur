// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.ElasticSearch.Persistence;

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
		services.TryAddSingleton<IPersistenceProvider>(sp =>
			sp.GetRequiredService<ElasticsearchPersistenceProvider>());

		return services;
	}
}
