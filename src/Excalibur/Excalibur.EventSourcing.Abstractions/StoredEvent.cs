// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.EventSourcing.Abstractions;

/// <summary>
/// Represents a stored event with persistence metadata.
/// </summary>
/// <param name="EventId">The unique event identifier.</param>
/// <param name="AggregateId">The aggregate identifier.</param>
/// <param name="AggregateType">The aggregate type name.</param>
/// <param name="EventType">The event type name.</param>
/// <param name="EventData">The serialized event data.</param>
/// <param name="Metadata">The serialized event metadata.</param>
/// <param name="Version">The event version within the aggregate.</param>
/// <param name="Timestamp">When the event occurred.</param>
/// <param name="IsDispatched">Whether the event has been dispatched via outbox.</param>
public sealed record StoredEvent(
	string EventId,
	string AggregateId,
	string AggregateType,
	string EventType,
	byte[] EventData,
	byte[]? Metadata,
	long Version,
	DateTimeOffset Timestamp,
	bool IsDispatched);
