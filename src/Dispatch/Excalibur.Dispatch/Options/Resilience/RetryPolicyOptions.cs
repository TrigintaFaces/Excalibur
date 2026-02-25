// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Transport;

namespace Excalibur.Dispatch.Options.Resilience;

/// <summary>
/// Options for configuring retry policies.
/// </summary>
public sealed class RetryPolicyOptions
{
	/// <summary>
	/// Gets or sets the maximum number of retry attempts.
	/// </summary>
	/// <value>The current <see cref="MaxRetryAttempts"/> value.</value>
	public int MaxRetryAttempts { get; set; } = 3;

	/// <summary>
	/// Gets or sets the maximum number of retry attempts (alias for MaxRetryAttempts).
	/// </summary>
	/// <value>
	/// The maximum number of retry attempts (alias for MaxRetryAttempts).
	/// </value>
	public int MaxAttempts
	{
		get => MaxRetryAttempts;
		set => MaxRetryAttempts = value;
	}

	/// <summary>
	/// Gets or sets the retry strategy to use.
	/// </summary>
	/// <value>The current <see cref="RetryStrategy"/> value.</value>
	public RetryStrategy RetryStrategy { get; set; } = RetryStrategy.FixedDelay;

	/// <summary>
	/// Gets or sets the base delay between retries.
	/// </summary>
	/// <value>
	/// The base delay between retries.
	/// </value>
	public TimeSpan BaseDelay { get; set; } = TimeSpan.FromSeconds(1);

	/// <summary>
	/// Gets or sets the maximum delay between retries (for exponential backoff).
	/// </summary>
	/// <value>
	/// The maximum delay between retries (for exponential backoff).
	/// </value>
	public TimeSpan MaxDelay { get; set; } = TimeSpan.FromMinutes(30);

	/// <summary>
	/// Gets or sets the multiplier for exponential backoff.
	/// </summary>
	/// <value>The current <see cref="BackoffMultiplier"/> value.</value>
	public double BackoffMultiplier { get; set; } = 2.0;

	/// <summary>
	/// Gets or sets a value indicating whether gets or sets whether to enable jitter to randomize retry delays.
	/// </summary>
	/// <value>The current <see cref="EnableJitter"/> value.</value>
	public bool EnableJitter { get; set; }

	/// <summary>
	/// Gets or sets the jitter factor (0.0 to 1.0) for randomizing retry delays.
	/// </summary>
	/// <value>The current <see cref="JitterFactor"/> value.</value>
	public double JitterFactor { get; set; } = 0.1;

	/// <summary>
	/// Gets or sets the timeout duration for operations.
	/// </summary>
	/// <value>
	/// The timeout duration for operations.
	/// </value>
	public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets the collection of exception types that should trigger retries.
	/// </summary>
	/// <value>The current <see cref="RetriableExceptions"/> value.</value>
	public HashSet<Type> RetriableExceptions { get; } = [];

	/// <summary>
	/// Gets the collection of exception types that should not trigger retries.
	/// </summary>
	/// <value>The current <see cref="NonRetriableExceptions"/> value.</value>
	public HashSet<Type> NonRetriableExceptions { get; } = [];

	/// <summary>
	/// Gets or sets a value indicating whether gets or sets whether to enable circuit breaker integration.
	/// </summary>
	/// <value>The current <see cref="EnableCircuitBreaker"/> value.</value>
	public bool EnableCircuitBreaker { get; set; }

	/// <summary>
	/// Gets or sets the number of consecutive failures before opening the circuit breaker.
	/// </summary>
	/// <value>The current <see cref="CircuitBreakerThreshold"/> value.</value>
	public int CircuitBreakerThreshold { get; set; } = 5;

	/// <summary>
	/// Gets or sets the number of consecutive failures before opening the circuit breaker (alias for CircuitBreakerThreshold).
	/// </summary>
	/// <value>
	/// The number of consecutive failures before opening the circuit breaker (alias for CircuitBreakerThreshold).
	/// </value>
	public int CircuitBreakerFailureThreshold
	{
		get => CircuitBreakerThreshold;
		set => CircuitBreakerThreshold = value;
	}

	/// <summary>
	/// Gets or sets the duration the circuit breaker remains open.
	/// </summary>
	/// <value>
	/// The duration the circuit breaker remains open.
	/// </value>
	public TimeSpan CircuitBreakerDuration { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets the time to wait before attempting to close the circuit (alias for CircuitBreakerDuration).
	/// </summary>
	/// <value>
	/// The time to wait before attempting to close the circuit (alias for CircuitBreakerDuration).
	/// </value>
	public TimeSpan CircuitBreakerRecoveryTimeout
	{
		get => CircuitBreakerDuration;
		set => CircuitBreakerDuration = value;
	}
}
