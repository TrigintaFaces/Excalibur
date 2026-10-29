// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing;

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
///                optionsMonitor.Get(MongoDbProjectionCollectionConvention.GetOptionsName&lt;OrderListItem&gt;()).DatabaseName,
///                MongoDbProjectionCollectionConvention.GetCollectionName&lt;OrderListItem&gt;(
///                    optionsMonitor.Get(MongoDbProjectionCollectionConvention.GetOptionsName&lt;OrderListItem&gt;())))
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
	/// The name of the options instance a projection's store settings live under.
	/// </summary>
	/// <typeparam name="TProjection">The projection type.</typeparam>
	/// <returns>The options name — the projection's full type name, falling back to its simple name.</returns>
	/// <remarks>
	/// <para>
	/// Use this rather than writing <c>nameof(MyProjection)</c> at the call site. The name is derived in
	/// three places — the store, the registration, and every consumer repository that reads the same
	/// bucket — and three derivations of one fact are three chances for one to drift.
	/// </para>
	/// <para>
	/// The FULL type name, not the simple one: two projections with the same simple name in different
	/// namespaces must not share one options instance, or each one's database and collection overrides
	/// would overwrite the other's and both would address a single destination.
	/// </para>
	/// </remarks>
	public static string GetOptionsName<TProjection>()
		where TProjection : class => typeof(TProjection).FullName ?? typeof(TProjection).Name;

	/// <summary>
	/// Resolves the MongoDB collection name for a projection type using the configured options.
	/// </summary>
	/// <typeparam name="TProjection">The projection type.</typeparam>
	/// <param name="options">The projection store options containing collection name configuration.</param>
	/// <returns>The resolved collection name (e.g. <c>"projections"</c>).</returns>
	/// <remarks>
	/// The projection's declared generation participates in the name, so a bump addresses a new collection.
	/// </remarks>
	public static string GetCollectionName<TProjection>(MongoDbProjectionStoreOptions options)
		where TProjection : class
	{
		ArgumentNullException.ThrowIfNull(options);
		return GetCollectionName(options, typeof(TProjection));
	}

	/// <summary>
	/// Resolves the MongoDB collection name for a projection type using the configured options.
	/// </summary>
	/// <param name="options">The projection store options containing collection name configuration.</param>
	/// <param name="projectionType">The projection type whose collection name is resolved.</param>
	/// <returns>The resolved collection name (e.g. <c>"projections-v2"</c>).</returns>
	/// <remarks>
	/// <para>
	/// Prefer this over the overload taking a type NAME. A generation is declared on the type, so a
	/// caller holding only a name cannot resolve one and will address the first generation's collection
	/// whatever the type declares.
	/// </para>
	/// <para>
	/// The generation lands as a trailing <c>-v{n}</c> segment -- a hyphen is legal in a collection name
	/// -- and it appears only from the second generation onward: a projection declaring no generation, or
	/// the first, resolves to exactly the collection it resolves to without this feature. The DATABASE is
	/// untouched; a generation moves the collection inside it.
	/// </para>
	/// </remarks>
	public static string GetCollectionName(MongoDbProjectionStoreOptions options, Type projectionType)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(projectionType);

		return ProjectionVersionConvention.Qualify(
			GetCollectionName(options, projectionType.Name),
			projectionType,
			'-');
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
