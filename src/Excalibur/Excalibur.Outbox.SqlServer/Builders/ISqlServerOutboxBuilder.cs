// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Outbox.SqlServer;

/// <summary>
/// Fluent builder interface for configuring SQL Server-specific outbox settings.
/// </summary>
/// <remarks>
/// <para>
/// This builder configures SQL Server-specific options such as table names, schema,
/// command timeouts, and row locking behavior.
/// All methods return <c>this</c> for method chaining.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// outbox.UseSqlServer(sql =>
/// {
///     sql.ConnectionString("Server=.;Database=MyDb;Trusted_Connection=True;")
///        .SchemaName("Messaging")
///        .TableName("OutboxMessages")
///        .CommandTimeout(TimeSpan.FromSeconds(60))
///        .UseRowLocking(true);
/// });
/// </code>
/// </example>
public interface ISqlServerOutboxBuilder
{
	/// <summary>
	/// Sets the SQL Server connection string.
	/// </summary>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="connectionString"/> is null, empty, or whitespace.
	/// </exception>
	/// <remarks>
	/// <para>
	/// This is the primary way to configure the connection string for the outbox store.
	/// Alternatively, configure <see cref="SqlServerOutboxOptions.ConnectionString"/> directly
	/// via the options pattern.
	/// </para>
	/// </remarks>
	ISqlServerOutboxBuilder ConnectionString(string connectionString);

	/// <summary>
	/// Sets the schema name for the outbox tables.
	/// </summary>
	/// <param name="schema">The schema name (e.g., "dbo", "Messaging").</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="schema"/> is null, empty, or whitespace.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Default is "dbo". Use this to organize outbox tables in a specific schema.
	/// </para>
	/// </remarks>
	ISqlServerOutboxBuilder SchemaName(string schema);

	/// <summary>
	/// Sets the table name for storing outbox messages.
	/// </summary>
	/// <param name="tableName">The table name.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="tableName"/> is null, empty, or whitespace.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Default is "OutboxMessages".
	/// </para>
	/// </remarks>
	ISqlServerOutboxBuilder TableName(string tableName);

	/// <summary>
	/// Sets the table name for storing transport delivery records.
	/// </summary>
	/// <param name="tableName">The table name for transport records.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="tableName"/> is null, empty, or whitespace.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Default is "OutboxMessageTransports".
	/// </para>
	/// </remarks>
	ISqlServerOutboxBuilder TransportsTableName(string tableName);

	/// <summary>
	/// Sets the table name for storing dead letter messages.
	/// </summary>
	/// <param name="tableName">The table name for dead letters.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="tableName"/> is null, empty, or whitespace.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Default is "OutboxDeadLetters".
	/// </para>
	/// </remarks>
	ISqlServerOutboxBuilder DeadLetterTableName(string tableName);

}
