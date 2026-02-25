// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Outbox.Health;

/// <summary>
/// Configuration options for the inbox background service health check.
/// </summary>
public sealed class InboxHealthCheckOptions
{
	/// <summary>
	/// Gets or sets how long the service can be inactive before it is considered unhealthy.
	/// </summary>
	/// <value>The inactivity timeout. Default is 5 minutes.</value>
	public TimeSpan UnhealthyInactivityTimeout { get; set; } = TimeSpan.FromMinutes(5);

	/// <summary>
	/// Gets or sets how long the service can be inactive before it is considered degraded.
	/// </summary>
	/// <value>The degraded inactivity timeout. Default is 2 minutes.</value>
	public TimeSpan DegradedInactivityTimeout { get; set; } = TimeSpan.FromMinutes(2);
}
