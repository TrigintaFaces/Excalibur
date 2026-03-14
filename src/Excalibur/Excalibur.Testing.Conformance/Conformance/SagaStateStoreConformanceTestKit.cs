// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


#pragma warning disable IDE0270 // Null check can be simplified

using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Models;

using SagaStateModel = Excalibur.Saga.Models.SagaState;

namespace Excalibur.Testing.Conformance;

/// <summary>
/// Abstract base class for ISagaStateStore conformance testing.
/// </summary>
/// <remarks>
/// <para>
/// Inherit from this class and implement <see cref="CreateStore"/> to verify that
/// your saga state store implementation conforms to the ISagaStateStore contract.
/// </para>
/// <para>
/// The test kit verifies core saga state operations including save, get, update,
/// delete, and isolation behavior.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class SqlServerSagaStateStoreConformanceTests : SagaStateStoreConformanceTestKit
/// {
///     protected override ISagaStateStore CreateStore() =&gt;
///         new SqlServerSagaStateStore(_connectionString);
///
///     protected override async Task CleanupAsync() =&gt;
///         await _fixture.CleanupAsync();
/// }
/// </code>
/// </example>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores",
	Justification = "Test method naming convention")]
public abstract class SagaStateStoreConformanceTestKit
{
	/// <summary>
	/// Creates a fresh saga state store instance for testing.
	/// </summary>
	/// <returns>An ISagaStateStore implementation to test.</returns>
	protected abstract ISagaStateStore CreateStore();

	/// <summary>
	/// Optional cleanup after each test.
	/// </summary>
	/// <returns>A task representing the cleanup operation.</returns>
	protected virtual Task CleanupAsync() => Task.CompletedTask;

	/// <summary>
	/// Generates a unique saga ID for test isolation.
	/// </summary>
	/// <returns>A unique saga identifier string.</returns>
	protected virtual string GenerateSagaId() => Guid.NewGuid().ToString();

	/// <summary>
	/// Creates a test saga state with the given ID.
	/// </summary>
	/// <param name="sagaId">The saga identifier.</param>
	/// <returns>A populated saga state for testing.</returns>
	protected virtual SagaStateModel CreateTestState(string sagaId) =>
		new()
		{
			SagaId = sagaId,
			SagaName = "TestSaga",
			Version = "1.0",
			CorrelationId = Guid.NewGuid().ToString(),
			Status = SagaStatus.Created,
			CurrentStepIndex = 0,
			DataJson = "{\"key\":\"value\"}",
			DataType = "TestData",
			StartedAt = DateTime.UtcNow,
			LastUpdatedAt = DateTime.UtcNow
		};

	#region Save Tests

	/// <summary>
	/// Verifies that saving a new saga state succeeds.
	/// </summary>
	public virtual async Task SaveStateAsync_NewState_ShouldSucceed()
	{
		var store = CreateStore();
		var sagaId = GenerateSagaId();
		var state = CreateTestState(sagaId);

		await store.SaveStateAsync(state, CancellationToken.None).ConfigureAwait(false);

		var loaded = await store.GetStateAsync(sagaId, CancellationToken.None)
			.ConfigureAwait(false);

		if (loaded is null)
		{
			throw new TestFixtureAssertionException("Expected saga state but got null after save");
		}

		if (loaded.SagaId != sagaId)
		{
			throw new TestFixtureAssertionException(
				$"SagaId mismatch: expected '{sagaId}', got '{loaded.SagaId}'");
		}

		if (loaded.SagaName != "TestSaga")
		{
			throw new TestFixtureAssertionException(
				$"SagaName mismatch: expected 'TestSaga', got '{loaded.SagaName}'");
		}
	}

