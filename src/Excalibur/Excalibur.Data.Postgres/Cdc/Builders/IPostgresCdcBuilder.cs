// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.Postgres.Cdc;

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
/// cdc.UsePostgres(connectionString, pg =>
/// {
///     pg.SchemaName("excalibur")
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
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="schema"/> is null or whitespace.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Default is "excalibur".
	/// </para>
	/// </remarks>
	IPostgresCdcBuilder SchemaName(string schema);

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
	/// Default is "cdc_state".
	/// </para>
	/// </remarks>
	IPostgresCdcBuilder StateTableName(string tableName);

	/// <summary>
	/// Sets the Postgres replication slot name.
	/// </summary>
	/// <param name="slotName">The replication slot name.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="slotName"/> is null or whitespace.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Default is "excalibur_cdc_slot". The slot will be created automatically
	/// if it doesn't exist.
	/// </para>
	/// </remarks>
	IPostgresCdcBuilder ReplicationSlotName(string slotName);

	/// <summary>
	/// Sets the Postgres publication name.
	/// </summary>
	/// <param name="publicationName">The publication name.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="publicationName"/> is null or whitespace.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Default is "excalibur_cdc_publication". The publication must be created
	/// on the server using CREATE PUBLICATION.
	/// </para>
	/// </remarks>
	IPostgresCdcBuilder PublicationName(string publicationName);

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
	/// Default is 1 second. Lower values increase responsiveness but also
	/// database load.
	/// </para>
	/// </remarks>
	IPostgresCdcBuilder PollingInterval(TimeSpan interval);

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
	/// Default is 1000. Larger batches can improve throughput but may increase
	/// memory usage and transaction duration.
	/// </para>
	/// </remarks>
	IPostgresCdcBuilder BatchSize(int size);

	/// <summary>
	/// Sets the timeout for replication operations.
	/// </summary>
	/// <param name="timeout">The timeout.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="timeout"/> is not positive.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Default is 30 seconds.
	/// </para>
	/// </remarks>
	IPostgresCdcBuilder Timeout(TimeSpan timeout);

	/// <summary>
	/// Sets the processor identifier for this CDC processor instance.
	/// </summary>
	/// <param name="processorId">The processor identifier.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="processorId"/> is null or whitespace.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Default is the machine name. Used to track position state when
	/// multiple processors are running.
	/// </para>
	/// </remarks>
	IPostgresCdcBuilder ProcessorId(string processorId);

	/// <summary>
	/// Sets whether to use binary protocol for logical replication.
	/// </summary>
	/// <param name="useBinary">Whether to use binary protocol.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// Default is false. Binary protocol is more efficient but may have
	/// compatibility issues with some data types.
	/// </para>
	/// </remarks>
	IPostgresCdcBuilder UseBinaryProtocol(bool useBinary = true);

	/// <summary>
	/// Sets whether to automatically create the replication slot if it doesn't exist.
	/// </summary>
	/// <param name="autoCreate">Whether to auto-create the slot.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// Default is true.
	/// </para>
	/// </remarks>
	IPostgresCdcBuilder AutoCreateSlot(bool autoCreate = true);
}
