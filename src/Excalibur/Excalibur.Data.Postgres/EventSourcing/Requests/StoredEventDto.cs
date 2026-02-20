// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.EventSourcing.Abstractions;

namespace Excalibur.Data.Postgres.EventSourcing;

/// <summary>
/// Data Transfer Object for Dapper materialization of stored events.
/// </summary>
/// <remarks>
/// Dapper requires either a parameterless constructor or a constructor with parameters
/// that exactly match the column types. Since Postgres returns timestamps as DateTime
/// but StoredEvent uses DateTimeOffset, this DTO bridges the gap by accepting DateTime
/// and converting to DateTimeOffset when creating the StoredEvent.
/// </remarks>
internal sealed class StoredEventDto
{
	/// <summary>
	/// Gets or sets the unique event identifier.
	/// </summary>
	public string EventId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the aggregate identifier.
	/// </summary>
	public string AggregateId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the aggregate type name.
	/// </summary>
	public string AggregateType { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the event type name.
	/// </summary>
	public string EventType { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the serialized event data.
	/// </summary>
	public byte[] EventData { get; set; } = [];

	/// <summary>
	/// Gets or sets the serialized metadata.
	/// </summary>
	public byte[]? Metadata { get; set; }

	/// <summary>
	/// Gets or sets the version number.
	/// </summary>
	public long Version { get; set; }

	/// <summary>
	/// Gets or sets the timestamp as DateTime (Postgres returns DateTime from TIMESTAMPTZ).
	/// </summary>
	public DateTime Timestamp { get; set; }

	/// <summary>
	/// Gets or sets whether the event has been dispatched.
	/// </summary>
	public bool IsDispatched { get; set; }

	/// <summary>
	/// Converts this DTO to a StoredEvent record.
	/// </summary>
	/// <returns>A new StoredEvent instance.</returns>
	public StoredEvent ToStoredEvent()
	{
		// Convert DateTime to DateTimeOffset, assuming UTC for timestamps from Postgres TIMESTAMPTZ
		var timestamp = Timestamp.Kind == DateTimeKind.Utc
			? new DateTimeOffset(Timestamp, TimeSpan.Zero)
			: new DateTimeOffset(DateTime.SpecifyKind(Timestamp, DateTimeKind.Utc), TimeSpan.Zero);

		return new StoredEvent(
			EventId,
			AggregateId,
			AggregateType,
			EventType,
			EventData,
			Metadata,
			Version,
			timestamp,
			IsDispatched);
	}
}
