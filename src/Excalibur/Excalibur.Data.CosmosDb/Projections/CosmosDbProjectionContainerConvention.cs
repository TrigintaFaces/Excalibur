// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.CosmosDb.Projections;

/// <summary>
/// Resolves Cosmos DB container names for projection stores using the configured naming convention.
/// </summary>
/// <remarks>
/// <para>
/// Use this class when building custom repositories (e.g. extending <see cref="CosmosDbRepositoryBase{TDocument}"/>)
/// that need to target the same container as an <c>IProjectionStore&lt;T&gt;</c>. This ensures both the projection
/// store and your custom repository resolve to the same container name from a single source of truth.
/// </para>
/// <para>
/// The default container name is <c>"projections"</c> (from <see cref="CosmosDbProjectionStoreOptions.ContainerName"/>).
/// Multiple projection types share the same container, distinguished by the <c>projectionType</c> partition key.
/// </para>
/// </remarks>
public static class CosmosDbProjectionContainerConvention
{
	/// <summary>
	/// Resolves the Cosmos DB container name for a projection type using the configured options.
	/// </summary>
	/// <typeparam name="TProjection">The projection type.</typeparam>
	/// <param name="options">The projection store options containing container name configuration.</param>
	/// <returns>The resolved container name (e.g. <c>"projections"</c>).</returns>
	public static string GetContainerName<TProjection>(CosmosDbProjectionStoreOptions options)
		where TProjection : class
	{
		ArgumentNullException.ThrowIfNull(options);
		return GetContainerName(options, typeof(TProjection).Name);
	}

	/// <summary>
	/// Resolves the Cosmos DB container name for a projection type name using the configured options.
	/// </summary>
	/// <param name="options">The projection store options containing container name configuration.</param>
	/// <param name="projectionTypeName">The projection type name (e.g. <c>"OrderSummary"</c>).</param>
	/// <returns>The resolved container name (e.g. <c>"projections"</c>).</returns>
	public static string GetContainerName(CosmosDbProjectionStoreOptions options, string projectionTypeName)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentException.ThrowIfNullOrWhiteSpace(projectionTypeName);

		// Cosmos DB uses a shared container for all projection types (unlike ES which uses per-type indices).
		// The projectionType partition key discriminates between types.
		return options.ContainerName;
	}
}
