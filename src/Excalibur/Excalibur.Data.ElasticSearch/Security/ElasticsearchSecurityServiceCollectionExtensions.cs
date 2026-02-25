// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Security;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Elasticsearch security services.
/// </summary>
public static class ElasticsearchSecurityServiceCollectionExtensions
{
	/// <summary>
	/// Adds Elasticsearch security services to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">The configuration action for security options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddElasticsearchSecurity(
		this IServiceCollection services,
		Action<ElasticsearchSecurityOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<ElasticsearchSecurityOptions>()
			.Configure(configure)
			.ValidateOnStart();

		services.TryAddSingleton<IElasticsearchSecurityProvider, DefaultElasticsearchSecurityProvider>();
		services.TryAddSingleton<SecurityPolicyEngine>();
		services.TryAddSingleton<SecurityEventAggregator>();

		return services;
	}
}
