// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Npgsql;

namespace Excalibur.Cdc.Postgres;

/// <summary>
/// Fluent builder interface for configuring Postgres CDC settings.
/// </summary>
/// <remarks>
/// <para>
/// This builder configures Postgres-specific CDC options such as schema name,
/// replication slot, publication name, and polling intervals.
/// All methods return <c>this</c> for method chaining.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// cdc.UsePostgres(pg =>
/// {
///     pg.ConnectionString(connectionString)
///       .SchemaName("excalibur")
///       .StateTableName("cdc_state")
///       .ReplicationSlotName("my_slot")
///       .PublicationName("my_publication")
///       .PollingInterval(TimeSpan.FromSeconds(1))
///       .BatchSize(1000);
/// });
/// </code>
/// </example>
public interface IPostgresCdcBuilder
{
	/// <summary>
	/// Sets the schema name for CDC state tables.
	/// </summary>
	/// <param name="schema">The schema name.</param>
	/// <returns>The builder for fluent chaining.</returns>
	IPostgresCdcBuilder SchemaName(string schema);

	/// <summary>
	/// Sets the table name for CDC processing state.
	/// </summary>
	/// <param name="tableName">The table name.</param>
	/// <returns>The builder for fluent chaining.</returns>
	IPostgresCdcBuilder StateTableName(string tableName);

	/// <summary>
	/// Sets the Postgres replication slot name.
	/// </summary>
	/// <param name="slotName">The replication slot name.</param>
	/// <returns>The builder for fluent chaining.</returns>
	IPostgresCdcBuilder ReplicationSlotName(string slotName);

	/// <summary>
	/// Sets the Postgres publication name.
	/// </summary>
	/// <param name="publicationName">The publication name.</param>
	/// <returns>The builder for fluent chaining.</returns>
	IPostgresCdcBuilder PublicationName(string publicationName);

	/// <summary>
	/// Sets the polling interval for CDC change detection.
	/// </summary>
	/// <param name="interval">The polling interval.</param>
	/// <returns>The builder for fluent chaining.</returns>
	IPostgresCdcBuilder PollingInterval(TimeSpan interval);

	/// <summary>
	/// Sets the batch size for CDC change processing.
	/// </summary>
	/// <param name="size">The batch size.</param>
	/// <returns>The builder for fluent chaining.</returns>
	IPostgresCdcBuilder BatchSize(int size);

	/// <summary>
	/// Sets the timeout for replication operations.
	/// </summary>
	/// <param name="timeout">The timeout.</param>
	/// <returns>The builder for fluent chaining.</returns>
	IPostgresCdcBuilder Timeout(TimeSpan timeout);

	/// <summary>
	/// Sets the processor identifier for this CDC processor instance.
	/// </summary>
	/// <param name="processorId">The processor identifier.</param>
	/// <returns>The builder for fluent chaining.</returns>
	IPostgresCdcBuilder ProcessorId(string processorId);

	/// <summary>
	/// Sets whether to use binary protocol for logical replication.
	/// </summary>
	/// <param name="useBinary">Whether to use binary protocol.</param>
	/// <returns>The builder for fluent chaining.</returns>
	IPostgresCdcBuilder UseBinaryProtocol(bool useBinary = true);

	/// <summary>
	/// Sets whether to automatically create the replication slot if it doesn't exist.
	/// </summary>
	/// <param name="autoCreate">Whether to auto-create the slot.</param>
	/// <returns>The builder for fluent chaining.</returns>
	IPostgresCdcBuilder AutoCreateSlot(bool autoCreate = true);

	/// <summary>
	/// Configures a separate connection for CDC state persistence.
	/// </summary>
	/// <param name="configure">An action to configure state store connection, schema, and table settings.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// When omitted, the source connection is used for state persistence (backward compatible).
	/// Follows the Microsoft Change Feed Processor pattern where lease/checkpoint storage
	/// can be deployed to a separate database from the CDC source.
	/// </para>
	/// <para>
	/// Use <c>state.ConnectionString(...)</c> or <c>state.ConnectionStringName(...)</c>
	/// within the configure action to set the state store connection.
	/// For provider-specific connection factories, use <see cref="StateConnectionFactory"/>.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="configure"/> is null.
	/// </exception>
	IPostgresCdcBuilder WithStateStore(Action<ICdcStateStoreBuilder> configure);

	/// <summary>
	/// Sets a factory function that creates Postgres connections for the CDC state store.
	/// </summary>
	/// <param name="stateConnectionFactory">A factory function that creates state store Postgres connections.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// Use this for DI-integrated scenarios where connection management is handled externally.
	/// Can be combined with <see cref="WithStateStore"/> for schema/table configuration.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="stateConnectionFactory"/> is null.
	/// </exception>
	IPostgresCdcBuilder StateConnectionFactory(Func<IServiceProvider, Func<NpgsqlConnection>> stateConnectionFactory);

	/// <summary>
	/// Sets the connection string for the Postgres CDC source database.
	/// </summary>
	/// <param name="connectionString">The Postgres connection string.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="connectionString"/> is null or whitespace.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Mutually exclusive with <see cref="ConnectionStringName"/>, <see cref="ConnectionFactory"/>,
	/// and <see cref="BindConfiguration"/>. The last one set wins for connection resolution.
	/// </para>
	/// </remarks>
	IPostgresCdcBuilder ConnectionString(string connectionString);

	/// <summary>
	/// Sets a factory function that creates Postgres connections for the CDC source database.
	/// </summary>
	/// <param name="connectionFactory">A factory function that creates Postgres connections.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="connectionFactory"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Use this when you need custom connection management, such as
	/// using dependency injection for connection pooling or custom connection strings.
	/// Mutually exclusive with <see cref="ConnectionString"/> and <see cref="ConnectionStringName"/>.
	/// </para>
	/// </remarks>
	IPostgresCdcBuilder ConnectionFactory(Func<IServiceProvider, Func<NpgsqlConnection>> connectionFactory);

	/// <summary>
	/// Binds Postgres CDC source options from an <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> section.
	/// </summary>
	/// <param name="sectionPath">The configuration section path (e.g., "Cdc:Source").</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// Uses <c>OptionsBuilder&lt;T&gt;.BindConfiguration()</c> with
	/// <c>ValidateDataAnnotations</c> and <c>ValidateOnStart</c>.
	/// Binds <see cref="PostgresCdcOptions"/> from the specified section.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="sectionPath"/> is null or whitespace.
	/// </exception>
	IPostgresCdcBuilder BindConfiguration(string sectionPath);

	/// <summary>
	/// Resolves the source connection string from <c>IConfiguration.GetConnectionString(name)</c>
	/// at registration time.
	/// </summary>
	/// <param name="name">The connection string name in the <c>ConnectionStrings</c> section.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="name"/> is null or whitespace.
	/// </exception>
	IPostgresCdcBuilder ConnectionStringName(string name);
}
