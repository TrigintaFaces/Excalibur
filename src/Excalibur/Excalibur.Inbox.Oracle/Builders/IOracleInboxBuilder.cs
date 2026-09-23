// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Oracle.ManagedDataAccess.Client;

namespace Excalibur.Inbox.Oracle;

/// <summary>
/// Fluent builder interface for configuring Oracle inbox store settings.
/// </summary>
/// <remarks>
/// <para>
/// Provides the canonical 4 connection overloads plus subsystem-specific configuration for
/// schema, table, command timeout, and retry count. Follows the builder pattern established
/// by <c>ISqlServerInboxBuilder</c>.
/// </para>
/// <para>
/// <b>Connection overloads are mutually exclusive (last-wins):</b> If multiple connection
/// methods are called, the last one takes effect.
/// </para>
/// <para>
/// <b>Usage:</b>
/// <code>
/// services.AddExcaliburInbox(inbox =&gt;
/// {
///     inbox.UseOracle(oracle =&gt;
///     {
///         oracle.ConnectionString("User Id=app;Password=...;Data Source=//localhost:1521/FREEPDB1")
///               .SchemaName("APP")
///               .TableName("INBOX_MESSAGES");
///     });
/// });
/// </code>
/// </para>
/// </remarks>
public interface IOracleInboxBuilder
{
	// --- Connection overloads (canonical 4) ---

	/// <summary>
	/// Sets the Oracle connection string.
	/// </summary>
	/// <param name="connectionString">The Oracle connection string.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="connectionString"/> is null or whitespace.
	/// </exception>
	IOracleInboxBuilder ConnectionString(string connectionString);

	/// <summary>
	/// Sets a factory function that creates Oracle connections. Use for wallet-based
	/// authentication, custom pooling, or credentials resolved at runtime.
	/// </summary>
	/// <param name="connectionFactory">
	/// A factory receiving <see cref="IServiceProvider"/> and returning a
	/// <c>Func&lt;OracleConnection&gt;</c> that creates connections on demand.
	/// </param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="connectionFactory"/> is null.
	/// </exception>
	IOracleInboxBuilder ConnectionFactory(
		Func<IServiceProvider, Func<OracleConnection>> connectionFactory);

	/// <summary>
	/// Resolves the connection string from <c>IConfiguration.GetConnectionString(name)</c>
	/// at service resolution time.
	/// </summary>
	/// <param name="name">The connection string name in the <c>ConnectionStrings</c> section.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="name"/> is null or whitespace.
	/// </exception>
	IOracleInboxBuilder ConnectionStringName(string name);

	// --- Feature-specific configuration ---

	/// <summary>
	/// Sets the schema name for the inbox table. When empty, the connecting user's default
	/// schema is used.
	/// </summary>
	/// <param name="schema">The schema name.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="schema"/> is null or whitespace.
	/// </exception>
	IOracleInboxBuilder SchemaName(string schema);

	/// <summary>
	/// Sets the inbox table name. Default: "INBOX_MESSAGES".
	/// </summary>
	/// <param name="tableName">The table name.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="tableName"/> is null or whitespace.
	/// </exception>
	IOracleInboxBuilder TableName(string tableName);

	/// <summary>
	/// Sets the command timeout in seconds. Default: 30.
	/// </summary>
	/// <param name="seconds">The command timeout in seconds. Must be at least 1.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="seconds"/> is less than 1.
	/// </exception>
	IOracleInboxBuilder CommandTimeoutSeconds(int seconds);

	/// <summary>
	/// Sets the maximum retry count for failed messages. Default: 3.
	/// </summary>
	/// <param name="maxRetryCount">The maximum number of retries. Must be zero or greater.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="maxRetryCount"/> is negative.
	/// </exception>
	IOracleInboxBuilder MaxRetryCount(int maxRetryCount);
}
