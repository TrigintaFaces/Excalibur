// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Resilience.Polly;

/// <summary>
/// Options for distributed circuit breaker.
/// </summary>
public sealed class DistributedCircuitBreakerOptions
{
	/// <summary>
	/// Gets or sets failure ratio threshold (0.0 to 1.0).
	/// </summary>
	/// <value>The failure ratio threshold between 0.0 and 1.0 that triggers circuit breaking.</value>
	[Range(0.0, 1.0)]
	public double FailureRatio { get; set; } = 0.5;

	/// <summary>
	/// Gets or sets minimum number of requests before evaluating failure ratio.
	/// </summary>
	/// <value>The minimum number of requests required before failure ratio evaluation.</value>
	[Range(1, int.MaxValue)]
	public int MinimumThroughput { get; set; } = 10;

	/// <summary>
	/// Gets or sets duration to sample for failure ratio calculation.
	/// </summary>
	/// <value>The duration over which to sample requests for failure ratio calculation.</value>
	public TimeSpan SamplingDuration { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets duration the circuit remains open.
	/// </summary>
	/// <value>The duration the circuit remains in the open state before transitioning to half-open.</value>
	public TimeSpan BreakDuration { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets consecutive failures to trigger open state.
	/// </summary>
	/// <value>The number of consecutive failures required to trigger the open state.</value>
	[Range(1, int.MaxValue)]
	public int ConsecutiveFailureThreshold { get; set; } = 5;

	/// <summary>
	/// Gets or sets consecutive successes needed to close from half-open.
	/// </summary>
	/// <value>The number of consecutive successes needed to transition from half-open to closed.</value>
	[Range(1, int.MaxValue)]
	public int SuccessThresholdToClose { get; set; } = 3;

	/// <summary>
	/// Gets or sets interval for syncing distributed state.
	/// </summary>
	/// <value>The interval at which distributed circuit breaker state is synchronized.</value>
	public TimeSpan SyncInterval { get; set; } = TimeSpan.FromSeconds(5);

	/// <summary>
	/// Gets or sets how long to retain metrics in cache.
	/// </summary>
	/// <value>The duration for which metrics are retained in the cache.</value>
	public TimeSpan MetricsRetention { get; set; } = TimeSpan.FromMinutes(10);
}
