// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;

namespace Excalibur.EventSourcing;

/// <summary>
/// Describes the committed identity and lifecycle state of an event without exposing its payload or metadata.
/// </summary>
/// <param name="Tenant">The partition recorded on the event.</param>
/// <param name="AggregateId">The aggregate identifier.</param>
/// <param name="AggregateType">The case-sensitive aggregate type.</param>
/// <param name="EventId">The event identifier.</param>
/// <param name="Version">The zero-based aggregate version.</param>
/// <param name="GlobalPosition">The global position, or zero if the provider does not assign one.</param>
/// <param name="EventType">The current event type, including an explicit erasure marker when erased.</param>
/// <param name="Timestamp">The stored event timestamp.</param>
/// <param name="ArchivedAt">The archive timestamp, if present.</param>
public sealed record EventStoreEventState(
	KeyedTenantPartition Tenant,
	string AggregateId,
	string AggregateType,
	string EventId,
	long Version,
	long GlobalPosition,
	string EventType,
	DateTimeOffset Timestamp,
	DateTimeOffset? ArchivedAt);
