// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Text.Json;

using Tests.Shared.Categories;

namespace Excalibur.Dispatch.Tests.Functional.Workflows.SagaStatePersistence;

/// <summary>
/// Functional tests for saga state persistence workflows.
/// Tests state save/load, recovery after restart, concurrent updates, version conflicts, and corrupted state handling.
/// </summary>
/// <remarks>
/// <para>
/// Sprint 197 - Saga Orchestration Advanced Tests.
/// bd-8oeef: Saga State Persistence Tests (5 tests).
/// </para>
/// <para>
/// These tests verify that saga state is properly persisted and recoverable.
/// Uses in-memory store with versioning support to simulate persistence behaviors.
/// </para>
/// </remarks>
[FunctionalTest]
public sealed class SagaStatePersistenceWorkflowTests : FunctionalTestBase
{
	/// <inheritdoc/>
	protected override TimeSpan TestTimeout => TestTimeouts.Functional;

	/// <summary>
	/// Test 1: Verifies that saga state is persisted after each step execution.
	/// </summary>
	[Fact]
	public async Task Saga_State_Is_Persisted_After_Each_Step()
	{
		// Arrange
		var store = new VersionedSagaStore();
		var saga = new PersistentSaga(store);
		var sagaId = "saga-persist-001";

		// Act - Execute 3 steps
		await RunWithTimeoutAsync(async _ =>
		{
			await saga.StartAsync(sagaId, new OrderData { OrderId = "ORD-001" }).ConfigureAwait(true);
			await saga.ExecuteStepAsync(sagaId, "ValidateOrder").ConfigureAwait(true);
			await saga.ExecuteStepAsync(sagaId, "ReserveInventory").ConfigureAwait(true);
			await saga.ExecuteStepAsync(sagaId, "ProcessPayment").ConfigureAwait(true);
		}).ConfigureAwait(true);

		// Assert - State was saved 4 times (start + 3 steps)
		store.SaveCount.ShouldBe(4);

		// Assert - Final state reflects all steps
		var state = await store.GetAsync(sagaId).ConfigureAwait(true);
		_ = state.ShouldNotBeNull();
		state.CompletedSteps.Count.ShouldBe(3);
		state.CompletedSteps.ShouldContain("ValidateOrder");
		state.CompletedSteps.ShouldContain("ReserveInventory");
		state.CompletedSteps.ShouldContain("ProcessPayment");
		state.Version.ShouldBe(4); // Initial + 3 updates
	}

	/// <summary>
	/// Test 2: Verifies that saga state is recovered after a simulated restart.
	/// </summary>
	[Fact]
	public async Task Saga_State_Is_Recovered_After_Restart()
	{
		// Arrange - Shared persistent store
		var store = new VersionedSagaStore();
		var sagaId = "saga-persist-002";

		// Act - Execute some steps, then simulate restart
		await RunWithTimeoutAsync(async _ =>
		{
			// Before restart - execute 2 steps
			var saga1 = new PersistentSaga(store);
			await saga1.StartAsync(sagaId, new OrderData { OrderId = "ORD-002" }).ConfigureAwait(true);
			await saga1.ExecuteStepAsync(sagaId, "ValidateOrder").ConfigureAwait(true);
			await saga1.ExecuteStepAsync(sagaId, "ReserveInventory").ConfigureAwait(true);

			// Simulate restart - create new saga instance
			var saga2 = new PersistentSaga(store);
			var recovered = await saga2.RecoverAsync(sagaId).ConfigureAwait(true);

			// Assert - Recovery successful
			recovered.ShouldBeTrue();

			// Continue from recovered state
			await saga2.ExecuteStepAsync(sagaId, "ProcessPayment").ConfigureAwait(true);
			await saga2.ExecuteStepAsync(sagaId, "ShipOrder").ConfigureAwait(true);
		}).ConfigureAwait(true);

		// Assert - Final state includes all steps
		var state = await store.GetAsync(sagaId).ConfigureAwait(true);
		_ = state.ShouldNotBeNull();
		state.CompletedSteps.Count.ShouldBe(4);
		state.Status.ShouldBe(SagaStatus.InProgress);
	}

