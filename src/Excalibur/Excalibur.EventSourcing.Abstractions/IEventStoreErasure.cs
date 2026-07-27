// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing;

/// <summary>
/// Extends <see cref="IEventStore"/> with GDPR Article 17 (Right to Erasure) support.
/// </summary>
/// <remarks>
/// <para>
/// Implementations perform cryptographic erasure by redacting or deleting event data
/// for a given aggregate. The stream itself may be retained (with tombstoned payloads)
/// to preserve the event sequence for other aggregates that reference these events.
/// </para>
/// <para>
/// Event stores that do not support erasure should not implement this interface.
/// </para>
/// <para>
/// This is a <b>capability of the event store</b>, not a separately registered service. It is never added to
/// the service collection on its own, so resolving it from the container always yields <see langword="null"/>.
/// Probe the resolved <see cref="IEventStore"/> instead:
/// </para>
/// <code>
/// var eventStore = serviceProvider.GetRequiredKeyedService&lt;IEventStore&gt;("default");
/// if (eventStore is IEventStoreErasure erasure)
/// {
///     _ = await erasure.EraseEventsAsync(aggregateId, aggregateType, requestId, cancellationToken);
/// }
/// </code>
/// <para>
/// A store wrapped by a decorator exposes this capability only if the decorator re-implements it and forwards.
/// A decorator that omits it silently strips the capability: the probe answers <see langword="false"/> for a
/// store that supports erasure.
/// </para>
/// </remarks>
public interface IEventStoreErasure
{
	/// <summary>
	/// Erases all event payloads for the specified aggregate, replacing them with
	/// a tombstone marker.
	/// </summary>
	/// <param name="aggregateId">The aggregate whose events should be erased.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="erasureRequestId">The GDPR erasure request tracking ID for audit purposes.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The number of events that were erased.</returns>
	Task<int> EraseEventsAsync(
		string aggregateId,
		string aggregateType,
		Guid erasureRequestId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Checks whether erasure has been performed for the specified aggregate.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns><see langword="true"/> if the aggregate's events have been erased; otherwise, <see langword="false"/>.</returns>
	Task<bool> IsErasedAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken);
}
