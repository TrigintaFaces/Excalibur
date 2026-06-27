// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


#pragma warning disable IDE0270 // Null check can be simplified

using Excalibur.Data;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.Testing.Conformance;

/// <summary>
/// Abstract base class for ISagaStore conformance testing.
/// </summary>
/// <remarks>
/// <para>
/// Inherit from this class and implement <see cref="CreateStore"/> to verify that
/// your saga store implementation conforms to the ISagaStore contract.
/// </para>
/// <para>
/// The test kit verifies core saga operations including save, load, update,
/// and isolation behavior.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class SqlServerSagaStoreConformanceTests : SagaStoreConformanceTestKit
/// {
///     private readonly SqlServerFixture _fixture;
///
///     protected override ISagaStore CreateStore() =>
///         new SqlServerSagaStore(_fixture.ConnectionString);
///
///     protected override async Task CleanupAsync() =>
///         await _fixture.CleanupAsync();
/// }
/// </code>
/// </example>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
public abstract class SagaStoreConformanceTestKit
{
	/// <summary>
	/// Creates a fresh saga store instance for testing.
	/// </summary>
	/// <returns>An ISagaStore implementation to test.</returns>
	protected abstract ISagaStore CreateStore();

	/// <summary>
	/// Optional cleanup after each test.
	/// </summary>
	/// <returns>A task representing the cleanup operation.</returns>
	protected virtual Task CleanupAsync() => Task.CompletedTask;

	/// <summary>
	/// Creates a test saga state with the given ID.
	/// </summary>
	/// <param name="sagaId">The saga identifier.</param>
	/// <returns>A test saga state.</returns>
	protected virtual TestSagaState CreateTestSagaState(Guid sagaId) =>
		TestSagaState.Create(sagaId);

	/// <summary>
	/// Generates a unique saga ID for test isolation.
	/// </summary>
	/// <returns>A unique saga identifier.</returns>
	protected virtual Guid GenerateSagaId() => Guid.NewGuid();

	/// <summary>
	/// Gets a value indicating whether the store under test enforces optimistic concurrency
	/// (version-gated, store-owns-increment — a stale-version save throws
	/// <see cref="ConcurrencyException"/> rather than silently overwriting, and a non-zero-version save
	/// against a missing saga does not resurrect it).
	/// </summary>
	/// <remarks>
	/// Default <see langword="false"/> so consumer stores that have not yet implemented the optimistic-
	/// concurrency contract are not held to it (the keystone facts early-return). A store that DOES
	/// implement it overrides this to <see langword="true"/> and is then held to
	/// <see cref="StaleSave_ThrowsConcurrencyException_NoLostUpdate"/>,
	/// <see cref="StaleSave_OnMissingSaga_DoesNotResurrect"/>, and
	/// <see cref="LoadAsync_ReturnsAuthoritativeVersion_AndReloadMutateSaveSucceeds"/>. This is the
	/// consumer-protection contract (FR-D5): the shipped kit must be able to FAIL a saga store that
	/// silently allows lost-updates or zombie-resurrection.
	/// </remarks>
	protected virtual bool SupportsOptimisticConcurrency => false;

	#region Save Tests

	/// <summary>
	/// Verifies that saving a new saga succeeds.
	/// </summary>
	public virtual async Task SaveAsync_NewSaga_ShouldSucceed()
	{
		var store = CreateStore();
		var sagaId = GenerateSagaId();
		var state = CreateTestSagaState(sagaId);

		await store.SaveAsync(state, CancellationToken.None).ConfigureAwait(false);

		var loaded = await store.LoadAsync<TestSagaState>(sagaId, CancellationToken.None)
			.ConfigureAwait(false);

		if (loaded is null)
		{
			throw new TestFixtureAssertionException("Expected saga state but got null");
		}

		if (loaded.SagaId != sagaId)
		{
			throw new TestFixtureAssertionException(
				$"SagaId mismatch: expected {sagaId}, got {loaded.SagaId}");
		}
	}

