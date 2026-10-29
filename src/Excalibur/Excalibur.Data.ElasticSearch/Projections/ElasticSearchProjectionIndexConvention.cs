// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using Excalibur.EventSourcing;

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
	/// namespaces. This bucket identity is separate from index naming, which defaults to the SIMPLE
	/// name and therefore does collide for such a pair — so configure distinct index names for those
	/// projections when they share a cluster. Failing to is refused at start-up rather than silently
	/// pointing two read models at one index.
	/// </para>
	/// </remarks>
	public static string GetOptionsName<TProjection>()
		where TProjection : class => GetOptionsName(typeof(TProjection));

	/// <summary>
	/// The name of the options instance a projection's store settings live under.
	/// </summary>
	/// <param name="projectionType">The projection type.</param>
	/// <returns>The options name — the projection's full type name, falling back to its simple name.</returns>
	/// <remarks>
	/// The overload for a caller holding a <see cref="Type"/> rather than a compile-time generic — the
	/// rebuild path, which is handed the projection type. It is the SAME derivation the generic overload
	/// performs, which is the point: a second derivation of the bucket name is a second chance for one to
	/// drift, and a caller that derived it from the SIMPLE name read an unconfigured default bucket and
	/// resolved a different index than the store writes to.
	/// </remarks>
	public static string GetOptionsName(Type projectionType)
	{
		ArgumentNullException.ThrowIfNull(projectionType);
		return projectionType.FullName ?? projectionType.Name;
	}

	/// <summary>
	/// Resolves the ElasticSearch index name for a projection type using the configured options.
	/// </summary>
	/// <typeparam name="TProjection">The projection type. The lowercased type name is used when
	/// <see cref="ElasticSearchProjectionIndexOptions.IndexName"/> is not set.</typeparam>
	/// <param name="options">The projection store options containing prefix and index name configuration.</param>
	/// <returns>The resolved index name (e.g. <c>"development-projections-ordersummary"</c>).</returns>
	/// <remarks>
	/// The projection's declared generation participates in the name, so a bump addresses a new index.
	/// </remarks>
	public static string GetIndexName<TProjection>(ElasticSearchProjectionStoreOptions options)
		where TProjection : class
	{
		ArgumentNullException.ThrowIfNull(options);
		return GetIndexName(options, typeof(TProjection));
	}

	/// <summary>
	/// Resolves the ElasticSearch index name for a projection type using the configured options.
	/// </summary>
	/// <param name="options">The projection store options containing prefix and index name configuration.</param>
	/// <param name="projectionType">The projection type whose index name is resolved.</param>
	/// <returns>The resolved index name (e.g. <c>"projections-ordersummary-v2"</c>).</returns>
	/// <remarks>
	/// <para>
	/// Prefer this over the overload taking a type NAME. A generation is declared on the type, so a
	/// caller holding only a name cannot resolve one and will address the first generation's index
	/// whatever the type declares.
	/// </para>
	/// <para>
	/// The generation lands as a trailing <c>-v{n}</c> segment, the convention side-by-side index
	/// workflows already use, and it appears only from the second generation onward: a projection
	/// declaring no generation, or the first, resolves to exactly the name it resolves to without this
	/// feature.
	/// </para>
	/// </remarks>
	public static string GetIndexName(ElasticSearchProjectionStoreOptions options, Type projectionType)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(projectionType);

		// The hyphen is the separator this convention already composes a prefix with, and index names
		// permit it. Applied to the fully resolved name, so a configured IndexName override is versioned
		// the same way the default type name is -- a consumer who named their index does not thereby opt
		// out of generations.
		return ProjectionVersionConvention.Qualify(
			GetIndexName(options, projectionType.Name),
			projectionType,
			'-');
	}

	/// <summary>
	/// Resolves the ElasticSearch index name for a projection type name using the configured options.
	/// </summary>
	/// <param name="options">The projection store options containing prefix and index name configuration.</param>
	/// <param name="projectionTypeName">The projection type name (e.g. <c>"OrderSummary"</c>).</param>
	/// <returns>The resolved index name (e.g. <c>"projections-ordersummary"</c>).</returns>
	/// <remarks>
	/// This overload resolves the first generation's index. A declared generation lives on the projection
	/// TYPE, which a name cannot carry, so use the overload taking a <see cref="Type"/> wherever one is
	/// in hand.
	/// </remarks>
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
