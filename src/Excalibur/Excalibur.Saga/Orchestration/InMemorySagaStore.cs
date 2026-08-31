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

	// Keyed on (tenant, sagaId) -- NOT on sagaId alone. The tenant is part of the identity of a saga here,
	// exactly as it is part of the primary key in every persistent provider, so two tenants holding the same
	// saga identifier occupy two entries and a lookup under one tenant cannot name the other's. Confinement
	// is therefore a property of the key rather than a predicate someone has to remember to apply: a read
	// that crosses the boundary is not "checked and refused", it is unaddressable. The partition type is
	// KeyedTenantPartition, whose stated purpose is to be the tenant component of a keyed store's key and
	// which has no absent inhabitant -- so a key carrying no tenant term is unconstructable.
	private readonly ConcurrentDictionary<(KeyedTenantPartition Tenant, Guid SagaId), StoredSaga> _store = new();

	private readonly ITenantContext _tenantContext;
	/// <summary>
	/// Gets the tenant term this store runs under, resolved in one place so every statement it builds binds
	/// the same value. The context is a required dependency, so the term is decided identically on every
	/// path: the store cannot resolve one partition on write and a different one on read.
	/// </summary>
	private TenantScope CurrentTenantScope =>
		TenantScope.FromContext(_tenantContext);

	/// <summary>
	/// Gets the ambient partition as the key component. Projected from the same <see cref="CurrentTenantScope"/>
	/// the write path stamps onto the state, so the key and the stamped term are the same value by
	/// construction and cannot drift apart.
	/// </summary>
	private KeyedTenantPartition CurrentPartition =>
		KeyedTenantPartition.FromScope(CurrentTenantScope);

	/// <summary>
	/// Builds the full identity of a saga under the ambient tenant. Every keyed operation goes through this,
	/// so no call site can address an entry by saga identifier alone.
	/// </summary>
	/// <param name="sagaId"> The saga identifier. </param>
	/// <returns> The composite key naming that saga within the ambient partition. </returns>
	private (KeyedTenantPartition Tenant, Guid SagaId) KeyFor(Guid sagaId) => (CurrentPartition, sagaId);


	/// <summary>
	/// Initializes a new instance of the <see cref="InMemorySagaStore"/> class.
	/// </summary>
	/// <param name="tenantContext">
	/// The ambient tenant context. Required: this store partitions rows by tenant, and it resolves that
	/// partition from here, so there is no state in which the partition is undecided. A single-tenant host
	/// receives the framework default context and operates as the one canonical tenant.
	/// </param>
	public InMemorySagaStore(ITenantContext tenantContext) => _tenantContext = tenantContext;

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
		// type-isolation contract (SagaStoreConformanceTestBase).
		// The ambient partition is part of the key, so this lookup can only ever name a saga inside the
		// caller's own tenant. Another tenant's saga is not filtered out here -- it is not reachable from
		// this key at all, and the caller receives the same null a genuinely missing saga returns. There is
		// deliberately NO tenant predicate after the lookup: on a statement already addressed by the full
		// key, a further tenant term selects a subset of an at-most-one-row result, so it cannot exclude a
		// foreign row and its only reachable effect is turning a correct hit into a miss.
		if (_store.TryGetValue(KeyFor(sagaId), out var stored) && stored.State is TSagaState typed)
		{
			// Return an INDEPENDENT copy: two concurrent loaders must receive isolated instances so each
			// carries its own version token (the optimistic-concurrency contract). Persistent
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

		// Stamp the ambient tenant, exactly as the persistent providers do on their write path. Without this
		// the store writes rows carrying no tenant while every read and purge resolves a concrete term, so a
		// saga saved here could never be addressed again by the scope that saved it. The term is taken from
		// the store's own resolved scope rather than from the caller's state, so a caller cannot write into
		// another tenant's partition by setting the field.
		snapshot.TenantId = CurrentTenantScope.TenantId;

		// Record the DECLARED type the saga was saved under (typeof(TSagaState).Name), matching the
		// persistent providers which persist SagaType from the declared type — the type-isolation key.
		// This keeps the surfaced SagaType consistent across every store under saga-state inheritance,
		// rather than reporting the runtime concrete type (state.GetType().Name).
		var stored = new StoredSaga(snapshot, typeof(TSagaState).Name);

		// Optimistic concurrency via an ATOMIC compare-and-swap (mirrors SqlServerSagaStore's version-gated
		// MERGE). AddOrUpdate's update factory re-runs against the latest stored value on contention and the
		// final swap is atomic, so a stale version is detected without a racy check-then-set (a plain
		// check-then-assign would be vacuous under concurrency). store-owns-increment.
		//
		// The key carries the tenant, so the version counter is PER PARTITION. Keyed on the saga identifier
		// alone it was shared across tenants, and the resulting failure was not a disclosure but a write
		// collision: a second tenant creating its own saga under an identifier another tenant already held
		// read the first tenant's version, failed the expected-version-0 guard, and could never create it --
		// while a save that did land overwrote the other tenant's in-flight process outright.
		_ = _store.AddOrUpdate(
			KeyFor(sagaState.SagaId),
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
		var scope = CurrentTenantScope;

		// Mirrors the SQL providers. A purge matches the ambient partition and nothing else: the untenanted
		// partition is a real partition -- the sagas that carry no tenant at all -- not a wildcard, so this
		// can never remove another tenant's saga, and the rows carrying no tenant remain reachable for
		// retention. Estate-wide retention is the separately named PurgeAllTenantsCompletedBeforeAsync.
		return PurgeAsync(
			threshold,
			saga => MatchesAmbientTenant(saga, scope),
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
		var scope = CurrentTenantScope;

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

		// Tenant-confined by the key: a saga identifier alone cannot reach across the boundary, because it
		// is not a whole key. A saga that exists in another tenant returns null -- the same answer the SQL
		// providers give, whose predicate simply does not match the row. No tenant predicate follows the
		// lookup, for the reason given on LoadAsync: on a fully-keyed at-most-one-row read, a further
		// tenant term cannot admit a foreign row and can only turn a correct hit into a miss.
		return _store.TryGetValue(KeyFor(sagaId), out var stored)
			? new ValueTask<SagaInstanceSummary?>(ToSummary(stored))
			: new ValueTask<SagaInstanceSummary?>((SagaInstanceSummary?)null);
	}

	/// <inheritdoc />
	public ValueTask<SagaStoreStatistics> GetStatisticsAsync(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Counts cover the ambient partition and no other, matching the SQL providers. The guard used to be
		// `scope.IsScoped && !MatchesAmbientTenant(...)`, written when an unscoped caller was expected to fall
		// through to estate-wide totals -- but CurrentTenantScope is TenantScope.FromContext and is always
		// scoped, so that conjunct is provably true and the estate-wide read had no reachable caller. Operators
		// who want totals across every tenant call GetAllTenantsStatisticsAsync, which says so at the call site.
		var scope = CurrentTenantScope;

		var completed = 0;
		var total = 0;
		foreach (var stored in _store.Values)
		{
			if (!MatchesAmbientTenant(stored.State, scope))
			{
				continue;
			}

			total++;
			if (stored.State.Completed)
			{
				completed++;
			}
		}

		return new ValueTask<SagaStoreStatistics>(Snapshot(total, completed));
	}

	/// <inheritdoc />
	public ValueTask<SagaStoreStatistics> GetAllTenantsStatisticsAsync(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// No tenant predicate at all: every partition is counted. Reachable only through this method's name,
		// never by an absent or permissive scope.
		var completed = 0;
		var total = 0;
		foreach (var stored in _store.Values)
		{
			total++;
			if (stored.State.Completed)
			{
				completed++;
			}
		}

		return new ValueTask<SagaStoreStatistics>(Snapshot(total, completed));
	}

	private static SagaStoreStatistics Snapshot(int total, int completed) => new()
	{
		RunningCount = total - completed,
		CompletedCount = completed,
		TotalCount = total,
		CapturedAt = DateTimeOffset.UtcNow,
	};

	/// <summary>
	/// Determines whether a saga belongs to the ambient tenant scope.
	/// </summary>
	/// <remarks>
	/// One predicate, mirroring <see cref="PurgeCompletedBeforeAsync"/> and the SQL providers: a caller
	/// matches its own partition. It used to branch on whether a scope was set, so a caller who had
	/// established no tenant matched the sagas carrying none. That branch is gone because the untenanted
	/// partition now carries a real term and is addressed by the same equality as any other tenant.
	/// "No tenant established" is a real partition here, never a wildcard — treating it as one is what
	/// turned every unscoped admin read into a cross-tenant disclosure.
	/// </remarks>
	/// <param name="state">The stored saga state.</param>
	/// <param name="scope">The ambient tenant scope.</param>
	/// <returns><see langword="true"/> when the saga is visible under <paramref name="scope"/>.</returns>
	private static bool MatchesAmbientTenant(SagaState state, TenantScope scope) =>
		string.Equals(state.TenantId, scope.TenantId, StringComparison.Ordinal);

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
