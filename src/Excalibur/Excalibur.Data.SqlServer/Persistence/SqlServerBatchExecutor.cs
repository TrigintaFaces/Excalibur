// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;
using System.Diagnostics;

using Excalibur.Data.Abstractions;
using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.SqlServer.Diagnostics;
using Excalibur.Dispatch.Abstractions.Diagnostics;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Excalibur.Data.SqlServer.Persistence;

internal sealed partial class SqlServerBatchExecutor
{
	private readonly SqlServerConnectionManager _connectionManager;
	private readonly SqlServerPersistenceMetrics _metrics;
	private readonly SqlServerPersistenceOptions _options;
	private readonly ILogger<SqlServerPersistenceProvider> _logger;

	internal SqlServerBatchExecutor(
		SqlServerConnectionManager connectionManager,
		SqlServerPersistenceMetrics metrics,
		SqlServerPersistenceOptions options,
		ILogger<SqlServerPersistenceProvider> logger)
	{
		_connectionManager = connectionManager;
		_metrics = metrics;
		_options = options;
		_logger = logger;
	}

	internal async Task<IEnumerable<object>> ExecuteBatchAsync(
		IReadOnlyList<IDataRequest<IDbConnection, object>> requests,
		CancellationToken cancellationToken)
	{
		using var activity = Activity.Current?.Source.StartActivity("SqlServer.ExecuteBatch");
		_ = activity?.SetTag("batch.size", requests.Count);

		using var connection = await _connectionManager.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
		if (connection is not SqlConnection sqlConnection)
		{
			throw new InvalidOperationException("Failed to create SQL Server connection for batch execution.");
		}

		var transaction = await sqlConnection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
		await using (transaction.ConfigureAwait(false))
		{
			var stopwatch = ValueStopwatch.StartNew();
			var results = new List<object>();

			try
			{
				foreach (var request in requests)
				{
					var result = await request.ResolveAsync(connection).ConfigureAwait(false);
					results.Add(result);
				}

				await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
				_metrics.RecordBatchExecution((long)stopwatch.Elapsed.TotalMilliseconds, success: true, requests.Count, results.Count);

				if (_options.EnableDetailedLogging)
				{
					LogBatchExecuted(_logger, requests.Count, (long)stopwatch.Elapsed.TotalMilliseconds);
				}

				return results;
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
				_metrics.RecordBatchExecution((long)stopwatch.Elapsed.TotalMilliseconds, success: false, requests.Count, 0);
				LogBatchError(_logger, ex);
				throw;
			}
		}
	}

	internal async Task<IEnumerable<object>> ExecuteBatchInTransactionAsync(
		IReadOnlyList<IDataRequest<IDbConnection, object>> requests,
		ITransactionScope transactionScope,
		CancellationToken cancellationToken)
	{
		using var activity = Activity.Current?.Source.StartActivity("SqlServer.ExecuteBatchInTransaction");
		_ = activity?.SetTag("batch.size", requests.Count);
		_ = activity?.SetTag("transaction.id", transactionScope.TransactionId);

		// Connection lifecycle is managed by the transaction scope - scope owns disposal
		// R0.8: Dispose objects before losing scope
#pragma warning disable CA2000
		var connection = await _connectionManager.GetOrCreateEnlistedConnectionAsync(transactionScope, cancellationToken)
			.ConfigureAwait(false);
#pragma warning restore CA2000
		var stopwatch = ValueStopwatch.StartNew();
		var results = new List<object>();

		try
		{
			foreach (var request in requests)
			{
				var result = await request.ResolveAsync(connection).ConfigureAwait(false);
				results.Add(result);
			}

			_metrics.RecordBatchExecution((long)stopwatch.Elapsed.TotalMilliseconds, success: true, requests.Count, results.Count);

			if (_options.EnableDetailedLogging)
			{
				LogBatchInTransactionExecuted(_logger, requests.Count, transactionScope.TransactionId,
					(long)stopwatch.Elapsed.TotalMilliseconds);
			}

			return results;
		}
		catch (Exception ex)
		{
			_metrics.RecordBatchExecution((long)stopwatch.Elapsed.TotalMilliseconds, success: false, requests.Count, 0);
			LogBatchInTransactionError(_logger, transactionScope.TransactionId, ex);
			throw;
		}
	}

	[LoggerMessage(DataSqlServerEventId.PersistenceBatchExecuted, LogLevel.Debug, "Executed batch of {RequestCount} DataRequests in {ElapsedMs}ms")]
	private static partial void LogBatchExecuted(ILogger logger, int requestCount, long elapsedMs);

	[LoggerMessage(DataSqlServerEventId.PersistenceBatchError, LogLevel.Error, "Error executing batch of DataRequests")]
	private static partial void LogBatchError(ILogger logger, Exception exception);

	[LoggerMessage(DataSqlServerEventId.PersistenceBatchInTransactionExecuted, LogLevel.Debug, "Executed batch of {RequestCount} DataRequests in transaction {TransactionId} in {ElapsedMs}ms")]
	private static partial void LogBatchInTransactionExecuted(ILogger logger, int requestCount, string transactionId, long elapsedMs);

	[LoggerMessage(DataSqlServerEventId.PersistenceBatchInTransactionError, LogLevel.Error, "Error executing batch of DataRequests in transaction {TransactionId}")]
	private static partial void LogBatchInTransactionError(ILogger logger, string transactionId, Exception exception);
}
