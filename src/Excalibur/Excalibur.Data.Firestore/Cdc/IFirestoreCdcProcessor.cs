// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.Firestore.Cdc;

/// <summary>
/// Defines the contract for processing Firestore CDC events via Realtime Listeners.
/// </summary>
public interface IFirestoreCdcProcessor : IAsyncDisposable, IDisposable
{
	/// <summary>
	/// Starts continuous CDC processing until cancellation.
	/// </summary>
	/// <param name="eventHandler">The handler for each change event.</param>
	/// <param name="cancellationToken">A token to observe for cancellation requests.</param>
	/// <returns>A task that completes when processing is stopped.</returns>
	/// <remarks>
	/// <para>
	/// This method starts a Firestore Realtime Listener and processes changes continuously.
	/// Position is confirmed after each successfully processed event.
	/// </para>
	/// <para>
	/// Use <see cref="ProcessBatchAsync"/> for manual batch control in serverless scenarios.
	/// </para>
	/// </remarks>
	Task StartAsync(
		Func<FirestoreDataChangeEvent, CancellationToken, Task> eventHandler,
		CancellationToken cancellationToken);

	/// <summary>
	/// Processes a single batch of changes and returns.
	/// </summary>
	/// <param name="eventHandler">The handler for each change event.</param>
	/// <param name="cancellationToken">A token to observe for cancellation requests.</param>
	/// <returns>The number of events processed in this batch.</returns>
	/// <remarks>
	/// <para>
	/// Use this method for serverless scenarios where you want explicit
	/// control over batch processing. Position must be confirmed manually
	/// via <see cref="ConfirmPositionAsync"/>.
	/// </para>
	/// <para>
	/// <b>Important:</b> Due to Firestore Realtime Listener semantics, ProcessBatchAsync
	/// can only reliably detect Modified events. Added and Removed events may not be
	/// captured when using batch processing mode. Use <see cref="StartAsync"/> for
	/// full change type support.
	/// </para>
	/// </remarks>
	Task<int> ProcessBatchAsync(
		Func<FirestoreDataChangeEvent, CancellationToken, Task> eventHandler,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets the current (uncommitted) position.
	/// </summary>
	/// <param name="cancellationToken">A token to observe for cancellation requests.</param>
	/// <returns>The current position.</returns>
	Task<FirestoreCdcPosition> GetCurrentPositionAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Confirms that all events up to the given position have been processed.
	/// </summary>
	/// <param name="position">The position to confirm.</param>
	/// <param name="cancellationToken">A token to observe for cancellation requests.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	/// <remarks>
	/// This persists the position to the state store. On restart,
	/// processing will resume from this confirmed position.
	/// </remarks>
	Task ConfirmPositionAsync(
		FirestoreCdcPosition position,
		CancellationToken cancellationToken);
}
