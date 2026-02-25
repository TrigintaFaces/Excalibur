// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Options.Core;

/// <summary>
/// Configuration options for message bus health checks.
/// </summary>
public sealed class MessageBusHealthCheckOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether health checks are enabled.
	/// </summary>
	/// <value> Default is false. </value>
	public bool Enabled { get; set; }

	/// <summary>
	/// Gets or sets the health check timeout.
	/// </summary>
	/// <value> Default is 15 seconds. </value>
	public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(15);

	/// <summary>
	/// Gets or sets the health check interval.
	/// </summary>
	/// <value> Default is 30 seconds. </value>
	public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets the number of consecutive failures before marking as unhealthy.
	/// </summary>
	/// <value> Default is 3. </value>
	public int FailureThreshold { get; set; } = 3;
}