	/// <summary>
	/// Verifies that saving an existing saga updates it.
	/// </summary>
	public virtual async Task SaveAsync_ExistingSaga_ShouldUpdate()
	{
		var store = CreateStore();
		var sagaId = GenerateSagaId();
		var state = CreateTestSagaState(sagaId);
		state.Status = "Initial";

		await store.SaveAsync(state, CancellationToken.None).ConfigureAwait(false);

		state.Status = "Updated";
		state.Counter = 42;
		await store.SaveAsync(state, CancellationToken.None).ConfigureAwait(false);

		var loaded = await store.LoadAsync<TestSagaState>(sagaId, CancellationToken.None)
			.ConfigureAwait(false);

		if (loaded is null)
		{
			throw new TestFixtureAssertionException("Expected saga state but got null");
		}

		if (loaded.Status != "Updated")
		{
			throw new TestFixtureAssertionException(
				$"Status mismatch: expected 'Updated', got '{loaded.Status}'");
		}

		if (loaded.Counter != 42)
		{
			throw new TestFixtureAssertionException(
				$"Counter mismatch: expected 42, got {loaded.Counter}");
		}
	}

	/// <summary>
	/// Verifies that the Completed flag is persisted.
	/// </summary>
	public virtual async Task SaveAsync_CompletedSaga_ShouldPersistCompletedFlag()
	{
		var store = CreateStore();
		var sagaId = GenerateSagaId();
		var state = CreateTestSagaState(sagaId);
		state.Completed = true;

		await store.SaveAsync(state, CancellationToken.None).ConfigureAwait(false);

		var loaded = await store.LoadAsync<TestSagaState>(sagaId, CancellationToken.None)
			.ConfigureAwait(false);

		if (loaded is null)
		{
			throw new TestFixtureAssertionException("Expected saga state but got null");
		}

		if (!loaded.Completed)
		{
			throw new TestFixtureAssertionException("Expected Completed to be true but got false");
		}
	}

	#endregion

	#region Load Tests

	/// <summary>
	/// Verifies that loading a non-existent saga returns null.
	/// </summary>
	public virtual async Task LoadAsync_NonExistent_ShouldReturnNull()
	{
		var store = CreateStore();
		var sagaId = GenerateSagaId();

		var loaded = await store.LoadAsync<TestSagaState>(sagaId, CancellationToken.None)
			.ConfigureAwait(false);

		if (loaded is not null)
		{
			throw new TestFixtureAssertionException(
				$"Expected null for non-existent saga but got state with status '{loaded.Status}'");
		}
	}

	/// <summary>
	/// Verifies that loading an existing saga returns its state.
	/// </summary>
	public virtual async Task LoadAsync_ExistingSaga_ShouldReturnState()
	{
		var store = CreateStore();
		var sagaId = GenerateSagaId();
		var state = CreateTestSagaState(sagaId);
		state.Status = "Persisted";

		await store.SaveAsync(state, CancellationToken.None).ConfigureAwait(false);

		var loaded = await store.LoadAsync<TestSagaState>(sagaId, CancellationToken.None)
			.ConfigureAwait(false);

		if (loaded is null)
		{
			throw new TestFixtureAssertionException("Expected saga state but got null");
		}

		if (loaded.Status != "Persisted")
		{
			throw new TestFixtureAssertionException(
				$"Status mismatch: expected 'Persisted', got '{loaded.Status}'");
		}
	}

	/// <summary>
	/// Verifies that loading after multiple updates returns the latest state.
	/// </summary>
	public virtual async Task LoadAsync_AfterMultipleUpdates_ShouldReturnLatest()
	{
		var store = CreateStore();
		var sagaId = GenerateSagaId();
		var state = CreateTestSagaState(sagaId);

		state.Counter = 1;
		await store.SaveAsync(state, CancellationToken.None).ConfigureAwait(false);

		state.Counter = 2;
		await store.SaveAsync(state, CancellationToken.None).ConfigureAwait(false);

		state.Counter = 3;
		await store.SaveAsync(state, CancellationToken.None).ConfigureAwait(false);

		var loaded = await store.LoadAsync<TestSagaState>(sagaId, CancellationToken.None)
			.ConfigureAwait(false);

		if (loaded is null)
		{
			throw new TestFixtureAssertionException("Expected saga state but got null");
		}

		if (loaded.Counter != 3)
		{
			throw new TestFixtureAssertionException(
				$"Counter mismatch: expected 3 (latest), got {loaded.Counter}");
		}
	}

	#endregion

	#region Round-Trip Tests

