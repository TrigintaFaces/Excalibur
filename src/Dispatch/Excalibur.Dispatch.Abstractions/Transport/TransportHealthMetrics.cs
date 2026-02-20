// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Transport;

/// <summary>
/// Metrics for transport health monitoring.
/// </summary>
public sealed class TransportHealthMetrics
{
	/// <summary>
	/// Initializes a new instance of the <see cref="TransportHealthMetrics"/> class.
	/// </summary>
	/// <param name="lastCheckTimestamp">Timestamp of the last health check.</param>
	/// <param name="lastStatus">The last health status.</param>
	/// <param name="consecutiveFailures">Number of consecutive failures.</param>
	/// <param name="totalChecks">Total number of health checks performed.</param>
	/// <param name="successRate">Success rate of health checks (0.0 to 1.0).</param>
	/// <param name="averageCheckDuration">Average duration of health checks.</param>
	/// <param name="customMetrics">Custom metrics dictionary.</param>
	public TransportHealthMetrics(
		DateTimeOffset lastCheckTimestamp,
		TransportHealthStatus lastStatus,
		int consecutiveFailures,
		long totalChecks,
		double successRate,
		TimeSpan averageCheckDuration,
		IReadOnlyDictionary<string, object>? customMetrics = null)
	{
		LastCheckTimestamp = lastCheckTimestamp;
		LastStatus = lastStatus;
		ConsecutiveFailures = consecutiveFailures;
		TotalChecks = totalChecks;
		SuccessRate = successRate;
		AverageCheckDuration = averageCheckDuration;
		CustomMetrics = customMetrics ?? new Dictionary<string, object>(StringComparer.Ordinal);
	}

	/// <summary>
	/// Gets the timestamp of the last health check.
	/// </summary>
	public DateTimeOffset LastCheckTimestamp { get; }

	/// <summary>
	/// Gets the last health status.
	/// </summary>
	public TransportHealthStatus LastStatus { get; }

	/// <summary>
	/// Gets the number of consecutive failures.
	/// </summary>
	public int ConsecutiveFailures { get; }

	/// <summary>
	/// Gets the total number of health checks performed.
	/// </summary>
	public long TotalChecks { get; }

	/// <summary>
	/// Gets the success rate of health checks (0.0 to 1.0).
	/// </summary>
	public double SuccessRate { get; }

	/// <summary>
	/// Gets the average duration of health checks.
	/// </summary>
	public TimeSpan AverageCheckDuration { get; }

	/// <summary>
	/// Gets custom metrics.
	/// </summary>
	public IReadOnlyDictionary<string, object> CustomMetrics { get; }

	/// <summary>
	/// Gets a value indicating whether the component is healthy.
	/// </summary>
	public bool IsHealthy => LastStatus == TransportHealthStatus.Healthy;

	/// <summary>
	/// Gets a value indicating whether the component is stable (no recent failures and high success rate).
	/// </summary>
	public bool IsStable => ConsecutiveFailures == 0 && SuccessRate >= 0.95;
}
