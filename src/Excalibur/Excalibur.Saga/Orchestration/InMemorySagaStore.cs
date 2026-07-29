// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

using Excalibur.Data;
using Excalibur.Dispatch;
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
internal sealed class InMemorySagaStore : ISagaStore, ISagaStoreAdmin
{
	// Populate mode repopulates get-only collection properties on deserialize (e.g. SagaState.ProcessedEventIds),
	// so the deep-copy clone preserves idempotency keys rather than silently dropping them.
	private static readonly JsonSerializerOptions CloneOptions = new()
	{
		PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate,
	};

	private readonly ConcurrentDictionary<Guid, StoredSaga> _store = new();

	private readonly ITenantContext? _tenantContext;

	/// <summary>
	/// Initializes a new instance of the <see cref="InMemorySagaStore"/> class.
	/// </summary>
	/// <param name="tenantContext">
	/// The ambient tenant context, or <see langword="null"/> in a single-tenant host. Supplied, it restricts the
	/// tenant-scoped purge to the ambient tenant's sagas; absent, that purge addresses the untenanted partition
	/// — the sagas carrying no tenant. The estate-wide purge ignores it by design.
	/// </param>
	public InMemorySagaStore(ITenantContext? tenantContext = null) => _tenantContext = tenantContext;

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
		if (_store.TryGetValue(sagaId, out var stored) && stored.State is TSagaState typed)
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

		// Record the DECLARED type the saga was saved under (typeof(TSagaState).Name), matching the
		// persistent providers which persist SagaType from the declared type — the type-isolation key.
		// This keeps the surfaced SagaType consistent across every store under saga-state inheritance,
		// rather than reporting the runtime concrete type (state.GetType().Name).
		var stored = new StoredSaga(snapshot, typeof(TSagaState).Name);

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

