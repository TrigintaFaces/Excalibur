// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Defines a retry strategy.
/// </summary>
public sealed class RetryStrategy
{
	/// <summary>
	/// Gets or sets the maximum number of retry attempts.
	/// </summary>
	/// <value>
	/// The maximum number of retry attempts.
	/// </value>
	public int MaxRetryAttempts { get; set; }

	/// <summary>
	/// Gets or sets the initial delay between retries.
	/// </summary>
	/// <value>
	/// The initial delay between retries.
	/// </value>
	public TimeSpan InitialDelay { get; set; }

	/// <summary>
	/// Gets or sets the maximum delay between retries.
	/// </summary>
	/// <value>
	/// The maximum delay between retries.
	/// </value>
	public TimeSpan MaxDelay { get; set; }

	/// <summary>
	/// Gets or sets the backoff type.
	/// </summary>
	/// <value>
	/// The backoff type.
	/// </value>
	public BackoffType BackoffType { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether gets or sets whether to add jitter to delays.
	/// </summary>
	/// <value>
	/// A value indicating whether gets or sets whether to add jitter to delays.
	/// </value>
	public bool JitterEnabled { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether gets or sets whether to enable circuit breaker.
	/// </summary>
	/// <value>
	/// A value indicating whether gets or sets whether to enable circuit breaker.
	/// </value>
	public bool CircuitBreakerEnabled { get; set; }

	/// <summary>
	/// Gets or sets the circuit breaker failure threshold.
	/// </summary>
	/// <value>
	/// The circuit breaker failure threshold.
	/// </value>
	public int CircuitBreakerThreshold { get; set; }

	/// <summary>
	/// Gets or sets the circuit breaker open duration.
	/// </summary>
	/// <value>
	/// The circuit breaker open duration.
	/// </value>
	public TimeSpan CircuitBreakerDuration { get; set; }
}
