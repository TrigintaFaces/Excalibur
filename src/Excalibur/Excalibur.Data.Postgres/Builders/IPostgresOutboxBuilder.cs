// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.Postgres;

/// <summary>
/// Fluent builder interface for configuring Postgres-specific outbox settings.
/// </summary>
/// <remarks>
/// <para>
/// This builder configures Postgres-specific options such as table names, schema,
/// command timeouts, and reservation behavior.
/// All methods return <c>this</c> for method chaining.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// outbox.UsePostgres(connectionString, postgres =>
/// {
///     postgres.SchemaName("messaging")
///             .TableName("outbox_messages")
///             .CommandTimeout(TimeSpan.FromSeconds(60))
///             .ReservationTimeout(TimeSpan.FromMinutes(5));
/// });
/// </code>
/// </example>
public interface IPostgresOutboxBuilder
{
	/// <summary>
	/// Sets the schema name for the outbox tables.
	/// </summary>
	/// <param name="schema">The schema name (e.g., "public", "messaging").</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="schema"/> is null, empty, or whitespace.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Default is "public". Use this to organize outbox tables in a specific schema.
	/// </para>
	/// </remarks>
	IPostgresOutboxBuilder SchemaName(string schema);

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
	/// Default is "outbox".
	/// </para>
	/// </remarks>
	IPostgresOutboxBuilder TableName(string tableName);

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
	/// Default is "outbox_dead_letters".
	/// </para>
	/// </remarks>
	IPostgresOutboxBuilder DeadLetterTableName(string tableName);

	/// <summary>
	/// Sets the command timeout for Postgres operations.
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
	IPostgresOutboxBuilder CommandTimeout(TimeSpan timeout);

	/// <summary>
	/// Sets the reservation timeout for message processing.
	/// </summary>
	/// <param name="timeout">The reservation timeout. Must be positive.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="timeout"/> is not positive.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Default is 5 minutes. Messages reserved for processing will be released
	/// after this timeout if not completed.
	/// </para>
	/// </remarks>
	IPostgresOutboxBuilder ReservationTimeout(TimeSpan timeout);

	/// <summary>
	/// Sets the maximum number of delivery attempts before moving to dead letter.
	/// </summary>
	/// <param name="maxAttempts">The maximum attempts. Must be greater than 0.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="maxAttempts"/> is less than or equal to 0.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Default is 5 attempts.
	/// </para>
	/// </remarks>
	IPostgresOutboxBuilder MaxAttempts(int maxAttempts);
}
