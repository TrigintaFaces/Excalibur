// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

using Excalibur.EventSourcing.Postgres.Requests;

using Microsoft.Extensions.Logging;

using Npgsql;

namespace Excalibur.EventSourcing.Postgres.Outbox.Partitioning;

/// <summary>
/// Manages creation and existence of partitioned outbox tables for Postgres.
/// </summary>
/// <remarks>
/// <para>
/// When partition count is 1, uses the existing single table (backward compatible).
/// When partition count &gt; 1, creates <c>{base_table}_0</c> through <c>{base_table}_{N-1}</c>
/// plus corresponding DLQ tables <c>{base_table}_{N}_dlq</c>.
/// </para>
/// <para>
/// Uses standard Postgres DDL (no native <c>PARTITION BY HASH</c>) for portability
/// and control over partition assignment via <c>IOutboxPartitioner</c>.
/// </para>
/// </remarks>
internal sealed class PostgresPartitionedOutboxTableManager
{
	private readonly NpgsqlDataSource _dataSource;
	private readonly string _schema;
	private readonly string _baseTable;
	private readonly ILogger _logger;

	internal PostgresPartitionedOutboxTableManager(
		NpgsqlDataSource dataSource,
		string schema,
		string baseTable,
		ILogger logger)
	{
		ArgumentNullException.ThrowIfNull(dataSource);
		ArgumentNullException.ThrowIfNull(logger);

		_dataSource = dataSource;
		_schema = schema;
		_baseTable = baseTable;
		_logger = logger;
	}

	/// <summary>
	/// Gets the outbox table name for a specific partition.
	/// </summary>
	internal string GetPartitionTableName(int partitionIndex)
	{
		return $"{_baseTable}_{partitionIndex}";
	}

	/// <summary>
	/// Gets the DLQ table name for a specific partition.
	/// </summary>
	internal string GetPartitionDlqTableName(int partitionIndex)
	{
		return $"{_baseTable}_{partitionIndex}_dlq";
	}

	/// <summary>
	/// Ensures all partition tables and DLQ tables exist.
	/// </summary>
	internal async Task EnsurePartitionTablesExistAsync(
		int partitionCount,
		CancellationToken cancellationToken)
	{
		await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

		for (var i = 0; i < partitionCount; i++)
		{
			var outboxTable = PgTableName.Format(_schema, GetPartitionTableName(i));
			var dlqTable = PgTableName.Format(_schema, GetPartitionDlqTableName(i));

			await EnsureTableExistsAsync(connection, outboxTable, isOutbox: true, cancellationToken).ConfigureAwait(false);
			await EnsureTableExistsAsync(connection, dlqTable, isOutbox: false, cancellationToken).ConfigureAwait(false);
		}

		_logger.LogInformation(
			"Ensured {Count} partition tables + DLQ tables exist in schema \"{Schema}\"",
			partitionCount, _schema);
	}

	private static async Task EnsureTableExistsAsync(
		NpgsqlConnection connection,
		string qualifiedTableName,
		bool isOutbox,
		CancellationToken cancellationToken)
	{
		var ddl = isOutbox
			? GetOutboxTableDdl(qualifiedTableName)
			: GetDlqTableDdl(qualifiedTableName);

		await connection.ExecuteAsync(new CommandDefinition(ddl, cancellationToken: cancellationToken))
			.ConfigureAwait(false);
	}

	private static string GetOutboxTableDdl(string qualifiedTableName)
	{
		return $"""
			CREATE TABLE IF NOT EXISTS {qualifiedTableName} (
			    id UUID NOT NULL DEFAULT gen_random_uuid(),
			    aggregate_id VARCHAR(256) NOT NULL,
			    aggregate_type VARCHAR(256) NOT NULL,
			    event_type VARCHAR(256) NOT NULL,
			    event_data BYTEA NOT NULL,
			    metadata TEXT NULL,
			    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
			    published_at TIMESTAMPTZ NULL,
			    retry_count INT NOT NULL DEFAULT 0,
			    PRIMARY KEY (id)
			);
			CREATE INDEX IF NOT EXISTS ix_{SanitizeName(qualifiedTableName)}_created_at
			    ON {qualifiedTableName} (created_at) WHERE published_at IS NULL;
			""";
	}

	private static string SanitizeName(string name)
	{
		return name
			.Replace("\"", "", StringComparison.Ordinal)
			.Replace(".", "_", StringComparison.Ordinal);
	}

	private static string GetDlqTableDdl(string qualifiedTableName)
	{
		return $"""
			CREATE TABLE IF NOT EXISTS {qualifiedTableName} (
			    id UUID NOT NULL DEFAULT gen_random_uuid(),
			    aggregate_id VARCHAR(256) NOT NULL,
			    aggregate_type VARCHAR(256) NOT NULL,
			    event_type VARCHAR(256) NOT NULL,
			    event_data BYTEA NOT NULL,
			    metadata TEXT NULL,
			    created_at TIMESTAMPTZ NOT NULL,
			    failed_at TIMESTAMPTZ NOT NULL DEFAULT now(),
			    error_message TEXT NULL,
			    retry_count INT NOT NULL DEFAULT 0,
			    PRIMARY KEY (id)
			);
			""";
	}
}