	/// <summary>
	/// Verifies that all properties are preserved through save/load cycle.
	/// </summary>
	public virtual async Task SaveAndLoad_ShouldPreserveAllProperties()
	{
		var store = CreateStore();
		var sagaId = GenerateSagaId();
		var state = CreateTestSagaState(sagaId);
		state.Status = "Complete";
		state.Counter = 100;
		state.CreatedUtc = new DateTime(2025, 1, 15, 10, 30, 0, DateTimeKind.Utc);
		state.Completed = true;
		state.CompletedUtc = new DateTime(2025, 1, 16, 14, 45, 0, DateTimeKind.Utc);
		state.Data["key1"] = "value1";
		state.Data["key2"] = "value2";

		await store.SaveAsync(state, CancellationToken.None).ConfigureAwait(false);

		var loaded = await store.LoadAsync<TestSagaState>(sagaId, CancellationToken.None)
			.ConfigureAwait(false);

		if (loaded is null)
		{
			throw new TestFixtureAssertionException("Expected saga state but got null");
		}

		if (loaded.SagaId != sagaId)
		{
			throw new TestFixtureAssertionException(
				$"SagaId mismatch: expected {sagaId}, got {loaded.SagaId}");
		}

		if (loaded.Status != "Complete")
		{
			throw new TestFixtureAssertionException(
				$"Status mismatch: expected 'Complete', got '{loaded.Status}'");
		}

		if (loaded.Counter != 100)
		{
			throw new TestFixtureAssertionException(
				$"Counter mismatch: expected 100, got {loaded.Counter}");
		}

		if (!loaded.Completed)
		{
			throw new TestFixtureAssertionException("Expected Completed to be true");
		}

		if (loaded.Data is null || loaded.Data.Count != 2)
		{
			throw new TestFixtureAssertionException("Expected Data dictionary with 2 entries");
		}

		if (!loaded.Data.TryGetValue("key1", out var value1) || value1 != "value1")
		{
			throw new TestFixtureAssertionException("Expected Data['key1'] = 'value1'");
		}
	}

	/// <summary>
	/// Verifies that DateTime values are preserved correctly.
	/// </summary>
	public virtual async Task SaveAndLoad_ShouldPreserveDateTimeValues()
	{
		var store = CreateStore();
		var sagaId = GenerateSagaId();
		var state = CreateTestSagaState(sagaId);
		var createdUtc = new DateTime(2025, 6, 15, 12, 30, 45, DateTimeKind.Utc);
		state.CreatedUtc = createdUtc;

		await store.SaveAsync(state, CancellationToken.None).ConfigureAwait(false);

		var loaded = await store.LoadAsync<TestSagaState>(sagaId, CancellationToken.None)
			.ConfigureAwait(false);

		if (loaded is null)
		{
			throw new TestFixtureAssertionException("Expected saga state but got null");
		}

		// Allow for minor precision differences in some stores
		var timeDiff = Math.Abs((loaded.CreatedUtc - createdUtc).TotalSeconds);
		if (timeDiff > 1)
		{
			throw new TestFixtureAssertionException(
				$"CreatedUtc mismatch: expected {createdUtc}, got {loaded.CreatedUtc}");
		}
	}

	#endregion

	#region Isolation Tests

	/// <summary>
	/// Verifies that sagas are isolated by saga ID.
	/// </summary>
	public virtual async Task Sagas_ShouldIsolateBySagaId()
	{
		var store = CreateStore();
		var sagaId1 = GenerateSagaId();
		var sagaId2 = GenerateSagaId();

		var state1 = CreateTestSagaState(sagaId1);
		state1.Counter = 111;
		var state2 = CreateTestSagaState(sagaId2);
		state2.Counter = 222;

		await store.SaveAsync(state1, CancellationToken.None).ConfigureAwait(false);
		await store.SaveAsync(state2, CancellationToken.None).ConfigureAwait(false);

		var loaded1 = await store.LoadAsync<TestSagaState>(sagaId1, CancellationToken.None)
			.ConfigureAwait(false);
		var loaded2 = await store.LoadAsync<TestSagaState>(sagaId2, CancellationToken.None)
			.ConfigureAwait(false);

		if (loaded1 is null || loaded1.Counter != 111)
		{
			throw new TestFixtureAssertionException(
				$"Expected saga1 counter 111 but got {loaded1?.Counter}");
		}

		if (loaded2 is null || loaded2.Counter != 222)
		{
			throw new TestFixtureAssertionException(
				$"Expected saga2 counter 222 but got {loaded2?.Counter}");
		}
	}

