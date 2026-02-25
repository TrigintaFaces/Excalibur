// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Configuration options for retry policies.
/// </summary>
public sealed class RetryPolicyOptions
{
	/// <summary>
	/// Gets or sets the default retry strategy.
	/// </summary>
	/// <value>
	/// The default retry strategy.
	/// </value>
	public RetryStrategy DefaultStrategy { get; set; } = new()
	{
		MaxRetryAttempts = 5,
		InitialDelay = TimeSpan.FromSeconds(5),
		MaxDelay = TimeSpan.FromMinutes(5),
		BackoffType = BackoffType.Exponential,
		JitterEnabled = true,
		CircuitBreakerEnabled = false,
	};

	/// <summary>
	/// Gets or sets the strategy for timeout errors.
	/// </summary>
	/// <value>
	/// The strategy for timeout errors.
	/// </value>
	public RetryStrategy TimeoutStrategy { get; set; } = new()
	{
		MaxRetryAttempts = 3,
		InitialDelay = TimeSpan.FromSeconds(10),
		MaxDelay = TimeSpan.FromMinutes(2),
		BackoffType = BackoffType.Linear,
		JitterEnabled = true,
		CircuitBreakerEnabled = true,
		CircuitBreakerThreshold = 3,
		CircuitBreakerDuration = TimeSpan.FromMinutes(1),
	};

	/// <summary>
	/// Gets or sets the strategy for transient errors.
	/// </summary>
	/// <value>
	/// The strategy for transient errors.
	/// </value>
	public RetryStrategy TransientErrorStrategy { get; set; } = new()
	{
		MaxRetryAttempts = 6,
		InitialDelay = TimeSpan.FromSeconds(2),
		MaxDelay = TimeSpan.FromMinutes(1),
		BackoffType = BackoffType.DecorrelatedJitter,
		JitterEnabled = false, // Already included in decorrelated jitter
		CircuitBreakerEnabled = false,
	};

	/// <summary>
	/// Gets or sets the strategy for resource exhaustion errors.
	/// </summary>
	/// <value>
	/// The strategy for resource exhaustion errors.
	/// </value>
	public RetryStrategy ResourceExhaustionStrategy { get; set; } = new()
	{
		MaxRetryAttempts = 3,
		InitialDelay = TimeSpan.FromMinutes(1),
		MaxDelay = TimeSpan.FromMinutes(10),
		BackoffType = BackoffType.Exponential,
		JitterEnabled = true,
		CircuitBreakerEnabled = true,
		CircuitBreakerThreshold = 2,
		CircuitBreakerDuration = TimeSpan.FromMinutes(5),
	};

	/// <summary>
	/// Gets custom strategies for specific message types.
	/// </summary>
	/// <value>
	/// Custom strategies for specific message types.
	/// </value>
	public Dictionary<string, RetryStrategy> CustomStrategies { get; } = [];

	/// <summary>
	/// Gets or sets a value indicating whether gets or sets whether to enable adaptive retry policies.
	/// </summary>
	/// <value>
	/// A value indicating whether gets or sets whether to enable adaptive retry policies.
	/// </value>
	public bool EnableAdaptiveRetries { get; set; } = true;
}
