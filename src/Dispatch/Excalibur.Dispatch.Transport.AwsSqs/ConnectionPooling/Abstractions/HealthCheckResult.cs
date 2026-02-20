// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Represents a connection pool health check result.
/// </summary>
public sealed class ConnectionPoolHealthCheckResult
{
	/// <summary>
	/// Gets or sets a value indicating whether the pool is healthy.
	/// </summary>
	/// <value>
	/// A value indicating whether the pool is healthy.
	/// </value>
	public bool IsHealthy { get; set; }

	/// <summary>
	/// Gets or sets the number of healthy connections.
	/// </summary>
	/// <value>
	/// The number of healthy connections.
	/// </value>
	public int HealthyConnections { get; set; }

	/// <summary>
	/// Gets or sets the number of unhealthy connections.
	/// </summary>
	/// <value>
	/// The number of unhealthy connections.
	/// </value>
	public int UnhealthyConnections { get; set; }

	/// <summary>
	/// Gets or sets the total number of connections.
	/// </summary>
	/// <value>
	/// The total number of connections.
	/// </value>
	public int TotalConnections { get; set; }

	/// <summary>
	/// Gets or sets the number of active connections.
	/// </summary>
	/// <value>
	/// The number of active connections.
	/// </value>
	public int ActiveConnections { get; set; }

	/// <summary>
	/// Gets or sets a descriptive message about the health check.
	/// </summary>
	/// <value>
	/// A descriptive message about the health check.
	/// </value>
	public string? Message { get; set; }

	/// <summary>
	/// Gets or sets the timestamp when the health check was performed.
	/// </summary>
	/// <value>
	/// The timestamp when the health check was performed.
	/// </value>
	public DateTimeOffset CheckedAt { get; set; } = DateTimeOffset.UtcNow;
}
