// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

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
/// Multiple projection types share the same table, distinguished by the <c>ProjectionType</c> attribute
/// and a compound partition key (<c>{ProjectionType}#{Id}</c>).
/// </para>
/// </remarks>
public static class DynamoDbProjectionTableConvention
{
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