	/// <summary>
	/// Verifies that updating one saga doesn't affect others.
	/// </summary>
	public virtual async Task UpdateOneSaga_ShouldNotAffectOthers()
	{
		var store = CreateStore();
		var sagaId1 = GenerateSagaId();
		var sagaId2 = GenerateSagaId();

		var state1 = CreateTestSagaState(sagaId1);
		state1.Status = "First";
		var state2 = CreateTestSagaState(sagaId2);
		state2.Status = "Second";

		await store.SaveAsync(state1, CancellationToken.None).ConfigureAwait(false);
		await store.SaveAsync(state2, CancellationToken.None).ConfigureAwait(false);

		// Update only state1 — reload to carry the STORE-OWNED version, mutate, save. e1tsq2 (S853): the
		// saga-store contract is optimistic concurrency / store-owns-increment, so the caller performs NO
		// version arithmetic — a manual `state1.Version++` would submit a stale expected-version and throw
		// ConcurrencyException. Reload-before-save is the provider-agnostic update pattern.
		var state1ToUpdate = await store.LoadAsync<TestSagaState>(sagaId1, CancellationToken.None)
			.ConfigureAwait(false);
		if (state1ToUpdate is null)
		{
			throw new TestFixtureAssertionException("Expected saga1 state to update but got null");
		}

		state1ToUpdate.Status = "Updated";
		await store.SaveAsync(state1ToUpdate, CancellationToken.None).ConfigureAwait(false);

		var loaded2 = await store.LoadAsync<TestSagaState>(sagaId2, CancellationToken.None)
			.ConfigureAwait(false);

		if (loaded2 is null)
		{
			throw new TestFixtureAssertionException("Expected saga2 state but got null");
		}

		if (loaded2.Status != "Second")
		{
			throw new TestFixtureAssertionException(
				$"Expected saga2 status 'Second' (unchanged) but got '{loaded2.Status}'");
		}
	}

	#endregion

	#region Edge Cases

	/// <summary>
	/// Verifies that saving a saga with default values works correctly.
	/// </summary>
	public virtual async Task SaveAsync_WithDefaultValues_ShouldSucceed()
	{
		var store = CreateStore();
		var sagaId = GenerateSagaId();
		var state = new TestSagaState { SagaId = sagaId };

		await store.SaveAsync(state, CancellationToken.None).ConfigureAwait(false);

		var loaded = await store.LoadAsync<TestSagaState>(sagaId, CancellationToken.None)
			.ConfigureAwait(false);

		if (loaded is null)
		{
			throw new TestFixtureAssertionException("Expected saga state but got null");
		}

		if (loaded.SagaId != sagaId)
		{
			throw new TestFixtureAssertionException(
				$"SagaId mismatch: expected {sagaId}, got {loaded.SagaId}");
		}

		if (loaded.Status != "Pending")
		{
			throw new TestFixtureAssertionException(
				$"Expected default status 'Pending' but got '{loaded.Status}'");
		}
	}

	#endregion

	#region Optimistic Concurrency Tests (e1tsq2 / FR-D5)

	// These keystone facts gate on SupportsOptimisticConcurrency. They mirror the framework's internal
	// reference contract (Tests.Shared SagaStoreConformanceTestBase, S853 e1tsq2) so the SHIPPED public kit
	// can validate that a consumer's saga store enforces optimistic concurrency — and, critically, can FAIL
	// a store that silently allows lost-updates or zombie-resurrection. A store that has not implemented the
	// contract leaves SupportsOptimisticConcurrency == false and these facts no-op.

	/// <summary>
	/// Verifies the store rejects a stale concurrent save with <see cref="ConcurrencyException"/> and the
	/// committed winner survives (no lost update). The canonical optimistic-concurrency contract:
	/// store-owns-increment, the caller performs no version arithmetic.
	/// </summary>
	public virtual async Task StaleSave_ThrowsConcurrencyException_NoLostUpdate()
	{
		// Capability-gated: only stores that declare optimistic concurrency are held to this contract.
		if (!SupportsOptimisticConcurrency)
		{
			return;
		}

		var store = CreateStore();
		var sagaId = GenerateSagaId();
		var initial = CreateTestSagaState(sagaId);
		initial.Status = "v1";
		await store.SaveAsync(initial, CancellationToken.None).ConfigureAwait(false);

		// Two parties load the same saga at the same version.
		var copy1 = await store.LoadAsync<TestSagaState>(sagaId, CancellationToken.None).ConfigureAwait(false);
		var copy2 = await store.LoadAsync<TestSagaState>(sagaId, CancellationToken.None).ConfigureAwait(false);
		if (copy1 is null || copy2 is null)
		{
			throw new TestFixtureAssertionException("Expected both loaded copies but got null");
		}

		// copy1 saves first — the store CASes on the loaded version and succeeds.
		copy1.Status = "winner";
		await store.SaveAsync(copy1, CancellationToken.None).ConfigureAwait(false);

		// copy2 still carries the now-stale version → its save MUST be rejected (no lost update).
		copy2.Status = "loser";
		var threw = false;
		try
		{
			await store.SaveAsync(copy2, CancellationToken.None).ConfigureAwait(false);
		}
		catch (ConcurrencyException)
		{
			threw = true;
		}

		if (!threw)
		{
			throw new TestFixtureAssertionException(
				"Expected ConcurrencyException on the stale concurrent save (lost-update protection)");
		}

		// The winner's write survived — the stale "loser" did NOT overwrite it.
		var persisted = await store.LoadAsync<TestSagaState>(sagaId, CancellationToken.None).ConfigureAwait(false);
		if (persisted is null || persisted.Status != "winner")
		{
			throw new TestFixtureAssertionException(
				$"Expected the committed winner to survive but got '{persisted?.Status}'");
		}
	}

