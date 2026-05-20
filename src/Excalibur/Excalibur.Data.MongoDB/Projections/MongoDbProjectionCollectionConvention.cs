// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.MongoDB.Projections;

/// <summary>
/// Resolves MongoDB collection names for projection stores using the configured naming convention.
/// </summary>
/// <remarks>
/// <para>
/// Use this class when building custom repositories (e.g. extending <see cref="MongoDbRepositoryBase{TDocument}"/>)
/// that need to target the same collection as an <c>IProjectionStore&lt;T&gt;</c>. This ensures both the projection
/// store and your custom repository resolve to the same collection name from a single source of truth.
/// </para>
/// <para>
/// The default collection name is <c>"projections"</c> (from <see cref="MongoDbProjectionStoreOptions.CollectionName"/>).
/// Multiple projection types share the same collection, distinguished by the <c>projectionType</c> field.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class OrderSearchRepository : MongoDbRepositoryBase&lt;OrderListItem&gt;
/// {
///     public OrderSearchRepository(
///         IMongoClient client,
///         IOptionsMonitor&lt;MongoDbProjectionStoreOptions&gt; optionsMonitor)
///         : base(client,
///                optionsMonitor.Get(nameof(OrderListItem)).DatabaseName,
///                MongoDbProjectionCollectionConvention.GetCollectionName&lt;OrderListItem&gt;(
///                    optionsMonitor.Get(nameof(OrderListItem))))
///     {
///     }
///
///     public override Task InitializeCollectionAsync(CancellationToken ct) => Task.CompletedTask;
/// }
/// </code>
/// </example>
public static class MongoDbProjectionCollectionConvention
{
	/// <summary>
	/// Resolves the MongoDB collection name for a projection type using the configured options.
	/// </summary>
	/// <typeparam name="TProjection">The projection type.</typeparam>
	/// <param name="options">The projection store options containing collection name configuration.</param>
	/// <returns>The resolved collection name (e.g. <c>"projections"</c>).</returns>
	public static string GetCollectionName<TProjection>(MongoDbProjectionStoreOptions options)
		where TProjection : class
	{
		ArgumentNullException.ThrowIfNull(options);
		return GetCollectionName(options, typeof(TProjection).Name);
	}

	/// <summary>
	/// Resolves the MongoDB collection name for a projection type name using the configured options.
	/// </summary>
	/// <param name="options">The projection store options containing collection name configuration.</param>
	/// <param name="projectionTypeName">The projection type name (e.g. <c>"OrderSummary"</c>).</param>
	/// <returns>The resolved collection name (e.g. <c>"projections"</c>).</returns>
	public static string GetCollectionName(MongoDbProjectionStoreOptions options, string projectionTypeName)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentException.ThrowIfNullOrWhiteSpace(projectionTypeName);

		// MongoDB uses a shared collection for all projection types (unlike ES which uses per-type indices).
		// The projectionType field in each document discriminates between types.
		return options.CollectionName;
	}
}
