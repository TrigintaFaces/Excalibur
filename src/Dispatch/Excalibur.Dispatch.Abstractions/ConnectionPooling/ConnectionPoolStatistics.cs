// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Contains statistics about connection pool usage and performance.
/// </summary>
public sealed class ConnectionPoolStatistics
{
	/// <summary>
	/// Gets or sets the name of the connection pool.
	/// </summary>
	/// <value>The current <see cref="PoolName"/> value.</value>
	public string PoolName { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the type of connections managed by this pool.
	/// </summary>
	/// <value>The current <see cref="ConnectionType"/> value.</value>
	public string ConnectionType { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the total number of connections ever created by this pool.
	/// </summary>
	/// <value>The current <see cref="TotalConnectionsCreated"/> value.</value>
	public long TotalConnectionsCreated { get; set; }

	/// <summary>
	/// Gets or sets the current number of connections in the pool (active + available).
	/// </summary>
	/// <value>The current <see cref="CurrentConnections"/> value.</value>
	public int CurrentConnections { get; set; }

	/// <summary>
	/// Gets or sets the number of connections currently in use.
	/// </summary>
	/// <value>The current <see cref="ActiveConnections"/> value.</value>
	public int ActiveConnections { get; set; }

	/// <summary>
	/// Gets or sets the number of connections available for immediate use.
	/// </summary>
	/// <value>The current <see cref="AvailableConnections"/> value.</value>
	public int AvailableConnections { get; set; }

	/// <summary>
	/// Gets or sets the maximum number of connections allowed in the pool.
	/// </summary>
	/// <value>The current <see cref="MaxConnections"/> value.</value>
	public int MaxConnections { get; set; }

	/// <summary>
	/// Gets or sets the minimum number of connections maintained in the pool.
	/// </summary>
	/// <value>The current <see cref="MinConnections"/> value.</value>
	public int MinConnections { get; set; }

	/// <summary>
	/// Gets or sets the total number of successful connection acquisitions.
	/// </summary>
	/// <value>The current <see cref="TotalAcquisitions"/> value.</value>
	public long TotalAcquisitions { get; set; }

	/// <summary>
	/// Gets or sets the number of acquisitions that were served from the pool (cache hits).
	/// </summary>
	/// <value>The current <see cref="PoolHits"/> value.</value>
	public long PoolHits { get; set; }

	/// <summary>
	/// Gets or sets the number of acquisitions that required creating new connections (cache misses).
	/// </summary>
	/// <value>The current <see cref="PoolMisses"/> value.</value>
	public long PoolMisses { get; set; }

	/// <summary>
	/// Gets or sets the total number of failed connection acquisition attempts.
	/// </summary>
	/// <value>The current <see cref="AcquisitionFailures"/> value.</value>
	public long AcquisitionFailures { get; set; }

	/// <summary>
	/// Gets or sets the total number of connections that failed health checks.
	/// </summary>
	/// <value>The current <see cref="HealthCheckFailures"/> value.</value>
	public long HealthCheckFailures { get; set; }

	/// <summary>
	/// Gets or sets the total number of connections disposed due to expiration.
	/// </summary>
	/// <value>The current <see cref="ExpiredConnections"/> value.</value>
	public long ExpiredConnections { get; set; }

	/// <summary>
	/// Gets or sets the average time to acquire a connection from the pool.
	/// </summary>
	/// <value>The current <see cref="AverageAcquisitionTime"/> value.</value>
	public TimeSpan AverageAcquisitionTime { get; set; }

	/// <summary>
	/// Gets or sets the longest time taken to acquire a connection.
	/// </summary>
	/// <value>The current <see cref="MaxAcquisitionTime"/> value.</value>
	public TimeSpan MaxAcquisitionTime { get; set; }

	/// <summary>
	/// Gets or sets the average lifetime of connections in the pool.
	/// </summary>
	/// <value>The current <see cref="AverageConnectionLifetime"/> value.</value>
	public TimeSpan AverageConnectionLifetime { get; set; }

	/// <summary>
	/// Gets or sets the time when these statistics were captured.
	/// </summary>
	/// <value>
	/// The time when these statistics were captured.
	/// </value>
	public DateTimeOffset CapturedAt { get; set; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets the pool hit rate as a percentage (0-100).
	/// </summary>
	/// <value>The current <see cref="HitRatePercentage"/> value.</value>
	public double HitRatePercentage =>
		TotalAcquisitions == 0 ? 0.0 : PoolHits * 100.0 / TotalAcquisitions;

	/// <summary>
	/// Gets the pool utilization as a percentage (0-100).
	/// </summary>
	/// <value>The current <see cref="UtilizationPercentage"/> value.</value>
	public double UtilizationPercentage =>
		MaxConnections == 0 ? 0.0 : ActiveConnections * 100.0 / MaxConnections;

	/// <summary>
	/// Gets the failure rate as a percentage (0-100).
	/// </summary>
	/// <value>The current <see cref="FailureRatePercentage"/> value.</value>
	public double FailureRatePercentage =>
		TotalAcquisitions == 0 ? 0.0 : AcquisitionFailures * 100.0 / TotalAcquisitions;

	/// <summary>
	/// Creates a summary string of key pool metrics.
	/// </summary>
	/// <returns> A formatted string containing key statistics. </returns>
	public override string ToString() =>
		$"Pool '{PoolName}': {CurrentConnections}/{MaxConnections} connections " +
		$"({ActiveConnections} active, {AvailableConnections} available), " +
		$"Hit Rate: {HitRatePercentage:F1}%, Utilization: {UtilizationPercentage:F1}%";
}
