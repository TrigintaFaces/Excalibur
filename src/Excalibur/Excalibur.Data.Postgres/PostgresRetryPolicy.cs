// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Data.Abstractions;
using Excalibur.Data.Abstractions.Resilience;
using Excalibur.Data.Postgres.Diagnostics;

using Microsoft.Extensions.Logging;

using Npgsql;

using Polly;
using Polly.Retry;

namespace Excalibur.Data.Postgres;

/// <summary>
/// Postgres-specific retry policy implementation for handling transient failures.
/// </summary>
public sealed partial class PostgresRetryPolicy : IDataRequestRetryPolicy
{
	private readonly PostgresProviderOptions _options;
	private readonly ILogger _logger;
	private readonly AsyncRetryPolicy _retryPolicy;

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresRetryPolicy" /> class.
	/// </summary>
	/// <param name="options"> The Postgres provider options. </param>
	/// <param name="logger"> The logger instance. </param>
	public PostgresRetryPolicy(PostgresProviderOptions options, ILogger logger)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		MaxRetryAttempts = _options.RetryCount;
		BaseRetryDelay = TimeSpan.FromSeconds(1);

		// Setup retry policy for transient failures
		_retryPolicy = Policy
			.Handle<NpgsqlException>(IsTransientError)
			.Or<PostgresException>(IsTransientError)
			.Or<TimeoutException>()
			.Or<InvalidOperationException>(ex => ex.Message.Contains("Timeout", StringComparison.OrdinalIgnoreCase))
			.WaitAndRetryAsync(
				MaxRetryAttempts,
				retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
				onRetry: (exception, timeSpan, retryCount, context) =>
					LogRetryAttempt(retryCount, timeSpan.TotalMilliseconds, exception));
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
	public async Task<TResult> ResolveDocumentAsync<TConnection, TResult>(
		IDocumentDataRequest<TConnection, TResult> request,
		Func<Task<TConnection>> connectionFactory,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);
		ArgumentNullException.ThrowIfNull(connectionFactory);

		// Postgres supports JSONB operations, so we can handle document requests
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
			PostgresException pgEx => IsTransientError(pgEx),
			NpgsqlException npgsqlEx => IsTransientError(npgsqlEx),
			TimeoutException => true,
			InvalidOperationException ioEx when ioEx.Message.Contains("Timeout", StringComparison.OrdinalIgnoreCase) => true,
			_ => false,
		};

	/// <summary>
	/// Determines if a Postgres exception represents a transient error.
	/// </summary>
	/// <param name="exception"> The Postgres exception to evaluate. </param>
	/// <returns> True if the error is transient; otherwise, false. </returns>
	private static bool IsTransientError(NpgsqlException exception)
	{
		// Postgres error codes that are considered transient
		// https://www.Postgres.org/docs/current/errcodes-appendix.html
		string[] transientErrorCodes =
		[
			"08000", "08003", "08006", "08001", "08004", // Connection exceptions
			"08007", // Connection failure during transaction
			"40001", "40P01", // Serialization failure / Deadlock
			"53000", "53100", "53200", "53300", "53400", // Insufficient resources
			"57P01", "57P02", "57P03", "57P04", // Admin shutdown / Crash shutdown / Cannot connect / Database dropped
			"58000", "58030", // System error / IO error
			"25P02", "25006", // In failed SQL transaction / Read only SQL transaction
			"55P03", // Lock not available (for advisory locks)
			"XX000", // Internal error
		];

		return exception.SqlState != null && transientErrorCodes.Any(exception.SqlState.StartsWith);
	}

	/// <summary>
	/// Determines if a Postgres exception represents a transient error.
	/// </summary>
	/// <param name="exception"> The Postgres exception to evaluate. </param>
	/// <returns> True if the error is transient; otherwise, false. </returns>
	private static bool IsTransientError(PostgresException exception)
	{
		// Postgres error codes that are considered transient
		// https://www.Postgres.org/docs/current/errcodes-appendix.html
		string[] transientErrorCodes =
		[
			"08000", "08003", "08006", "08001", "08004", // Connection exceptions
			"08007", // Connection failure during transaction
			"40001", "40P01", // Serialization failure / Deadlock
			"53000", "53100", "53200", "53300", "53400", // Insufficient resources
			"57P01", "57P02", "57P03", "57P04", // Admin shutdown / Crash shutdown / Cannot connect / Database dropped
			"58000", "58030", // System error / IO error
			"25P02", "25006", // In failed SQL transaction / Read only SQL transaction
			"55P03", // Lock not available (for advisory locks)
			"XX000", // Internal error
		];

		return exception.SqlState != null && transientErrorCodes.Any(exception.SqlState.StartsWith);
	}

	// Source-generated logging methods
	[LoggerMessage(DataPostgresEventId.RetryAttempt, LogLevel.Warning,
		"Postgres operation failed with transient error. Retry {RetryCount} after {TimeSpan}ms")]
	private partial void LogRetryAttempt(int retryCount, double timeSpan, Exception ex);
}
