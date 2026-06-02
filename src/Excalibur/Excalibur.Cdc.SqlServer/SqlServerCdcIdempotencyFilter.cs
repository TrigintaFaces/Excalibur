// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

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
/// Suitable for multi-instance deployments where multiple CDC consumers may process the
/// same events on crash/restart. The filter uses the CDC-native <c>(tableName, LSN, seqVal)</c>
/// composite key, stored in a SQL Server table with a clustered primary key for optimal
/// point-lookup performance.
/// </para>
/// <para>
/// Old records are cleaned up periodically via <see cref="CleanupAsync"/> based on the
/// configured <see cref="SqlServerCdcIdempotencyFilterOptions.RetentionPeriod"/>.
/// </para>
/// </remarks>
internal sealed partial class SqlServerCdcIdempotencyFilter : ICdcIdempotencyFilter
{
	private readonly IDbConnection _connection;
	private readonly SqlServerCdcIdempotencyFilterOptions _options;
	private readonly ILogger<SqlServerCdcIdempotencyFilter> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerCdcIdempotencyFilter"/> class.
	/// </summary>
	/// <param name="connection">The database connection (shared with CDC state store).</param>
	/// <param name="options">The idempotency filter options.</param>
	/// <param name="logger">The logger instance.</param>
	public SqlServerCdcIdempotencyFilter(
		IDbConnection connection,
		IOptions<SqlServerCdcIdempotencyFilterOptions> options,
		ILogger<SqlServerCdcIdempotencyFilter> logger)
	{
		ArgumentNullException.ThrowIfNull(connection);
		ArgumentNullException.ThrowIfNull(options);
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		_connection = connection;
		_options = options.Value;
		_options.Validate();
	}

	/// <inheritdoc />
	public async Task<bool> IsProcessedAsync(
		string tableName,
		byte[] lsn,
		byte[] seqVal,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(tableName);
		ArgumentNullException.ThrowIfNull(lsn);
		ArgumentNullException.ThrowIfNull(seqVal);

		var sql = $"""
			SELECT CASE WHEN EXISTS (
				SELECT 1 FROM {_options.QualifiedTableName}
				WHERE TableName = @tableName AND Lsn = @lsn AND SeqVal = @seqVal
			) THEN 1 ELSE 0 END
			""";

		var result = await _connection.Ready().QuerySingleAsync<int>(
			new CommandDefinition(
				sql,
				new { tableName, lsn, seqVal },
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
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(tableName);
		ArgumentNullException.ThrowIfNull(lsn);
		ArgumentNullException.ThrowIfNull(seqVal);

		var sql = $"""
			INSERT INTO {_options.QualifiedTableName} (TableName, Lsn, SeqVal, ProcessedAt)
			VALUES (@tableName, @lsn, @seqVal, SYSUTCDATETIME())
			""";

		try
		{
			await _connection.Ready().ExecuteAsync(
				new CommandDefinition(
					sql,
					new { tableName, lsn, seqVal },
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

		var deleted = await _connection.Ready().ExecuteAsync(
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
