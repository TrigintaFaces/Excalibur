// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.SqlServer.Cdc;

/// <summary>
/// Fluent builder interface for configuring SQL Server CDC settings.
/// </summary>
/// <remarks>
/// <para>
/// This builder configures SQL Server-specific CDC options such as schema name,
/// state table name, polling intervals, and batch sizes.
/// All methods return <c>this</c> for method chaining.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// cdc.UseSqlServer(connectionString, sql =>
/// {
///     sql.SchemaName("cdc")
///        .StateTableName("CdcProcessingState")
///        .PollingInterval(TimeSpan.FromSeconds(5))
///        .BatchSize(100)
///        .CommandTimeout(TimeSpan.FromSeconds(30));
/// });
/// </code>
/// </example>
public interface ISqlServerCdcBuilder
{
	/// <summary>
	/// Sets the schema name for CDC state tables.
	/// </summary>
	/// <param name="schema">The schema name.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="schema"/> is null or whitespace.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Default is "Cdc".
	/// </para>
	/// </remarks>
	ISqlServerCdcBuilder SchemaName(string schema);

	/// <summary>
	/// Sets the table name for CDC processing state.
	/// </summary>
	/// <param name="tableName">The table name.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="tableName"/> is null or whitespace.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Default is "CdcProcessingState".
	/// </para>
	/// </remarks>
	ISqlServerCdcBuilder StateTableName(string tableName);

	/// <summary>
	/// Sets the polling interval for CDC change detection.
	/// </summary>
	/// <param name="interval">The polling interval.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="interval"/> is not positive.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Default is 5 seconds. Lower values increase responsiveness but also
	/// database load.
	/// </para>
	/// </remarks>
	ISqlServerCdcBuilder PollingInterval(TimeSpan interval);

	/// <summary>
	/// Sets the batch size for CDC change processing.
	/// </summary>
	/// <param name="size">The batch size.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="size"/> is not positive.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Default is 100. Larger batches can improve throughput but may increase
	/// memory usage and transaction duration.
	/// </para>
	/// </remarks>
	ISqlServerCdcBuilder BatchSize(int size);

	/// <summary>
	/// Sets the command timeout for database operations.
	/// </summary>
	/// <param name="timeout">The command timeout.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="timeout"/> is not positive.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Default is 30 seconds.
	/// </para>
	/// </remarks>
	ISqlServerCdcBuilder CommandTimeout(TimeSpan timeout);
}
