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
/// A null payload has TWO causes and they are not interchangeable -- <see cref="ArchivedAt"/> is what
/// distinguishes them:
/// <list type="bullet">
/// <item>
/// <b>Erased</b> (<c>ArchivedAt</c> is null): the event was tombstoned under a data-subject request.
/// The payload is GONE and is not retrievable from anywhere.
/// </item>
/// <item>
/// <b>Archived</b> (<c>ArchivedAt</c> has a value): the payload was moved to cold storage to reclaim
/// space in the hot store. It IS retrievable, and a host configured for tiered storage reads it back
/// transparently.
/// </item>
/// </list>
/// Treating an archived event as erased silently drops data that still exists, so check
/// <see cref="ArchivedAt"/> before concluding a payload is lost. In both cases the entry REMAINS in
/// the stream, keeping versions and global positions contiguous.
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
	/// Gets the time this event's payload was moved to cold storage, or <see langword="null"/> when the
	/// payload is still held by the hot store.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Archival moves the PAYLOAD and leaves the ENTRY. The row, its version and its global position all
	/// remain in the hot store, so the global stream has no holes and a projection rebuild still replays
	/// every event in order -- it reads archived payloads back through cold storage. Deleting the row
	/// instead would make the archived events invisible to every global-stream consumer while remaining
	/// perfectly readable per-aggregate, which is the kind of partial disappearance nothing downstream
	/// can detect.
	/// </para>
	/// <para>
	/// This is also what separates an archived entry from an erased one: see
	/// <see cref="EventData"/>. An erased payload is gone; an archived payload is retrievable.
	/// </para>
	/// </remarks>
	public DateTimeOffset? ArchivedAt { get; init; }
}
