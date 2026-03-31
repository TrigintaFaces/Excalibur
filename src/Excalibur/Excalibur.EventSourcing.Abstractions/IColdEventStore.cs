// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.Abstractions;

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
	/// Writes events to cold storage for a specific aggregate.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="events">The events to archive.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task representing the asynchronous write operation.</returns>
	Task WriteAsync(
		string aggregateId,
		IReadOnlyList<StoredEvent> events,
		CancellationToken cancellationToken);

	/// <summary>
	/// Reads all archived events for an aggregate from cold storage.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The archived events in version order.</returns>
	Task<IReadOnlyList<StoredEvent>> ReadAsync(
		string aggregateId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Reads archived events for an aggregate from a specific version.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="fromVersion">The version to start reading from (exclusive).</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The archived events from the specified version in order.</returns>
	Task<IReadOnlyList<StoredEvent>> ReadAsync(
		string aggregateId,
		long fromVersion,
		CancellationToken cancellationToken);

	/// <summary>
	/// Checks whether any archived events exist for an aggregate.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns><see langword="true"/> if archived events exist; otherwise, <see langword="false"/>.</returns>
	Task<bool> HasArchivedEventsAsync(
		string aggregateId,
		CancellationToken cancellationToken);
}
