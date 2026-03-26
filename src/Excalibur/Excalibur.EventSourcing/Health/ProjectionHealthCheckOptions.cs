// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.Health;

/// <summary>
/// Configuration options for <see cref="ProjectionHealthCheck"/>.
/// </summary>
internal sealed class ProjectionHealthCheckOptions
{
	/// <summary>
	/// Gets or sets the maximum lag (in events) before the async projection
	/// is considered unhealthy. Default is 1000.
	/// </summary>
	public long UnhealthyLagThreshold { get; set; } = 1000;

	/// <summary>
	/// Gets or sets the maximum lag (in events) before the async projection
	/// is considered degraded. Default is 100.
	/// </summary>
	public long DegradedLagThreshold { get; set; } = 100;

	/// <summary>
	/// Gets or sets the maximum time window for inline projection errors.
	/// If any error occurred within this window, health is degraded.
	/// Default is 5 minutes.
	/// </summary>
	public TimeSpan InlineErrorWindow { get; set; } = TimeSpan.FromMinutes(5);
}
