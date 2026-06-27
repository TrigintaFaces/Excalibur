// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

using Excalibur.Data;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.Saga.Orchestration;

/// <summary>
/// In-memory implementation of saga state storage for development and testing scenarios. Provides thread-safe storage of saga states using
/// concurrent collections, suitable for single-instance deployments and non-persistent workflows.
/// </summary>
/// <remarks>
/// This implementation does not persist state across application restarts. For production scenarios requiring durability, use a persistent
/// saga store implementation.
/// </remarks>
internal sealed class InMemorySagaStore : ISagaStore
{
	// Populate mode repopulates get-only collection properties on deserialize (e.g. SagaState.ProcessedEventIds),
	// so the deep-copy clone preserves idempotency keys rather than silently dropping them.
	private static readonly JsonSerializerOptions CloneOptions = new()
	{
		PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate,
	};

	private readonly ConcurrentDictionary<Guid, SagaState> _store = new();

	/// <summary>
	/// Loads a saga state by its identifier from the in-memory store. Returns null if no saga with the specified ID exists in the store.
	/// </summary>
	/// <typeparam name="TSagaState"> The type of saga state to load. </typeparam>
	/// <param name="sagaId"> The unique identifier of the saga to load. </param>
	/// <param name="cancellationToken"> Token to cancel the load operation. </param>
	/// <returns> A task containing the saga state if found, otherwise null. </returns>
	public Task<TSagaState?> LoadAsync<TSagaState>(Guid sagaId, CancellationToken cancellationToken)
		where TSagaState : SagaState
	{
		// Safe downcast: a different saga type stored under this id is "not found" from the requested
		// type's perspective -> return null (graceful), never throw InvalidCastException. A hard
		// (TSagaState?)state cast would throw on a concrete-type mismatch, violating the ISagaStore
		// type-isolation contract (SagaStoreConformanceTestBase). [bd-c9ioqa]
		if (_store.TryGetValue(sagaId, out var state) && state is TSagaState typed)
		{
			// Return an INDEPENDENT copy: two concurrent loaders must receive isolated instances so each
			// carries its own version token (the optimistic-concurrency contract — e1tsq2). Persistent
			// stores get this for free via deserialize-on-read; the in-memory store must clone to match.
			return Task.FromResult<TSagaState?>(Clone(typed));
		}

		return Task.FromResult<TSagaState?>(null);
	}

	/// <summary>
	/// Saves a saga state to the in-memory store using optimistic concurrency.
	/// </summary>
	/// <typeparam name="TSagaState"> The type of saga state to save. </typeparam>
	/// <param name="sagaState"> The saga state to save to the store. </param>
	/// <param name="cancellationToken"> Token to cancel the save operation. </param>
	/// <returns> A completed task representing the synchronous save operation. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="sagaState" /> is null. </exception>
	/// <exception cref="ConcurrencyException">
	/// Thrown when the persisted version no longer matches the loaded (expected) version — a concurrent
	/// writer advanced the saga between load and save. The newer write is preserved (no lost update).
	/// </exception>
	public Task SaveAsync<TSagaState>(TSagaState sagaState, CancellationToken cancellationToken)
		where TSagaState : SagaState
	{
		ArgumentNullException.ThrowIfNull(sagaState);

		var expectedVersion = sagaState.Version;

		// Snapshot an isolated copy so later caller mutations cannot leak into the store (matching the
		// serialize-on-write semantics of the persistent providers), and stamp the store-owned version.
		var snapshot = Clone(sagaState);
		snapshot.Version = expectedVersion + 1;

		// Optimistic concurrency via an ATOMIC compare-and-swap (mirrors SqlServerSagaStore's version-gated
		// MERGE). AddOrUpdate's update factory re-runs against the latest stored value on contention and the
		// final swap is atomic, so a stale version is detected without a racy check-then-set (a plain
		// check-then-assign would be vacuous under concurrency). store-owns-increment.
		_ = _store.AddOrUpdate(
			sagaState.SagaId,
			addValueFactory: _ =>
			{
				// No-resurrect guard (SqlServer reference contract): only a brand-new saga (expected
				// version 0) may be inserted on the absent-key path. A stale save (expected > 0) against a
				// missing key is a deleted/completed saga — throw rather than resurrect it at a high version
				// (a zombie saga with duplicate side-effects). Mirrors the MERGE's "@ExpectedVersion = 0"-
				// guarded INSERT branch; makes resurrection structurally inexpressible.
				if (expectedVersion != 0)
				{
					throw new ConcurrencyException(
						nameof(SagaState),
						sagaState.SagaId.ToString(),
						expectedVersion,
						actualVersion: -1L);
				}

				return snapshot;
			},
			updateValueFactory: (_, existing) =>
			{
				if (existing.Version != expectedVersion)
				{
					throw new ConcurrencyException(
						nameof(SagaState),
						sagaState.SagaId.ToString(),
						expectedVersion,
						existing.Version);
				}

				return snapshot;
			});

		// Store-owns-increment write-back (mirrors SqlServerSagaStore): advance the in-memory token so a
		// subsequent save on the same object uses the new persisted version instead of re-conflicting.
		sagaState.Version = expectedVersion + 1;

		return Task.CompletedTask;
	}

	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "In-memory dev/test store mirrors the persistent providers' reflection-based JSON snapshot to isolate copies.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "In-memory dev/test store mirrors the persistent providers' reflection-based JSON snapshot to isolate copies.")]
	private static TSagaState Clone<TSagaState>(TSagaState state)
		where TSagaState : SagaState
	{
		// Serialize/deserialize the RUNTIME type so derived saga-state fields are captured, then return a
		// fresh instance — an isolated deep copy used for both load (per-caller isolation) and save (snapshot).
		var runtimeType = state.GetType();
		var json = JsonSerializer.Serialize(state, runtimeType, CloneOptions);
		return (TSagaState)JsonSerializer.Deserialize(json, runtimeType, CloneOptions)!;
	}
}
