// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Configures security alerting and notification systems.
/// </summary>
public sealed class SecurityAlertingOptions
{
	/// <summary>
	/// Gets a value indicating whether security alerting is enabled.
	/// </summary>
	/// <value> True to enable security event notifications, false otherwise. </value>
	public bool Enabled { get; init; } = true;

	/// <summary>
	/// Gets the minimum severity level for alerts.
	/// </summary>
	/// <value> The minimum security event severity to trigger notifications. </value>
	public SecurityEventSeverity MinimumSeverity { get; init; } = SecurityEventSeverity.Medium;

	/// <summary>
	/// Gets the alert notification channels.
	/// </summary>
	/// <value> List of notification channels for security alerts. </value>
	public List<string> NotificationChannels { get; init; } = [];

	/// <summary>
	/// Gets the alert escalation timeout.
	/// </summary>
	/// <value> The time to wait before escalating unacknowledged alerts. Defaults to 30 minutes. </value>
	public TimeSpan EscalationTimeout { get; init; } = TimeSpan.FromMinutes(30);
}
