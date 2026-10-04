// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;

using Dapper;

using Excalibur.Data.SqlServer.Diagnostics;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Cdc.SqlServer;

/// <summary>
/// SQL Server-backed implementation of <see cref="ICdcIdempotencyFilter"/> that persists
/// processed event records in a database table for durable, multi-instance deduplication.
/// </summary>
/// <remarks>
/// <para>
/// Suitable for multi-instance deployments where multiple CDC consumers may process the same events on
/// crash or restart. The key is <c>(TableName, Lsn, SeqVal, ConsumerId)</c>, stored with a clustered
/// primary key for point-lookup performance.
/// </para>
/// <para>
/// <b><c>ConsumerId</c> is part of the key, not decoration</b>, and this sentence previously omitted it.
/// Without it the dedupe namespace is table-plus-position, so the first consumer to process a change marks
/// it done for every other consumer of that table and the others skip a change they never saw. A duplicate
/// merely reprocesses, which an idempotent handler absorbs; a suppression is silent and unrecoverable.
/// </para>
/// <para>
/// Old records are cleaned up periodically via <see cref="CleanupAsync"/> based on the
/// configured <see cref="SqlServerCdcIdempotencyFilterOptions.RetentionPeriod"/>.
/// </para>
/// </remarks>
internal sealed partial class SqlServerCdcIdempotencyFilter : ICdcIdempotencyFilter
{
	private readonly Func<IDbConnection> _connectionFactory;
	private readonly SqlServerCdcIdempotencyFilterOptions _options;
	private readonly ILogger<SqlServerCdcIdempotencyFilter> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerCdcIdempotencyFilter"/> class.
	/// </summary>
	/// <param name="connectionFactory">
	/// Creates a connection per operation. The filter owns and disposes each one.
	/// </param>
	/// <param name="options">The idempotency filter options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <remarks>
	/// <para>
	/// <b>A FACTORY rather than a connection, and the previous shape could not work.</b> This type is
	/// registered as a singleton, so taking an <see cref="IDbConnection"/> meant one connection held for
	/// the process lifetime and shared across every call. <see cref="IDbConnection"/> is not thread-safe,
	/// CDC processes changes concurrently, and the three statements below would have interleaved on it.
	/// A process-lifetime connection also defeats pooling and cannot recover from a transient fault.
	/// </para>
	/// <para>
	/// It was additionally unconstructable: nothing in this framework registers an
	/// <see cref="IDbConnection"/>, so resolving the filter threw before any of that could matter. Every
	/// sibling in this package takes a factory — the CDC connection builder's own shape is
	/// <c>Func&lt;IServiceProvider, Func&lt;SqlConnection&gt;&gt;</c> — and this now matches it.
	/// </para>
	/// </remarks>
	public SqlServerCdcIdempotencyFilter(
		Func<IDbConnection> connectionFactory,
		IOptions<SqlServerCdcIdempotencyFilterOptions> options,
		ILogger<SqlServerCdcIdempotencyFilter> logger)
	{
		ArgumentNullException.ThrowIfNull(connectionFactory);
		ArgumentNullException.ThrowIfNull(options);
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		_connectionFactory = connectionFactory;
		_options = options.Value;
		_options.Validate();
	}

	/// <inheritdoc />
	public async Task<bool> IsProcessedAsync(
		string tableName,
		byte[] lsn,
		byte[] seqVal,
		CdcConsumerIdentity consumer,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(tableName);
		ArgumentNullException.ThrowIfNull(lsn);
		ArgumentNullException.ThrowIfNull(seqVal);
		ArgumentException.ThrowIfNullOrWhiteSpace(consumer.ConnectionIdentifier);
		ArgumentException.ThrowIfNullOrWhiteSpace(consumer.DatabaseName);

		// The predicate carries EVERY axis the checkpoint matches on -- connection identifier, database
		// name, table -- plus the position. A key missing any of them is coarser than the position it
		// guards, and a coarser dedupe namespace suppresses: the first consumer to reach a position marks
		// it done for everyone sharing the coarser key. The database name was the axis that was missing.
		var sql = $"""
			SELECT CASE WHEN EXISTS (
				SELECT 1 FROM {_options.QualifiedTableName}
				WHERE TableName = @tableName AND Lsn = @lsn AND SeqVal = @seqVal
				  AND ConsumerId = @consumerId AND DatabaseName = @databaseName
			) THEN 1 ELSE 0 END
			""";

		using var connection = _connectionFactory();

		var result = await connection.Ready().QuerySingleAsync<int>(
			new CommandDefinition(
				sql,
				new
				{
					tableName,
					lsn,
					seqVal,
					consumerId = consumer.ConnectionIdentifier,
					databaseName = consumer.DatabaseName,
				},
				commandTimeout: DbTimeouts.RegularTimeoutSeconds,
				cancellationToken: cancellationToken)).ConfigureAwait(false);

		if (result > 0)
		{
			LogDuplicateEventSkipped(tableName, CdcChangeDetector.ByteArrayToHex(lsn), CdcChangeDetector.ByteArrayToHex(seqVal));
		}

		return result > 0;
	}

