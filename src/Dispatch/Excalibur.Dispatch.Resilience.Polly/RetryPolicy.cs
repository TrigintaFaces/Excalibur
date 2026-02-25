// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Security.Cryptography;

using Excalibur.Dispatch.Abstractions.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Polly;
using Polly.Retry;

namespace Excalibur.Dispatch.Resilience.Polly;

/// <summary>
/// Retry policy with jitter to prevent thundering herd problems.
/// </summary>
public partial class RetryPolicy : IRetryPolicy
{
	private readonly ResiliencePipeline _pipeline;
	private readonly RetryOptions _options;
	private readonly ILogger _logger;
	private readonly ITimeoutManager? _timeoutManager;

	/// <summary>
	/// Initializes a new instance of the <see cref="RetryPolicy" /> class.
	/// </summary>
	/// <param name="options"> Retry options. </param>
	/// <param name="logger"> Optional logger instance. </param>
	/// <param name="timeoutManager"> Optional timeout manager. </param>
	public RetryPolicy(
		RetryOptions options,
		ILogger? logger = null,
		ITimeoutManager? timeoutManager = null)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? NullLogger.Instance;
		_timeoutManager = timeoutManager;

		LogJitterStrategyUsed(_options.JitterStrategy.ToString(), _options.JitterFactor);

