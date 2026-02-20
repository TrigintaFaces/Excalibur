// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.Abstractions.Resilience;

/// <summary>
/// Configuration options for DataRequest resilience policies including retry, circuit breaker, and timeout settings.
/// </summary>
public interface IDataRequestResilienceOptions
{
	/// <summary>
	/// Gets or sets the maximum number of retry attempts for DataRequest execution.
	/// </summary>
	/// <value>
	/// The maximum number of retry attempts for DataRequest execution.
	/// </value>
	int MaxRetryAttempts { get; set; }

	/// <summary>
	/// Gets or sets the base delay between retry attempts.
	/// </summary>
	/// <value>
	/// The base delay between retry attempts.
	/// </value>
	TimeSpan BaseRetryDelay { get; set; }

	/// <summary>
	/// Gets or sets the maximum delay between retry attempts (for exponential backoff).
	/// </summary>
	/// <value>
	/// The maximum delay between retry attempts (for exponential backoff).
	/// </value>
	TimeSpan MaxRetryDelay { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to use exponential backoff for retry delays.
	/// </summary>
	/// <value>
	/// A value indicating whether to use exponential backoff for retry delays.
	/// </value>
	bool UseExponentialBackoff { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to add jitter to retry delays to prevent thundering herd.
	/// </summary>
	/// <value>
	/// A value indicating whether to add jitter to retry delays to prevent thundering herd.
	/// </value>
	bool UseJitter { get; set; }

	/// <summary>
	/// Gets or sets the circuit breaker failure threshold before opening the circuit.
	/// </summary>
	/// <value>
	/// The circuit breaker failure threshold before opening the circuit.
	/// </value>
	int CircuitBreakerFailureThreshold { get; set; }

	/// <summary>
	/// Gets or sets the circuit breaker sampling duration for tracking failures.
	/// </summary>
	/// <value>
	/// The circuit breaker sampling duration for tracking failures.
	/// </value>
	TimeSpan CircuitBreakerSamplingDuration { get; set; }

	/// <summary>
	/// Gets or sets the minimum throughput required for circuit breaker evaluation.
	/// </summary>
	/// <value>
	/// The minimum throughput required for circuit breaker evaluation.
	/// </value>
	int CircuitBreakerMinimumThroughput { get; set; }

	/// <summary>
	/// Gets or sets the duration the circuit breaker stays open before attempting to close.
	/// </summary>
	/// <value>
	/// The duration the circuit breaker stays open before attempting to close.
	/// </value>
	TimeSpan CircuitBreakerDurationOfBreak { get; set; }

	/// <summary>
	/// Gets or sets the default timeout for DataRequest execution.
	/// </summary>
	/// <value>
	/// The default timeout for DataRequest execution.
	/// </value>
	TimeSpan DefaultTimeout { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to enable resilience policies.
	/// </summary>
	/// <value>
	/// A value indicating whether to enable resilience policies.
	/// </value>
	bool EnableResilience { get; set; }

	/// <summary>
	/// Gets provider-specific resilience options.
	/// </summary>
	/// <value>
	/// Provider-specific resilience options.
	/// </value>
	IDictionary<string, object> ProviderSpecificOptions { get; }

	/// <summary>
	/// Validates the resilience options and throws an exception if invalid.
	/// </summary>
	/// <exception cref="ArgumentException"> Thrown when options are invalid. </exception>
	void Validate();
}
