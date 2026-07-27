// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Options.Resilience;
using Excalibur.Dispatch.Transport;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace Excalibur.Dispatch.Resilience.Polly;

/// <summary>
/// Factory for creating Polly-based retry policies for message bus operations.
/// </summary>
/// <remarks>
/// <para>
/// This factory creates comprehensive Polly policies that include retry, circuit breaker,
/// and timeout strategies wrapped together. For consumers who want fine-grained control
/// over individual retry policies, use <see cref="PollyRetryPolicyAdapter"/> instead.
/// </para>
/// <para>
/// The policies created combine:
/// <list type="bullet">
///   <item>Retry with exponential backoff</item>
///   <item>Advanced circuit breaker with failure rate detection</item>
///   <item>Optimistic timeout strategy</item>
/// </list>
/// </para>
/// </remarks>
public sealed partial class PollyRetryPolicyFactory
{
	private readonly ILogger<PollyRetryPolicyFactory> _logger;
	private readonly RetryPolicyOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="PollyRetryPolicyFactory" /> class.
	/// </summary>
	/// <param name="logger"> The logger instance. </param>
	/// <param name="options"> The retry policy options. </param>
	public PollyRetryPolicyFactory(ILogger<PollyRetryPolicyFactory> logger, IOptions<RetryPolicyOptions> options)
	{
		ArgumentNullException.ThrowIfNull(logger);
		ArgumentNullException.ThrowIfNull(options);

		_logger = logger;
		_options = options.Value;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="PollyRetryPolicyFactory" /> class with default options.
	/// </summary>
	/// <param name="logger"> The logger instance. </param>
	public PollyRetryPolicyFactory(ILogger<PollyRetryPolicyFactory> logger)
		: this(logger, Microsoft.Extensions.Options.Options.Create(new RetryPolicyOptions()))
	{
	}

	/// <summary>
	/// Creates a resilience pipeline based on the provided message bus options.
	/// </summary>
	/// <param name="busOptions"> The message bus configuration options containing retry settings. </param>
	/// <returns> A <see cref="ResiliencePipeline"/> configured with retry, circuit breaker, and timeout behavior. </returns>
	public ResiliencePipeline Create(MessageBusOptions busOptions)
	{
		ArgumentNullException.ThrowIfNull(busOptions);

		var name = busOptions.Name ?? "Default";
		var baseDelay = _options.Backoff.BaseDelay;

		// Polly v8 pipeline. Strategies execute outer→inner in the order added, so this preserves the
		// prior v7 composition: Timeout wraps Circuit Breaker wraps Retry.
		var builder = new ResiliencePipelineBuilder()
			.AddTimeout(new TimeoutStrategyOptions
			{
				Timeout = _options.Timeout,
				OnTimeout = args =>
				{
					LogOperationTimeout(args.Timeout.TotalSeconds, name);
					return default;
				},
			})
			.AddCircuitBreaker(new CircuitBreakerStrategyOptions
			{
				ShouldHandle = new PredicateBuilder().Handle<Exception>(IsTransientException),
				FailureRatio = 0.5, // 50% failure rate
				SamplingDuration = TimeSpan.FromSeconds(10),
				// Polly v8 requires a minimum throughput of at least 2.
				MinimumThroughput = Math.Max(2, _options.CircuitBreaker.CircuitBreakerThreshold),
				BreakDuration = _options.CircuitBreaker.CircuitBreakerDuration,
				OnOpened = args =>
				{
					LogCircuitBreakerOpened(args.BreakDuration.TotalSeconds, name);
					return default;
				},
				OnClosed = _ =>
				{
					LogCircuitBreakerReset(name);
					return default;
				},
				OnHalfOpened = _ =>
				{
					LogCircuitBreakerHalfOpen(name);
					return default;
				},
			});

		// Add the retry strategy only when retries are configured. Polly v8's RetryStrategyOptions rejects
		// MaxRetryAttempts < 1, whereas the prior v7 behavior treated 0 as "no retries" — so a 0 setting
		// means simply omit the retry strategy.
		if (_options.MaxRetryAttempts > 0)
		{
			builder.AddRetry(new RetryStrategyOptions
			{
				ShouldHandle = new PredicateBuilder().Handle<Exception>(IsTransientException),
				MaxRetryAttempts = _options.MaxRetryAttempts,
				DelayGenerator = args =>
				{
					// AttemptNumber is 0-based; matches the prior 2^(retryAttempt-1) exponential curve.
					var delay = TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * Math.Pow(2, args.AttemptNumber));
					return ValueTask.FromResult<TimeSpan?>(delay);
				},
				OnRetry = args =>
				{
					LogRetryAttempt(args.AttemptNumber + 1, args.RetryDelay.TotalMilliseconds, name, args.Outcome.Exception);
					return default;
				},
			});
		}

		return builder.Build();
	}

	/// <summary>
	/// Creates an <see cref="IRetryPolicy"/> adapter wrapping a Polly pipeline for the given bus options.
	/// </summary>
	/// <param name="busOptions">The message bus configuration options.</param>
	/// <returns>An <see cref="IRetryPolicy"/> implementation backed by Polly.</returns>
	public IRetryPolicy CreateRetryPolicyAdapter(MessageBusOptions busOptions)
	{
		ArgumentNullException.ThrowIfNull(busOptions);

		var retryOptions = new RetryOptions
		{
			MaxRetries = _options.MaxRetryAttempts,
			BaseDelay = _options.Backoff.BaseDelay,
			BackoffStrategy = BackoffStrategy.Exponential,
			UseJitter = _options.Backoff.EnableJitter,
		};

		return new PollyRetryPolicyAdapter(retryOptions, _logger);
	}

	private static bool IsTransientException(Exception exception) =>

		// Define what exceptions should be considered transient
		exception switch
		{
			TaskCanceledException => false,
			OperationCanceledException => false,
			BrokenCircuitException => false,
			ArgumentException => false,
			InvalidOperationException => false,
			NotSupportedException => false,
			_ => true, // Consider all other exceptions as transient by default
		};

	// Source-generated logging methods
	[LoggerMessage(ResilienceEventId.RetryAttemptStarted, LogLevel.Warning,
		"Retry {RetryCount} after {Delay}ms for operation in bus {BusName}")]
	private partial void LogRetryAttempt(int retryCount, double delay, string busName, Exception? ex);

	[LoggerMessage(ResilienceEventId.CircuitBreakerOpened, LogLevel.Error,
		"Circuit breaker opened for {Duration}s on bus {BusName}")]
	private partial void LogCircuitBreakerOpened(double duration, string busName);

	[LoggerMessage(ResilienceEventId.CircuitBreakerReset, LogLevel.Information,
		"Circuit breaker reset on bus {BusName}")]
	private partial void LogCircuitBreakerReset(string busName);

	[LoggerMessage(ResilienceEventId.CircuitBreakerHalfOpen, LogLevel.Information,
		"Circuit breaker half-open on bus {BusName}")]
	private partial void LogCircuitBreakerHalfOpen(string busName);

	[LoggerMessage(ResilienceEventId.RetryOperationTimeout, LogLevel.Warning,
		"Operation timed out after {Timeout}s on bus {BusName}")]
	private partial void LogOperationTimeout(double timeout, string busName);
}