	/// <summary>
	/// Verifies that saving preserves the saga status.
	/// </summary>
	public virtual async Task SaveStateAsync_ShouldPreserveStatus()
	{
		var store = CreateStore();
		var sagaId = GenerateSagaId();
		var state = CreateTestState(sagaId);
		state.Status = SagaStatus.Running;

		await store.SaveStateAsync(state, CancellationToken.None).ConfigureAwait(false);

		var loaded = await store.GetStateAsync(sagaId, CancellationToken.None)
			.ConfigureAwait(false);

		if (loaded is null)
		{
			throw new TestFixtureAssertionException("Expected saga state but got null");
		}

		if (loaded.Status != SagaStatus.Running)
		{
			throw new TestFixtureAssertionException(
				$"Status mismatch: expected Running, got {loaded.Status}");
		}
	}

	/// <summary>
	/// Verifies that saving preserves the correlation ID and data.
	/// </summary>
	public virtual async Task SaveStateAsync_ShouldPreserveCorrelationAndData()
	{
		var store = CreateStore();
		var sagaId = GenerateSagaId();
		var state = CreateTestState(sagaId);
		var correlationId = Guid.NewGuid().ToString();
		state.CorrelationId = correlationId;
		state.DataJson = "{\"order\":123}";
		state.DataType = "OrderData";
		state.CurrentStepIndex = 3;

		await store.SaveStateAsync(state, CancellationToken.None).ConfigureAwait(false);

		var loaded = await store.GetStateAsync(sagaId, CancellationToken.None)
			.ConfigureAwait(false);

		if (loaded is null)
		{
			throw new TestFixtureAssertionException("Expected saga state but got null");
		}

		if (loaded.CorrelationId != correlationId)
		{
			throw new TestFixtureAssertionException(
				$"CorrelationId mismatch: expected '{correlationId}', got '{loaded.CorrelationId}'");
		}

		if (loaded.DataJson != "{\"order\":123}")
		{
			throw new TestFixtureAssertionException(
				$"DataJson mismatch: expected '{{\"order\":123}}', got '{loaded.DataJson}'");
		}

		if (loaded.CurrentStepIndex != 3)
		{
			throw new TestFixtureAssertionException(
				$"CurrentStepIndex mismatch: expected 3, got {loaded.CurrentStepIndex}");
		}
	}

	#endregion

	#region Get Tests

	/// <summary>
	/// Verifies that getting a non-existent saga returns null.
	/// </summary>
	public virtual async Task GetStateAsync_NonExistent_ShouldReturnNull()
	{
		var store = CreateStore();
		var sagaId = GenerateSagaId();

		var loaded = await store.GetStateAsync(sagaId, CancellationToken.None)
			.ConfigureAwait(false);

		if (loaded is not null)
		{
			throw new TestFixtureAssertionException(
				$"Expected null for non-existent saga but got state with status {loaded.Status}");
		}
	}

	/// <summary>
	/// Verifies that getting an existing saga returns its full state.
	/// </summary>
	public virtual async Task GetStateAsync_Existing_ShouldReturnState()
	{
		var store = CreateStore();
		var sagaId = GenerateSagaId();
		var state = CreateTestState(sagaId);

		await store.SaveStateAsync(state, CancellationToken.None).ConfigureAwait(false);

		var loaded = await store.GetStateAsync(sagaId, CancellationToken.None)
			.ConfigureAwait(false);

		if (loaded is null)
		{
			throw new TestFixtureAssertionException("Expected saga state but got null");
		}

		if (loaded.SagaId != sagaId)
		{
			throw new TestFixtureAssertionException(
				$"SagaId mismatch: expected '{sagaId}', got '{loaded.SagaId}'");
		}
	}

	#endregion

	#region Update Tests

	/// <summary>
	/// Verifies that updating an existing saga returns true.
	/// </summary>
	public virtual async Task UpdateStateAsync_Existing_ShouldReturnTrue()
	{
		var store = CreateStore();
		var sagaId = GenerateSagaId();
		var state = CreateTestState(sagaId);

		await store.SaveStateAsync(state, CancellationToken.None).ConfigureAwait(false);

		state.Status = SagaStatus.Running;
		state.CurrentStepIndex = 1;
		state.LastUpdatedAt = DateTime.UtcNow;

		var result = await store.UpdateStateAsync(state, CancellationToken.None)
			.ConfigureAwait(false);

		if (!result)
		{
			throw new TestFixtureAssertionException(
				"Expected UpdateStateAsync to return true for existing saga");
		}
	}

