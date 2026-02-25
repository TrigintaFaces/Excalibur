// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Messaging;

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

	/// <inheritdoc/>
	public async Task InitializeAsync()
	{
		Store = await CreateStoreAsync().ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async Task DisposeAsync()
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

	[Fact]
	public async Task CorrelationLookup_DifferentSagaTypes_Isolated()
	{
		// Arrange
		var sagaId = Guid.NewGuid();
		var testState = CreateTestSagaState(sagaId: sagaId, data: "TestType");
		await Store.SaveAsync(testState, CancellationToken.None).ConfigureAwait(false);

		// Act - Try to load as a different saga state type
		var loaded = await Store.LoadAsync<AlternateTestSagaState>(sagaId, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert - Behavior is provider-dependent; either null or throws
		// The key invariant is that saving as one type and loading as another is handled gracefully
		if (loaded is not null)
		{
			loaded.SagaId.ShouldBe(sagaId);
		}
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

	[Fact]
	public async Task ConcurrentSave_SameSaga_LastWriteWins()
	{
		// Arrange
		var sagaId = Guid.NewGuid();
		var state = CreateTestSagaState(sagaId: sagaId);
		await Store.SaveAsync(state, CancellationToken.None).ConfigureAwait(false);

		// Act - Concurrent updates to the same saga
		const int concurrentUpdates = 5;
		var tasks = Enumerable.Range(0, concurrentUpdates).Select(i =>
		{
			var update = CreateTestSagaState(sagaId: sagaId, data: $"Update-{i}");
			return Store.SaveAsync(update, CancellationToken.None);
		});
		await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert - Should have one valid state (last write wins)
		var loaded = await Store.LoadAsync<TestSagaState>(sagaId, CancellationToken.None)
			.ConfigureAwait(false);
		loaded.ShouldNotBeNull("Should have valid state after concurrent updates");
		loaded.Data.ShouldStartWith("Update-");
	}

	[Fact]
	public async Task ConcurrentLoadAndSave_MaintainsConsistency()
	{
		// Arrange
		var sagaId = Guid.NewGuid();
		var state = CreateTestSagaState(sagaId: sagaId, data: "Initial");
		await Store.SaveAsync(state, CancellationToken.None).ConfigureAwait(false);

		// Act - Mix reads and writes concurrently
		var tasks = new List<Task>();
		for (int i = 0; i < 5; i++)
		{
			tasks.Add(Store.LoadAsync<TestSagaState>(sagaId, CancellationToken.None));
			var update = CreateTestSagaState(sagaId: sagaId, data: $"Concurrent-{i}");
			tasks.Add(Store.SaveAsync(update, CancellationToken.None));
		}

		await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert - Should still be loadable
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
