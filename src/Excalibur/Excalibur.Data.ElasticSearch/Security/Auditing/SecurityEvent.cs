// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Represents a generic security event.
/// </summary>
public sealed class SecurityEvent
{
	/// <summary>
	/// Gets or sets the unique identifier for the security event.
	/// </summary>
	/// <value>
	/// The unique identifier for the security event.
	/// </value>
	public Guid EventId { get; set; }

	/// <summary>
	/// Gets or sets the timestamp when the security event occurred.
	/// </summary>
	/// <value>
	/// The timestamp when the security event occurred.
	/// </value>
	public DateTimeOffset Timestamp { get; set; }

	/// <summary>
	/// Gets or sets the type of security event.
	/// </summary>
	/// <value>
	/// The type of security event.
	/// </value>
	public string EventType { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the severity level of the security event.
	/// </summary>
	/// <value>
	/// The severity level of the security event.
	/// </value>
	public string Severity { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the source system or component that generated the event.
	/// </summary>
	/// <value>
	/// The source system or component that generated the event.
	/// </value>
	public string? Source { get; set; }

	/// <summary>
	/// Gets or sets the user identifier associated with the security event.
	/// </summary>
	/// <value>
	/// The user identifier associated with the security event.
	/// </value>
	public string? UserId { get; set; }

	/// <summary>
	/// Gets or sets the source IP address from which the event originated.
	/// </summary>
	/// <value>
	/// The source IP address from which the event originated.
	/// </value>
	public string? SourceIpAddress { get; set; }

	/// <summary>
	/// Gets or sets the user agent string associated with the security event.
	/// </summary>
	/// <value>
	/// The user agent string associated with the security event.
	/// </value>
	public string? UserAgent { get; set; }

	/// <summary>
	/// Gets or sets additional contextual data for the security event.
	/// </summary>
	/// <value>
	/// Additional contextual data for the security event.
	/// </value>
	public Dictionary<string, object>? AdditionalData { get; set; }
}