	/// <summary>
	/// Test 3: Verifies that concurrent updates are handled correctly.
	/// </summary>
	[Fact]
	public async Task Saga_Concurrent_Updates_Are_Handled()
	{
		// Arrange
		var store = new VersionedSagaStore();
		var sagaId = "saga-persist-003";

		// Act - Start saga, then attempt concurrent updates
		var results = await RunWithTimeoutAsync(async _ =>
		{
			var saga = new PersistentSaga(store);
			await saga.StartAsync(sagaId, new OrderData { OrderId = "ORD-003" }).ConfigureAwait(true);

			// Simulate 2 parallel updates
			var task1 = saga.ExecuteStepAsync(sagaId, "Step1");
			var task2 = saga.ExecuteStepAsync(sagaId, "Step2");

			var outcomes = new List<(string Step, bool Success, string? Error)>();

			try
			{
				await task1.ConfigureAwait(true);
				outcomes.Add(("Step1", true, null));
			}
			catch (ConcurrencyException ex)
			{
				outcomes.Add(("Step1", false, ex.Message));
			}

			try
			{
				await task2.ConfigureAwait(true);
				outcomes.Add(("Step2", true, null));
			}
			catch (ConcurrencyException ex)
			{
				outcomes.Add(("Step2", false, ex.Message));
			}

			return outcomes;
		}).ConfigureAwait(true);

		// Assert - At least one succeeded (could be both if no conflict)
		// With optimistic locking, one may fail with concurrency exception
		results.Count(r => r.Success).ShouldBeGreaterThanOrEqualTo(1);

		// Assert - State is consistent
		var state = await store.GetAsync(sagaId).ConfigureAwait(true);
		_ = state.ShouldNotBeNull();
		state.CompletedSteps.Count.ShouldBeGreaterThanOrEqualTo(1);
	}

	/// <summary>
	/// Test 4: Verifies that stale version updates are detected and rejected.
	/// </summary>
	[Fact]
	public async Task Saga_State_Version_Conflicts_Are_Detected()
	{
		// Arrange
		var store = new VersionedSagaStore();
		var sagaId = "saga-persist-004";

		// Act - Create conflict scenario
		ConcurrencyException? caughtException = null;

		await RunWithTimeoutAsync(async _ =>
		{
			// Initialize saga
			var saga = new PersistentSaga(store);
			await saga.StartAsync(sagaId, new OrderData { OrderId = "ORD-004" }).ConfigureAwait(true);

			// Load state
			var state = await store.GetAsync(sagaId).ConfigureAwait(true);
			var originalVersion = state.Version;

			// Simulate another process updating the state
			state.CompletedSteps.Add("ExternalUpdate");
			state.Version++;
			await store.SaveAsync(state).ConfigureAwait(true);

			// Now try to update with stale version
			try
			{
				await saga.ExecuteStepWithVersionAsync(sagaId, "MyStep", originalVersion)
					.ConfigureAwait(true);
			}
			catch (ConcurrencyException ex)
			{
				caughtException = ex;
			}
		}).ConfigureAwait(true);

		// Assert - Concurrency exception was thrown
		_ = caughtException.ShouldNotBeNull();
		caughtException.Message.ShouldContain("version");
	}

	/// <summary>
	/// Test 5: Verifies that corrupted state is handled gracefully.
	/// </summary>
	[Fact]
	public async Task Saga_Corrupted_State_Is_Handled_Gracefully()
	{
		// Arrange
		var store = new VersionedSagaStore();
		var sagaId = "saga-persist-005";

		// Act - Inject corrupted data and attempt recovery
		CorruptedStateException? caughtException = null;

		await RunWithTimeoutAsync(async ct =>
		{
			// Store corrupted JSON directly
			store.InjectCorruptedState(sagaId, "{ invalid json here }}}");

			// Try to recover
			var saga = new PersistentSaga(store);
			try
			{
				await saga.RecoverAsync(sagaId).ConfigureAwait(true);
			}
			catch (CorruptedStateException ex)
			{
				caughtException = ex;
			}
		}).ConfigureAwait(true);

		// Assert - Corrupted state exception was thrown with useful info
		_ = caughtException.ShouldNotBeNull();
		caughtException.SagaId.ShouldBe(sagaId);
		caughtException.Message.ShouldContain("corrupted");
	}

	#region Test Infrastructure

	/// <summary>
	/// Saga status.
	/// </summary>
	public enum SagaStatus
	{
		Pending,
		InProgress,
		Completed,
		Failed,
	}

	/// <summary>
	/// Order data for saga.
	/// </summary>
	public sealed class OrderData
	{
		public string OrderId { get; init; } = string.Empty;
	}