	/// <inheritdoc />
	public async Task MarkProcessedAsync(
		string tableName,
		byte[] lsn,
		byte[] seqVal,
		CdcConsumerIdentity consumer,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(tableName);
		ArgumentNullException.ThrowIfNull(lsn);
		ArgumentNullException.ThrowIfNull(seqVal);
		ArgumentException.ThrowIfNullOrWhiteSpace(consumer.ConnectionIdentifier);
		ArgumentException.ThrowIfNullOrWhiteSpace(consumer.DatabaseName);

		var sql = $"""
			INSERT INTO {_options.QualifiedTableName}
				(TableName, Lsn, SeqVal, ConsumerId, DatabaseName, ProcessedAt)
			VALUES (@tableName, @lsn, @seqVal, @consumerId, @databaseName, SYSUTCDATETIME())
			""";

		try
		{
			using var connection = _connectionFactory();

			await connection.Ready().ExecuteAsync(
				new CommandDefinition(
					sql,
					new
					{
						tableName,
						lsn,
						seqVal,
						consumerId = consumer.ConnectionIdentifier,
						databaseName = consumer.DatabaseName,
					},
					commandTimeout: DbTimeouts.RegularTimeoutSeconds,
					cancellationToken: cancellationToken)).ConfigureAwait(false);
		}
		catch (SqlException ex) when (IsDuplicateKeyViolation(ex))
		{
			// Idempotent: event was already marked by another instance or a prior call.
			LogDuplicateInsertIgnored(tableName, CdcChangeDetector.ByteArrayToHex(lsn), CdcChangeDetector.ByteArrayToHex(seqVal));
		}
	}

	/// <summary>
	/// Deletes processed event records older than the configured retention period.
	/// Uses batched DELETE to prevent long-running transactions from blocking CDC processing.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The number of records deleted.</returns>
	internal async Task<int> CleanupAsync(CancellationToken cancellationToken)
	{
		var sql = $"""
			DELETE TOP (@batchSize) FROM {_options.QualifiedTableName}
			WHERE ProcessedAt < @cutoff
			""";

		using var connection = _connectionFactory();

		var deleted = await connection.Ready().ExecuteAsync(
			new CommandDefinition(
				sql,
				new
				{
					batchSize = _options.CleanupBatchSize,
					cutoff = DateTime.UtcNow - _options.RetentionPeriod,
				},
				commandTimeout: DbTimeouts.RegularTimeoutSeconds,
				cancellationToken: cancellationToken)).ConfigureAwait(false);

		if (deleted > 0)
		{
			LogCleanupCompleted(deleted, _options.RetentionPeriod);
		}

		return deleted;
	}

	/// <summary>
	/// Checks whether the SQL exception is a primary key / unique constraint violation (error 2627 or 2601).
	/// </summary>
	private static bool IsDuplicateKeyViolation(SqlException ex)
		=> ex.Number is 2627 or 2601;

	[LoggerMessage(DataSqlServerEventId.CdcIdempotencyDuplicateSkippedSql, LogLevel.Debug,
		"Duplicate CDC event skipped (SQL): table={TableName}, LSN={Lsn}, SeqVal={SeqVal}")]
	private partial void LogDuplicateEventSkipped(string tableName, string lsn, string seqVal);

	[LoggerMessage(DataSqlServerEventId.CdcIdempotencyCleanupCompleted, LogLevel.Debug,
		"Cleaned up {Count} expired idempotency records for retention period {RetentionPeriod}")]
	private partial void LogCleanupCompleted(int count, TimeSpan retentionPeriod);

	[LoggerMessage(DataSqlServerEventId.CdcIdempotencyDuplicateInsertIgnored, LogLevel.Debug,
		"Duplicate key ignored during MarkProcessedAsync — event already tracked: table={TableName}, LSN={Lsn}, SeqVal={SeqVal}")]
	private partial void LogDuplicateInsertIgnored(string tableName, string lsn, string seqVal);
}
