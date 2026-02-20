// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Options.Resilience;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Resilience;

/// <summary>
/// Default retry policy implementation using <see cref="IBackoffCalculator"/> for delay calculation.
/// </summary>
/// <remarks>
/// <para>
/// This is a zero-dependency implementation that provides retry functionality without
/// requiring external libraries like Polly. It uses the existing <see cref="IBackoffCalculator"/>
/// infrastructure for calculating retry delays.
/// </para>
/// <para>
/// The policy handles:
/// <list type="bullet">
///   <item>Maximum retry attempts with configurable count</item>
///   <item>Configurable retriable/non-retriable exception types</item>
///   <item>Automatic exclusion of cancellation exceptions from retry</item>
///   <item>Backoff delay calculation via <see cref="IBackoffCalculator"/></item>
/// </list>
/// </para>
/// </remarks>
public sealed partial class DefaultRetryPolicy : IRetryPolicy
{
	private readonly RetryPolicyOptions _options;
	private readonly IBackoffCalculator _backoff;
	private readonly ILogger _logger;
	private readonly bool _hasRetriableFilters;
	private readonly bool _hasNonRetriableFilters;

	/// <summary>
	/// Initializes a new instance of the <see cref="DefaultRetryPolicy"/> class.
	/// </summary>
	/// <param name="options">The retry policy options.</param>
	/// <param name="backoff">
	/// Optional backoff calculator. If null, defaults to <see cref="ExponentialBackoffCalculator"/>
	/// configured from the provided options.
	/// </param>
	/// <param name="logger">Optional logger instance.</param>
	public DefaultRetryPolicy(
		RetryPolicyOptions options,
		IBackoffCalculator? backoff = null,
		ILogger<DefaultRetryPolicy>? logger = null)
	{
		ArgumentNullException.ThrowIfNull(options);

		_options = options;
		_logger = logger ?? NullLogger<DefaultRetryPolicy>.Instance;
		_backoff = backoff ?? new ExponentialBackoffCalculator(
			options.BaseDelay,
			options.MaxDelay,
			options.BackoffMultiplier,
			options.EnableJitter,
			options.JitterFactor);
		_hasRetriableFilters = options.RetriableExceptions.Count > 0;
		_hasNonRetriableFilters = options.NonRetriableExceptions.Count > 0;
	}

	/// <inheritdoc />
	public async Task<TResult> ExecuteAsync<TResult>(
		Func<CancellationToken, Task<TResult>> action,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(action);

		var attempt = 0;
		while (true)
		{
			try
			{
				attempt++;
				return await action(cancellationToken).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				if (IsCancellation(ex))
				{
					throw;
				}

				if (attempt >= _options.MaxRetryAttempts || !ShouldRetry(ex))
				{
					if (attempt >= _options.MaxRetryAttempts)
					{
						LogMaxRetriesExceeded(_logger, _options.MaxRetryAttempts);
					}

					throw;
				}

				var delay = _backoff.CalculateDelay(attempt);
				LogRetryAttempt(_logger, attempt, _options.MaxRetryAttempts, delay.TotalMilliseconds);

				if (delay > TimeSpan.Zero)
				{
					await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
				}
			}
		}
	}

	/// <inheritdoc />
	public async Task ExecuteAsync(
		Func<CancellationToken, Task> action,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(action);

		_ = await ExecuteAsync(async ct =>
		{
			await action(ct).ConfigureAwait(false);
			return true;
		}, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Determines whether the exception is a cancellation exception.
	/// </summary>
	private static bool IsCancellation(Exception ex) =>
		ex is OperationCanceledException or TaskCanceledException;

	// Source-generated logging methods
	[LoggerMessage(MiddlewareEventId.RetryAttemptStarted, LogLevel.Warning,
		"Retry attempt {Attempt}/{MaxAttempts} after {DelayMs}ms delay")]
	private static partial void LogRetryAttempt(ILogger logger, int attempt, int maxAttempts, double delayMs);

	[LoggerMessage(MiddlewareEventId.RetryExhausted, LogLevel.Error,
		"Max retry attempts ({MaxAttempts}) exceeded")]
	private static partial void LogMaxRetriesExceeded(ILogger logger, int maxAttempts);

	/// <summary>
	/// Determines whether the exception should trigger a retry.
	/// </summary>
	/// <param name="ex">The exception that occurred.</param>
	/// <returns><c>true</c> if the operation should be retried; otherwise, <c>false</c>.</returns>
	private bool ShouldRetry(Exception ex)
	{
		var exceptionType = ex.GetType();

		// Don't retry if this exception type is explicitly non-retriable
		if (_hasNonRetriableFilters && _options.NonRetriableExceptions.Contains(exceptionType))
		{
			return false;
		}

		// If retriable exceptions list is specified, only retry those types
		if (_hasRetriableFilters)
		{
			return _options.RetriableExceptions.Contains(exceptionType);
		}

		// By default, retry all non-cancellation exceptions
		return true;
	}
}
