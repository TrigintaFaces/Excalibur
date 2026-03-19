// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Data.SqlClient;

namespace Excalibur.Cdc.SqlServer;

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

	/// <summary>
	/// Sets the database name for CDC processing.
	/// </summary>
	/// <param name="name">The database name.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="name"/> is null or whitespace.
	/// </exception>
	/// <remarks>
	/// <para>
	/// When set, an <see cref="IDatabaseConfig"/> is automatically registered
	/// in the service collection, eliminating the need for manual registration.
	/// </para>
	/// </remarks>
	ISqlServerCdcBuilder DatabaseName(string name);

	/// <summary>
	/// Sets the unique identifier for the CDC source database connection.
	/// </summary>
	/// <param name="identifier">The connection identifier.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="identifier"/> is null or whitespace.
	/// </exception>
	ISqlServerCdcBuilder DatabaseConnectionIdentifier(string identifier);

	/// <summary>
	/// Sets the unique identifier for the state store database connection.
	/// </summary>
	/// <param name="identifier">The connection identifier.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="identifier"/> is null or whitespace.
	/// </exception>
	ISqlServerCdcBuilder StateConnectionIdentifier(string identifier);

	/// <summary>
	/// Sets the CDC capture instances to process.
	/// </summary>
	/// <param name="captureInstances">The capture instance names.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="captureInstances"/> is null.
	/// </exception>
	ISqlServerCdcBuilder CaptureInstances(params string[] captureInstances);

	/// <summary>
	/// Sets whether processing should stop when a table handler is missing.
	/// </summary>
	/// <param name="stop"><see langword="true"/> to stop on missing handlers; <see langword="false"/> to skip.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// Default is <see langword="true"/>. Set to <see langword="false"/> for production
	/// scenarios where unknown tables should be silently skipped.
	/// </para>
	/// </remarks>
	ISqlServerCdcBuilder StopOnMissingTableHandler(bool stop);

	/// <summary>
	/// Configures a separate connection for CDC state persistence using a connection string.
	/// </summary>
	/// <param name="connectionString">The state store connection string.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// When omitted, the source connection is used for state persistence (backward compatible).
	/// Follows the Microsoft Change Feed Processor pattern where lease/checkpoint storage
	/// can be deployed to a separate database from the CDC source.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="connectionString"/> is null or whitespace.
	/// </exception>
	ISqlServerCdcBuilder WithStateStore(string connectionString);

	/// <summary>
	/// Configures a separate connection for CDC state persistence with state store configuration.
	/// </summary>
	/// <param name="connectionString">The state store connection string.</param>
	/// <param name="configure">An action to configure state store schema and table settings.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="connectionString"/> is null or whitespace.
	/// </exception>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="configure"/> is null.
	/// </exception>
	ISqlServerCdcBuilder WithStateStore(string connectionString, Action<ICdcStateStoreBuilder> configure);

	/// <summary>
	/// Configures a separate connection factory for CDC state persistence.
	/// </summary>
	/// <param name="stateConnectionFactory">A factory function that creates state store SQL connections.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// When omitted, the source connection factory is used for state persistence (backward compatible).
	/// Use this overload for DI-integrated scenarios where connection management is handled externally.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="stateConnectionFactory"/> is null.
	/// </exception>
	ISqlServerCdcBuilder WithStateStore(Func<IServiceProvider, Func<SqlConnection>> stateConnectionFactory);

	/// <summary>
	/// Configures a separate connection factory for CDC state persistence with state store configuration.
	/// </summary>
	/// <param name="stateConnectionFactory">A factory function that creates state store SQL connections.</param>
	/// <param name="configure">An action to configure state store schema and table settings.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="stateConnectionFactory"/> or <paramref name="configure"/> is null.
	/// </exception>
	ISqlServerCdcBuilder WithStateStore(
		Func<IServiceProvider, Func<SqlConnection>> stateConnectionFactory,
		Action<ICdcStateStoreBuilder> configure);

	/// <summary>
	/// Binds SQL Server CDC source options from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.
	/// </summary>
	/// <param name="sectionPath">The configuration section path (e.g., "Cdc:Source").</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// Uses <c>OptionsBuilder&lt;T&gt;.BindConfiguration()</c> with
	/// <c>ValidateDataAnnotations</c> and <c>ValidateOnStart</c>.
	/// Binds <see cref="SqlServerCdcOptions"/> from the specified section.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="sectionPath"/> is null or whitespace.
	/// </exception>
	ISqlServerCdcBuilder BindConfiguration(string sectionPath);

	/// <summary>
	/// Resolves the source connection string from <c>IConfiguration.GetConnectionString(name)</c>
	/// at registration time.
	/// </summary>
	/// <param name="name">The connection string name in the <c>ConnectionStrings</c> section.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// This is a convenience method that resolves the connection string from
	/// <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> at DI registration time.
	/// Equivalent to manually reading the connection string and passing it to <c>UseSqlServer(connectionString)</c>.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="name"/> is null or whitespace.
	/// </exception>
	ISqlServerCdcBuilder ConnectionStringName(string name);
}
