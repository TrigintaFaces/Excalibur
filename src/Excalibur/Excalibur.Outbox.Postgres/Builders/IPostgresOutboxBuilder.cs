// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Outbox.Postgres;

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
/// outbox.UsePostgres(postgres =>
/// {
///     postgres.ConnectionString(connectionString)
///             .SchemaName("messaging")
///             .TableName("outbox_messages")
///             .CommandTimeout(TimeSpan.FromSeconds(60))
///             .ReservationTimeout(TimeSpan.FromMinutes(5));
/// });
/// </code>
/// </example>
public interface IPostgresOutboxBuilder
{
	/// <summary>
	/// Sets the connection string for the Postgres outbox database.
	/// </summary>
	/// <param name="connectionString">The Postgres connection string.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="connectionString"/> is null, empty, or whitespace.
	/// </exception>
	/// <remarks>
	/// <para>
	/// This is the recommended way to provide a connection string for the outbox store.
	/// Mutually exclusive with <see cref="ConnectionFactory"/>. The last one set wins.
	/// </para>
	/// </remarks>
	IPostgresOutboxBuilder ConnectionString(string connectionString);

	/// <summary>
	/// Sets a factory function that provides an <see cref="Excalibur.Data.Abstractions.IDb"/> instance.
	/// </summary>
	/// <param name="dbFactory">A factory function that provides an <see cref="Excalibur.Data.Abstractions.IDb"/> instance.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="dbFactory"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Use this for advanced scenarios like multi-database setups,
	/// custom connection pooling, or IDb integration.
	/// Mutually exclusive with <see cref="ConnectionString"/>. The last one set wins.
	/// </para>
	/// </remarks>
	IPostgresOutboxBuilder ConnectionFactory(Func<IServiceProvider, Excalibur.Data.Abstractions.IDb> dbFactory);

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

}
