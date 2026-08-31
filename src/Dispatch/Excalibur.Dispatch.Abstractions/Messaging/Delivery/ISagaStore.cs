// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Messaging;

/// <summary>
/// Defines the contract for saga persistence services that handle loading and saving of saga state across business process boundaries. The
/// saga store provides durable storage for long-running processes, enabling saga recovery and consistency in distributed system environments.
/// </summary>
/// <remarks>
/// <para>
/// Saga stores are critical for maintaining business process integrity across system failures, restarts, and distributed operation
/// scenarios. Implementations may use various storage mechanisms including relational databases, document stores, event stores, or
/// distributed caches. The store must handle concurrent access, state versioning, and consistency requirements appropriate for the specific
/// business process and system architecture needs.
/// </para>
/// <para>
/// This is the basic saga persistence contract in the Dispatch layer. For saga orchestration with
/// coordination and timeout support, see the Excalibur.Saga package.
/// </para>
/// <para>
/// <b>Tenant confinement.</b> <see cref="LoadAsync{TSagaState}"/> and <see cref="SaveAsync{TSagaState}"/>
/// are confined to the ambient tenant established for this store instance: a confined load returns the
/// caller's own saga state and never another tenant's, and a confined save can neither overwrite nor be
/// overwritten by another tenant's state for the same <c>sagaId</c>. Which mechanism a given provider
/// uses to hold that boundary is declared by its capability marker — <see cref="ITenantScopingCapability{TContract}"/>
/// for a store that reads an ambient tenant — and the package's own <c>ARCHITECTURE.md</c> states the
/// falsifiable guarantee and how it is verified. A store presenting no marker is not confined by the
/// framework.
/// </para>
/// </remarks>
[TenantOwned]
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
	/// <para>
	/// Confined to the ambient tenant established for this store instance: resolves the caller's own saga
	/// state for <paramref name="sagaId"/> when it exists, and never resolves state stored under another
	/// tenant's identical <paramref name="sagaId"/> — such a lookup returns <see langword="null"/>, the same
	/// outcome as a genuinely missing saga.
	/// </para>
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
	/// <para>
	/// Confined to the ambient tenant established for this store instance: the save can neither overwrite
	/// another tenant's saga state for the same identifier nor be overwritten by one — the two occupy
	/// separate partitions even when every other field is identical.
	/// </para>
	/// </remarks>
	Task SaveAsync<TSagaState>(TSagaState sagaState, CancellationToken cancellationToken)
		where TSagaState : SagaState;

	/// <summary>
	/// Asynchronously purges completed saga instances whose completion time is strictly earlier than the
	/// specified threshold, reclaiming storage for terminated business processes that no longer need to be retained.
	/// </summary>
	/// <param name="threshold">
	/// The exclusive upper bound on completion time. Only sagas that are completed and whose
	/// <see cref="SagaState.CompletedAt"/> is strictly before this instant are purged.
	/// </param>
	/// <param name="cancellationToken"> Token to monitor for cancellation requests during the purge operation. </param>
	/// <returns>
	/// A task that represents the asynchronous purge operation, containing the number of saga instances removed
	/// from storage.
	/// </returns>
	/// <remarks>
	/// <para>
	/// Only sagas that have reached a terminal state (<see cref="SagaState.Completed"/> is <see langword="true"/>
	/// with a non-null <see cref="SagaState.CompletedAt"/>) are eligible. Running sagas, and completed sagas whose
	/// completion instant is at or after <paramref name="threshold"/>, are never removed. Implementations must
	/// perform the purge atomically with respect to concurrent saves and return the exact count removed so callers
	/// can drive retention policies and emit metrics.
	/// </para>
	/// <para>
	/// Purge-by-age is an <b>optional capability</b>. The default implementation throws
	/// <see cref="NotSupportedException"/> — a store that cannot purge by completion age fails loud rather than
	/// silently pretending it purged (returning <c>0</c> would hide the missing capability from a retention
	/// scheduler). Stores that support it (the in-memory store and the first-party persistence providers) override this method with a
	/// real purge; a decorator overrides it to forward to its inner store.
	/// </para>
	/// <para>
	/// <b>This purge is tenant-scoped.</b> It removes only sagas belonging to the store's own scope: the ambient
	/// tenant when one is resolved, or the untenanted partition — the sagas carrying no tenant at all — when
	/// none is. Both are real scopes, so rows written without a tenant stay reachable for retention instead of
	/// accumulating forever, and neither scope can reach the other's rows. Estate-wide retention — deleting completed sagas
	/// across every tenant — is a deliberately separate operation, <see cref="PurgeAllTenantsCompletedBeforeAsync"/>,
	/// because cross-tenant deletion must be something a caller states explicitly rather than something reached
	/// by omitting a scope.
	/// </para>
	/// </remarks>
	/// <exception cref="NotSupportedException">Thrown by stores that do not support purge-by-age.</exception>
	Task<int> PurgeCompletedBeforeAsync(DateTimeOffset threshold, CancellationToken cancellationToken) =>
		throw new NotSupportedException(
			$"This saga store does not support purge-by-age. Store type: '{GetType().FullName}'. " +
			"Use a store that implements PurgeCompletedBeforeAsync (the in-memory store and the SqlServer, " +
				"Postgres, Oracle, MongoDB, Cosmos DB, DynamoDB, and Firestore providers), " +
			"or drive retention through the store's own mechanism.");

	/// <summary>
	/// Asynchronously purges completed saga instances across <b>every</b> tenant, ignoring any tenant scope the
	/// store would otherwise apply. This is the operator-level retention sweep.
	/// </summary>
	/// <param name="threshold">
	/// The exclusive upper bound on completion time. Only sagas that are completed and whose
	/// <see cref="SagaState.CompletedAt"/> is strictly before this instant are purged.
	/// </param>
	/// <param name="cancellationToken"> Token to monitor for cancellation requests during the purge operation. </param>
	/// <returns>
	/// A task that represents the asynchronous purge operation, containing the total number of saga instances
	/// removed across all tenants.
	/// </returns>
	/// <remarks>
	/// <para>
	/// Eligibility is identical to <see cref="PurgeCompletedBeforeAsync"/> — only terminated sagas older than
	/// <paramref name="threshold"/> are removed — but no tenant discriminator is applied, so every tenant's rows
	/// are in range. This is also the only purge that runs without a resolved tenant, because it is the only one
	/// that does not need to name one.
	/// </para>
	/// <para>
	/// <b>The name is the safety control.</b> Cross-tenant deletion is reachable only by writing "AllTenants"
	/// at the call site, never by omitting a scope or by passing a value that happens to mean "everything". A
	/// caller cannot arrive here by forgetting something, and a reviewer reading the call site sees the intent
	/// without having to trace where a scope came from.
	/// </para>
	/// <para>
	/// Like the scoped purge this is an <b>optional capability</b> whose default implementation throws. A store
	/// that supports it overrides this method; <b>a decorator must override it to forward to its inner store</b>
	/// — a decorator that does not override inherits this throwing default, so a decorated store would report
	/// the capability as missing even though the store underneath supports it.
	/// </para>
	/// </remarks>
	/// <exception cref="NotSupportedException">Thrown by stores that do not support estate-wide purge-by-age.</exception>
	Task<int> PurgeAllTenantsCompletedBeforeAsync(DateTimeOffset threshold, CancellationToken cancellationToken) =>
		throw new NotSupportedException(
			$"This saga store does not support estate-wide purge-by-age. Store type: '{GetType().FullName}'. " +
			"Use a store that implements PurgeAllTenantsCompletedBeforeAsync (the in-memory store and the SqlServer, " +
				"Postgres, Oracle, MongoDB, Cosmos DB, DynamoDB, and Firestore providers), " +
			"or drive retention through the store's own mechanism.");
}
