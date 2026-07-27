// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data;
using Excalibur.Dispatch.Messaging;

namespace Tests.Shared.Conformance.Saga;

/// <summary>
/// Base class for ISagaStore conformance tests.
/// Implementations must provide a concrete ISagaStore instance for testing.
/// </summary>
/// <remarks>
/// <para>
/// This conformance test kit verifies that saga store implementations
/// correctly implement the ISagaStore interface contract, including:
/// </para>
/// <list type="bullet">
///   <item>Save and load state round-trip</item>
///   <item>Load non-existent saga returns null</item>
///   <item>Correlation lookup by saga ID</item>
///   <item>Concurrent update handling</item>
///   <item>Timeout/completed saga state management</item>
/// </list>
/// <para>
/// To create conformance tests for your own ISagaStore implementation:
/// <list type="number">
///   <item>Inherit from SagaStoreConformanceTestBase</item>
///   <item>Override CreateStoreAsync() to create an instance of your ISagaStore implementation</item>
///   <item>Override CleanupAsync() to properly clean up resources between tests</item>
/// </list>
/// </para>
/// </remarks>
[Trait("Category", "Conformance")]
[Trait("Component", "Saga")]
public abstract class SagaStoreConformanceTestBase : IAsyncLifetime
{
	/// <summary>
	/// The saga store instance under test.
	/// </summary>
	protected ISagaStore Store { get; private set; } = null!;

	/// <summary>
	/// Gets a value indicating whether the store under test enforces optimistic concurrency
	/// (version-gated, store-owns-increment — a stale-version save throws
	/// <see cref="ConcurrencyException"/> rather than silently overwriting).
	/// </summary>
	/// <remarks>
	/// <para>
	/// e1tsq2 (S853) capability seam — the canonical saga-store contract is optimistic concurrency. The
	/// three distributed providers (Mongo/Firestore/Cosmos) override this to <see langword="true"/> and are
	/// held to <see cref="StaleSave_ThrowsConcurrencyException_NoLostUpdate"/>.
	/// </para>
	/// <para>
	/// <b>TRANSITIONAL.</b> Default <see langword="false"/> covers stores that have NOT yet implemented the
	/// contract (currently <c>InMemorySagaStore</c>) — a DECLARED + TRACKED gap (<c>bd-boxiyl</c>), not a
	/// co-equal contract. When <c>boxiyl</c> lands (InMemory version-gated + its consumers swept), this flag
	/// and the <c>!flag</c>-gated <see cref="ConcurrentSave_SameSaga_LastWriteWins"/> declaration are deleted
	/// and <see cref="StaleSave_ThrowsConcurrencyException_NoLostUpdate"/> becomes unconditional.
	/// </para>
	/// </remarks>
	protected virtual bool SupportsOptimisticConcurrency => false;

	/// <summary>
	/// Gets a value indicating whether the store round-trips <see cref="SagaState.ProcessedEventIds"/>
	/// across <c>SaveAsync</c>/<c>LoadAsync</c>, so the coordinator's idempotent-replay guard survives a
	/// reload (a re-delivered already-processed event id is deduped after the saga is reloaded from the store).
	/// </summary>
	/// <remarks>
	/// <para>
	/// uclyao (S865) capability seam. The dedup decision itself lives in
	/// <c>SagaCoordinator.HandleEventInternalAsync</c> (<c>if (!sagaState.TryMarkEventProcessed(id)) return;</c>
	/// — no re-save, no version bump, no <see cref="ConcurrencyException"/>); the STORE's contribution is
	/// persisting/restoring the processed-id set so that guard fires after a reload. Stores that round-trip
	/// <see cref="SagaState.ProcessedEventIds"/> (e.g. <c>InMemorySagaStore</c> via
	/// <c>JsonObjectCreationHandling.Populate</c>) override this to <see langword="true"/> and are held to
	/// <see cref="IdempotentReplay_ReDeliveredEvent_IsDeduped_VersionUnchanged"/>.
	/// </para>
	/// <para>
	/// <b>TRANSITIONAL.</b> Default <see langword="false"/> keeps a provider whose round-trip has not yet been
	/// verified from failing CI; each provider flips this to <see langword="true"/> once its
	/// <see cref="SagaState.ProcessedEventIds"/> round-trip is confirmed (its lane). Tracked so it does not
	/// silently stay opted-out.
	/// </para>
	/// </remarks>
	protected virtual bool SupportsIdempotentReplay => false;

