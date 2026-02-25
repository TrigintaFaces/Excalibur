// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.Abstractions;

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
/// Consumers that do not need erasure support should not implement this interface.
/// Use <c>GetService(typeof(IEventStoreErasure))</c> to probe for erasure capability.
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
