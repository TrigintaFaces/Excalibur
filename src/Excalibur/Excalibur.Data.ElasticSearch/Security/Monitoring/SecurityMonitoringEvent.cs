// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Represents a security monitoring event.
/// </summary>
/// <summary>
/// Represents a security monitoring event.
/// </summary>
public sealed class SecurityMonitoringEvent
{
	/// <summary>
	/// Gets or sets the type of security event.
	/// </summary>
	/// <value>
	/// The type of security event.
	/// </value>
	public string EventType { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the timestamp when the security event occurred.
	/// </summary>
	/// <value>
	/// The timestamp when the security event occurred.
	/// </value>
	public DateTimeOffset Timestamp { get; set; }

	/// <summary>
	/// Gets or sets additional details about the security event.
	/// </summary>
	/// <value>
	/// Additional details about the security event.
	/// </value>
	public string? Details { get; set; }
}
