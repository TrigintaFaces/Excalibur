// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch;

/// <summary>
/// Configures circuit breaker behavior for preventing cascading failures.
/// </summary>
public sealed class CircuitBreakerOptions
{
	/// <summary>
	/// Gets a value indicating whether circuit breaker is enabled.
	/// </summary>
	/// <value> A <see cref="bool" /> indicating whether circuit breaker is active. Defaults to <c> true </c>. </value>
	public bool Enabled { get; init; } = true;

	/// <summary>
	/// Gets the failure threshold before opening the circuit.
	/// </summary>
	/// <value> An <see cref="int" /> representing the consecutive failure count to trigger circuit opening. Defaults to 5. </value>
	public int FailureThreshold { get; init; } = 5;

	/// <summary>
	/// Gets the minimum throughput before circuit breaker evaluation.
	/// </summary>
	/// <value> An <see cref="int" /> representing the minimum requests in the sampling duration. Defaults to 10. </value>
	public int MinimumThroughput { get; init; } = 10;

	/// <summary>
	/// Gets the duration to keep the circuit open.
	/// </summary>
	/// <value> A <see cref="TimeSpan" /> representing the break duration. Defaults to 30 seconds. </value>
	public TimeSpan BreakDuration { get; init; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets the sampling duration for failure rate calculation.
	/// </summary>
	/// <value> A <see cref="TimeSpan" /> representing the sampling window. Defaults to 60 seconds. </value>
	public TimeSpan SamplingDuration { get; init; } = TimeSpan.FromSeconds(60);

	/// <summary>
	/// Gets the failure rate threshold (0.0 to 1.0) for opening the circuit.
	/// </summary>
	/// <value> A <see cref="double" /> representing the failure rate threshold. Defaults to 0.5 (50%). </value>
	public double FailureRateThreshold { get; init; } = 0.5;
}
