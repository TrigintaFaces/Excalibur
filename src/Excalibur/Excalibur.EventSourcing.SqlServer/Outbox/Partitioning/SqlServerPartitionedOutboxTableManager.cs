// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

using Excalibur.EventSourcing.SqlServer.Requests;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Excalibur.EventSourcing.SqlServer.Outbox.Partitioning;

/// <summary>
/// Manages creation and existence of partitioned outbox tables for SQL Server.
/// </summary>
/// <remarks>
/// <para>
/// When partition count is 1, uses the existing single table (backward compatible).
/// When partition count &gt; 1, creates <c>{BaseTable}_0</c> through <c>{BaseTable}_{N-1}</c>
/// plus corresponding DLQ tables <c>{BaseTable}_{N}_dlq</c>.
/// </para>
/// </remarks>
internal sealed class SqlServerPartitionedOutboxTableManager
{
	private readonly Func<SqlConnection> _connectionFactory;
	private readonly string _schema;
	private readonly string _baseTable;
	private readonly ILogger _logger;

	internal SqlServerPartitionedOutboxTableManager(
		Func<SqlConnection> connectionFactory,
		string schema,
		string baseTable,
		ILogger logger)
	{
		ArgumentNullException.ThrowIfNull(connectionFactory);
		ArgumentNullException.ThrowIfNull(logger);

		_connectionFactory = connectionFactory;
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
		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		for (var i = 0; i < partitionCount; i++)
		{
			var outboxTable = SqlTableName.Format(_schema, GetPartitionTableName(i));
			var dlqTable = SqlTableName.Format(_schema, GetPartitionDlqTableName(i));

			await EnsureTableExistsAsync(connection, outboxTable, isOutbox: true, cancellationToken).ConfigureAwait(false);
			await EnsureTableExistsAsync(connection, dlqTable, isOutbox: false, cancellationToken).ConfigureAwait(false);
		}

		_logger.LogInformation(
			"Ensured {Count} partition tables + DLQ tables exist in schema [{Schema}]",
			partitionCount, _schema);
	}

	private static async Task EnsureTableExistsAsync(
		SqlConnection connection,
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
			IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'{qualifiedTableName}') AND type = 'U')
			BEGIN
			    CREATE TABLE {qualifiedTableName} (
			        [Id] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
			        [AggregateId] NVARCHAR(256) NOT NULL,
			        [AggregateType] NVARCHAR(256) NOT NULL,
			        [EventType] NVARCHAR(256) NOT NULL,
			        [EventData] VARBINARY(MAX) NOT NULL,
			        [Metadata] NVARCHAR(MAX) NULL,
			        [CreatedAt] DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
			        [PublishedAt] DATETIMEOFFSET NULL,
			        [RetryCount] INT NOT NULL DEFAULT 0,
			        CONSTRAINT [PK_{SanitizeName(qualifiedTableName)}] PRIMARY KEY CLUSTERED ([Id])
			    );
			    CREATE INDEX [IX_{SanitizeName(qualifiedTableName)}_CreatedAt]
			        ON {qualifiedTableName} ([CreatedAt]) WHERE [PublishedAt] IS NULL;
			END
			""";
	}

	private static string SanitizeName(string name)
	{
		return name
			.Replace("[", "", StringComparison.Ordinal)
			.Replace("]", "", StringComparison.Ordinal)
			.Replace(".", "_", StringComparison.Ordinal);
	}

	private static string GetDlqTableDdl(string qualifiedTableName)
	{
		return $"""
			IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'{qualifiedTableName}') AND type = 'U')
			BEGIN
			    CREATE TABLE {qualifiedTableName} (
			        [Id] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
			        [AggregateId] NVARCHAR(256) NOT NULL,
			        [AggregateType] NVARCHAR(256) NOT NULL,
			        [EventType] NVARCHAR(256) NOT NULL,
			        [EventData] VARBINARY(MAX) NOT NULL,
			        [Metadata] NVARCHAR(MAX) NULL,
			        [CreatedAt] DATETIMEOFFSET NOT NULL,
			        [FailedAt] DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
			        [ErrorMessage] NVARCHAR(MAX) NULL,
			        [RetryCount] INT NOT NULL DEFAULT 0,
			        CONSTRAINT [PK_{SanitizeName(qualifiedTableName)}] PRIMARY KEY CLUSTERED ([Id])
			    );
			END
			""";
	}
}
