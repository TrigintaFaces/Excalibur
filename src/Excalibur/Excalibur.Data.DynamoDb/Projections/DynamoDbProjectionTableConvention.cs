// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing;

namespace Excalibur.Data.DynamoDb.Projections;

/// <summary>
/// Resolves DynamoDB table names for projection stores using the configured naming convention.
/// </summary>
/// <remarks>
/// <para>
/// Use this class when building custom repositories (e.g. extending <see cref="DynamoDbRepositoryBase{TDocument}"/>)
/// that need to target the same table as an <c>IProjectionStore&lt;T&gt;</c>. This ensures both the projection
/// store and your custom repository resolve to the same table name from a single source of truth.
/// </para>
/// <para>
/// The default table name is <c>"Projections"</c> (from <see cref="DynamoDbProjectionStoreOptions.TableName"/>).
/// Multiple projection types share the same table, distinguished by a compound partition key
/// (<c>{discriminator}#{Id}</c>) and a scan filter on the same discriminator, where the discriminator is
/// <see cref="GetProjectionDiscriminator{TProjection}"/>.
/// </para>
/// </remarks>
public static class DynamoDbProjectionTableConvention
{
	/// <summary>
	/// The discriminator that separates one projection type's items from another's inside the shared table.
	/// </summary>
	/// <typeparam name="TProjection">The projection type.</typeparam>
	/// <returns>The projection's full type name, falling back to its simple name.</returns>
	/// <remarks>
	/// <para>
	/// This value is stamped into the compound partition key (<c>{discriminator}#{id}</c>), written to the
	/// projection metadata, matched by every scan filter, and carried in the store's destination identity.
	/// Use it when building a custom repository that must address the same items as
	/// <c>IProjectionStore&lt;T&gt;</c>, rather than deriving the prefix independently.
	/// </para>
	/// <para>
	/// The name is FULLY QUALIFIED because the single-table design makes it normal for unrelated
	/// projections to share one table: two classes with the same simple name in different namespaces
	/// would otherwise write to the same partition keys and read each other's items, silently
	/// overwriting one read model with the other wherever their ids coincide. A full type name is a few
	/// hundred bytes at most against DynamoDB's 2048-byte partition-key limit.
	/// </para>
	/// <para>
	/// <see cref="Type.FullName"/> is <see langword="null"/> only for a type whose identity depends on
	/// an open generic parameter, which cannot be a closed projection type here; the simple name is
	/// used in that case so the discriminator is never empty.
	/// </para>
	/// <para>
	/// THE DECLARED GENERATION PARTICIPATES IN THE DISCRIMINATOR, NOT IN THE TABLE NAME. A bump must
	/// address a distinct destination, and in this single-table design the discriminator IS the
	/// per-projection address: it prefixes every partition key, filters every scan, and carries into the
	/// destination identity, all from this one value. Versioning the table instead would demand the
	/// consumer provision a second table -- infrastructure they own and we cannot create for them --
	/// before a read model could declare a new shape, and would leave the generation stamped in two
	/// places that could disagree. Here the new generation writes under its own key prefix in the table
	/// that already exists, while the previous generation keeps its items and keeps serving reads.
	/// A projection declaring no generation, or the first, resolves to exactly the discriminator it
	/// resolves to without this feature.
	/// </para>
	/// <para>
	/// The hyphen is legal: a partition key is any non-empty UTF-8 string within the 2048-byte limit.
	/// </para>
	/// </remarks>
	public static string GetProjectionDiscriminator<TProjection>()
		where TProjection : class => ProjectionVersionConvention.Qualify(
			GetOptionsName<TProjection>(),
			typeof(TProjection),
			'-');

	/// <summary>
	/// The name of the options instance a projection's store settings live under.
	/// </summary>
	/// <typeparam name="TProjection">The projection type.</typeparam>
	/// <returns>The projection's full type name, falling back to its simple name.</returns>
	/// <remarks>
	/// <para>
	/// Settings are registered per projection type, so each store reads its own table name. Use this
	/// rather than writing <c>nameof(MyProjection)</c> at the call site: the registration, the store and
	/// any consumer repository reading the same bucket must all derive the one name from one function.
	/// </para>
	/// <para>
	/// The declared generation deliberately does NOT participate. Settings are a configuration concern,
	/// so a read model that declares a new shape keeps reading the settings its registration configured
	/// instead of silently falling back to an unconfigured bucket.
	/// </para>
	/// </remarks>
	public static string GetOptionsName<TProjection>()
		where TProjection : class => typeof(TProjection).FullName ?? typeof(TProjection).Name;

	/// <summary>
	/// Resolves the DynamoDB table name for a projection type using the configured options.
	/// </summary>
	/// <typeparam name="TProjection">The projection type.</typeparam>
	/// <param name="options">The projection store options containing table name configuration.</param>
	/// <returns>The resolved table name (e.g. <c>"Projections"</c>).</returns>
	public static string GetTableName<TProjection>(DynamoDbProjectionStoreOptions options)
		where TProjection : class
	{
		ArgumentNullException.ThrowIfNull(options);
		return GetTableName(options, typeof(TProjection).Name);
	}

	/// <summary>
	/// Resolves the DynamoDB table name for a projection type name using the configured options.
	/// </summary>
	/// <param name="options">The projection store options containing table name configuration.</param>
	/// <param name="projectionTypeName">The projection type name (e.g. <c>"OrderSummary"</c>).</param>
	/// <returns>The resolved table name (e.g. <c>"Projections"</c>).</returns>
	public static string GetTableName(DynamoDbProjectionStoreOptions options, string projectionTypeName)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentException.ThrowIfNullOrWhiteSpace(projectionTypeName);

		// DynamoDB uses a shared table for all projection types (unlike ES which uses per-type indices).
		// The ProjectionType attribute and compound PK discriminate between types.
		return options.TableName;
	}
}