	/// <summary>
	/// Gets a value indicating whether the store under test implements
	/// <see cref="ISagaStore.PurgeCompletedBeforeAsync"/> (d0wpug seam). Stores WITH a purge implementation
	/// (in-memory, SqlServer, Postgres, MongoDB) leave this <see langword="true"/> and are held to the
	/// deletion/retention behavior. Cloud stores that do not yet implement purge override this to
	/// <see langword="false"/>; they hit the interface's default-implementation contract, which throws
	/// <see cref="NotSupportedException"/> (fail-loud, per the SA ruling) — the gate asserts THAT contract
	/// non-vacuously rather than skipping silently.
	/// </summary>
	protected virtual bool SupportsCompletedPurge => true;

	/// <inheritdoc/>
	public async ValueTask InitializeAsync()
	{
		Store = await CreateStoreAsync().ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async ValueTask DisposeAsync()
	{
		await CleanupAsync().ConfigureAwait(false);

		if (Store is IAsyncDisposable asyncDisposable)
		{
			await asyncDisposable.DisposeAsync().ConfigureAwait(false);
		}
		else if (Store is IDisposable disposable)
		{
			disposable.Dispose();
		}
	}

	/// <summary>
	/// Creates a new instance of the ISagaStore implementation under test.
	/// </summary>
	/// <returns>A configured ISagaStore instance.</returns>
	protected abstract Task<ISagaStore> CreateStoreAsync();

	/// <summary>
	/// Cleans up the ISagaStore instance after each test.
	/// </summary>
	protected abstract Task CleanupAsync();

	#region Helper Methods

	/// <summary>
	/// Creates a test saga state with the specified saga ID and optional properties.
	/// </summary>
	protected static TestSagaState CreateTestSagaState(
		Guid? sagaId = null,
		bool completed = false,
		string? data = null)
	{
		return new TestSagaState
		{
			SagaId = sagaId ?? Guid.NewGuid(),
			Completed = completed,
			Data = data ?? $"TestData-{Guid.NewGuid():N}"
		};
	}

	#endregion Helper Methods

	#region Interface Implementation Tests

	[Fact]
	public void Store_ShouldImplementISagaStore()
	{
		// Assert
		_ = Store.ShouldBeAssignableTo<ISagaStore>();
	}

	#endregion Interface Implementation Tests

	#region SaveState Tests

	[Fact]
	public async Task SaveAsync_NewSaga_Succeeds()
	{
		// Arrange
		var state = CreateTestSagaState();

		// Act & Assert - Should not throw
		await Should.NotThrowAsync(async () =>
			await Store.SaveAsync(state, CancellationToken.None).ConfigureAwait(false));
	}

	[Fact]
	public async Task SaveAsync_PreservesAllProperties()
	{
		// Arrange
		var sagaId = Guid.NewGuid();
		var state = CreateTestSagaState(sagaId: sagaId, data: "SpecialData");

		// Act
		await Store.SaveAsync(state, CancellationToken.None).ConfigureAwait(false);
		var loaded = await Store.LoadAsync<TestSagaState>(sagaId, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		loaded.ShouldNotBeNull();
		loaded.SagaId.ShouldBe(sagaId);
		loaded.Data.ShouldBe("SpecialData");
		loaded.Completed.ShouldBeFalse();
	}

	[Fact]
	public async Task SaveAsync_UpdateExisting_OverwritesState()
	{
		// Arrange
		var sagaId = Guid.NewGuid();
		var state = CreateTestSagaState(sagaId: sagaId, data: "Original");
		await Store.SaveAsync(state, CancellationToken.None).ConfigureAwait(false);

		// Act - Update
		state.Data = "Updated";
		await Store.SaveAsync(state, CancellationToken.None).ConfigureAwait(false);

		var loaded = await Store.LoadAsync<TestSagaState>(sagaId, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		loaded.ShouldNotBeNull();
		loaded.Data.ShouldBe("Updated");
	}

	[Fact]
	public async Task SaveAsync_CompletedSaga_Persists()
	{
		// Arrange
		var sagaId = Guid.NewGuid();
		var state = CreateTestSagaState(sagaId: sagaId, completed: true);

		// Act
		await Store.SaveAsync(state, CancellationToken.None).ConfigureAwait(false);
		var loaded = await Store.LoadAsync<TestSagaState>(sagaId, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		loaded.ShouldNotBeNull();
		loaded.Completed.ShouldBeTrue("Completed flag should be persisted");
	}

	#endregion SaveState Tests

	#region LoadState Tests

	[Fact]
	public async Task LoadAsync_ExistingSaga_ReturnsSagaState()
	{
		// Arrange
		var sagaId = Guid.NewGuid();
		var state = CreateTestSagaState(sagaId: sagaId);
		await Store.SaveAsync(state, CancellationToken.None).ConfigureAwait(false);

		// Act
		var loaded = await Store.LoadAsync<TestSagaState>(sagaId, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		loaded.ShouldNotBeNull();
		loaded.SagaId.ShouldBe(sagaId);
	}

	[Fact]
	public async Task LoadAsync_NonExistentSaga_ReturnsNull()
	{
		// Arrange
		var nonExistentId = Guid.NewGuid();

		// Act
		var loaded = await Store.LoadAsync<TestSagaState>(nonExistentId, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		loaded.ShouldBeNull("Loading non-existent saga should return null");
	}

	[Fact]
	public async Task LoadAsync_MultipleSagas_ReturnsCorrectOne()
	{
		// Arrange
		var saga1 = CreateTestSagaState(data: "Saga1");
		var saga2 = CreateTestSagaState(data: "Saga2");
		var saga3 = CreateTestSagaState(data: "Saga3");

		await Store.SaveAsync(saga1, CancellationToken.None).ConfigureAwait(false);
		await Store.SaveAsync(saga2, CancellationToken.None).ConfigureAwait(false);
		await Store.SaveAsync(saga3, CancellationToken.None).ConfigureAwait(false);

		// Act
		var loaded = await Store.LoadAsync<TestSagaState>(saga2.SagaId, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		loaded.ShouldNotBeNull();
		loaded.SagaId.ShouldBe(saga2.SagaId);
		loaded.Data.ShouldBe("Saga2");
	}

	#endregion LoadState Tests

	#region CorrelationLookup Tests

	[Fact]
	public async Task CorrelationLookup_BySagaId_FindsCorrectSaga()
	{
		// Arrange
		var sagaId = Guid.NewGuid();
		var state = CreateTestSagaState(sagaId: sagaId, data: "CorrelatedData");
		await Store.SaveAsync(state, CancellationToken.None).ConfigureAwait(false);

		// Act - Load by saga ID (the fundamental correlation)
		var loaded = await Store.LoadAsync<TestSagaState>(sagaId, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		loaded.ShouldNotBeNull();
		loaded.Data.ShouldBe("CorrelatedData");
	}

	/// <summary>
	/// 1f5om2 (S853, DATA-CORRUPTION) — author≠impl TYPE-ISOLATION conformance, single-owned by
	/// TestsDeveloper (forge cl.7), reused by EVERY saga-store conformance subclass (uniform cross-provider
	/// contract — there is no capability gate; type-isolation is mandatory for all stores).
	/// </summary>
	/// <remarks>
	/// <para>
	/// Uniform contract: a typed <c>LoadAsync&lt;TSagaState&gt;(id)</c> MUST return <see langword="null"/>
	/// when the saga persisted at <paramref name="id"/> is a DIFFERENT type that merely shares the Guid — it
	/// must NEVER mis-deserialize the wrong-typed saga's blob into <c>TSagaState</c> (silent data corruption).
	/// Reference-correct: <c>InMemorySagaStore</c> (<c>state is TSagaState</c> typed check → null). The fix
	/// added a <c>SagaType</c> filter (discriminator = <c>typeof(TSagaState).Name</c>, matching the Save path)
	/// to the Load path of SqlServer/Postgres/Mongo; Cosmos (PartitionKey=sagaType), Firestore
	/// (docId=<c>{sagaId}_{sagaType}</c>), and DynamoDb (SK=sagaType) already enforced it structurally.
	/// </para>
	/// <para>
	/// NON-VACUOUS: this was previously a tolerant test (accepted a mis-deserialized non-null result). It now
	/// asserts <see langword="null"/>. RED on the pre-fix SqlServer/Postgres/Mongo load-by-id-only path (which
	/// returned a mis-deserialized <see cref="AlternateTestSagaState"/>); GREEN on the type-scoped load.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task CorrelationLookup_DifferentSagaTypes_Isolated()
	{
		// Arrange — persist a TestSagaState (the store records SagaType = "TestSagaState").
		var sagaId = Guid.NewGuid();
		var testState = CreateTestSagaState(sagaId: sagaId, data: "TestType");
		await Store.SaveAsync(testState, CancellationToken.None).ConfigureAwait(false);

		// Act — load the SAME id as a DIFFERENT saga type (SagaType "AlternateTestSagaState").
		var loaded = await Store.LoadAsync<AlternateTestSagaState>(sagaId, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert — the wrong-typed saga must NOT be returned (no mis-deserialization). Uniform null contract.
		loaded.ShouldBeNull(
			"LoadAsync<TSagaState>(id) must return null when the saga at id is a different type (1f5om2 type-isolation)");
	}

	#endregion CorrelationLookup Tests

	#region ConcurrentUpdate Tests

	[Fact]
	public async Task ConcurrentSave_DifferentSagas_AllSucceed()
	{
		// Arrange
		const int sagaCount = 10;
		var sagas = Enumerable.Range(0, sagaCount)
			.Select(_ => CreateTestSagaState())
			.ToList();

		// Act - Save all concurrently
		var tasks = sagas.Select(s =>
			Store.SaveAsync(s, CancellationToken.None));
		await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert - All should be loadable
		foreach (var saga in sagas)
		{
			var loaded = await Store.LoadAsync<TestSagaState>(saga.SagaId, CancellationToken.None)
				.ConfigureAwait(false);
			loaded.ShouldNotBeNull($"Saga {saga.SagaId} should be loadable after concurrent save");
		}
	}

	// NOTE: e1tsq2 (S853) — the former `ConcurrentSave_SameSaga_LastWriteWins` test was DELETED. It
	// certified last-write-wins (silent lost update), which is precisely the bug e1tsq2 eliminates. The
	// canonical saga-store contract is now optimistic concurrency (uniform — InMemory pulled in this sprint,
	// bd-boxiyl folded); the conformance assertion is `StaleSave_ThrowsConcurrencyException_NoLostUpdate`.

	/// <summary>
	/// e1tsq2 (S853, DATA LOSS) — author≠impl optimistic-concurrency conformance, single-owned by
	/// TestsDeveloper (forge cl.7), reused by every store declaring
	/// <see cref="SupportsOptimisticConcurrency"/> == <see langword="true"/> (Mongo/Firestore/Cosmos).
	/// </summary>
	/// <remarks>
	/// Canonical contract (reuses <c>SqlServerSagaStore.cs:198-222</c>, store-owns-increment): two parties
	/// load the same saga at version N; the first save succeeds (store increments to N+1); the second, still
	/// carrying the now-stale N, MUST throw <see cref="ConcurrencyException"/>
	/// and MUST NOT overwrite the winner. Deterministic SEQUENTIAL shape (not a flaky race) per the GUIDE.
	/// RED on the pre-fix blind upsert/SetAsync (the stale save silently overwrites — the lost update).
	/// </remarks>
	[Fact]
	public async Task StaleSave_ThrowsConcurrencyException_NoLostUpdate()
	{
		// Capability-gated: only stores that declare optimistic concurrency are held to this contract.
		if (!SupportsOptimisticConcurrency)
		{
			return;
		}

		// Arrange — persist a saga (store owns the insert/increment), then load TWO copies at the same version.
		var sagaId = Guid.NewGuid();
		var initial = CreateTestSagaState(sagaId: sagaId, data: "v1");
		await Store.SaveAsync(initial, CancellationToken.None).ConfigureAwait(false);

		var copy1 = await Store.LoadAsync<TestSagaState>(sagaId, CancellationToken.None).ConfigureAwait(false);
		var copy2 = await Store.LoadAsync<TestSagaState>(sagaId, CancellationToken.None).ConfigureAwait(false);
		copy1.ShouldNotBeNull();
		copy2.ShouldNotBeNull();

		// Act — copy1 saves first (no caller arithmetic): the store CASes on the loaded version and succeeds.
		copy1!.Data = "winner";
		await Store.SaveAsync(copy1, CancellationToken.None).ConfigureAwait(false);

		// copy2 still carries the now-stale version → the save MUST be rejected (no lost update).
		copy2!.Data = "loser";

		// Assert — stale save throws; RED on the pre-fix blind upsert which silently overwrote.
		_ = await Should.ThrowAsync<ConcurrencyException>(
			() => Store.SaveAsync(copy2, CancellationToken.None)).ConfigureAwait(false);

		// The winner's write survived — the stale "loser" did NOT overwrite it.
		var persisted = await Store.LoadAsync<TestSagaState>(sagaId, CancellationToken.None).ConfigureAwait(false);
		persisted.ShouldNotBeNull();
		persisted!.Data.ShouldBe("winner", "the stale concurrent save must not overwrite the committed winner");
	}

	/// <summary>
	/// e1tsq2 / skl8r7 (S853, DATA-INTEGRITY) — author≠impl NO-RESURRECT conformance, single-owned by
	/// TestsDeveloper (forge cl.7), reused by every store declaring
	/// <see cref="SupportsOptimisticConcurrency"/> == <see langword="true"/>.
	/// </summary>
	/// <remarks>
	/// Completes the optimistic-concurrency contract (SA 16395 MUST-FIX): the reference
	/// <c>SqlServerSagaStore</c> MERGE guards the INSERT branch to <c>@ExpectedVersion = 0</c>. A save
	/// carrying a NON-ZERO expected version against a MISSING row (a saga loaded then deleted, or a stale
	/// token for a row that has since moved on) MUST throw
	/// <see cref="Excalibur.Data.ConcurrencyException"/> and MUST NOT re-create (resurrect) the row — a
	/// store that blocks stale-overwrite but allows stale-resurrect is only a partial mirror (zombie saga).
	/// RED on a blind upsert / unguarded insert (which re-creates the row at the stale version).
	/// </remarks>
	[Fact]
	public async Task StaleSave_OnMissingSaga_Throws_DoesNotResurrect()
	{
		// Capability-gated: only stores that declare optimistic concurrency are held to this contract.
		if (!SupportsOptimisticConcurrency)
		{
			return;
		}

		// Arrange — a state carrying a non-zero expected version for a saga that does NOT exist in the store
		// (never persisted here; models a since-deleted saga still held by a caller at its loaded version).
		var sagaId = Guid.NewGuid();
		var staleState = CreateTestSagaState(sagaId: sagaId);
		staleState.Version = 5;

		// Act + Assert — the no-resurrect guard rejects the insert (expected > 0 ⇒ update-only ⇒ 0 rows ⇒
		// ConcurrencyException). RED on a blind upsert that would re-create the row at version 5.
		_ = await Should.ThrowAsync<ConcurrencyException>(
			() => Store.SaveAsync(staleState, CancellationToken.None)).ConfigureAwait(false);

		// The saga was NOT resurrected — no zombie row exists.
		var loaded = await Store.LoadAsync<TestSagaState>(sagaId, CancellationToken.None).ConfigureAwait(false);
		loaded.ShouldBeNull("a stale-version save against a missing saga must not resurrect it");
	}

	/// <summary>
	/// uclyao (S865, IDEMPOTENT-REPLAY) — author≠impl conformance, single-owned by TestsDeveloper
	/// (forge cl.7), reused by every store declaring <see cref="SupportsIdempotentReplay"/> == <see langword="true"/>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Pins the store's half of the coordinator's idempotent-replay contract (SA seam, msg 20404): a
	/// re-delivered <b>already-processed</b> event id (present in <see cref="SagaState.ProcessedEventIds"/>)
	/// is a no-op — the coordinator's guard (<c>SagaCoordinator.HandleEventInternalAsync</c>:
	/// <c>if (!sagaState.TryMarkEventProcessed(id)) return;</c>) returns BEFORE <c>SaveAsync</c>, so the saga
	/// <see cref="SagaState.Version"/> is unchanged and no <see cref="ConcurrencyException"/> is thrown; a
	/// genuinely NEW event id advances the version normally. This test models that guard against the REAL
	/// store round-trip: it drives <see cref="SagaState.TryMarkEventProcessed"/> exactly as the coordinator
	/// does, but across a <c>SaveAsync</c>→<c>LoadAsync</c> boundary — so it proves the store persisted the
	/// processed-id set (the store's contribution), without re-testing the coordinator engine.
	/// </para>
	/// <para>
	/// NON-VACUOUS: a store that DROPS <see cref="SagaState.ProcessedEventIds"/> on save/load reloads an empty
	/// set → <c>TryMarkEventProcessed(x)</c> returns <see langword="true"/> on replay (dedup silently fails) →
	/// RED on both the <c>ShouldContain</c> and the <c>ShouldBeFalse</c> assertions. GREEN only when the set
	/// round-trips. Conflict-preservation (a stale divergent save STILL throws — SA step 4) is locked
	/// separately by <see cref="StaleSave_ThrowsConcurrencyException_NoLostUpdate"/> under
	/// <see cref="SupportsOptimisticConcurrency"/>; replay-dedup must not blanket-swallow that.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task IdempotentReplay_ReDeliveredEvent_IsDeduped_VersionUnchanged()
	{
		// Capability-gated: only stores whose ProcessedEventIds round-trip is verified are held to this.
		if (!SupportsIdempotentReplay)
		{
			return;
		}

		const string eventId = "evt-x";
		var sagaId = Guid.NewGuid();

		// Step 1 — first delivery of event x: mark processed on a fresh saga, then save (store increments).
		var initial = CreateTestSagaState(sagaId: sagaId, data: "v1");
		initial.TryMarkEventProcessed(eventId).ShouldBeTrue("first delivery marks the event id processed");
		await Store.SaveAsync(initial, CancellationToken.None).ConfigureAwait(false);

		var afterFirst = await Store.LoadAsync<TestSagaState>(sagaId, CancellationToken.None).ConfigureAwait(false);
		afterFirst.ShouldNotBeNull();
		var versionAfterFirst = afterFirst!.Version;

		// Step 2 (the uclyao case) — re-delivery of the SAME event id. The store must have round-tripped
		// ProcessedEventIds so the coordinator's replay guard fires on the reloaded saga.
		afterFirst.ProcessedEventIds.ShouldContain(eventId,
			"the store must round-trip ProcessedEventIds so a re-delivered event is deduped after reload");
		afterFirst.TryMarkEventProcessed(eventId).ShouldBeFalse(
			"an already-processed event id is a duplicate — the coordinator returns early WITHOUT re-saving");

		// The coordinator returns before SaveAsync on a duplicate → no version bump, no ConcurrencyException.
		var afterReplay = await Store.LoadAsync<TestSagaState>(sagaId, CancellationToken.None).ConfigureAwait(false);
		afterReplay.ShouldNotBeNull();
		afterReplay!.Version.ShouldBe(versionAfterFirst, "a deduped replay must not advance the saga version");

		// Step 3 — a genuinely NEW event id advances the saga normally (dedup didn't wedge the saga).
		var next = await Store.LoadAsync<TestSagaState>(sagaId, CancellationToken.None).ConfigureAwait(false);
		next!.TryMarkEventProcessed("evt-y").ShouldBeTrue("a new event id is processed");
		next.Data = "v2";
		await Store.SaveAsync(next, CancellationToken.None).ConfigureAwait(false);

		var afterSecond = await Store.LoadAsync<TestSagaState>(sagaId, CancellationToken.None).ConfigureAwait(false);
		afterSecond.ShouldNotBeNull();
		afterSecond!.Version.ShouldBeGreaterThan(versionAfterFirst, "a genuinely new event advances the version");
		afterSecond.Data.ShouldBe("v2");
	}

	[Fact]
	public async Task ConcurrentLoadAndSave_MaintainsConsistency()
	{
		// Arrange
		var sagaId = Guid.NewGuid();
		var state = CreateTestSagaState(sagaId: sagaId, data: "Initial");
		await Store.SaveAsync(state, CancellationToken.None).ConfigureAwait(false);

		// Act — concurrent read + read-modify-write. e1tsq2 (S853): under the optimistic-concurrency
		// contract a writer that loses the version race is REJECTED with ConcurrencyException (exactly the
		// orchestration layer's reload-before-save pattern) — that is correct behaviour, not corruption, so
		// it is tolerated here. (For a non-optimistic store the catch simply never triggers.)
		var tasks = new List<Task>();
		for (int i = 0; i < 5; i++)
		{
			tasks.Add(Store.LoadAsync<TestSagaState>(sagaId, CancellationToken.None));
			var index = i;
			tasks.Add(Task.Run(async () =>
			{
				try
				{
					var current = await Store.LoadAsync<TestSagaState>(sagaId, CancellationToken.None)
						.ConfigureAwait(false);
					if (current is null)
					{
						return;
					}

					current.Data = $"Concurrent-{index}";
					await Store.SaveAsync(current, CancellationToken.None).ConfigureAwait(false);
				}
				catch (ConcurrencyException)
				{
					// Expected under contention — the losing writer is rejected with no lost update.
				}
			}));
		}

		await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert — the store remains consistent and loadable (no corruption from concurrent operations).
		var loaded = await Store.LoadAsync<TestSagaState>(sagaId, CancellationToken.None)
			.ConfigureAwait(false);
		loaded.ShouldNotBeNull("Saga state should remain consistent after concurrent operations");
	}

	#endregion ConcurrentUpdate Tests

	#region Timeout Tests

	[Fact]
	public async Task SaveAndLoad_CompletedSaga_PreservesCompletedState()
	{
		// Arrange
		var sagaId = Guid.NewGuid();
		var state = CreateTestSagaState(sagaId: sagaId);

		// Act - Save, then complete
		await Store.SaveAsync(state, CancellationToken.None).ConfigureAwait(false);
		state.Completed = true;
		await Store.SaveAsync(state, CancellationToken.None).ConfigureAwait(false);

		var loaded = await Store.LoadAsync<TestSagaState>(sagaId, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		loaded.ShouldNotBeNull();
		loaded.Completed.ShouldBeTrue("Completed state should be preserved");
	}

	[Fact]
	public async Task SaveAsync_MultipleSagaStates_InSequence()
	{
		// Arrange & Act
		var states = new List<TestSagaState>();
		for (int i = 0; i < 5; i++)
		{
			var state = CreateTestSagaState(data: $"Sequential-{i}");
			states.Add(state);
			await Store.SaveAsync(state, CancellationToken.None).ConfigureAwait(false);
		}

		// Assert - All should be independently loadable
		foreach (var state in states)
		{
			var loaded = await Store.LoadAsync<TestSagaState>(state.SagaId, CancellationToken.None)
				.ConfigureAwait(false);
			loaded.ShouldNotBeNull();
			loaded.Data.ShouldBe(state.Data);
		}
	}

	#endregion Timeout Tests

	#region Purge Tests (d0wpug — ISagaStore.PurgeCompletedBeforeAsync)

	/// <summary>
	/// d0wpug (S872) — author≠impl PURGE conformance, reused by every saga-store conformance subclass
	/// (uniform cross-provider contract for the new <see cref="ISagaStore.PurgeCompletedBeforeAsync"/> seam).
	/// </summary>
	/// <remarks>
	/// <para>
	/// Contract (<c>ISagaStore.PurgeCompletedBeforeAsync</c>, threshold = exclusive upper bound on
	/// <see cref="SagaState.CompletedAt"/>): only sagas that are <see cref="SagaState.Completed"/> with a
	/// non-null <see cref="SagaState.CompletedAt"/> STRICTLY BEFORE the threshold are physically deleted;
	/// completed sagas at/after the threshold are retained, and running (non-completed) sagas are NEVER
	/// removed. The return value is the exact count removed.
	/// </para>
	/// <para>
	/// NON-VACUOUS: a store that ignores the threshold (purges everything), ignores the completed flag
	/// (purges a running saga), or is a no-op (removes nothing) fails one of the three retention/deletion
	/// assertions below. RED against the pre-seam baseline (no purge implementation).
	/// </para>
	/// </remarks>
	[Fact]
	public async Task PurgeCompletedBefore_DeletesOldCompleted_RetainsNewerAndRunning()
	{
		if (!SupportsCompletedPurge)
		{
			// The store advertises no purge support: it hits the interface default implementation, which
			// throws NotSupportedException (SA ruling — fail loud, not a silent no-op). Assert THAT contract
			// non-vacuously rather than skipping.
			_ = await Should.ThrowAsync<NotSupportedException>(async () =>
				await Store.PurgeCompletedBeforeAsync(DateTimeOffset.UtcNow, CancellationToken.None)
					.ConfigureAwait(false)).ConfigureAwait(false);
			return;
		}

		// Anchor on a fixed cutoff so the three fixtures fall deterministically on either side.
		var cutoff = DateTimeOffset.UtcNow;

		// (a) COMPLETED, CompletedAt strictly BEFORE the cutoff → the single purge-eligible saga.
		var oldCompleted = CreateTestSagaState(completed: true);
		oldCompleted.CompletedAt = cutoff.AddMinutes(-30);
		await Store.SaveAsync(oldCompleted, CancellationToken.None).ConfigureAwait(false);

		// (b) COMPLETED, CompletedAt AT/AFTER the cutoff → retained (threshold is exclusive).
		var newCompleted = CreateTestSagaState(completed: true);
		newCompleted.CompletedAt = cutoff.AddMinutes(30);
		await Store.SaveAsync(newCompleted, CancellationToken.None).ConfigureAwait(false);

		// (c) RUNNING (not completed), no CompletedAt → never eligible regardless of age.
		var running = CreateTestSagaState(completed: false);
		await Store.SaveAsync(running, CancellationToken.None).ConfigureAwait(false);

		// Act
		var removed = await Store.PurgeCompletedBeforeAsync(cutoff, CancellationToken.None).ConfigureAwait(false);

		// Assert — exactly the one old-completed saga is purged and physically gone; the other two survive.
		removed.ShouldBe(1, "exactly the one completed saga older than the cutoff must be purged");

		(await Store.LoadAsync<TestSagaState>(oldCompleted.SagaId, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeNull("a completed saga whose CompletedAt is before the cutoff must be physically deleted");
		(await Store.LoadAsync<TestSagaState>(newCompleted.SagaId, CancellationToken.None).ConfigureAwait(false))
			.ShouldNotBeNull("a completed saga whose CompletedAt is at/after the cutoff must be retained");
		(await Store.LoadAsync<TestSagaState>(running.SagaId, CancellationToken.None).ConfigureAwait(false))
			.ShouldNotBeNull("a running (non-completed) saga must never be purged");
	}

	[Fact]
	public async Task PurgeCompletedBefore_EmptyStore_RemovesNothing()
	{
		if (!SupportsCompletedPurge)
		{
			// No purge support → the interface default throws NotSupportedException (SA ruling). Assert it.
			_ = await Should.ThrowAsync<NotSupportedException>(async () =>
				await Store.PurgeCompletedBeforeAsync(DateTimeOffset.UtcNow, CancellationToken.None)
					.ConfigureAwait(false)).ConfigureAwait(false);
			return;
		}

		var removed = await Store.PurgeCompletedBeforeAsync(DateTimeOffset.UtcNow, CancellationToken.None)
			.ConfigureAwait(false);

		removed.ShouldBe(0, "purging an empty store must remove nothing");
	}

	#endregion Purge Tests (d0wpug — ISagaStore.PurgeCompletedBeforeAsync)
}

/// <summary>
/// Test saga state for conformance testing.
/// </summary>
public class TestSagaState : SagaState
{
	/// <summary>
	/// Gets or sets test data for the saga state.
	/// </summary>
	public string Data { get; set; } = string.Empty;
}

/// <summary>
/// Alternate test saga state for type isolation testing.
/// </summary>
public class AlternateTestSagaState : SagaState
{
	/// <summary>
	/// Gets or sets alternate data for the saga state.
	/// </summary>
	public string AlternateData { get; set; } = string.Empty;
}
