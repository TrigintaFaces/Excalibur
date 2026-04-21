// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Data.SqlClient;

namespace Excalibur.Inbox.SqlServer;

/// <summary>
/// Fluent builder interface for configuring SQL Server inbox store settings.
/// </summary>
/// <remarks>
/// <para>
/// Provides the canonical 4 connection overloads plus subsystem-specific configuration
/// for inbox schema, table, and deduplication window. Follows the builder pattern
/// established by <see cref="Excalibur.EventSourcing.SqlServer.ISqlServerEventSourcingBuilder"/>.
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
///     inbox.UseSqlServer(sql =&gt;
///     {
///         sql.ConnectionString("Server=...;Database=Messaging;...")
///            .SchemaName("dbo")
///            .TableName("inbox_messages")
///            .DeduplicationWindow(TimeSpan.FromDays(7));
///     });
/// });
/// </code>
/// </para>
/// </remarks>
public interface ISqlServerInboxBuilder
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
	ISqlServerInboxBuilder ConnectionString(string connectionString);

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
	ISqlServerInboxBuilder ConnectionFactory(
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
	ISqlServerInboxBuilder ConnectionStringName(string name);

	/// <summary>
	/// Binds options from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.
	/// </summary>
	/// <param name="sectionPath">The configuration section path (e.g., "Inbox:SqlServer").</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="sectionPath"/> is null or whitespace.
	/// </exception>
	ISqlServerInboxBuilder BindConfiguration(string sectionPath);

	// --- Feature-specific configuration ---

	/// <summary>
	/// Sets the schema name for the inbox table. Default: "dbo".
	/// </summary>
	/// <param name="schema">The schema name.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="schema"/> is null or whitespace.
	/// </exception>
	ISqlServerInboxBuilder SchemaName(string schema);

	/// <summary>
	/// Sets the inbox table name. Default: "inbox_messages".
	/// </summary>
	/// <param name="tableName">The table name.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="tableName"/> is null or whitespace.
	/// </exception>
	ISqlServerInboxBuilder TableName(string tableName);

	/// <summary>
	/// Sets the message deduplication window. Default: 7 days.
	/// </summary>
	/// <param name="window">The deduplication time window. Must be positive.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="window"/> is zero or negative.
	/// </exception>
	ISqlServerInboxBuilder DeduplicationWindow(TimeSpan window);

	/// <summary>
	/// Enables SQL Server health checks for the inbox store.
	/// </summary>
	/// <param name="name">Optional health check name. Default: "sqlserver-inbox".</param>
	/// <returns>The builder for fluent chaining.</returns>
	ISqlServerInboxBuilder EnableHealthChecks(string? name = null);
}
