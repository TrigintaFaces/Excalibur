// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Domain.Model;

namespace Excalibur.EventSourcing.Abstractions;

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
/// </remarks>
public interface ISnapshotStore
{
	/// <summary>
	/// Gets the latest snapshot for an aggregate.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The latest snapshot, or null if no snapshot exists.</returns>
	ValueTask<ISnapshot?> GetLatestSnapshotAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken);

	/// <summary>
	/// Saves a snapshot for an aggregate.
	/// </summary>
	/// <param name="snapshot">The snapshot to save.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task that represents the asynchronous save operation.</returns>
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