	/// <summary>
	/// Verifies that updating a non-existent saga returns false.
	/// </summary>
	public virtual async Task UpdateStateAsync_NonExistent_ShouldReturnFalse()
	{
		var store = CreateStore();
		var sagaId = GenerateSagaId();
		var state = CreateTestState(sagaId);

		var result = await store.UpdateStateAsync(state, CancellationToken.None)
			.ConfigureAwait(false);

		if (result)
		{
			throw new TestFixtureAssertionException(
				"Expected UpdateStateAsync to return false for non-existent saga");
		}
	}

	/// <summary>
	/// Verifies that updates are persisted correctly.
	/// </summary>
	public virtual async Task UpdateStateAsync_ShouldPersistChanges()
	{
		var store = CreateStore();
		var sagaId = GenerateSagaId();
		var state = CreateTestState(sagaId);

		await store.SaveStateAsync(state, CancellationToken.None).ConfigureAwait(false);

		state.Status = SagaStatus.Completed;
		state.CurrentStepIndex = 5;
		state.CompletedAt = DateTime.UtcNow;
		state.ErrorMessage = null;

		await store.UpdateStateAsync(state, CancellationToken.None).ConfigureAwait(false);

		var loaded = await store.GetStateAsync(sagaId, CancellationToken.None)
			.ConfigureAwait(false);

		if (loaded is null)
		{
			throw new TestFixtureAssertionException("Expected saga state but got null after update");
		}

		if (loaded.Status != SagaStatus.Completed)
		{
			throw new TestFixtureAssertionException(
				$"Status mismatch: expected Completed, got {loaded.Status}");
		}

		if (loaded.CurrentStepIndex != 5)
		{
			throw new TestFixtureAssertionException(
				$"CurrentStepIndex mismatch: expected 5, got {loaded.CurrentStepIndex}");
		}

		if (loaded.CompletedAt is null)
		{
			throw new TestFixtureAssertionException("Expected CompletedAt to be set after update");
		}
	}

	/// <summary>
	/// Verifies that updating with an error message persists it.
	/// </summary>
	public virtual async Task UpdateStateAsync_WithError_ShouldPersistErrorMessage()
	{
		var store = CreateStore();
		var sagaId = GenerateSagaId();
		var state = CreateTestState(sagaId);

		await store.SaveStateAsync(state, CancellationToken.None).ConfigureAwait(false);

		state.Status = SagaStatus.Failed;
		state.ErrorMessage = "Step 2 failed: timeout";
		state.LastUpdatedAt = DateTime.UtcNow;

		await store.UpdateStateAsync(state, CancellationToken.None).ConfigureAwait(false);

		var loaded = await store.GetStateAsync(sagaId, CancellationToken.None)
			.ConfigureAwait(false);

		if (loaded is null)
		{
			throw new TestFixtureAssertionException("Expected saga state but got null");
		}

		if (loaded.ErrorMessage != "Step 2 failed: timeout")
		{
			throw new TestFixtureAssertionException(
				$"ErrorMessage mismatch: expected 'Step 2 failed: timeout', got '{loaded.ErrorMessage}'");
		}
	}

	#endregion

	#region Delete Tests

	/// <summary>
	/// Verifies that deleting an existing saga returns true.
	/// </summary>
	public virtual async Task DeleteStateAsync_Existing_ShouldReturnTrue()
	{
		var store = CreateStore();
		var sagaId = GenerateSagaId();
		var state = CreateTestState(sagaId);

		await store.SaveStateAsync(state, CancellationToken.None).ConfigureAwait(false);

		var result = await store.DeleteStateAsync(sagaId, CancellationToken.None)
			.ConfigureAwait(false);

		if (!result)
		{
			throw new TestFixtureAssertionException(
				"Expected DeleteStateAsync to return true for existing saga");
		}
	}

