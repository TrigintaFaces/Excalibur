// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

namespace Excalibur.EventSourcing;

/// <summary>
/// Defines the contract for cold (archive) event storage operations.
/// </summary>
/// <remarks>
/// <para>
/// Cold storage is optimized for cost and capacity, not read speed. Events are stored
/// in compressed, immutable batches (e.g., in blob storage).
/// </para>
/// <para>
/// Used by the <c>TieredEventStoreDecorator</c> for transparent read-through when
/// events are missing from the hot tier, and by <c>EventArchiveService</c> for
/// writing archived events.
/// </para>
/// </remarks>
public interface IColdEventStore
{
	/// <summary>
	/// Writes events to cold storage for a specific aggregate and returns the durable low-water mark:
	/// the highest version <c>V</c> such that <strong>every</strong> version <c>&lt;= V</c> for this
	/// aggregate is durably committed in cold storage (post-upload/flush/quorum), and therefore safe for
	/// the caller to delete from the hot tier.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The returned value is a <strong>contiguous durable prefix</strong>, never the merely-submitted
	/// maximum. An implementation that persists only part of the batch (or defers a buffered write) MUST
	/// return the highest contiguously-durable version, not the highest submitted one — returning the
	/// submitted max while the write is not yet durable authorizes the caller to destroy the only other
	/// copy of not-yet-archived events. Implementations that upload the whole batch atomically and await
	/// the storage receipt return the submitted maximum only after that receipt confirms durability.
	/// </para>
	/// <para>
	/// Defined early-return acks: an empty <paramref name="events"/> input returns <c>-1</c> ("nothing
	/// durably added by this call; delete nothing"); a batch already fully present returns the confirmed
	/// existing maximum for the submitted range. The caller deletes hot events only up to the returned
	/// watermark, so a partial or deferred cold write bounds hot deletion rather than losing data.
	/// </para>
	/// </remarks>
	/// <param name="tenant">
	/// The tenant partition that owns the events. Cold storage keys are composed with this term, so events
	/// archived under one tenant are unreachable from another tenant's read or watermark check.
	/// </param>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="events">The events to archive.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>
	/// The durable low-water-mark version safe to delete from the hot tier, or <c>-1</c> when nothing was
	/// durably added by this call.
	/// </returns>
	Task<long> WriteAsync(
		KeyedTenantPartition tenant,
		string aggregateId,
		IReadOnlyList<StoredEvent> events,
		CancellationToken cancellationToken);

	/// <summary>
	/// Reads all archived events for an aggregate from cold storage.
	/// </summary>
	/// <param name="tenant">The tenant partition that owns the events.</param>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The archived events in version order.</returns>
	Task<IReadOnlyList<StoredEvent>> ReadAsync(
		KeyedTenantPartition tenant,
		string aggregateId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Reads archived events for an aggregate from a specific version.
	/// </summary>
	/// <param name="tenant">The tenant partition that owns the events.</param>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="fromVersion">The version to start reading from (exclusive).</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The archived events from the specified version in order.</returns>
	Task<IReadOnlyList<StoredEvent>> ReadAsync(
		KeyedTenantPartition tenant,
		string aggregateId,
		long fromVersion,
		CancellationToken cancellationToken);

	/// <summary>
	/// Checks whether any archived events exist for an aggregate.
	/// </summary>
	/// <param name="tenant">The tenant partition that owns the events.</param>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns><see langword="true"/> if archived events exist; otherwise, <see langword="false"/>.</returns>
	Task<bool> HasArchivedEventsAsync(
		KeyedTenantPartition tenant,
		string aggregateId,
		CancellationToken cancellationToken);
}
