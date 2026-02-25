// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Saga.Models;

using SagaStateModel = Excalibur.Saga.Models.SagaState;

namespace Excalibur.Saga.Abstractions;

/// <summary>
/// Provides core CRUD operations for saga state persistence.
/// </summary>
public interface ISagaStateStore
{
	/// <summary>
	/// Saves the saga state.
	/// </summary>
	/// <param name="state"> The saga state to save. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	Task SaveStateAsync(SagaStateModel state, CancellationToken cancellationToken);

	/// <summary>
	/// Gets the saga state by ID.
	/// </summary>
	/// <param name="sagaId"> The saga identifier. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The saga state, or null if not found. </returns>
	Task<SagaStateModel?> GetStateAsync(string sagaId, CancellationToken cancellationToken);

	/// <summary>
	/// Updates the saga state.
	/// </summary>
	/// <param name="state"> The updated saga state. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> True if the update was successful, false otherwise. </returns>
	Task<bool> UpdateStateAsync(SagaStateModel state, CancellationToken cancellationToken);

	/// <summary>
	/// Deletes the saga state.
	/// </summary>
	/// <param name="sagaId"> The saga identifier. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> True if the deletion was successful, false otherwise. </returns>
	Task<bool> DeleteStateAsync(string sagaId, CancellationToken cancellationToken);
}

/// <summary>
/// Provides query and maintenance operations for saga state stores.
/// </summary>
/// <remarks>
/// <para>
/// Separated from <see cref="ISagaStateStore"/> following the Interface Segregation Principle.
/// Implementations that support querying and maintenance should implement both interfaces.
/// </para>
/// </remarks>
public interface ISagaStateStoreQuery
{
	/// <summary>
	/// Gets saga states by status.
	/// </summary>
	/// <param name="status"> The saga status to filter by. </param>
	/// <param name="limit"> The maximum number of results. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A collection of saga states with the specified status. </returns>
	Task<IEnumerable<SagaStateModel>> GetByStatusAsync(
		SagaStatus status,
		int limit,
		CancellationToken cancellationToken);

	/// <summary>
	/// Marks expired sagas for cleanup.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The number of sagas marked for cleanup. </returns>
	Task<int> MarkExpiredSagasAsync(CancellationToken cancellationToken);
}
