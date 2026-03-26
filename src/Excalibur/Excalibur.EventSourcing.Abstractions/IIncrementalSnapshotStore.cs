// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.Abstractions;

/// <summary>
/// Stores incremental snapshots using base + delta strategy for reduced storage
/// and faster writes compared to full snapshots on every save.
/// </summary>
/// <typeparam name="TState">The aggregate state type.</typeparam>
/// <remarks>
/// <para>
/// Incremental snapshots store only the delta (changes) since the last full snapshot.
/// On load, the base snapshot is merged with ordered deltas to reconstruct the full state.
/// After <c>CompactionThreshold</c> deltas, a full snapshot is saved and prior deltas
/// are deleted.
/// </para>
/// <para>
/// This is a UNIQUE competitive advantage -- no competing .NET event sourcing framework
/// offers incremental snapshots.
/// </para>
/// </remarks>
public interface IIncrementalSnapshotStore<TState>
	where TState : class
{
	/// <summary>
	/// Loads the full state by loading the base snapshot and merging ordered deltas.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>
	/// The reconstructed state, or <see langword="null"/> if no snapshot exists.
	/// </returns>
	Task<TState?> LoadAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken);

	/// <summary>
	/// Saves a delta snapshot containing only changes since the last save.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="delta">The delta state to persist.</param>
	/// <param name="version">The aggregate version at this delta.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	Task SaveDeltaAsync(
		string aggregateId,
		string aggregateType,
		TState delta,
		long version,
		CancellationToken cancellationToken);

	/// <summary>
	/// Saves a full snapshot, replacing the base and deleting prior deltas.
	/// Called automatically when delta count reaches compaction threshold.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="state">The full state to persist.</param>
	/// <param name="version">The aggregate version at this snapshot.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	Task SaveFullAsync(
		string aggregateId,
		string aggregateType,
		TState state,
		long version,
		CancellationToken cancellationToken);
}
