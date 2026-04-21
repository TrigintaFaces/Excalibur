// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.EventSourcing.DependencyInjection;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.OpenSearch.MaterializedViews;

/// <summary>
/// Extension methods for registering OpenSearch materialized view store.
/// </summary>
public static class OpenSearchMaterializedViewExtensions
{
	/// <summary>
	/// Configures the materialized view store to use OpenSearch.
	/// </summary>
	/// <param name="builder">The materialized views builder.</param>
	/// <param name="configure">Action to configure OpenSearch options.</param>
	/// <returns>The builder for fluent configuration.</returns>
	/// <example>
	/// <code>
	/// services.AddMaterializedViews(builder =>
	/// {
	///     builder.UseOpenSearch(options =>
	///     {
	///         options.NodeUri = "http://localhost:9200";
	///     });
	/// });
	/// </code>
	/// </example>
	public static IMaterializedViewsBuilder UseOpenSearch(
		this IMaterializedViewsBuilder builder,
		Action<OpenSearchMaterializedViewStoreOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		_ = builder.Services.AddOptions<OpenSearchMaterializedViewStoreOptions>()
			.Configure(configure)
			.ValidateOnStart();

		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<OpenSearchMaterializedViewStoreOptions>, OpenSearchMaterializedViewStoreOptionsValidator>());

		return builder.UseStore<OpenSearchMaterializedViewStore>();
	}

	/// <summary>
	/// Configures the materialized view store to use OpenSearch with a node URI.
	/// </summary>
	/// <param name="builder">The materialized views builder.</param>
	/// <param name="nodeUri">The OpenSearch node URI.</param>
	/// <returns>The builder for fluent configuration.</returns>
	/// <example>
	/// <code>
	/// services.AddMaterializedViews(builder =>
	/// {
	///     builder.UseOpenSearch("http://localhost:9200");
	/// });
	/// </code>
	/// </example>
	public static IMaterializedViewsBuilder UseOpenSearch(
		this IMaterializedViewsBuilder builder,
		string nodeUri)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentException.ThrowIfNullOrWhiteSpace(nodeUri);

		return builder.UseOpenSearch(options =>
		{
			options.NodeUri = nodeUri;
		});
	}
}
