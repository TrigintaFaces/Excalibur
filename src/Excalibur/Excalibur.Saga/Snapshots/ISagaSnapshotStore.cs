// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Models;

namespace Excalibur.Saga.Snapshots;

/// <summary>
/// Provides snapshot persistence for saga state, enabling fast recovery without
/// replaying the entire step history.
/// </summary>
/// <remarks>
/// <para>
/// Saga snapshots capture the full state of a saga at a point in time. When a saga
/// needs to be restored (e.g., after a process restart), the latest snapshot is loaded
/// instead of replaying all steps from the beginning.
/// </para>
/// <para>
/// This follows the same pattern as <c>ISnapshotStore</c> in
/// <c>Excalibur.EventSourcing</c> but specialized for saga state.
/// </para>
/// </remarks>
public interface ISagaSnapshotStore
{
	/// <summary>
	/// Saves a snapshot of the current saga state.
	/// </summary>
	/// <param name="sagaId">The identifier of the saga to snapshot.</param>
	/// <param name="state">The saga state to persist as a snapshot.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>A task representing the asynchronous save operation.</returns>
	Task SaveSnapshotAsync(string sagaId, SagaState state, CancellationToken cancellationToken);

	/// <summary>
	/// Loads the most recent snapshot for a saga instance.
	/// </summary>
	/// <param name="sagaId">The identifier of the saga to load the snapshot for.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>
	/// The most recent saga state snapshot if found; otherwise, <see langword="null"/>.
	/// </returns>
	Task<SagaState?> LoadSnapshotAsync(string sagaId, CancellationToken cancellationToken);

	/// <summary>
	/// Deletes all snapshots for a saga instance.
	/// </summary>
	/// <param name="sagaId">The identifier of the saga whose snapshots should be deleted.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>
	/// <see langword="true"/> if any snapshots were deleted;
	/// <see langword="false"/> if no snapshots existed.
	/// </returns>
	Task<bool> DeleteSnapshotAsync(string sagaId, CancellationToken cancellationToken);
}
