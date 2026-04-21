// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Projections;

/// <summary>
/// Resolves ElasticSearch index names for projection stores using the configured naming convention.
/// </summary>
/// <remarks>
/// <para>
/// Use this class when building custom repositories (e.g. extending <see cref="ElasticRepositoryBase{TDocument}"/>)
/// that need to target the same index as an <c>IProjectionStore&lt;T&gt;</c>. This ensures both the projection
/// store and your custom repository resolve to the same index name from a single source of truth.
/// </para>
/// <para>
/// The naming convention is: <c>{IndexPrefix}-{IndexName ?? projectionTypeName}</c>.
/// When <see cref="ElasticSearchProjectionStoreOptions.IndexPrefix"/> is empty/whitespace, the prefix is omitted.
/// When <see cref="ElasticSearchProjectionStoreOptions.IndexName"/> is set, it replaces the projection type name.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // In a custom repository that needs the same index as IProjectionStore&lt;OrderListItem&gt;
/// public class OrderSearchRepository : ElasticRepositoryBase&lt;OrderListItem&gt;
/// {
///     public OrderSearchRepository(
///         ElasticsearchClient client,
///         IOptionsMonitor&lt;ElasticSearchProjectionStoreOptions&gt; optionsMonitor)
///         : base(client, ElasticSearchProjectionIndexConvention.GetIndexName&lt;OrderListItem&gt;(
///             optionsMonitor.Get(nameof(OrderListItem))))
///     {
///     }
/// }
/// </code>
/// </example>
public static class ElasticSearchProjectionIndexConvention
{
	/// <summary>
	/// Resolves the ElasticSearch index name for a projection type using the configured options.
	/// </summary>
	/// <typeparam name="TProjection">The projection type. The lowercased type name is used when
	/// <see cref="ElasticSearchProjectionStoreOptions.IndexName"/> is not set.</typeparam>
	/// <param name="options">The projection store options containing prefix and index name configuration.</param>
	/// <returns>The resolved index name (e.g. <c>"development-projections-ordersummary"</c>).</returns>
	public static string GetIndexName<TProjection>(ElasticSearchProjectionStoreOptions options)
		where TProjection : class
	{
		ArgumentNullException.ThrowIfNull(options);
		return GetIndexName(options, typeof(TProjection).Name);
	}

	/// <summary>
	/// Resolves the ElasticSearch index name for a projection type name using the configured options.
	/// </summary>
	/// <param name="options">The projection store options containing prefix and index name configuration.</param>
	/// <param name="projectionTypeName">The projection type name (e.g. <c>"OrderSummary"</c>).</param>
	/// <returns>The resolved index name (e.g. <c>"projections-ordersummary"</c>).</returns>
	public static string GetIndexName(ElasticSearchProjectionStoreOptions options, string projectionTypeName)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentException.ThrowIfNullOrWhiteSpace(projectionTypeName);

		var name = !string.IsNullOrWhiteSpace(options.IndexName)
			? options.IndexName
			: projectionTypeName.ToLowerInvariant();

		return string.IsNullOrWhiteSpace(options.IndexPrefix)
			? name
			: $"{options.IndexPrefix}-{name}";
	}
}
