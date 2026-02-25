// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Data.Abstractions;
using Excalibur.Data.Abstractions.Resilience;
using Excalibur.Data.MongoDB.Diagnostics;

using Microsoft.Extensions.Logging;

using MongoDB.Driver;

namespace Excalibur.Data.MongoDB;

/// <summary>
/// MongoDB retry policy implementation.
/// </summary>
internal sealed partial class MongoDbRetryPolicy(int maxRetryAttempts, ILogger logger) : IDataRequestRetryPolicy
{
	/// <summary>
	/// Gets the initial delay before the first retry attempt.
	/// </summary>
	/// <value> The initial delay before the first retry attempt. </value>
	public static TimeSpan InitialDelay => TimeSpan.FromSeconds(1);

	/// <summary>
	/// Gets the maximum delay between retry attempts.
	/// </summary>
	/// <value> The maximum delay between retry attempts. </value>
	public static TimeSpan MaxDelay => TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets the maximum number of retry attempts.
	/// </summary>
	/// <value> The maximum number of retry attempts. </value>
	public int MaxRetryAttempts { get; } = maxRetryAttempts;

	/// <summary>
	/// Gets the base delay between retry attempts.
	/// </summary>
	/// <value> The base delay between retry attempts. </value>
	public TimeSpan BaseRetryDelay => TimeSpan.FromSeconds(1);

	/// <summary>
	/// Determines if an exception represents a transient failure that should be retried, considering the current attempt number.
	/// </summary>
	/// <param name="exception"> The exception to evaluate. </param>
	/// <param name="attemptNumber"> The current retry attempt number. </param>
	/// <returns> True if the exception represents a transient failure and the attempt number is within limits; otherwise, false. </returns>
	public bool ShouldRetry(Exception exception, int attemptNumber) =>
		attemptNumber <= MaxRetryAttempts &&
		exception is MongoException or MongoConnectionException;

	/// <summary>
	/// Determines if an exception represents a transient failure that should be retried.
	/// </summary>
	/// <param name="exception"> The exception to evaluate. </param>
	/// <returns> True if the exception represents a transient failure; otherwise, false. </returns>
	public bool ShouldRetry(Exception exception) => exception is MongoException or MongoConnectionException;

	/// <summary>
	/// Calculates the delay before the next retry attempt using exponential backoff.
	/// </summary>
	/// <param name="attemptNumber"> The current retry attempt number. </param>
	/// <returns> The delay to wait before the next retry attempt, capped at the maximum delay. </returns>
	public TimeSpan GetDelay(int attemptNumber)
	{
		var delay = TimeSpan.FromSeconds(Math.Pow(2, attemptNumber));
		return delay > MaxDelay ? MaxDelay : delay;
	}

	/// <summary>
	/// Executes a DataRequest with retry logic for transient failures.
	/// </summary>
	/// <typeparam name="TConnection"> The type of the database connection. </typeparam>
	/// <typeparam name="TResult"> The type of the result. </typeparam>
	/// <param name="request"> The data request to execute. </param>
	/// <param name="connectionFactory"> Factory function to create connections. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The result of the data request execution. </returns>
	public async Task<TResult> ResolveAsync<TConnection, TResult>(
		IDataRequest<TConnection, TResult> request,
		Func<Task<TConnection>> connectionFactory,
		CancellationToken cancellationToken)
	{
		Exception? lastException = null;

		for (var attempt = 0; attempt <= MaxRetryAttempts; attempt++)
		{
			try
			{
				var connection = await connectionFactory().ConfigureAwait(false);
				return await DataRequestExtensions.ResolveAsync(request, connection, cancellationToken).ConfigureAwait(false);
			}
			catch (Exception ex) when (ShouldRetry(ex, attempt))
			{
				lastException = ex;
				if (attempt < MaxRetryAttempts)
				{
					var delay = GetDelay(attempt + 1);
					LogMongoOperationRetry(attempt + 1, MaxRetryAttempts, delay.TotalMilliseconds, ex);
					await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
				}
			}
		}

		throw lastException ?? new InvalidOperationException("No exception captured during retry attempts");
	}

	/// <summary>
	/// Executes a document DataRequest with retry logic for transient failures.
	/// </summary>
	/// <typeparam name="TConnection"> The type of the document database connection. </typeparam>
	/// <typeparam name="TResult"> The type of the result. </typeparam>
	/// <param name="request"> The document data request to execute. </param>
	/// <param name="connectionFactory"> Factory function to create connections. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The result of the document data request execution. </returns>
	public async Task<TResult> ResolveDocumentAsync<TConnection, TResult>(
		IDocumentDataRequest<TConnection, TResult> request,
		Func<Task<TConnection>> connectionFactory,
		CancellationToken cancellationToken)
	{
		Exception? lastException = null;

		for (var attempt = 0; attempt <= MaxRetryAttempts; attempt++)
		{
			try
			{
				var connection = await connectionFactory().ConfigureAwait(false);
				return await DataRequestExtensions.ResolveAsync(request, connection, cancellationToken).ConfigureAwait(false);
			}
			catch (Exception ex) when (ShouldRetry(ex, attempt))
			{
				lastException = ex;
				if (attempt < MaxRetryAttempts)
				{
					var delay = GetDelay(attempt + 1);
					LogMongoDocumentOperationRetry(attempt + 1, MaxRetryAttempts, delay.TotalMilliseconds, ex);
					await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
				}
			}
		}

		throw lastException ?? new InvalidOperationException("No exception captured during retry attempts");
	}

	// Source-generated logging methods
	[LoggerMessage(DataMongoDbEventId.MongoOperationRetry, LogLevel.Warning,
		"MongoDB operation failed. Retry {Attempt}/{MaxAttempts} after {Delay}ms")]
	private partial void LogMongoOperationRetry(int attempt, int maxAttempts, double delay, Exception? ex);

	[LoggerMessage(DataMongoDbEventId.MongoDocumentOperationRetry, LogLevel.Warning,
		"MongoDB document operation failed. Retry {Attempt}/{MaxAttempts} after {Delay}ms")]
	private partial void LogMongoDocumentOperationRetry(int attempt, int maxAttempts, double delay, Exception? ex);
}
