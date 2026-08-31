// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Data.Resilience;
using Excalibur.Data.SqlServer.Diagnostics;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

using Polly;
using Polly.Retry;

namespace Excalibur.Data.SqlServer;

/// <summary>
/// SQL Server-specific retry policy implementation for handling transient failures.
/// </summary>
internal sealed partial class SqlServerRetryPolicy : IRelationalDataRequestRetryPolicy
{
	private readonly ILogger _logger;
	private readonly AsyncRetryPolicy _retryPolicy;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerRetryPolicy" /> class.
	/// </summary>
	/// <param name="options"> The SQL Server provider options. </param>
	/// <param name="logger"> The logger instance. </param>
	public SqlServerRetryPolicy(SqlServerProviderOptions options, ILogger logger)
		: this(
			(options ?? throw new ArgumentNullException(nameof(options))).RetryCount,
			TimeSpan.FromSeconds(1),
			logger)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerRetryPolicy" /> class from explicit retry settings.
	/// </summary>
	/// <param name="maxRetryAttempts"> The maximum number of retry attempts. </param>
	/// <param name="baseRetryDelay"> The base delay between retry attempts. </param>
	/// <param name="logger"> The logger instance. </param>
	public SqlServerRetryPolicy(int maxRetryAttempts, TimeSpan baseRetryDelay, ILogger logger)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		MaxRetryAttempts = maxRetryAttempts;
		BaseRetryDelay = baseRetryDelay;

		// Setup retry policy for transient failures
		_retryPolicy = Policy
			.Handle<SqlException>(IsTransientError)
			.Or<TimeoutException>()
			.Or<InvalidOperationException>(ex => ex.Message.Contains("Timeout expired", StringComparison.OrdinalIgnoreCase))
			.WaitAndRetryAsync(
				MaxRetryAttempts,
				CalculateDelay,
				onRetry: (exception, timeSpan, retryCount, context) =>
					LogSqlServerOperationRetry(retryCount, timeSpan.TotalMilliseconds, exception));
	}

	/// <summary>
	/// Gets the ceiling on any single backoff delay.
	/// </summary>
	/// <value> The ceiling on any single backoff delay. Thirty seconds, matching the other providers. </value>
	private static TimeSpan MaxRetryDelay => TimeSpan.FromSeconds(30);

	/// <inheritdoc />
	public int MaxRetryAttempts { get; }

	/// <inheritdoc />
	public TimeSpan BaseRetryDelay { get; }

	/// <summary>
	/// Calculates the backoff delay before a retry attempt, grown exponentially from
	/// <see cref="BaseRetryDelay" /> and bounded by <see cref="MaxRetryDelay" />.
	/// </summary>
	/// <param name="retryAttempt"> The retry attempt number (1-based). </param>
	/// <returns> The delay to wait before retrying, never exceeding <see cref="MaxRetryDelay" />. </returns>
	/// <remarks>
	/// <para>
	/// The schedule is grown from the base delay this type was constructed with. It previously ignored
	/// that value entirely and returned two raised to the attempt number, in seconds - so a caller who
	/// supplied a base delay was given a schedule unrelated to it, and the delay had no ceiling at all.
	/// </para>
	/// <para>
	/// Composed with a minimum, so the ceiling can only ever tighten the wait; relaxing it requires
	/// turning the minimum into a maximum rather than any ordinary edit.
	/// </para>
	/// </remarks>
	internal TimeSpan CalculateDelay(int retryAttempt)
	{
		var exponential = BaseRetryDelay.TotalMilliseconds * Math.Pow(2, retryAttempt - 1);
		return TimeSpan.FromMilliseconds(Math.Min(exponential, MaxRetryDelay.TotalMilliseconds));
	}

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
