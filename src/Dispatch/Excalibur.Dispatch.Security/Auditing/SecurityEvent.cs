// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Security;

/// <summary>
/// Represents a security event.
/// </summary>
public sealed class SecurityEvent
{
	/// <summary>
	/// Gets or initializes the unique identifier for the security event.
	/// </summary>
	/// <value>
	/// The unique identifier for this security event.
	/// </value>
	public Guid Id { get; init; }

	/// <summary>
	/// Gets or initializes the timestamp when the security event occurred.
	/// </summary>
	/// <value>
	/// The timestamp when this security event occurred.
	/// </value>
	public DateTimeOffset Timestamp { get; init; }

	/// <summary>
	/// Gets or initializes the type of the security event.
	/// </summary>
	/// <value>
	/// The type of security event that occurred.
	/// </value>
	public SecurityEventType EventType { get; init; }

	/// <summary>
	/// Gets or initializes the description of the security event.
	/// </summary>
	/// <value>
	/// A human-readable description of this security event.
	/// </value>
	public string Description { get; init; } = string.Empty;

	/// <summary>
	/// Gets or initializes the severity level of the security event.
	/// </summary>
	/// <value>
	/// The severity level of this security event.
	/// </value>
	public SecuritySeverity Severity { get; init; }

	/// <summary>
	/// Gets or initializes the correlation identifier associated with the security event.
	/// </summary>
	/// <value>
	/// The correlation identifier linking this event to related operations.
	/// </value>
	public Guid? CorrelationId { get; init; }

	/// <summary>
	/// Gets or initializes the identifier of the user associated with the security event.
	/// </summary>
	/// <value>
	/// The user identifier associated with this security event.
	/// </value>
	public string? UserId { get; init; }

	/// <summary>
	/// Gets or initializes the source IP address associated with the security event.
	/// </summary>
	/// <value>
	/// The source IP address from which this security event originated.
	/// </value>
	public string? SourceIp { get; init; }

	/// <summary>
	/// Gets or initializes the user agent string associated with the security event.
	/// </summary>
	/// <value>
	/// The user agent string of the client that triggered this security event.
	/// </value>
	public string? UserAgent { get; init; }

	/// <summary>
	/// Gets or initializes the type of message associated with the security event.
	/// </summary>
	/// <value>
	/// The message type that was being processed when this security event occurred.
	/// </value>
	public string? MessageType { get; init; }

	/// <summary>
	/// Gets or initializes additional data associated with the security event.
	/// </summary>
	/// <value>
	/// Additional contextual data related to this security event.
	/// </value>
	public IDictionary<string, object?> AdditionalData { get; init; } = new Dictionary<string, object?>(StringComparer.Ordinal);
}
