// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.EventStore;

/// <summary>
/// Metadata content for event store events.
/// </summary>
public sealed class EventMetadata
{
	/// <summary>
	/// Gets or sets the event type name.
	/// </summary>
	/// <value>
	/// The event type name.
	/// </value>
	public string EventType { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the event version.
	/// </summary>
	/// <value>
	/// The event version.
	/// </value>
	public int EventVersion { get; set; }

	/// <summary>
	/// Gets or sets the timestamp when the event occurred.
	/// </summary>
	/// <value>
	/// The timestamp when the event occurred.
	/// </value>
	public DateTimeOffset Timestamp { get; set; }

	/// <summary>
	/// Gets or sets the user who triggered the event.
	/// </summary>
	/// <value>
	/// The user who triggered the event.
	/// </value>
	public string? UserId { get; set; }

	/// <summary>
	/// Gets or sets the correlation ID for tracing.
	/// </summary>
	/// <value>
	/// The correlation ID for tracing.
	/// </value>
	public string? CorrelationId { get; set; }

	/// <summary>
	/// Gets or sets the causation ID linking to the cause of this event.
	/// </summary>
	/// <value>
	/// The causation ID linking to the cause of this event.
	/// </value>
	public string? CausationId { get; set; }

	/// <summary>
	/// Gets additional metadata properties.
	/// </summary>
	/// <value>
	/// Additional metadata properties.
	/// </value>
	public Dictionary<string, object?> Properties { get; } = new(StringComparer.Ordinal);
}