		// Build the resilience pipeline with enhanced retry
		_pipeline = BuildPipeline();
	}

	/// <inheritdoc />
	public Task<TResult> ExecuteAsync<TResult>(
		Func<CancellationToken, Task<TResult>> action,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(action);
		return ExecuteAsync(() => action(cancellationToken), cancellationToken);
	}

	/// <inheritdoc />
	public Task ExecuteAsync(
		Func<CancellationToken, Task> action,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(action);
		return ExecuteAsync(() => action(cancellationToken), cancellationToken);
	}

	/// <summary>
	/// Executes an operation with enhanced retry logic including jitter.
	/// </summary>
	/// <typeparam name="T">The type of result returned by the operation.</typeparam>
	/// <param name="operation">The operation to execute with retry protection.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="operationName">Optional name for the operation used in logging and metrics.</param>
	/// <returns>The result of the operation.</returns>
	public async Task<T> ExecuteAsync<T>(
		Func<Task<T>> operation,
		CancellationToken cancellationToken,
		string? operationName = null)
	{
		ArgumentNullException.ThrowIfNull(operation);

		var stopwatch = ValueStopwatch.StartNew();

		try
		{
			// Get timeout from manager if available and operation name provided
			using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			cts.CancelAfter(operationName != null && _timeoutManager != null
				? _timeoutManager.GetTimeout(operationName)
				: _options.OperationTimeout ?? TimeSpan.FromSeconds(30));

			return await _pipeline.ExecuteAsync(
				async _ => await operation().ConfigureAwait(false),
				cts.Token).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			LogMaxRetriesExceeded(
				_options.MaxRetries,
				_options.MaxRetries,
				stopwatch.ElapsedMilliseconds,
				ex);
			throw;
		}
	}

	/// <summary>
	/// Executes an operation with enhanced retry logic including jitter.
	/// </summary>
	/// <param name="operation">The operation to execute with retry protection.</param>
	/// <param name="operationName">Optional name for the operation used in logging and metrics.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	public async Task ExecuteAsync(
		Func<Task> operation,
		CancellationToken cancellationToken,
		string? operationName = null) =>
		_ = await ExecuteAsync(
			async () =>
			{
				await operation().ConfigureAwait(false);
				return 0; // Dummy return value
			}, cancellationToken, operationName).ConfigureAwait(false);

	private static bool IsRetryableException(Exception ex) =>

		// Default retryable exceptions
		ex switch
		{
			TimeoutException => true,
			OperationCanceledException => false, // Don't retry cancellations
			ArgumentException => false, // Don't retry bad arguments
			InvalidOperationException => false, // Don't retry invalid operations
			_ => true, // Retry other exceptions by default
		};

	private ResiliencePipeline BuildPipeline()
	{
		var builder = new ResiliencePipelineBuilder();

		// Add retry with custom delay generator that includes jitter
		_ = builder.AddRetry(new RetryStrategyOptions
		{
			MaxRetryAttempts = _options.MaxRetries,
			DelayGenerator = GenerateDelayWithJitterAsync,
			ShouldHandle = new PredicateBuilder()
				.Handle<Exception>(ex => _options.ShouldRetry?.Invoke(ex) ?? IsRetryableException(ex)),
			OnRetry = args =>
			{
				var delay = args.RetryDelay;
				LogRetryWithJitter(
					args.AttemptNumber,
					delay.TotalMilliseconds,
					_options.BaseDelay.TotalMilliseconds,
					args.Outcome.Exception?.Message,
					args.Outcome.Exception);
				return ValueTask.CompletedTask;
			},
		});

		// Add timeout if configured
		if (_options.OperationTimeout.HasValue)
		{
			_ = builder.AddTimeout(_options.OperationTimeout.Value);
		}

		return builder.Build();
	}

	private ValueTask<TimeSpan?> GenerateDelayWithJitterAsync(RetryDelayGeneratorArguments<object> args)
	{
		// Calculate base delay using configured backoff strategy
		var baseDelay = CalculateBaseDelay(args.AttemptNumber);

		// Apply jitter based on strategy
		var jitteredDelay = ApplyJitter(baseDelay, args.AttemptNumber);

		// Apply max delay cap if configured
		if (_options.MaxDelay.HasValue && jitteredDelay > _options.MaxDelay.Value)
		{
			jitteredDelay = _options.MaxDelay.Value;
		}

		return ValueTask.FromResult<TimeSpan?>(jitteredDelay);
	}

	private TimeSpan CalculateBaseDelay(int attemptNumber) =>
		_options.BackoffStrategy switch
		{
			BackoffStrategy.Fixed => _options.BaseDelay,
			BackoffStrategy.Linear => TimeSpan.FromMilliseconds(_options.BaseDelay.TotalMilliseconds * attemptNumber),
			BackoffStrategy.Exponential => TimeSpan.FromMilliseconds(_options.BaseDelay.TotalMilliseconds *
																	 Math.Pow(2, attemptNumber - 1)),
			BackoffStrategy.Fibonacci => CalculateFibonacciDelay(attemptNumber),
			_ => _options.BaseDelay,
		};

	private TimeSpan ApplyJitter(TimeSpan baseDelay, int attemptNumber) => _options.JitterStrategy switch
	{
		JitterStrategy.None => baseDelay,
		JitterStrategy.Full => DelayHelpers.ApplyFullJitter(baseDelay),
		JitterStrategy.Equal => DelayHelpers.ApplyEqualJitter(baseDelay),
		JitterStrategy.Decorrelated => ApplyDecorrelatedJitter(baseDelay),
		JitterStrategy.Exponential => DelayHelpers.ApplyExponentialJitter(baseDelay, attemptNumber),
		_ => baseDelay,
	};

	private TimeSpan ApplyDecorrelatedJitter(TimeSpan baseDelay)
	{
		var minDelay = _options.BaseDelay.TotalMilliseconds;
		var maxDelay = baseDelay.TotalMilliseconds * 3;
		var jitterMs = minDelay + (DelayHelpers.GetSecureRandomDouble() * (maxDelay - minDelay));
		return TimeSpan.FromMilliseconds(jitterMs);
	}

	private TimeSpan CalculateFibonacciDelay(int attemptNumber)
	{
		int fib1 = 1, fib2 = 1;
		for (var i = 2; i < attemptNumber; i++)
		{
			var temp = fib1 + fib2;
			fib1 = fib2;
			fib2 = temp;
		}

		return TimeSpan.FromMilliseconds(_options.BaseDelay.TotalMilliseconds * fib2);
	}

	// Source-generated logging methods
	[LoggerMessage(ResilienceEventId.RetryWithJitter, LogLevel.Information,
		"Retry attempt {AttemptNumber} with jittered delay {JitteredDelay}ms (base: {BaseDelay}ms). Reason: {Reason}")]
	private partial void LogRetryWithJitter(int attemptNumber, double jitteredDelay, double baseDelay, string? reason, Exception? ex);

	[LoggerMessage(ResilienceEventId.RetryMaxExceeded, LogLevel.Error,
		"Max retries ({MaxRetries}) exceeded after {Attempts} attempts over {TotalTime}ms")]
	private partial void LogMaxRetriesExceeded(int maxRetries, int attempts, double totalTime, Exception? ex);

	[LoggerMessage(ResilienceEventId.JitterStrategyUsed, LogLevel.Debug,
		"Using jitter strategy '{Strategy}' with factor {Factor}")]
	private partial void LogJitterStrategyUsed(string strategy, double factor);

	private static class DelayHelpers
	{
		internal static TimeSpan ApplyFullJitter(TimeSpan baseDelay)
		{
			var jitterMs = GetSecureRandomDouble() * baseDelay.TotalMilliseconds;
			return TimeSpan.FromMilliseconds(jitterMs);
		}

		internal static TimeSpan ApplyEqualJitter(TimeSpan baseDelay)
		{
			var halfDelay = baseDelay.TotalMilliseconds / 2;
			var jitterMs = halfDelay + (GetSecureRandomDouble() * halfDelay);
			return TimeSpan.FromMilliseconds(jitterMs);
		}

		internal static TimeSpan ApplyExponentialJitter(TimeSpan baseDelay, int attemptNumber)
		{
			var jitterRange = Math.Pow(2, attemptNumber) * 0.5;
			var jitterMs = baseDelay.TotalMilliseconds + (GetSecureRandomDouble() * jitterRange * 1000);
			return TimeSpan.FromMilliseconds(jitterMs);
		}

		internal static double GetSecureRandomDouble()
		{
			Span<byte> bytes = stackalloc byte[8];
			RandomNumberGenerator.Fill(bytes);
			var value = BitConverter.ToUInt64(bytes);
			return (double)value / ulong.MaxValue;
		}
	}
}
