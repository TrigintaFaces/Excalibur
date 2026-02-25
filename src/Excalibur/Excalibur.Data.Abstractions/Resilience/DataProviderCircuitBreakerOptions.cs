// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Data.Abstractions.Resilience;

/// <summary>
/// Configuration options for the data provider circuit breaker.
/// </summary>
/// <remarks>
/// Reference: Polly v8 <c>CircuitBreakerStrategyOptions</c> — minimal configuration
/// with failure threshold, break duration, and sampling window.
/// </remarks>
public sealed class DataProviderCircuitBreakerOptions
{
	/// <summary>
	/// Gets or sets the number of consecutive failures required to trip the circuit breaker.
	/// </summary>
	/// <value>The failure threshold count. Defaults to 5.</value>
	[Range(1, int.MaxValue)]
	public int FailureThreshold { get; set; } = 5;

	/// <summary>
	/// Gets or sets the duration the circuit remains open before transitioning to half-open.
	/// </summary>
	/// <value>The break duration. Defaults to 30 seconds.</value>
	public TimeSpan BreakDuration { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets the time window over which failures are counted.
	/// </summary>
	/// <value>The sampling window. Defaults to 60 seconds.</value>
	public TimeSpan SamplingWindow { get; set; } = TimeSpan.FromSeconds(60);

	/// <summary>
	/// Gets or sets the number of trial operations allowed in half-open state.
	/// </summary>
	/// <value>The number of trial operations. Defaults to 1.</value>
	[Range(1, int.MaxValue)]
	public int HalfOpenTrialCount { get; set; } = 1;
}
