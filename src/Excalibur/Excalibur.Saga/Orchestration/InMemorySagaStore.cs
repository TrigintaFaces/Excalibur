// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;

using Excalibur.Dispatch.Abstractions.Messaging;

namespace Excalibur.Saga.Orchestration;

/// <summary>
/// In-memory implementation of saga state storage for development and testing scenarios. Provides thread-safe storage of saga states using
/// concurrent collections, suitable for single-instance deployments and non-persistent workflows.
/// </summary>
/// <remarks>
/// This implementation does not persist state across application restarts. For production scenarios requiring durability, use a persistent
/// saga store implementation.
/// </remarks>
public sealed class InMemorySagaStore : ISagaStore
{
	private readonly ConcurrentDictionary<Guid, SagaState> _store = new();

	/// <summary>
	/// Loads a saga state by its identifier from the in-memory store. Returns null if no saga with the specified ID exists in the store.
	/// </summary>
	/// <typeparam name="TSagaState"> The type of saga state to load. </typeparam>
	/// <param name="sagaId"> The unique identifier of the saga to load. </param>
	/// <param name="cancellationToken"> Token to cancel the load operation. </param>
	/// <returns> A task containing the saga state if found, otherwise null. </returns>
	public Task<TSagaState?> LoadAsync<TSagaState>(Guid sagaId, CancellationToken cancellationToken)
		where TSagaState : SagaState =>
		Task.FromResult(_store.TryGetValue(sagaId, out var state) ? (TSagaState?)state : null);

	/// <summary>
	/// Saves a saga state to the in-memory store, overwriting any existing state with the same ID. The operation is atomic and thread-safe
	/// through the underlying concurrent dictionary.
	/// </summary>
	/// <typeparam name="TSagaState"> The type of saga state to save. </typeparam>
	/// <param name="sagaState"> The saga state to save to the store. </param>
	/// <param name="cancellationToken"> Token to cancel the save operation. </param>
	/// <returns> A completed task representing the synchronous save operation. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="sagaState" /> is null. </exception>
	public Task SaveAsync<TSagaState>(TSagaState sagaState, CancellationToken cancellationToken)
		where TSagaState : SagaState
	{
		ArgumentNullException.ThrowIfNull(sagaState);

		_store[sagaState.SagaId] = sagaState;
		return Task.CompletedTask;
	}
}
