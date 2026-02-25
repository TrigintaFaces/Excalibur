// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Manages session state persistence and retrieval.
/// </summary>
public interface ISessionStateManager
{
	/// <summary>
	/// Gets the state for a session.
	/// </summary>
	/// <typeparam name="TState"> The type of the state. </typeparam>
	/// <param name="sessionId"> The session identifier. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The session state if found, default value otherwise. </returns>
	Task<TState?> GetStateAsync<TState>(
		string sessionId,
		CancellationToken cancellationToken)
		where TState : class;

	/// <summary>
	/// Sets the state for a session.
	/// </summary>
	/// <typeparam name="TState"> The type of the state. </typeparam>
	/// <param name="sessionId"> The session identifier. </param>
	/// <param name="state"> The state to set. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> True if the state was set, false otherwise. </returns>
	Task<bool> SetStateAsync<TState>(
		string sessionId,
		TState state,
		CancellationToken cancellationToken)
		where TState : class;

	/// <summary>
	/// Updates the state for a session.
	/// </summary>
	/// <typeparam name="TState"> The type of the state. </typeparam>
	/// <param name="sessionId"> The session identifier. </param>
	/// <param name="updateFunc"> The function to update the state. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The updated state. </returns>
	Task<TState?> UpdateStateAsync<TState>(
		string sessionId,
		Func<TState?, TState?> updateFunc,
		CancellationToken cancellationToken)
		where TState : class;

	/// <summary>
	/// Deletes the state for a session.
	/// </summary>
	/// <param name="sessionId"> The session identifier. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> True if the state was deleted, false otherwise. </returns>
	Task<bool> DeleteStateAsync(
		string sessionId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Creates a checkpoint for the session state.
	/// </summary>
	/// <param name="sessionId"> The session identifier. </param>
	/// <param name="checkpointId"> The checkpoint identifier. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> True if the checkpoint was created, false otherwise. </returns>
	Task<bool> CreateCheckpointAsync(
		string sessionId,
		string checkpointId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Restores the session state from a checkpoint.
	/// </summary>
	/// <param name="sessionId"> The session identifier. </param>
	/// <param name="checkpointId"> The checkpoint identifier. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> True if the checkpoint was restored, false otherwise. </returns>
	Task<bool> RestoreCheckpointAsync(
		string sessionId,
		string checkpointId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Lists available checkpoints for a session.
	/// </summary>
	/// <param name="sessionId"> The session identifier. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A list of checkpoint information. </returns>
	Task<IReadOnlyList<CheckpointInfo>> ListCheckpointsAsync(
		string sessionId,
		CancellationToken cancellationToken);
}
