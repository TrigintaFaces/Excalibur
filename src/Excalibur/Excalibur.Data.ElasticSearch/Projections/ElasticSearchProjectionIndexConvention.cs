// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


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
/// When <see cref="ElasticSearchProjectionIndexOptions.IndexPrefix"/> is empty/whitespace, the prefix is omitted.
/// When <see cref="ElasticSearchProjectionIndexOptions.IndexName"/> is set, it replaces the projection type name.
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
///             optionsMonitor.Get(ElasticSearchProjectionIndexConvention.GetOptionsName&lt;OrderListItem&gt;())))
///     {
///     }
/// }
/// </code>
/// </example>
public static class ElasticSearchProjectionIndexConvention
{
	/// <summary>
	/// The name of the options instance a projection's store settings live under.
	/// </summary>
	/// <typeparam name="TProjection">The projection type.</typeparam>
	/// <returns>The options name — the projection's full type name, falling back to its simple name.</returns>
	/// <remarks>
	/// <para>
	/// Use this rather than writing <c>nameof(MyProjection)</c> at the call site. It was derived
	/// independently in three places — the store, the registration, and every consumer repository that
	/// reads the same bucket — and three derivations of one fact are three chances for one to drift.
	/// </para>
	/// <para>
	/// The full type name isolates settings for projections with the same simple name in different
	/// namespaces. This bucket identity is separate from index naming: configure distinct index names
	/// for those projections when they share a cluster.
	/// </para>
	/// </remarks>
	public static string GetOptionsName<TProjection>()
		where TProjection : class => typeof(TProjection).FullName ?? typeof(TProjection).Name;

	/// <summary>
	/// Resolves the ElasticSearch index name for a projection type using the configured options.
	/// </summary>
	/// <typeparam name="TProjection">The projection type. The lowercased type name is used when
	/// <see cref="ElasticSearchProjectionIndexOptions.IndexName"/> is not set.</typeparam>
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

		var name = !string.IsNullOrWhiteSpace(options.Index.IndexName)
			? options.Index.IndexName
			: projectionTypeName;

		var composed = string.IsNullOrWhiteSpace(options.Index.IndexPrefix)
			? name
			: $"{options.Index.IndexPrefix}-{name}";

		// Elasticsearch index names MUST be lowercase (an uppercase character yields a 400
		// invalid_index_name_exception). Lowercase the entire composed name so a consumer-supplied
		// IndexPrefix/IndexName or environment-derived segment (e.g. "Development") cannot produce an
		// invalid index name.
		return composed.ToLowerInvariant();
	}
}
