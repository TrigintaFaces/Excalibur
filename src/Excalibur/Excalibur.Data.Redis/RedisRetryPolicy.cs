// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Data.Abstractions;
using Excalibur.Data.Abstractions.Resilience;
using Excalibur.Data.Redis.Diagnostics;

using Microsoft.Extensions.Logging;

using StackExchange.Redis;

namespace Excalibur.Data.Redis;

/// <summary>
/// Redis retry policy implementation.
/// </summary>
internal sealed partial class RedisRetryPolicy(int maxRetryAttempts, ILogger logger) : IDataRequestRetryPolicy
{
	/// <inheritdoc />
	public static TimeSpan InitialDelay => TimeSpan.FromSeconds(1);

	/// <inheritdoc />
	public static TimeSpan MaxDelay => TimeSpan.FromSeconds(30);

	/// <inheritdoc />
	public int MaxRetryAttempts { get; } = maxRetryAttempts;

	/// <inheritdoc />
	public TimeSpan BaseRetryDelay => TimeSpan.FromSeconds(1);

	/// <inheritdoc />
	public bool ShouldRetry(Exception exception, int attemptNumber) =>
		attemptNumber <= MaxRetryAttempts &&
		exception is RedisException or RedisTimeoutException or RedisConnectionException;

	/// <inheritdoc />
	public bool ShouldRetry(Exception exception) =>
		exception is RedisException or RedisTimeoutException or RedisConnectionException;

	/// <inheritdoc />
	public TimeSpan GetDelay(int attemptNumber)
	{
		var delay = TimeSpan.FromSeconds(Math.Pow(2, attemptNumber));
		return delay > MaxDelay ? MaxDelay : delay;
	}

	/// <inheritdoc />
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
					LogRetryWarning(logger, ex, attempt + 1, MaxRetryAttempts, delay.TotalMilliseconds);
					await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
				}
			}
		}

		throw lastException ?? new InvalidOperationException(
				Resources.RedisRetryPolicy_NoExceptionCapturedDuringRetryAttempts);
	}

	/// <inheritdoc />
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
					LogDocumentRetryWarning(logger, ex, attempt + 1, MaxRetryAttempts, delay.TotalMilliseconds);
					await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
				}
			}
		}

		throw lastException ?? new InvalidOperationException(
				Resources.RedisRetryPolicy_NoExceptionCapturedDuringRetryAttempts);
	}

	[LoggerMessage(DataRedisEventId.RetryWarning, LogLevel.Warning, "Redis operation failed. Retry {Attempt}/{MaxAttempts} after {Delay}ms")]
	private static partial void LogRetryWarning(ILogger logger, Exception exception, int attempt, int maxAttempts, double delay);

	[LoggerMessage(DataRedisEventId.DocumentRetryWarning, LogLevel.Warning, "Redis document operation failed. Retry {Attempt}/{MaxAttempts} after {Delay}ms")]
	private static partial void LogDocumentRetryWarning(ILogger logger, Exception exception, int attempt, int maxAttempts, double delay);
}
