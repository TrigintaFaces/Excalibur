// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Messaging;

/// <summary>
/// Defines the contract for saga persistence services that handle loading and saving of saga state across business process boundaries. The
/// saga store provides durable storage for long-running processes, enabling saga recovery and consistency in distributed system environments.
/// </summary>
/// <remarks>
/// Saga stores are critical for maintaining business process integrity across system failures, restarts, and distributed operation
/// scenarios. Implementations may use various storage mechanisms including relational databases, document stores, event stores, or
/// distributed caches. The store must handle concurrent access, state versioning, and consistency requirements appropriate for the specific
/// business process and system architecture needs.
/// </remarks>
public interface ISagaStore
{
	/// <summary>
	/// Asynchronously loads the saga state for the specified saga identifier from persistent storage. This method enables saga recovery and
	/// continuation of business processes across system boundaries.
	/// </summary>
	/// <typeparam name="TSagaState"> The type of saga state to load, must inherit from <see cref="SagaState" />. </typeparam>
	/// <param name="sagaId"> The unique identifier of the saga instance to load. </param>
	/// <param name="cancellationToken"> Token to monitor for cancellation requests during load operations. </param>
	/// <returns>
	/// A task that represents the asynchronous load operation, containing the saga state if found, or <c> null </c> if no saga with the
	/// specified identifier exists in storage.
	/// </returns>
	/// <remarks>
	/// Implementations should handle storage-specific error conditions gracefully and may implement caching strategies for performance
	/// optimization. The method should preserve saga state integrity and handle concurrent access scenarios appropriately. Failed loads due
	/// to storage issues should propagate exceptions to enable proper error handling in the saga coordination layer.
	/// </remarks>
	Task<TSagaState?> LoadAsync<TSagaState>(Guid sagaId, CancellationToken cancellationToken)
		where TSagaState : SagaState;

	/// <summary>
	/// Asynchronously persists the specified saga state to durable storage, ensuring business process continuity and enabling recovery
	/// across system failures or restarts.
	/// </summary>
	/// <typeparam name="TSagaState"> The type of saga state to save, must inherit from <see cref="SagaState" />. </typeparam>
	/// <param name="sagaState"> The saga state instance containing current business process data and status. </param>
	/// <param name="cancellationToken"> Token to monitor for cancellation requests during save operations. </param>
	/// <returns> A task that represents the asynchronous save operation. </returns>
	/// <remarks>
	/// Implementations must ensure transactional consistency and handle concurrent modification scenarios appropriately, potentially using
	/// optimistic concurrency control or version-based conflict resolution. The save operation should be atomic and durable, guaranteeing
	/// that saga state changes are reliably persisted before the operation completes. Failed saves should propagate exceptions to enable
	/// proper error handling and potential retry logic in calling code.
	/// </remarks>
	Task SaveAsync<TSagaState>(TSagaState sagaState, CancellationToken cancellationToken)
		where TSagaState : SagaState;
}