	/// <summary>
	/// Saga state with versioning for optimistic concurrency.
	/// </summary>
	public sealed class SagaState
	{
		public string SagaId { get; init; } = string.Empty;
		public int Version { get; set; } = 1;
		public SagaStatus Status { get; set; } = SagaStatus.Pending;
		public OrderData Data { get; init; } = new();
		public List<string> CompletedSteps { get; init; } = new();
		public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
		public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
	}

	/// <summary>
	/// Concurrency exception for version conflicts.
	/// </summary>
	public sealed class ConcurrencyException : Exception
	{
		public ConcurrencyException(string message) : base(message)
		{
		}
	}

	/// <summary>
	/// Exception for corrupted saga state.
	/// </summary>
	public sealed class CorruptedStateException : Exception
	{
		public CorruptedStateException(string sagaId, string message, Exception? inner = null)
			: base(message, inner)
		{
			SagaId = sagaId;
		}

		public string SagaId { get; }
	}

	/// <summary>
	/// Versioned saga store with optimistic concurrency.
	/// </summary>
	public sealed class VersionedSagaStore
	{
		private readonly ConcurrentDictionary<string, string> _serializedStates = new();
		private readonly ConcurrentDictionary<string, int> _versions = new();
		private int _saveCount;

		public int SaveCount => _saveCount;

		public Task SaveAsync(SagaState state)
		{
			var expectedVersion = state.Version - 1;
			var currentVersion = _versions.GetOrAdd(state.SagaId, 0);

			if (currentVersion != expectedVersion && currentVersion != 0)
			{
				throw new ConcurrencyException(
					$"Version conflict: expected {expectedVersion}, current is {currentVersion}");
			}

			state.UpdatedAt = DateTimeOffset.UtcNow;
			var json = JsonSerializer.Serialize(state);
			_serializedStates[state.SagaId] = json;
			_versions[state.SagaId] = state.Version;

			_ = Interlocked.Increment(ref _saveCount);
			return Task.CompletedTask;
		}

		public Task<SagaState?> GetAsync(string sagaId)
		{
			if (_serializedStates.TryGetValue(sagaId, out var json))
			{
				try
				{
					return Task.FromResult(JsonSerializer.Deserialize<SagaState>(json));
				}
				catch (JsonException ex)
				{
					throw new CorruptedStateException(sagaId,
						$"Saga state for {sagaId} is corrupted", ex);
				}
			}
			return Task.FromResult<SagaState?>(null);
		}

		public void InjectCorruptedState(string sagaId, string corruptedJson)
		{
			_serializedStates[sagaId] = corruptedJson;
			_versions[sagaId] = 1;
		}
	}

	/// <summary>
	/// Saga with persistence support.
	/// </summary>
	public sealed class PersistentSaga : IDisposable
	{
		private readonly VersionedSagaStore _store;
		private readonly SemaphoreSlim _lock = new(1, 1);
		private bool _disposed;

		public PersistentSaga(VersionedSagaStore store)
		{
			_store = store;
		}

		public async Task StartAsync(string sagaId, OrderData data)
		{
			var state = new SagaState
			{
				SagaId = sagaId,
				Version = 1,
				Status = SagaStatus.InProgress,
				Data = data,
			};
			await _store.SaveAsync(state).ConfigureAwait(false);
		}

		public async Task<bool> RecoverAsync(string sagaId)
		{
			var state = await _store.GetAsync(sagaId).ConfigureAwait(false);
			return state != null;
		}

		public async Task ExecuteStepAsync(string sagaId, string stepName)
		{
			await _lock.WaitAsync().ConfigureAwait(false);
			try
			{
				var state = await _store.GetAsync(sagaId).ConfigureAwait(false)
					?? throw new InvalidOperationException($"Saga {sagaId} not found");

				state.CompletedSteps.Add(stepName);
				state.Version++;
				await _store.SaveAsync(state).ConfigureAwait(false);
			}
			finally
			{
				_ = _lock.Release();
			}
		}

		public async Task ExecuteStepWithVersionAsync(string sagaId, string stepName, int expectedVersion)
		{
			var state = await _store.GetAsync(sagaId).ConfigureAwait(false)
				?? throw new InvalidOperationException($"Saga {sagaId} not found");

			if (state.Version != expectedVersion)
			{
				throw new ConcurrencyException(
					$"Version conflict: expected {expectedVersion}, current is {state.Version}");
			}

			state.CompletedSteps.Add(stepName);
			state.Version++;
			await _store.SaveAsync(state).ConfigureAwait(false);
		}

		public void Dispose()
		{
			if (_disposed)
				return;
			_disposed = true;
			_lock.Dispose();
		}
	}

	#endregion Test Infrastructure
}
