// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Options.Resilience;

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
	/// Creates an asynchronous Polly policy based on the provided message bus options.
	/// </summary>
	/// <param name="busOptions"> The message bus configuration options containing retry settings. </param>
	/// <returns> An asynchronous Polly policy instance configured with retry, circuit breaker, and timeout behavior. </returns>
	public IAsyncPolicy Create(IMessageBusOptions busOptions)
	{
		ArgumentNullException.ThrowIfNull(busOptions);

		var retryPolicy = CreateRetryPolicyInternal(busOptions);
		var circuitBreakerPolicy = CreateCircuitBreakerPolicy(busOptions);
		var timeoutPolicy = CreateTimeoutPolicy(busOptions);

		// Combine policies: Timeout wraps Circuit Breaker wraps Retry
		return Policy.WrapAsync(timeoutPolicy, circuitBreakerPolicy, retryPolicy);
	}

	/// <summary>
	/// Creates an <see cref="IRetryPolicy"/> adapter wrapping a Polly pipeline for the given bus options.
	/// </summary>
	/// <param name="busOptions">The message bus configuration options.</param>
	/// <returns>An <see cref="IRetryPolicy"/> implementation backed by Polly.</returns>
	public IRetryPolicy CreateRetryPolicyAdapter(IMessageBusOptions busOptions)
	{
		ArgumentNullException.ThrowIfNull(busOptions);

		var retryOptions = new RetryOptions
		{
			MaxRetries = _options.MaxRetryAttempts,
			BaseDelay = _options.BaseDelay,
			BackoffStrategy = BackoffStrategy.Exponential,
			UseJitter = _options.EnableJitter,
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

	private AsyncRetryPolicy CreateRetryPolicyInternal(IMessageBusOptions busOptions)
	{
		var retryCount = _options.MaxRetryAttempts;
		var delay = _options.BaseDelay;

		return Policy
			.Handle<Exception>(IsTransientException)
			.WaitAndRetryAsync(
				retryCount,
				retryAttempt => TimeSpan.FromMilliseconds(delay.TotalMilliseconds * Math.Pow(2, retryAttempt - 1)),
				onRetry: (exception, timespan, retryCount, context) => LogRetryAttempt(
					retryCount,
					timespan.TotalMilliseconds,
					busOptions.Name ?? "Default",
					exception));
	}

	private AsyncCircuitBreakerPolicy CreateCircuitBreakerPolicy(IMessageBusOptions busOptions) =>
		Policy
			.Handle<Exception>(IsTransientException)
			.AdvancedCircuitBreakerAsync(
				failureThreshold: 0.5, // 50% failure rate
				samplingDuration: TimeSpan.FromSeconds(10),
				minimumThroughput: _options.CircuitBreakerThreshold,
				durationOfBreak: _options.CircuitBreakerDuration,
				onBreak: (result, state, duration, context) => LogCircuitBreakerOpened(
					duration.TotalSeconds,
					busOptions.Name ?? "Default"),
				onReset: context => LogCircuitBreakerReset(
					busOptions.Name ?? "Default"),
				onHalfOpen: () => LogCircuitBreakerHalfOpen(
					busOptions.Name ?? "Default"));

	private AsyncTimeoutPolicy CreateTimeoutPolicy(IMessageBusOptions busOptions) =>
		Policy.TimeoutAsync(
			_options.Timeout,
			TimeoutStrategy.Optimistic,
			onTimeoutAsync: (context, timespan, task) =>
			{
				LogOperationTimeout(
					timespan.TotalSeconds,
					busOptions.Name ?? "Default");

				return Task.CompletedTask;
			});

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