	/// <summary>
	/// Verifies that a non-zero-version save against a MISSING saga is rejected with
	/// <see cref="ConcurrencyException"/> and does NOT re-create (resurrect) the saga. A store that blocks
	/// stale-overwrite but allows stale-resurrect is only a partial mirror (zombie saga).
	/// </summary>
	public virtual async Task StaleSave_OnMissingSaga_DoesNotResurrect()
	{
		// Capability-gated: only stores that declare optimistic concurrency are held to this contract.
		if (!SupportsOptimisticConcurrency)
		{
			return;
		}

		var store = CreateStore();
		var sagaId = GenerateSagaId();

		// A state carrying a non-zero expected version for a saga that does not exist in the store
		// (models a since-deleted/completed saga still held by a caller at its loaded version).
		var staleState = CreateTestSagaState(sagaId);
		staleState.Version = 5;

		var threw = false;
		try
		{
			await store.SaveAsync(staleState, CancellationToken.None).ConfigureAwait(false);
		}
		catch (ConcurrencyException)
		{
			threw = true;
		}

		if (!threw)
		{
			throw new TestFixtureAssertionException(
				"Expected ConcurrencyException — a non-zero-version save against a missing saga must not resurrect it");
		}

		// No zombie row exists — the saga was NOT resurrected.
		var loaded = await store.LoadAsync<TestSagaState>(sagaId, CancellationToken.None).ConfigureAwait(false);
		if (loaded is not null)
		{
			throw new TestFixtureAssertionException(
				"Expected no zombie saga — the stale-version save must not resurrect a missing saga");
		}
	}

	/// <summary>
	/// Verifies that <c>LoadAsync</c> returns the authoritative, store-incremented version (not the caller's
	/// pre-save value), and that a reload-mutate-save round-trip succeeds without manual version arithmetic
	/// (store-owns-increment).
	/// </summary>
	public virtual async Task LoadAsync_ReturnsAuthoritativeVersion_AndReloadMutateSaveSucceeds()
	{
		// Capability-gated: only stores that declare optimistic concurrency are held to this contract.
		if (!SupportsOptimisticConcurrency)
		{
			return;
		}

		var store = CreateStore();
		var sagaId = GenerateSagaId();
		var state = CreateTestSagaState(sagaId);
		await store.SaveAsync(state, CancellationToken.None).ConfigureAwait(false);

		// LoadAsync returns the store-incremented version (the e1tsq2 contract: the version is authoritative
		// store-owned state, not the caller's pre-save 0).
		var loaded = await store.LoadAsync<TestSagaState>(sagaId, CancellationToken.None).ConfigureAwait(false);
		if (loaded is null)
		{
			throw new TestFixtureAssertionException("Expected saga state but got null");
		}

		if (loaded.Version <= 0)
		{
			throw new TestFixtureAssertionException(
				$"Expected LoadAsync to return the store-incremented version (> 0) but got {loaded.Version}");
		}

		// Reload-mutate-save round-trips WITHOUT manual version arithmetic — the store owns the increment.
		loaded.Status = "updated";
		await store.SaveAsync(loaded, CancellationToken.None).ConfigureAwait(false);

		var reloaded = await store.LoadAsync<TestSagaState>(sagaId, CancellationToken.None).ConfigureAwait(false);
		if (reloaded is null || reloaded.Status != "updated")
		{
			throw new TestFixtureAssertionException(
				$"Expected reload-mutate-save to succeed and persist 'updated' but got '{reloaded?.Status}'");
		}
	}

	#endregion
}
