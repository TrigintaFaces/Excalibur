// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data.Common;
using System.Net.Sockets;

using Excalibur.Data.Abstractions;
using Excalibur.Data.Abstractions.Resilience;

using Microsoft.Extensions.Logging;

using Npgsql;

namespace Excalibur.Data.Postgres.Persistence;

/// <summary>
/// Postgres-specific implementation of DataRequest retry policy that handles Postgres transient failures and provides appropriate retry logic.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="PostgresDataRequestRetryPolicy" /> class. </remarks>
/// <param name="options"> The Postgres persistence options. </param>
/// <param name="logger"> The logger for diagnostic output. </param>
public sealed class PostgresDataRequestRetryPolicy(PostgresPersistenceOptions options, ILogger logger) : IDataRequestRetryPolicy
{
	private readonly PostgresPersistenceOptions _options = options ?? throw new ArgumentNullException(nameof(options));
	private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	/// <inheritdoc />
	public int MaxRetryAttempts => _options.MaxRetryAttempts;

	/// <inheritdoc />
	public TimeSpan BaseRetryDelay => TimeSpan.FromMilliseconds(_options.RetryDelayMilliseconds);

	/// <inheritdoc />
	public async Task<TResult> ResolveAsync<TConnection, TResult>(
		IDataRequest<TConnection, TResult> request,
		Func<Task<TConnection>> connectionFactory,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);
		ArgumentNullException.ThrowIfNull(connectionFactory);

		var attempt = 0;
		Exception? lastException = null;

		while (attempt <= MaxRetryAttempts)
		{
			try
			{
				var connection = await connectionFactory().ConfigureAwait(false);
				var result = await request.ResolveAsync(connection).ConfigureAwait(false);

				if (attempt > 0)
				{
					_logger.LogInformation("DataRequest succeeded after {AttemptCount} retry attempts", attempt);
				}

				return result;
			}
			catch (Exception ex) when (ShouldRetry(ex) && attempt < MaxRetryAttempts)
			{
				lastException = ex;
				attempt++;

				var delay = CalculateDelay(attempt);

				_logger.LogWarning(
					ex,
					"DataRequest failed on attempt {AttemptCount}/{MaxAttempts}. Retrying after {DelayMs}ms. Error: {ErrorMessage}",
					attempt, MaxRetryAttempts + 1, delay.TotalMilliseconds, ex.Message);

				await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				// Non-retryable exception or max attempts reached
				_logger.LogError(
					ex,
					"DataRequest failed permanently after {AttemptCount} attempts. Error: {ErrorMessage}",
					attempt + 1, ex.Message);
				throw;
			}
		}

		// This should not be reached, but just in case
		throw lastException ?? new InvalidOperationException("DataRequest execution failed after all retry attempts");
	}

	/// <inheritdoc />
	public async Task<TResult> ResolveDocumentAsync<TConnection, TResult>(
		IDocumentDataRequest<TConnection, TResult> request,
		Func<Task<TConnection>> connectionFactory,
		CancellationToken cancellationToken)
	{
		// Postgres doesn't typically use document requests, but we can delegate to the standard execute method if the request also
		// implements IDataRequest
		if (request is IDataRequest<TConnection, TResult> dataRequest)
		{
			return await ResolveAsync(dataRequest, connectionFactory, cancellationToken).ConfigureAwait(false);
		}

		throw new NotSupportedException(
			"Postgres provider does not support document-specific DataRequests. Use regular IDataRequest<TConnection, TResult> instead.");
	}

	/// <inheritdoc />
	public bool ShouldRetry(Exception exception) =>
		exception switch
		{
			// Postgres-specific transient exceptions
			NpgsqlException { IsTransient: true } => true,

			// General database exceptions that might be transient
			DbException dbEx when IsTransientDbException(dbEx) => true,

			// Timeout exceptions are typically retryable
			TimeoutException => true,

			// Socket and network exceptions
			SocketException => true,
			IOException => true,

			// Task cancellation should not be retried
			OperationCanceledException => false,

			// Other exceptions are not retryable by default
			_ => false,
		};

	/// <summary>
	/// Determines if a database exception represents a transient failure.
	/// </summary>
	/// <param name="exception"> The database exception to evaluate. </param>
	/// <returns> True if the exception is transient; otherwise, false. </returns>
	private static bool IsTransientDbException(DbException exception)
	{
		// Check for common transient database error messages or codes
		var message = exception.Message?.ToUpperInvariant() ?? string.Empty;

		return message.Contains("TIMEOUT", StringComparison.Ordinal) ||
			   message.Contains("CONNECTION", StringComparison.Ordinal) ||
			   message.Contains("NETWORK", StringComparison.Ordinal) ||
			   message.Contains("DEADLOCK", StringComparison.Ordinal) ||
			   message.Contains("LOCK TIMEOUT", StringComparison.Ordinal) ||
			   message.Contains("CONNECTION RESET", StringComparison.Ordinal) ||
			   message.Contains("BROKEN PIPE", StringComparison.Ordinal);
	}

	/// <summary>
	/// Calculates the delay for the specified retry attempt using exponential backoff.
	/// </summary>
	/// <param name="attempt"> The retry attempt number (1-based). </param>
	/// <returns> The delay to wait before retrying. </returns>
	private TimeSpan CalculateDelay(int attempt)
	{
		// Exponential backoff with jitter
		var exponentialDelay = TimeSpan.FromMilliseconds(
			BaseRetryDelay.TotalMilliseconds * Math.Pow(2, attempt - 1));

		// Add some jitter to prevent thundering herd
		// CA5394: Random used for retry jitter, not cryptographic purposes (ADR-039)
#pragma warning disable CA5394
		var jitter = Random.Shared.NextDouble() * 0.1; // 10% jitter
#pragma warning restore CA5394
		var jitterAmount = TimeSpan.FromMilliseconds(exponentialDelay.TotalMilliseconds * jitter);

		return exponentialDelay.Add(jitterAmount);
	}
}
