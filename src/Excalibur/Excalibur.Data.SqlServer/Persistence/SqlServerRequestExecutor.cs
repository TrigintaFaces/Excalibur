// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;
using System.Diagnostics;

using Excalibur.Data.Abstractions;
using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.Abstractions.Resilience;
using Excalibur.Data.SqlServer.Diagnostics;
using Excalibur.Dispatch.Abstractions.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Data.SqlServer.Persistence;

internal sealed partial class SqlServerRequestExecutor
{
	private readonly SqlServerConnectionManager _connectionManager;
	private readonly SqlServerPersistenceMetrics _metrics;
	private readonly SqlServerPersistenceOptions _options;
	private readonly IDataRequestRetryPolicy _retryPolicy;
	private readonly ILogger<SqlServerPersistenceProvider> _logger;

	internal SqlServerRequestExecutor(
		SqlServerConnectionManager connectionManager,
		SqlServerPersistenceMetrics metrics,
		SqlServerPersistenceOptions options,
		IDataRequestRetryPolicy retryPolicy,
		ILogger<SqlServerPersistenceProvider> logger)
	{
		_connectionManager = connectionManager;
		_metrics = metrics;
		_options = options;
		_retryPolicy = retryPolicy;
		_logger = logger;
	}

	internal async Task<TResult> ExecuteAsync<TResult>(
		IDataRequest<IDbConnection, TResult> request,
		CancellationToken cancellationToken)
	{
		using var activity = Activity.Current?.Source.StartActivity("SqlServer.ExecuteDataRequest");
		_ = activity?.SetTag("request.type", request.GetType().Name);

		return await _retryPolicy.ResolveAsync(
			request,
			async () => await _connectionManager.CreateConnectionAsync(cancellationToken).ConfigureAwait(false),
			cancellationToken).ConfigureAwait(false);
	}

	internal async Task<TResult> ExecuteInTransactionAsync<TResult>(
		IDataRequest<IDbConnection, TResult> request,
		ITransactionScope transactionScope,
		CancellationToken cancellationToken)
	{
		using var activity = Activity.Current?.Source.StartActivity("SqlServer.ExecuteInTransaction");
		_ = activity?.SetTag("request.type", request.GetType().Name);
		_ = activity?.SetTag("transaction.id", transactionScope.TransactionId);

		// Connection lifecycle is managed by the transaction scope - scope owns disposal R0.8: Dispose objects before losing scope
#pragma warning disable CA2000
		var connection = await _connectionManager.GetOrCreateEnlistedConnectionAsync(transactionScope, cancellationToken)
			.ConfigureAwait(false);
#pragma warning restore CA2000
		var stopwatch = ValueStopwatch.StartNew();

		try
		{
			var result = await request.ResolveAsync(connection).ConfigureAwait(false);
			_metrics.RecordDataRequestExecution((long)stopwatch.Elapsed.TotalMilliseconds, success: true);

			if (_options.EnableDetailedLogging)
			{
				LogDataRequestExecuted(_logger, request.GetType().Name, transactionScope.TransactionId,
					(long)stopwatch.Elapsed.TotalMilliseconds);
			}

			return result;
		}
		catch (Exception ex)
		{
			_metrics.RecordDataRequestExecution((long)stopwatch.Elapsed.TotalMilliseconds, success: false);
			LogDataRequestError(_logger, request.GetType().Name, transactionScope.TransactionId, ex);
			throw;
		}
	}

	internal Task<TResult> ExecuteBulkAsync<TResult>(
		IDataRequest<IDbConnection, TResult> request,
		CancellationToken cancellationToken) =>
		_retryPolicy.ResolveAsync(
			request,
			async () => await _connectionManager.CreateConnectionAsync(cancellationToken).ConfigureAwait(false),
			cancellationToken);

	internal Task<TResult> ExecuteStoredProcedureAsync<TResult>(
		IDataRequest<IDbConnection, TResult> request,
		CancellationToken cancellationToken) =>
		_retryPolicy.ResolveAsync(
			request,
			async () => await _connectionManager.CreateConnectionAsync(cancellationToken).ConfigureAwait(false),
			cancellationToken);

	[LoggerMessage(DataSqlServerEventId.PersistenceDataRequestExecuted, LogLevel.Debug, "Executed DataRequest {RequestType} in transaction {TransactionId} in {ElapsedMs}ms")]
	private static partial void LogDataRequestExecuted(ILogger logger, string requestType, string transactionId, long elapsedMs);

	[LoggerMessage(DataSqlServerEventId.PersistenceDataRequestError, LogLevel.Error, "Error executing DataRequest {RequestType} in transaction {TransactionId}")]
	private static partial void LogDataRequestError(ILogger logger, string requestType, string transactionId, Exception exception);
}
