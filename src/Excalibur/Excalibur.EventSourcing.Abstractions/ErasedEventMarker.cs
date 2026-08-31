// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.EventSourcing;

/// <summary>
/// Defines the framework-controlled marker that identifies a GDPR-erased (tombstoned) event in an
/// event store.
/// </summary>
/// <remarks>
/// <para>
/// GDPR Article 17 erasure tombstones an aggregate's events in place: each event's payload is nulled
/// (or replaced) and its <see cref="StoredEvent.EventType"/> is overwritten with
/// <see cref="EventType"/> while the stream sequence is preserved. The marker is a closed,
/// framework-owned discriminator -- never a user event type -- so the erased state can be recognized
/// <em>structurally</em> (a positive marker check) rather than heuristically (treating a deserialization
/// failure as erasure, which would mask genuine corruption).
/// </para>
/// <para>
/// This constant is the single source of truth for the tombstone marker. Event store and erasure
/// implementations (<c>IEventStoreErasure</c>) MUST write and recognize this exact value so that
/// rehydration (e.g. <c>EventSourcedRepository.GetByIdAsync</c>) can distinguish an erased stream from
/// a corrupt one.
/// </para>
/// </remarks>
public static class ErasedEventMarker
{
	/// <summary>
	/// The reserved <see cref="StoredEvent.EventType"/> value written in place of an erased event's
	/// original type. Aggregate rehydration recognizes this marker positively (before any
	/// deserialization attempt) and returns a defined erased sentinel rather than failing loud.
	/// </summary>
	public const string EventType = "$erased";

	/// <summary>
	/// Determines whether a stored event's type name is the erasure tombstone marker.
	/// </summary>
	/// <param name="eventType"> The stored event's <see cref="StoredEvent.EventType"/> value. </param>
	/// <returns>
	/// <see langword="true"/> when the value is exactly <see cref="EventType"/>; otherwise
	/// <see langword="false"/>.
	/// </returns>
	/// <remarks>
	/// Every reader of a stored event stream calls this <em>before</em> attempting to resolve or deserialize
	/// the event, so an erased event is recognized by its marker rather than by the failure it would
	/// otherwise cause. The comparison is ordinal and exact: only the reserved marker matches, so a
	/// genuinely corrupt or unregistered event type is never mistaken for an erasure.
	/// </remarks>
	public static bool IsErased(string? eventType) =>
		string.Equals(eventType, EventType, StringComparison.Ordinal);
}
