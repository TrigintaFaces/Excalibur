// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Options.Core;

/// <summary>
/// Configuration options for health checks.
/// </summary>
public sealed class HealthCheckOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether health checks are enabled.
	/// </summary>
	/// <value> Default is false. </value>
	public bool Enabled { get; set; }

	/// <summary>
	/// Gets or sets the health check timeout.
	/// </summary>
	/// <value> Default is 10 seconds. </value>
	public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);

	/// <summary>
	/// Gets or sets the health check interval.
	/// </summary>
	/// <value> Default is 30 seconds. </value>
	public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(30);
}
