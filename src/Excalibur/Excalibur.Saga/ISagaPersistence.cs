// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Saga;

/// <summary>
/// Provides persistence for saga state.
/// </summary>
public interface ISagaPersistence
{
	/// <summary>
	/// Saves the saga state.
	/// </summary>
	/// <typeparam name="TSagaData"> The type of saga data. </typeparam>
	/// <param name="state"> The saga state to persist. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns>A task that represents the asynchronous save operation.</returns>
	Task SaveSagaAsync<TSagaData>(SagaPersistedState<TSagaData> state, CancellationToken cancellationToken)
		where TSagaData : class;

	/// <summary>
	/// Loads the saga state.
	/// </summary>
	/// <typeparam name="TSagaData"> The type of saga data. </typeparam>
	/// <param name="sagaId"> The saga identifier. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> The persisted state if found, null otherwise. </returns>
	Task<SagaPersistedState<TSagaData>?> LoadSagaAsync<TSagaData>(string sagaId, CancellationToken cancellationToken)
		where TSagaData : class;

	/// <summary>
	/// Lists active sagas.
	/// </summary>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> List of active saga summaries. </returns>
	Task<IEnumerable<SagaSummary>> ListActiveSagasAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Deletes a saga from persistence.
	/// </summary>
	/// <param name="sagaId"> The saga identifier. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns>A task that represents the asynchronous delete operation.</returns>
	Task DeleteSagaAsync(string sagaId, CancellationToken cancellationToken);
}

