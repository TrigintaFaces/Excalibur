// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.ElasticSearch.Outbox;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Configuration;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Elasticsearch outbox store.
/// </summary>
public static class ElasticsearchOutboxExtensions
{
	/// <summary>
	/// Adds Elasticsearch outbox store to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddElasticsearchOutboxStore(
		this IServiceCollection services,
		Action<ElasticsearchOutboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<ElasticsearchOutboxOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		services.TryAddSingleton<ElasticsearchOutboxStore>();
		services.TryAddSingleton<IOutboxStore>(sp => sp.GetRequiredService<ElasticsearchOutboxStore>());
		services.TryAddSingleton<IOutboxStoreAdmin>(sp => sp.GetRequiredService<ElasticsearchOutboxStore>());

		return services;
	}

	/// <summary>
	/// Configures the dispatch builder to use Elasticsearch outbox store.
	/// </summary>
	/// <param name="builder">The dispatch builder.</param>
	/// <param name="configure">Action to configure the options.</param>
	/// <returns>The dispatch builder for fluent configuration.</returns>
	public static IDispatchBuilder UseElasticsearchOutboxStore(
		this IDispatchBuilder builder,
		Action<ElasticsearchOutboxOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddElasticsearchOutboxStore(configure);

		return builder;
	}
}