	/// <summary>
	/// Verifies that deleting a non-existent saga returns false.
	/// </summary>
	public virtual async Task DeleteStateAsync_NonExistent_ShouldReturnFalse()
	{
		var store = CreateStore();
		var sagaId = GenerateSagaId();

		var result = await store.DeleteStateAsync(sagaId, CancellationToken.None)
			.ConfigureAwait(false);

		if (result)
		{
			throw new TestFixtureAssertionException(
				"Expected DeleteStateAsync to return false for non-existent saga");
		}
	}

	/// <summary>
	/// Verifies that a deleted saga can no longer be retrieved.
	/// </summary>
	public virtual async Task DeleteStateAsync_ShouldRemoveState()
	{
		var store = CreateStore();
		var sagaId = GenerateSagaId();
		var state = CreateTestState(sagaId);

		await store.SaveStateAsync(state, CancellationToken.None).ConfigureAwait(false);
		await store.DeleteStateAsync(sagaId, CancellationToken.None).ConfigureAwait(false);

		var loaded = await store.GetStateAsync(sagaId, CancellationToken.None)
			.ConfigureAwait(false);

		if (loaded is not null)
		{
			throw new TestFixtureAssertionException(
				"Expected null after delete but got saga state");
		}
	}

	#endregion

	#region Isolation Tests

	/// <summary>
	/// Verifies that saga states are isolated by saga ID.
	/// </summary>
	public virtual async Task States_ShouldIsolateBySagaId()
	{
		var store = CreateStore();
		var sagaId1 = GenerateSagaId();
		var sagaId2 = GenerateSagaId();

		var state1 = CreateTestState(sagaId1);
		state1.SagaName = "Saga1";
		var state2 = CreateTestState(sagaId2);
		state2.SagaName = "Saga2";

		await store.SaveStateAsync(state1, CancellationToken.None).ConfigureAwait(false);
		await store.SaveStateAsync(state2, CancellationToken.None).ConfigureAwait(false);

		var loaded1 = await store.GetStateAsync(sagaId1, CancellationToken.None)
			.ConfigureAwait(false);
		var loaded2 = await store.GetStateAsync(sagaId2, CancellationToken.None)
			.ConfigureAwait(false);

		if (loaded1 is null || loaded1.SagaName != "Saga1")
		{
			throw new TestFixtureAssertionException(
				$"Expected saga1 name 'Saga1' but got '{loaded1?.SagaName}'");
		}

		if (loaded2 is null || loaded2.SagaName != "Saga2")
		{
			throw new TestFixtureAssertionException(
				$"Expected saga2 name 'Saga2' but got '{loaded2?.SagaName}'");
		}
	}

	/// <summary>
	/// Verifies that deleting one saga does not affect others.
	/// </summary>
	public virtual async Task DeleteOne_ShouldNotAffectOthers()
	{
		var store = CreateStore();
		var sagaId1 = GenerateSagaId();
		var sagaId2 = GenerateSagaId();

		var state1 = CreateTestState(sagaId1);
		var state2 = CreateTestState(sagaId2);

		await store.SaveStateAsync(state1, CancellationToken.None).ConfigureAwait(false);
		await store.SaveStateAsync(state2, CancellationToken.None).ConfigureAwait(false);

		await store.DeleteStateAsync(sagaId1, CancellationToken.None).ConfigureAwait(false);

		var loaded1 = await store.GetStateAsync(sagaId1, CancellationToken.None)
			.ConfigureAwait(false);
		var loaded2 = await store.GetStateAsync(sagaId2, CancellationToken.None)
			.ConfigureAwait(false);

		if (loaded1 is not null)
		{
			throw new TestFixtureAssertionException(
				"Expected deleted saga1 to be null");
		}

		if (loaded2 is null)
		{
			throw new TestFixtureAssertionException(
				"Expected saga2 to still exist after deleting saga1");
		}
	}

	#endregion
}
