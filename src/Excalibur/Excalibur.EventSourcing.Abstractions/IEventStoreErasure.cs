// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

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
/// if (eventStore.GetService(typeof(IEventStoreErasure)) is IEventStoreErasure erasure)
/// {
///     _ = await erasure.EraseEventsAsync(aggregateId, aggregateType, requestId, cancellationToken);
/// }
/// </code>
/// <para>
/// Ask the store for the capability, as above; do not test its type. A store is commonly reached through a
/// decorator, whose own interface list is fixed when it is compiled while the capabilities of the store it
/// wraps are known only at run time — so a type test reports the decorator and answers <see langword="false"/>
/// for a store that supports erasure perfectly well. A decorator answers this probe on behalf of the store it
/// wraps, and a decorator that cannot honour the capability over its inner store answers <see langword="null"/>
/// rather than claiming it.
/// </para>
/// <para>
/// <b>Tenant confinement.</b> As a capability of <see cref="IEventStore"/>, this interface is confined the
/// same way its host store is: <see cref="EraseEventsAsync"/> tombstones only the caller's own tenant's
/// stream for the given aggregate, never another tenant's, and <see cref="IsErasedAsync"/> answers for the
/// caller's own tenant only. See <see cref="IEventStore"/> for the full confinement statement and which
/// capability marker governs it.
/// </para>
/// </remarks>
[TenantOwned]
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
	/// <remarks>
	/// Confined to the ambient tenant established for this store instance: tombstones events only in the
	/// caller's own tenant's stream for <paramref name="aggregateId"/>, and cannot reach or be triggered
	/// by another tenant's stream under the same identifier.
	/// </remarks>
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
	/// <remarks>
	/// Confined to the ambient tenant established for this store instance: answers for the caller's own
	/// tenant's stream, never another tenant's stream under the same identifier.
	/// </remarks>
	Task<bool> IsErasedAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken);
}
