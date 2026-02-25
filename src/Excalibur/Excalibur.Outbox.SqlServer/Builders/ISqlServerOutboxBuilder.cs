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
/// outbox.UseSqlServer(connectionString, sql =>
/// {
///     sql.SchemaName("Messaging")
///        .TableName("OutboxMessages")
///        .CommandTimeout(TimeSpan.FromSeconds(60))
///        .UseRowLocking(true);
/// });
/// </code>
/// </example>
public interface ISqlServerOutboxBuilder
{
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

	/// <summary>
	/// Sets the command timeout for SQL operations.
	/// </summary>
	/// <param name="timeout">The command timeout. Must be positive.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="timeout"/> is not positive.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Default is 30 seconds. Increase for high-volume scenarios or slow networks.
	/// </para>
	/// </remarks>
	ISqlServerOutboxBuilder CommandTimeout(TimeSpan timeout);

	/// <summary>
	/// Enables or disables row-level locking for concurrent access.
	/// </summary>
	/// <param name="enable">True to enable row-level locking.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// Default is true. Row locking prevents multiple processors from picking up
	/// the same messages, which is essential in distributed deployments.
	/// </para>
	/// </remarks>
	ISqlServerOutboxBuilder UseRowLocking(bool enable = true);

	/// <summary>
	/// Sets the default batch size for retrieving messages.
	/// </summary>
	/// <param name="size">The batch size. Must be greater than 0.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="size"/> is less than or equal to 0.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Default is 100. This is the SQL Server-specific batch size for queries.
	/// </para>
	/// </remarks>
	ISqlServerOutboxBuilder DefaultBatchSize(int size);
}
