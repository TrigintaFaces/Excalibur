// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Data.Abstractions;
using Excalibur.Data.Abstractions.Resilience;
using Excalibur.Data.SqlServer.Diagnostics;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

using Polly;
using Polly.Retry;

namespace Excalibur.Data.SqlServer;

/// <summary>
/// SQL Server-specific retry policy implementation for handling transient failures.
/// </summary>
public sealed partial class SqlServerRetryPolicy : IDataRequestRetryPolicy
{
	private readonly SqlServerProviderOptions _options;
	private readonly ILogger _logger;
	private readonly AsyncRetryPolicy _retryPolicy;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerRetryPolicy" /> class.
	/// </summary>
	/// <param name="options"> The SQL Server provider options. </param>
	/// <param name="logger"> The logger instance. </param>
	public SqlServerRetryPolicy(SqlServerProviderOptions options, ILogger logger)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		MaxRetryAttempts = _options.RetryCount;
		BaseRetryDelay = TimeSpan.FromSeconds(1);

		// Setup retry policy for transient failures
		_retryPolicy = Policy
			.Handle<SqlException>(IsTransientError)
			.Or<TimeoutException>()
			.Or<InvalidOperationException>(ex => ex.Message.Contains("Timeout expired", StringComparison.OrdinalIgnoreCase))
			.WaitAndRetryAsync(
				MaxRetryAttempts,
				retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
				onRetry: (exception, timeSpan, retryCount, context) =>
					LogSqlServerOperationRetry(retryCount, timeSpan.TotalMilliseconds, exception));
	}

	/// <inheritdoc />
	public int MaxRetryAttempts { get; }

	/// <inheritdoc />
	public TimeSpan BaseRetryDelay { get; }

	/// <inheritdoc />
	public async Task<TResult> ResolveAsync<TConnection, TResult>(
		IDataRequest<TConnection, TResult> request,
		Func<Task<TConnection>> connectionFactory,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);
		ArgumentNullException.ThrowIfNull(connectionFactory);

		return await _retryPolicy.ExecuteAsync(
			async ct =>
			{
				var connection = await connectionFactory().ConfigureAwait(false);
				try
				{
					return await request.ResolveAsync(connection).ConfigureAwait(false);
				}
				finally
				{
					// Dispose connection if it's disposable
					if (connection is IDisposable disposable)
					{
						disposable.Dispose();
					}
				}
			}, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public Task<TResult> ResolveDocumentAsync<TConnection, TResult>(
		IDocumentDataRequest<TConnection, TResult> request,
		Func<Task<TConnection>> connectionFactory,
		CancellationToken cancellationToken) =>

		// SQL Server doesn't support document operations directly
		throw new NotSupportedException("SQL Server does not support document-based data requests.");

	/// <inheritdoc />
	public bool ShouldRetry(Exception exception) =>
		exception switch
		{
			SqlException sqlEx => IsTransientError(sqlEx),
			TimeoutException => true,
			InvalidOperationException ioEx when ioEx.Message.Contains("Timeout expired", StringComparison.OrdinalIgnoreCase) => true,
			_ => false,
		};

	/// <summary>
	/// Determines if a SQL exception represents a transient error.
	/// </summary>
	/// <param name="exception"> The SQL exception to evaluate. </param>
	/// <returns> True if the error is transient; otherwise, false. </returns>
	private static bool IsTransientError(SqlException exception)
	{
		// List of SQL error numbers that are considered transient
		// https://docs.microsoft.com/en-us/azure/azure-sql/database/troubleshoot-common-errors-issues
		// https://docs.microsoft.com/en-us/sql/relational-databases/errors-events/database-engine-events-and-errors
		int[] transientErrors =
		[
			49918, 49919, 49920, // Resource governance errors
			4060, 4221, // Login failures
			40143, 40613, 40501, 40540, 40197, // Service errors
			10928, 10929, // Resource limit errors
			20, 64, 233, // Connection errors
			8645, 8651, 8657, // Memory errors
			1204, 1205, 1222, // Lock/deadlock errors
			-2, 2, 53, // Network errors
			701, 802, // Memory pressure
			617, 669, 671, // Other resource errors
			596, // Session killed by backup/restore operation (critical for CDC)
			9001, 9002, // Transaction log errors (log not available, log full)
			3960, 3961, // Snapshot isolation conflicts
			121, 1232, // Connection transport errors
			0, // Provider: Named Pipes Provider, error: 40 - Could not open a connection to SQL Server
		];

		return transientErrors.Contains(exception.Number);
	}

	// Source-generated logging methods
	[LoggerMessage(DataSqlServerEventId.SqlServerOperationRetry, LogLevel.Warning,
		"SQL Server operation failed with transient error. Retry {RetryCount} after {TimeSpan}ms")]
	private partial void LogSqlServerOperationRetry(int retryCount, double timeSpan, Exception ex);
}
