// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


namespace Excalibur.EventSourcing;

/// <summary>
/// Represents a stored event with persistence metadata.
/// </summary>
/// <param name="EventId">The unique event identifier.</param>
/// <param name="AggregateId">The aggregate identifier.</param>
/// <param name="AggregateType">The aggregate type name.</param>
/// <param name="EventType">The event type name.</param>
/// <param name="EventData">
/// The serialized event data, or <see langword="null"/> when the payload is not in the hot store.
/// A missing payload must be interpreted using the explicit event type and archive metadata:
/// <list type="bullet">
/// <item>
/// <b>Erased</b> (the event type is the explicit erasure marker): the payload must not be restored
/// or delivered. An erasure marker can retain an archive timestamp.
/// </item>
/// <item>
/// <b>Archived</b> (not erased, and <c>ArchivedAt</c> has a value): the payload was moved to cold
/// storage. A reader must resolve and validate that payload before delivering the event.
/// </item>
/// </list>
/// A null payload without an explicit erasure marker is unresolved, including when no archive
/// timestamp is present. It must fail processing rather than silently advance a checkpoint.
/// Archival and erasure preserve the entry's version and global position in the hot stream.
/// </param>
/// <param name="Metadata">The serialized event metadata.</param>
/// <param name="Version">The event version within the aggregate.</param>
/// <param name="Timestamp">When the event occurred.</param>
public sealed record StoredEvent(
	string EventId,
	string AggregateId,
	string AggregateType,
	string EventType,
	byte[]? EventData,
	byte[]? Metadata,
	long Version,
	DateTimeOffset Timestamp)
{
	/// <summary>
	/// Gets the authoritative tenant term read from the event's owning store, or
	/// <see langword="null"/> when the provider or stored representation does not supply it.
	/// </summary>
	/// <remarks>
	/// Unknown provenance is not an untenanted event. Providers with a tenant column return
	/// its explicit tenant term, including the reserved <c>__untenanted__</c> term for legacy
	/// untenanted rows. Cross-tenant readers must not substitute their ambient tenant when
	/// this value is unknown. Older serialized events remain readable with an unknown value.
	/// </remarks>
	public string? TenantId { get; init; }

	/// <summary>
	/// Gets the global stream position: the store-wide, monotonically increasing append ordinal of
	/// this event across all aggregates. Used by global-stream consumers to order and checkpoint in
	/// global append order.
	/// </summary>
	/// <value>
	/// <para>
	/// The 1-based global ordinal, or <c>0</c> when the provider does not assign one. Distinct from
	/// <see cref="Version"/>, which is the per-aggregate version.
	/// </para>
	/// <para>
	/// <b>Not every provider assigns a global position.</b> The relational providers (SQL Server,
	/// PostgreSQL, Oracle, SQLite) and the in-memory provider allocate it from a position counter inside
	/// the appending transaction, so their committed positions are always a contiguous prefix with no
	/// holes. The document and key-value providers do not: they report no position for an append and
	/// leave this value at <c>0</c>, so a global-stream ordering over them is not available and must not
	/// be inferred from this property. Check the position reported by the append rather than assuming
	/// one was assigned.
	/// </para>
	/// </value>
	public long GlobalPosition { get; init; }

	/// <summary>
/// Gets the recorded time this event's payload was moved to cold storage, or <see langword="null"/>
/// when no archival timestamp is recorded. This value does not establish whether an event was erased.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Archival moves the PAYLOAD and leaves the ENTRY. The row, its version and its global position all
/// remain in the hot store. A projection rebuild must resolve archived payloads before replaying
/// those events; an unresolved payload must stop progress. Deleting the row
	/// instead would make the archived events invisible to every global-stream consumer while remaining
	/// perfectly readable per-aggregate, which is the kind of partial disappearance nothing downstream
	/// can detect.
	/// </para>
	/// <para>
/// Check the explicit erasure marker before attempting restoration, even if this timestamp remains
/// populated. See <see cref="EventData"/> for the missing-payload processing rules.
	/// </para>
	/// </remarks>
	public DateTimeOffset? ArchivedAt { get; init; }
}
