// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Event arguments for when a security event is recorded in the audit system.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="SecurityEventRecordedEventArgs" /> class.
/// </remarks>
/// <param name="eventType"> The type of security event that was recorded. </param>
/// <param name="eventId"> The unique identifier of the recorded event. </param>
/// <param name="timestamp"> The timestamp when the event was recorded. </param>
public sealed class SecurityEventRecordedEventArgs(string eventType, string eventId, DateTimeOffset timestamp) : EventArgs
{
	/// <summary>
	/// Gets the type of security event that was recorded.
	/// </summary>
	/// <value> The category or classification of the security event (e.g., "authentication_failure", "privilege_escalation"). </value>
	public string EventType { get; } = eventType ?? throw new ArgumentNullException(nameof(eventType));

	/// <summary>
	/// Gets the unique identifier of the recorded event.
	/// </summary>
	/// <value> A unique string identifier used to track and reference this specific security event. </value>
	public string EventId { get; } = eventId ?? throw new ArgumentNullException(nameof(eventId));

	/// <summary>
	/// Gets the timestamp when the event was recorded.
	/// </summary>
	/// <value> The UTC timestamp when the security event was recorded in the audit system. </value>
	public DateTimeOffset Timestamp { get; } = timestamp;

	/// <summary>
	/// Gets additional metadata associated with the security event.
	/// </summary>
	/// <value> A dictionary containing additional contextual information about the security event, or null if no metadata is available. </value>
	public IReadOnlyDictionary<string, object>? Metadata { get; init; }

	/// <summary>
	/// Gets the severity level of the security event.
	/// </summary>
	/// <value> The severity classification of the event (e.g., "Critical", "High", "Medium", "Low"), or null if not specified. </value>
	public string? Severity { get; init; }

	/// <summary>
	/// Gets the user or system that triggered the security event.
	/// </summary>
	/// <value>
	/// The identifier of the user account or system component that initiated the security event, or null if the source is unknown.
	/// </value>
	public string? Source { get; init; }
}
