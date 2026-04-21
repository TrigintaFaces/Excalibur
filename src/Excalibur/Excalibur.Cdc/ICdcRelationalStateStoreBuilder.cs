// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Cdc;

/// <summary>
/// Extends <see cref="ICdcStateStoreBuilder"/> with relational-specific settings
/// (schema name, connection string) for SQL Server and Postgres CDC state stores.
/// </summary>
/// <remarks>
/// <para>
/// Non-relational providers (DynamoDB, Firestore, CosmosDB, MongoDB) use
/// <see cref="ICdcStateStoreBuilder"/> directly and do not implement these methods.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// cdc.UseSqlServer(sql =&gt;
/// {
///     sql.ConnectionString(sourceConnectionString)
///        .WithStateStore(state =&gt;
///        {
///            state.ConnectionString(stateConnectionString)
///                 .SchemaName("dbo")
///                 .TableName("CdcProcessingState");
///        });
/// });
/// </code>
/// </example>
public interface ICdcRelationalStateStoreBuilder : ICdcStateStoreBuilder
{
	/// <summary>
	/// Sets the table name for CDC checkpoint persistence, narrowing the inherited return type
	/// from <see cref="ICdcStateStoreBuilder"/> so relational-only members (e.g.
	/// <see cref="ConnectionString"/>, <see cref="SchemaName"/>) remain chainable after this call.
	/// </summary>
	/// <param name="tableName">The table name.</param>
	/// <returns>The builder for fluent chaining, typed as <see cref="ICdcRelationalStateStoreBuilder"/>.</returns>
	new ICdcRelationalStateStoreBuilder TableName(string tableName);

	/// <summary>
	/// Binds state store options from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section,
	/// narrowing the inherited return type so relational-only members remain chainable after this call.
	/// </summary>
	/// <param name="sectionPath">The configuration section path.</param>
	/// <returns>The builder for fluent chaining, typed as <see cref="ICdcRelationalStateStoreBuilder"/>.</returns>
	new ICdcRelationalStateStoreBuilder BindConfiguration(string sectionPath);

	/// <summary>
	/// Sets the database schema for the CDC checkpoint table.
	/// </summary>
	/// <param name="schema">The schema name.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="schema"/> is null or whitespace.
	/// </exception>
	ICdcRelationalStateStoreBuilder SchemaName(string schema);

	/// <summary>
	/// Sets the state store connection string directly.
	/// </summary>
	/// <param name="connectionString">The state store connection string.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="connectionString"/> is null or whitespace.
	/// </exception>
	ICdcRelationalStateStoreBuilder ConnectionString(string connectionString);

	/// <summary>
	/// Resolves the state store connection string from <c>IConfiguration.GetConnectionString(name)</c>
	/// at DI resolution time.
	/// </summary>
	/// <param name="name">The connection string name in the <c>ConnectionStrings</c> section.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="name"/> is null or whitespace.
	/// </exception>
	ICdcRelationalStateStoreBuilder ConnectionStringName(string name);
}
