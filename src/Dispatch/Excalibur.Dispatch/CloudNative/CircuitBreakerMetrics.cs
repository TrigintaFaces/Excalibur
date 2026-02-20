// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.CloudNative;

/// <summary>
/// Detailed metrics for circuit breaker.
/// </summary>
public sealed class CircuitBreakerMetrics
{
	private long _totalRequests;
	private long _successfulRequests;
	private long _failedRequests;
	private long _rejectedRequests;
	private long _fallbackExecutions;

	/// <summary>
	/// Gets or sets the total number of requests processed by the circuit breaker.
	/// </summary>
	/// <value> The cumulative count of requests observed by the circuit breaker. </value>
	public long TotalRequests
	{
		get => _totalRequests;
		set => _totalRequests = value;
	}

	/// <summary>
	/// Gets or sets the number of successful requests processed by the circuit breaker.
	/// </summary>
	/// <value> The count of requests that completed successfully. </value>
	public long SuccessfulRequests
	{
		get => _successfulRequests;
		set => _successfulRequests = value;
	}

	/// <summary>
	/// Gets or sets the number of failed requests processed by the circuit breaker.
	/// </summary>
	/// <value> The count of requests that ended in failure. </value>
	public long FailedRequests
	{
		get => _failedRequests;
		set => _failedRequests = value;
	}

	/// <summary>
	/// Gets or sets the number of requests rejected by the circuit breaker when open.
	/// </summary>
	/// <value> The count of requests rejected due to the breaker state. </value>
	public long RejectedRequests
	{
		get => _rejectedRequests;
		set => _rejectedRequests = value;
	}

	/// <summary>
	/// Gets or sets the number of fallback executions performed by the circuit breaker.
	/// </summary>
	/// <value> The count of fallback executions performed. </value>
	public long FallbackExecutions
	{
		get => _fallbackExecutions;
		set => _fallbackExecutions = value;
	}

	/// <summary>
	/// Gets the success rate as a percentage of successful requests out of total requests.
	/// </summary>
	/// <value> The success percentage, expressed as a fraction between 0 and 1. </value>
	public double SuccessRate => TotalRequests > 0 ? (double)SuccessfulRequests / TotalRequests : 0;

	/// <summary>
	/// Gets or sets the average response time for requests processed by the circuit breaker.
	/// </summary>
	/// <value> The mean response latency across requests. </value>
	public TimeSpan AverageResponseTime { get; set; }

	/// <summary>
	/// Gets or sets the current state of the circuit breaker (Open, Closed, HalfOpen).
	/// </summary>
	/// <value> The current state of the breaker. </value>
	public ResilienceState CurrentState { get; set; }

	/// <summary>
	/// Gets or sets the number of consecutive failures recorded by the circuit breaker.
	/// </summary>
	/// <value> The number of sequential failures tracked. </value>
	public int ConsecutiveFailures { get; set; }

	/// <summary>
	/// Gets or sets the number of consecutive successes recorded by the circuit breaker.
	/// </summary>
	/// <value> The number of sequential successes tracked. </value>
	public int ConsecutiveSuccesses { get; set; }

	internal void IncrementTotalRequests() => Interlocked.Increment(ref _totalRequests);

	internal void IncrementRejectedRequests() => Interlocked.Increment(ref _rejectedRequests);

	internal void IncrementFallbackExecutions() => Interlocked.Increment(ref _fallbackExecutions);

	internal void IncrementSuccessfulRequests() => Interlocked.Increment(ref _successfulRequests);

	internal void IncrementFailedRequests() => Interlocked.Increment(ref _failedRequests);
}
