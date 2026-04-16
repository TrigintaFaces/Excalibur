// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Data.SqlClient;

namespace Excalibur.Data.IdentityMap.SqlServer.Builders;

/// <summary>
/// Fluent builder interface for configuring the SQL Server identity map store.
/// </summary>
/// <remarks>
/// <para>
/// Provides the canonical 4 connection overloads plus subsystem-specific configuration
/// for identity map table name, schema, command timeout, and batch size. Follows the
/// builder pattern established by <see cref="Excalibur.Cdc.SqlServer.ISqlServerCdcConnectionBuilder"/>.
/// </para>
/// <para>
/// <b>Connection overloads are mutually exclusive (last-wins):</b> If multiple connection
/// methods are called, the last one takes effect. Each call overwrites any previously
/// configured connection.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// identity.UseSqlServer(sql =>
/// {
///     sql.ConnectionString("Server=.;Database=MyDb;Trusted_Connection=True;")
///        .SchemaName("dbo")
///        .TableName("IdentityMap")
///        .MaxBatchSize(200);
/// });
/// </code>
/// </example>
public interface ISqlServerIdentityMapBuilder
{
	// --- Connection overloads (canonical 4) ---

	/// <summary>
	/// Sets the SQL Server connection string.
	/// </summary>
	/// <param name="connectionString">The connection string.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="connectionString"/> is null or whitespace.
	/// </exception>
	ISqlServerIdentityMapBuilder ConnectionString(string connectionString);

	/// <summary>
	/// Sets a factory function that creates SQL connections.
	/// Use for Azure Managed Identity, Key Vault, or custom connection pooling.
	/// </summary>
	/// <param name="connectionFactory">
	/// A factory receiving <see cref="IServiceProvider"/> and returning a
	/// <c>Func&lt;SqlConnection&gt;</c> that creates connections on demand.
	/// </param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="connectionFactory"/> is null.
	/// </exception>
	ISqlServerIdentityMapBuilder ConnectionFactory(
		Func<IServiceProvider, Func<SqlConnection>> connectionFactory);

	/// <summary>
	/// Resolves the connection string from <c>IConfiguration.GetConnectionString(name)</c>
	/// at service resolution time.
	/// </summary>
	/// <param name="name">The connection string name in the <c>ConnectionStrings</c> section.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="name"/> is null or whitespace.
	/// </exception>
	ISqlServerIdentityMapBuilder ConnectionStringName(string name);

	/// <summary>
	/// Binds options from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.
	/// </summary>
	/// <param name="sectionPath">The configuration section path (e.g., "IdentityMap:SqlServer").</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="sectionPath"/> is null or whitespace.
	/// </exception>
	ISqlServerIdentityMapBuilder BindConfiguration(string sectionPath);

	// --- Feature-specific configuration ---

	/// <summary>
	/// Sets the database schema name for the identity map table. Default: "dbo".
	/// </summary>
	/// <param name="schemaName">The schema name.</param>
	/// <returns>The builder for fluent chaining.</returns>
	ISqlServerIdentityMapBuilder SchemaName(string schemaName);

	/// <summary>
	/// Sets the identity map table name. Default: "IdentityMap".
	/// </summary>
	/// <param name="tableName">The table name.</param>
	/// <returns>The builder for fluent chaining.</returns>
	ISqlServerIdentityMapBuilder TableName(string tableName);

	/// <summary>
	/// Sets the command timeout.
	/// </summary>
	/// <param name="timeout">The command timeout.</param>
	/// <returns>The builder for fluent chaining.</returns>
	ISqlServerIdentityMapBuilder CommandTimeout(TimeSpan timeout);

	/// <summary>
	/// Sets the maximum number of items in a single batch resolve operation. Default: 100.
	/// </summary>
	/// <param name="maxBatchSize">The maximum batch size.</param>
	/// <returns>The builder for fluent chaining.</returns>
	ISqlServerIdentityMapBuilder MaxBatchSize(int maxBatchSize);

	/// <summary>
	/// Enables SQL Server health checks for the identity map store.
	/// </summary>
	/// <param name="name">Optional health check name. Default: "sqlserver-identitymap".</param>
	/// <returns>The builder for fluent chaining.</returns>
	ISqlServerIdentityMapBuilder EnableHealthChecks(string? name = null);
}
