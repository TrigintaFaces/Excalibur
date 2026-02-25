// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Saga.Health;

/// <summary>
/// Configuration options for the saga health check.
/// </summary>
/// <remarks>
/// <para>
/// These options control how the health check evaluates saga infrastructure health:
/// <list type="bullet">
/// <item><description><see cref="StuckThreshold"/>: Time after which a non-updating saga is considered stuck</description></item>
/// <item><description><see cref="UnhealthyStuckThreshold"/>: Number of stuck sagas that triggers Unhealthy status</description></item>
/// <item><description><see cref="DegradedFailedThreshold"/>: Number of failed sagas that triggers Degraded status</description></item>
/// </list>
/// </para>
/// <para>
/// Example configuration:
/// <code>
/// services.AddHealthChecks()
///     .AddSagaHealthCheck(configure: options =>
///     {
///         options.StuckThreshold = TimeSpan.FromMinutes(30);
///         options.UnhealthyStuckThreshold = 5;
///         options.DegradedFailedThreshold = 3;
///     });
/// </code>
/// </para>
/// </remarks>
public sealed class SagaHealthCheckOptions
{
	/// <summary>
	/// Gets or sets the time threshold for considering a saga "stuck".
	/// A saga is stuck if it has not been updated within this duration.
	/// </summary>
	/// <value>The stuck threshold. Default is 1 hour.</value>
	public TimeSpan StuckThreshold { get; set; } = TimeSpan.FromHours(1);

	/// <summary>
	/// Gets or sets the number of stuck sagas that triggers Unhealthy status.
	/// When the stuck saga count meets or exceeds this threshold, the health check returns Unhealthy.
	/// </summary>
	/// <value>The unhealthy stuck threshold. Default is 10.</value>
	[Range(1, int.MaxValue)]
	public int UnhealthyStuckThreshold { get; set; } = 10;

	/// <summary>
	/// Gets or sets the number of failed sagas that triggers Degraded status.
	/// When the failed saga count meets or exceeds this threshold (but stuck count is below
	/// <see cref="UnhealthyStuckThreshold"/>), the health check returns Degraded.
	/// </summary>
	/// <value>The degraded failed threshold. Default is 5.</value>
	[Range(1, int.MaxValue)]
	public int DegradedFailedThreshold { get; set; } = 5;

	/// <summary>
	/// Gets or sets the maximum number of stuck sagas to retrieve for analysis.
	/// This limits the query result size for performance.
	/// </summary>
	/// <value>The stuck saga limit. Default is 100.</value>
	[Range(1, int.MaxValue)]
	public int StuckLimit { get; set; } = 100;

	/// <summary>
	/// Gets or sets the maximum number of failed sagas to retrieve for analysis.
	/// This limits the query result size for performance.
	/// </summary>
	/// <value>The failed saga limit. Default is 100.</value>
	[Range(1, int.MaxValue)]
	public int FailedLimit { get; set; } = 100;
}
