// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Domain.Model;

namespace Excalibur.EventSourcing.Abstractions;

/// <summary>
/// Manages snapshot creation and restoration for aggregates.
/// </summary>
public interface ISnapshotManager
{
	/// <summary>
	/// Creates a snapshot for an aggregate.
	/// </summary>
	/// <typeparam name="TAggregate">The type of aggregate.</typeparam>
	/// <param name="aggregate">The aggregate to snapshot.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The created snapshot.</returns>
	Task<ISnapshot> CreateSnapshotAsync<TAggregate>(
		TAggregate aggregate,
		CancellationToken cancellationToken)
		where TAggregate : IAggregateRoot, IAggregateSnapshotSupport;

	/// <summary>
	/// Saves a snapshot to the store.
	/// </summary>
	/// <param name="streamId">The stream identifier.</param>
	/// <param name="snapshot">The snapshot to save.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task that represents the asynchronous save operation.</returns>
	Task SaveSnapshotAsync(
		string streamId,
		ISnapshot snapshot,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets the latest snapshot for a stream.
	/// </summary>
	/// <param name="streamId">The stream identifier.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The latest snapshot or null if none exists.</returns>
	Task<ISnapshot?> GetLatestSnapshotAsync(
		string streamId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Restores an aggregate from a snapshot.
	/// </summary>
	/// <typeparam name="TAggregate">The type of aggregate.</typeparam>
	/// <param name="snapshot">The snapshot to restore from.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The restored aggregate.</returns>
	Task<TAggregate> RestoreFromSnapshotAsync<TAggregate>(
		ISnapshot snapshot,
		CancellationToken cancellationToken)
		where TAggregate : IAggregateRoot, IAggregateSnapshotSupport, new();
}
