// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Data.SqlClient;

namespace Excalibur.Outbox.SqlServer;

/// <summary>
/// Fluent builder interface for configuring SQL Server-specific outbox settings.
/// </summary>
/// <remarks>
/// <para>
/// Provides the canonical 4 connection overloads plus subsystem-specific configuration
/// for outbox table names, schema, and operational settings. Follows the builder pattern
/// established by <see cref="Excalibur.Cdc.SqlServer.ISqlServerCdcConnectionBuilder"/>.
/// </para>
/// <para>
/// <b>Connection overloads are mutually exclusive (last-wins):</b> If multiple connection
/// methods are called, the last one takes effect. Each call overwrites any previously
/// configured connection.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// outbox.UseSqlServer(sql =>
/// {
///     sql.ConnectionString("Server=.;Database=MyDb;Trusted_Connection=True;")
///        .SchemaName("Messaging")
///        .TableName("OutboxMessages");
/// });
/// </code>
/// </example>
public interface ISqlServerOutboxBuilder
{
	// --- Connection overloads (canonical 4) ---

	/// <summary>
	/// Sets the SQL Server connection string.
	/// </summary>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="connectionString"/> is null or whitespace.
	/// </exception>
	ISqlServerOutboxBuilder ConnectionString(string connectionString);

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
	ISqlServerOutboxBuilder ConnectionFactory(
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
	ISqlServerOutboxBuilder ConnectionStringName(string name);

	/// <summary>
	/// Binds options from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.
	/// </summary>
	/// <param name="sectionPath">The configuration section path (e.g., "Outbox:SqlServer").</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="sectionPath"/> is null or whitespace.
	/// </exception>
	ISqlServerOutboxBuilder BindConfiguration(string sectionPath);

	// --- Feature-specific configuration ---

	/// <summary>
	/// Sets the schema name for the outbox tables. Default: "dbo".
	/// </summary>
	/// <param name="schema">The schema name (e.g., "dbo", "Messaging").</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="schema"/> is null or whitespace.
	/// </exception>
	ISqlServerOutboxBuilder SchemaName(string schema);

	/// <summary>
	/// Sets the table name for storing outbox messages. Default: "OutboxMessages".
	/// </summary>
	/// <param name="tableName">The table name.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="tableName"/> is null or whitespace.
	/// </exception>
	ISqlServerOutboxBuilder TableName(string tableName);

	/// <summary>
	/// Sets the table name for storing transport delivery records. Default: "OutboxMessageTransports".
	/// </summary>
	/// <param name="tableName">The table name for transport records.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="tableName"/> is null or whitespace.
	/// </exception>
	ISqlServerOutboxBuilder TransportsTableName(string tableName);

	/// <summary>
	/// Sets the table name for storing dead letter messages. Default: "OutboxDeadLetters".
	/// </summary>
	/// <param name="tableName">The table name for dead letters.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="tableName"/> is null or whitespace.
	/// </exception>
	ISqlServerOutboxBuilder DeadLetterTableName(string tableName);

	/// <summary>
	/// Enables SQL Server health checks for the outbox store.
	/// </summary>
	/// <param name="name">Optional health check name. Default: "sqlserver-outbox".</param>
	/// <returns>The builder for fluent chaining.</returns>
	ISqlServerOutboxBuilder EnableHealthChecks(string? name = null);
}
