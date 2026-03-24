// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Cdc.CosmosDb;

/// <summary>
/// Fluent builder interface for configuring CosmosDB CDC settings.
/// </summary>
/// <remarks>
/// <para>
/// This builder configures CosmosDB-specific CDC options such as database ID,
/// container ID, change feed settings, and state store connections.
/// All methods return <c>this</c> for method chaining.
/// </para>
/// </remarks>
public interface ICosmosDbCdcBuilder
{
	/// <summary>
	/// Sets the CosmosDB connection string for the CDC source.
	/// </summary>
	/// <param name="connectionString">The CosmosDB connection string.</param>
	/// <returns>The builder for fluent chaining.</returns>
	ICosmosDbCdcBuilder ConnectionString(string connectionString);

	/// <summary>
	/// Sets the database ID for CDC processing.
	/// </summary>
	/// <param name="databaseId">The database identifier.</param>
	/// <returns>The builder for fluent chaining.</returns>
	ICosmosDbCdcBuilder DatabaseId(string databaseId);

	/// <summary>
	/// Sets the container ID for CDC processing.
	/// </summary>
	/// <param name="containerId">The container identifier.</param>
	/// <returns>The builder for fluent chaining.</returns>
	ICosmosDbCdcBuilder ContainerId(string containerId);

	/// <summary>
	/// Sets the processor name for this CDC instance.
	/// </summary>
	/// <param name="processorName">The processor name.</param>
	/// <returns>The builder for fluent chaining.</returns>
	ICosmosDbCdcBuilder ProcessorName(string processorName);

	/// <summary>
	/// Configures change feed settings.
	/// </summary>
	/// <param name="configure">Action to configure change feed options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	ICosmosDbCdcBuilder ChangeFeed(Action<CosmosDbChangeFeedOptions> configure);

	/// <summary>
	/// Configures a separate connection for CDC state persistence.
	/// </summary>
	/// <param name="configure">An action to configure state store settings including connection, database, and container.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// When omitted, the source connection is used for state persistence (backward compatible).
	/// Follows the Microsoft Change Feed Processor pattern where lease storage
	/// can be deployed to a separate CosmosDB account.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="configure"/> is null.
	/// </exception>
	ICosmosDbCdcBuilder WithStateStore(Action<ICdcStateStoreBuilder> configure);

	/// <summary>
	/// Binds CosmosDB CDC source options from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.
	/// </summary>
	/// <param name="sectionPath">The configuration section path (e.g., "Cdc:CosmosDb").</param>
	/// <returns>The builder for fluent chaining.</returns>
	ICosmosDbCdcBuilder BindConfiguration(string sectionPath);
}
