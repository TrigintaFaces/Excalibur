// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.EventSourcing.DependencyInjection;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Data.ElasticSearch.MaterializedViews;

/// <summary>
/// Extension methods for registering Elasticsearch materialized view store.
/// </summary>
public static class ElasticSearchMaterializedViewExtensions
{
	/// <summary>
	/// Configures the materialized view store to use Elasticsearch.
	/// </summary>
	/// <param name="builder">The materialized views builder.</param>
	/// <param name="configure">Action to configure Elasticsearch options.</param>
	/// <returns>The builder for fluent configuration.</returns>
	/// <example>
	/// <code>
	/// services.AddMaterializedViews(builder =>
	/// {
	///     builder.UseElasticSearch(options =>
	///     {
	///         options.NodeUri = "http://localhost:9200";
	///     });
	/// });
	/// </code>
	/// </example>
	public static IMaterializedViewsBuilder UseElasticSearch(
		this IMaterializedViewsBuilder builder,
		Action<ElasticSearchMaterializedViewStoreOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddOptions<ElasticSearchMaterializedViewStoreOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		return builder.UseStore<ElasticSearchMaterializedViewStore>();
	}

	/// <summary>
	/// Configures the materialized view store to use Elasticsearch with a node URI.
	/// </summary>
	/// <param name="builder">The materialized views builder.</param>
	/// <param name="nodeUri">The Elasticsearch node URI.</param>
	/// <returns>The builder for fluent configuration.</returns>
	/// <example>
	/// <code>
	/// services.AddMaterializedViews(builder =>
	/// {
	///     builder.UseElasticSearch("http://localhost:9200");
	/// });
	/// </code>
	/// </example>
	public static IMaterializedViewsBuilder UseElasticSearch(
		this IMaterializedViewsBuilder builder,
		string nodeUri)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentException.ThrowIfNullOrWhiteSpace(nodeUri);

		return builder.UseElasticSearch(options =>
		{
			options.NodeUri = nodeUri;
		});
	}
}