				return stored;
			},
			updateValueFactory: (_, existing) =>
			{
				if (existing.State.Version != expectedVersion)
				{
					throw new ConcurrencyException(
						nameof(SagaState),
						sagaState.SagaId.ToString(),
						expectedVersion,
						existing.State.Version);
				}

				return stored;
			});

		// Store-owns-increment write-back (mirrors SqlServerSagaStore): advance the in-memory token so a
		// subsequent save on the same object uses the new persisted version instead of re-conflicting.
		sagaState.Version = expectedVersion + 1;

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task<int> PurgeCompletedBeforeAsync(DateTimeOffset threshold, CancellationToken cancellationToken)
	{
		var scope = TenantScope.FromContext(_tenantContext);

		// Mirrors the SQL providers' three-way split. A scoped purge matches its own tenant; an unscoped one
		// matches the untenanted partition -- the sagas that carry no tenant at all -- rather than everything.
		// "No tenant established" is a real scope here, not a wildcard, so this can never remove another
		// tenant's saga, and the rows carrying no tenant remain reachable for retention.
		return PurgeAsync(
			threshold,
			saga => scope.IsScoped
				? string.Equals(saga.TenantId, scope.TenantId, StringComparison.Ordinal)
				: string.IsNullOrEmpty(saga.TenantId),
			cancellationToken);
	}

	/// <inheritdoc />
	public Task<int> PurgeAllTenantsCompletedBeforeAsync(DateTimeOffset threshold, CancellationToken cancellationToken) =>
		PurgeAsync(threshold, static _ => true, cancellationToken);

	private Task<int> PurgeAsync(
		DateTimeOffset threshold,
		Func<SagaState, bool> tenantMatches,
		CancellationToken cancellationToken)
	{
		var removed = 0;

		// ConcurrentDictionary's enumerator is a moving snapshot, and the key/value TryRemove overload removes
		// an entry only when it still maps to the observed state — so a concurrent save that re-activates a saga
		// (clearing CompletedAt) is not purged out from under the caller.
		foreach (var entry in _store)
		{
			cancellationToken.ThrowIfCancellationRequested();

			if (entry.Value.State.CompletedAt is { } completedAt
				&& completedAt < threshold
				&& tenantMatches(entry.Value.State)
				&& _store.TryRemove(entry))
			{
				removed++;
			}
		}

		return Task.FromResult(removed);
	}

	/// <inheritdoc />
	public ValueTask<IReadOnlyList<SagaInstanceSummary>> QuerySagasAsync(
		SagaQueryFilter filter,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(filter);
		cancellationToken.ThrowIfCancellationRequested();

		// Snapshot enumeration over the concurrent dictionary; the store owns no created/updated instant,
		// so summaries carry lifecycle state + version only (age-based "stuck" is a separate seam).
		//
		// The ambient term is UNCONDITIONAL, matching PurgeCompletedBefore above and the SQL providers.
		// It was absent, so this handed a multi-tenant host every tenant's summaries whenever the caller
		// passed no TenantId filter -- and the filter is optional, so the default was the leak.
		// SagaQueryFilter.TenantId narrows WITHIN the ambient tenant; it does not select one.
		var scope = TenantScope.FromContext(_tenantContext);

		var matches = _store.Values
			.Where(s => (filter.IsCompleted is not { } wantCompleted || s.State.Completed == wantCompleted)
				&& MatchesAmbientTenant(s.State, scope)
				&& (filter.TenantId is null || string.Equals(s.State.TenantId, filter.TenantId, StringComparison.Ordinal)))
			.OrderBy(static s => s.State.SagaId)
			.Skip(Math.Max(0, filter.Skip))
			.Take(Math.Max(0, filter.MaxResults))
			.Select(ToSummary)
			.ToArray();

		return new ValueTask<IReadOnlyList<SagaInstanceSummary>>(matches);
	}

	/// <inheritdoc />
	public ValueTask<SagaInstanceSummary?> GetSummaryAsync(Guid sagaId, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Tenant-checked: a saga id alone must not reach across the boundary. Returning null for a saga
		// that exists in another tenant is the same answer the SQL providers give, whose predicate simply
		// does not match the row.
		var scope = TenantScope.FromContext(_tenantContext);

		return _store.TryGetValue(sagaId, out var stored) && MatchesAmbientTenant(stored.State, scope)
			? new ValueTask<SagaInstanceSummary?>(ToSummary(stored))
			: new ValueTask<SagaInstanceSummary?>((SagaInstanceSummary?)null);
	}

	/// <inheritdoc />
	public ValueTask<SagaStoreStatistics> GetStatisticsAsync(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Statistics diverge from the two summary reads DELIBERATELY, matching the SQL providers: a scoped
		// caller counts only its own tenant, and an unscoped one still gets estate-wide totals, which is
		// the operator diagnostic. Only the identifying data — saga ids, types, tenants — is closed off to
		// an unscoped caller; bare counts are not. Scoping this would both break that diagnostic and put
		// the in-memory store out of step with the providers it stands in for.
		var scope = TenantScope.FromContext(_tenantContext);

		var completed = 0;
		var total = 0;
		foreach (var stored in _store.Values)
		{
			if (scope.IsScoped && !MatchesAmbientTenant(stored.State, scope))
			{
				continue;
			}

			total++;
			if (stored.State.Completed)
			{
				completed++;
			}
		}

		return new ValueTask<SagaStoreStatistics>(new SagaStoreStatistics
		{
			RunningCount = total - completed,
			CompletedCount = completed,
			TotalCount = total,
			CapturedAt = DateTimeOffset.UtcNow,
		});
	}

	/// <summary>
	/// Determines whether a saga belongs to the ambient tenant scope.
	/// </summary>
	/// <remarks>
	/// The three-way split mirrors <see cref="PurgeCompletedBeforeAsync"/> and the SQL providers: a scoped
	/// caller matches its own tenant; an unscoped one matches the sagas carrying no tenant at all, rather
	/// than everything. "No tenant established" is a real scope here, not a wildcard — treating it as one
	/// is what turned every unscoped admin read into a cross-tenant disclosure.
	/// </remarks>
	/// <param name="state">The stored saga state.</param>
	/// <param name="scope">The ambient tenant scope.</param>
	/// <returns><see langword="true"/> when the saga is visible under <paramref name="scope"/>.</returns>
	private static bool MatchesAmbientTenant(SagaState state, TenantScope scope) =>
		scope.IsScoped
			? string.Equals(state.TenantId, scope.TenantId, StringComparison.Ordinal)
			: string.IsNullOrEmpty(state.TenantId);

	private static SagaInstanceSummary ToSummary(StoredSaga stored) => new()
	{
		SagaId = stored.State.SagaId,
		// Report the DECLARED type the saga was stored under (the persisted stores' type-isolation key),
		// not the runtime concrete type — so SagaType means the same thing in every store.
		SagaType = stored.SagaType,
		IsCompleted = stored.State.Completed,
		CompletedAt = stored.State.CompletedAt,
		TenantId = stored.State.TenantId,
		Version = stored.State.Version,
	};

	/// <summary>
	/// A stored saga plus the declared type name it was saved under. The declared type (not the runtime
	/// concrete type) is the authoritative <c>SagaType</c>, matching the persistent providers' isolation key.
	/// </summary>
	private sealed record StoredSaga(SagaState State, string SagaType);

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
