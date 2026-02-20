// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Transport.AwsSqs;

/// <summary>
/// Configuration options for the AWS SQS circuit breaker integration.
/// </summary>
/// <remarks>
/// <para>
/// Integrates circuit breaker protection with AWS SQS operations via the
/// <c>IDistributedCircuitBreaker</c> from <c>Excalibur.Dispatch.Resilience.Polly</c>.
/// When the failure threshold is exceeded, the circuit opens and subsequent
/// operations fail fast without calling the AWS SDK.
/// </para>
/// <para>
/// This follows the Microsoft <c>ResiliencePipelineBuilder</c> pattern,
/// specifically the circuit breaker strategy configuration approach.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddAwsSqsCircuitBreaker(options =>
/// {
///     options.FailureThreshold = 5;
///     options.BreakDuration = TimeSpan.FromSeconds(30);
///     options.SamplingDuration = TimeSpan.FromSeconds(60);
/// });
/// </code>
/// </example>
public sealed class AwsSqsCircuitBreakerOptions
{
	/// <summary>
	/// Gets or sets the number of failures required to trip the circuit breaker.
	/// </summary>
	/// <remarks>
	/// When the number of failures within the <see cref="SamplingDuration"/> exceeds
	/// this threshold, the circuit opens and operations fail fast.
	/// </remarks>
	/// <value>The failure threshold. Default is 5.</value>
	[Range(1, 100)]
	public int FailureThreshold { get; set; } = 5;

	/// <summary>
	/// Gets or sets the duration the circuit remains open before transitioning to half-open.
	/// </summary>
	/// <remarks>
	/// After this duration, the circuit transitions to half-open state and allows
	/// a single probe operation. If the probe succeeds, the circuit closes;
	/// if it fails, the circuit reopens for another break duration.
	/// </remarks>
	/// <value>The break duration. Default is 30 seconds.</value>
	public TimeSpan BreakDuration { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets the time window over which failures are counted.
	/// </summary>
	/// <remarks>
	/// Failures are tracked within a rolling window of this duration.
	/// Once a failure falls outside the window, it no longer counts toward
	/// the <see cref="FailureThreshold"/>.
	/// </remarks>
	/// <value>The sampling duration. Default is 60 seconds.</value>
	public TimeSpan SamplingDuration { get; set; } = TimeSpan.FromSeconds(60);
}
