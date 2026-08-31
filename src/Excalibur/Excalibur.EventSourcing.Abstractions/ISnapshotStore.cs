// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.Domain.Model;

namespace Excalibur.EventSourcing;

/// <summary>
/// Provides storage operations for aggregate snapshots.
/// </summary>
/// <remarks>
/// <para>
/// Snapshots are used to optimize aggregate hydration by storing periodic state checkpoints.
/// This interface is intentionally separate from event storage for clean separation of concerns.
/// </para>
/// <para>
/// Interface uses ValueTask for synchronous completion optimization.
/// In-memory implementations complete synchronously without allocation overhead.
/// </para>
/// <para>
/// <b>Tenant confinement.</b> Every operation is confined to the ambient tenant established for this
/// store instance: <see cref="GetLatestSnapshotAsync"/> returns the caller's own tenant's latest
/// snapshot and never another tenant's snapshot for the same aggregate, and every save or delete can
/// neither affect nor be affected by another tenant's snapshots under the same identifier. Which
/// mechanism a given provider uses to hold that boundary is declared by its capability marker —
/// <see cref="ITenantScopingCapability{TContract}"/> for a store that reads an ambient tenant — and the
/// package's own <c>ARCHITECTURE.md</c> states the falsifiable guarantee and how it is verified. A store
/// presenting no marker is not confined by the framework.
/// </para>
/// </remarks>
[TenantOwned]
public interface ISnapshotStore
{
	/// <summary>
	/// Gets the latest snapshot for an aggregate.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The latest snapshot, or null if no snapshot exists.</returns>
	/// <remarks>
	/// Confined to the ambient tenant established for this store instance: a snapshot stored under
	/// another tenant for this <paramref name="aggregateId"/> is reported as not found, the same as one
	/// that was never taken.
	/// </remarks>
	ValueTask<ISnapshot?> GetLatestSnapshotAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken);

	/// <summary>
	/// Saves a snapshot for an aggregate, keeping the highest version stored for it.
	/// </summary>
	/// <param name="snapshot">The snapshot to save.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task that represents the asynchronous save operation.</returns>
	/// <remarks>
	/// <para>
	/// <b>Total.</b> Every well-formed snapshot is an acceptable argument. There is no version, payload
	/// size, or identifier shape a caller must avoid, and no state of the store in which a save is
	/// illegal.
	/// </para>
	/// <para>
	/// <b>Monotone upsert.</b> After this completes, the version readable for
	/// <c>(aggregateId, aggregateType)</c> within the ambient tenant is the highest that has ever been
	/// successfully saved for it. A save carrying a HIGHER version than the stored one replaces it; a
	/// save carrying a version LOWER THAN OR EQUAL to the stored one is a <b>successful no-op</b> — the
	/// call returns normally and the stored snapshot is left alone. Losing a race to a concurrent writer
	/// that stored a newer version is that same successful no-op, not an error: the caller asked for a
	/// version to be readable, and a newer one already is.
	/// </para>
	/// <para>
	/// <b>Modifies only its own key.</b> No other aggregate, aggregate type, or tenant is observably
	/// affected.
	/// </para>
	/// <para>
	/// <b>Faults.</b> Throws only for an invalid argument, for cancellation, or for an infrastructure
	/// failure that prevented the store from establishing the outcome above. Implementations MUST NOT
	/// report a lost race as a fault, and MUST NOT return normally having neither stored the snapshot nor
	/// established that a version at least as high is stored — a silent drop reports success for a
	/// snapshot nobody holds.
	/// </para>
	/// </remarks>
	/// <exception cref="ConcurrencyException">
	/// May be thrown by an implementation that retries a contended write when it exhausts its attempts
	/// without either storing the snapshot or establishing that a version at least as high is already
	/// stored. This is the infrastructure-failure case above, reported in the abstraction's own currency:
	/// the outcome is UNKNOWN, which is why it is a fault rather than the successful no-op that a proven
	/// supersede produces. Implementations that write without retrying cannot reach this state and do not
	/// throw it.
	/// </exception>
	ValueTask SaveSnapshotAsync(
		ISnapshot snapshot,
		CancellationToken cancellationToken);

	/// <summary>
	/// Deletes all snapshots for an aggregate.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task that represents the asynchronous delete operation.</returns>
	ValueTask DeleteSnapshotsAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken);

	/// <summary>
	/// Deletes snapshots older than a specified version.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="olderThanVersion">Delete snapshots with version less than this value.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task that represents the asynchronous delete operation.</returns>
	ValueTask DeleteSnapshotsOlderThanAsync(
		string aggregateId,
		string aggregateType,
		long olderThanVersion,
		CancellationToken cancellationToken);
}
