// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using MemoryPack;

namespace Excalibur.Dispatch.Serialization.MemoryPack;

/// <summary>
/// Internal envelope for domain events in event store wire format.
/// </summary>
/// <remarks>
/// <para>
/// This envelope wraps domain events for persistence in the event store.
/// Uses [MemoryPackOrder] for explicit field ordering to support schema evolution.
/// </para>
/// </remarks>
[MemoryPackable]
public sealed partial class EventEnvelope
{
	/// <summary>
	/// Gets or sets the unique event identifier.
	/// </summary>
	[MemoryPackOrder(0)]
	public required Guid EventId { get; init; }

	/// <summary>
	/// Gets or sets the aggregate identifier.
	/// </summary>
	[MemoryPackOrder(1)]
	public required Guid AggregateId { get; init; }

	/// <summary>
	/// Gets or sets the aggregate type name.
	/// </summary>
	[MemoryPackOrder(2)]
	public required string AggregateType { get; init; }

	/// <summary>
	/// Gets or sets the event type name.
	/// </summary>
	[MemoryPackOrder(3)]
	public required string EventType { get; init; }

	/// <summary>
	/// Gets or sets the aggregate version at the time of this event.
	/// </summary>
	[MemoryPackOrder(4)]
	public required long Version { get; init; }

	/// <summary>
	/// Gets or sets the serialized event payload.
	/// </summary>
	[MemoryPackOrder(5)]
	public required byte[] Payload { get; init; }

	/// <summary>
	/// Gets or sets the timestamp when the event occurred.
	/// </summary>
	[MemoryPackOrder(6)]
	public required DateTimeOffset OccurredAt { get; init; }

	/// <summary>
	/// Gets or sets optional event metadata.
	/// </summary>
	[MemoryPackOrder(7)]
	public Dictionary<string, string>? Metadata { get; init; }

	/// <summary>
	/// Gets or sets the schema version for migration detection.
	/// </summary>
	[MemoryPackOrder(8)]
	public int SchemaVersion { get; init; } = 1;
}
