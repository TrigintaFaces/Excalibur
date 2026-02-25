// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Messaging;

using Excalibur.Saga.Orchestration;

namespace Excalibur.Saga.Tests.Orchestration;

/// <summary>
/// Unit tests for <see cref="InMemorySagaStore"/>.
/// Verifies in-memory saga state storage operations including load, save, and thread safety.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class InMemorySagaStoreShould
{
	private readonly InMemorySagaStore _sut = new();

	#region LoadAsync Tests

	[Fact]
	public async Task ReturnNull_WhenSagaDoesNotExist()
	{
		// Arrange
		var nonExistentId = Guid.NewGuid();

		// Act
		var result = await _sut.LoadAsync<TestSagaState>(nonExistentId, CancellationToken.None);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task ReturnSavedState_WhenSagaExists()
	{
		// Arrange
		var sagaId = Guid.NewGuid();
		var state = new TestSagaState { SagaId = sagaId, Value = "test-value" };
		await _sut.SaveAsync(state, CancellationToken.None);

		// Act
		var result = await _sut.LoadAsync<TestSagaState>(sagaId, CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
		result.SagaId.ShouldBe(sagaId);
		result.Value.ShouldBe("test-value");
	}

	[Fact]
	public async Task ReturnLatestState_AfterMultipleSaves()
	{
		// Arrange
		var sagaId = Guid.NewGuid();
		var state1 = new TestSagaState { SagaId = sagaId, Value = "first" };
		var state2 = new TestSagaState { SagaId = sagaId, Value = "second" };
		var state3 = new TestSagaState { SagaId = sagaId, Value = "third" };

		await _sut.SaveAsync(state1, CancellationToken.None);
		await _sut.SaveAsync(state2, CancellationToken.None);
		await _sut.SaveAsync(state3, CancellationToken.None);

		// Act
		var result = await _sut.LoadAsync<TestSagaState>(sagaId, CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
		result.Value.ShouldBe("third");
	}

	[Fact]
	public async Task IsolateDifferentSagas()
	{
		// Arrange
		var sagaId1 = Guid.NewGuid();
		var sagaId2 = Guid.NewGuid();
		var state1 = new TestSagaState { SagaId = sagaId1, Value = "saga-1" };
		var state2 = new TestSagaState { SagaId = sagaId2, Value = "saga-2" };

		await _sut.SaveAsync(state1, CancellationToken.None);
		await _sut.SaveAsync(state2, CancellationToken.None);

		// Act
		var result1 = await _sut.LoadAsync<TestSagaState>(sagaId1, CancellationToken.None);
		var result2 = await _sut.LoadAsync<TestSagaState>(sagaId2, CancellationToken.None);

		// Assert
		result1.ShouldNotBeNull();
		result1.Value.ShouldBe("saga-1");
		result2.ShouldNotBeNull();
		result2.Value.ShouldBe("saga-2");
	}

	#endregion

	#region SaveAsync Tests

	[Fact]
	public async Task ThrowArgumentNullException_WhenStateIsNull()
	{
		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await _sut.SaveAsync<TestSagaState>(null!, CancellationToken.None));
	}

	[Fact]
	public async Task SaveNewState_Successfully()
	{
		// Arrange
		var sagaId = Guid.NewGuid();
		var state = new TestSagaState { SagaId = sagaId, Value = "new-state" };

		// Act
		await _sut.SaveAsync(state, CancellationToken.None);

		// Assert
		var loaded = await _sut.LoadAsync<TestSagaState>(sagaId, CancellationToken.None);
		loaded.ShouldNotBeNull();
		loaded.Value.ShouldBe("new-state");
	}

	[Fact]
	public async Task OverwriteExistingState()
	{
		// Arrange
		var sagaId = Guid.NewGuid();
		var originalState = new TestSagaState { SagaId = sagaId, Value = "original" };
		var updatedState = new TestSagaState { SagaId = sagaId, Value = "updated" };

		await _sut.SaveAsync(originalState, CancellationToken.None);

		// Act
		await _sut.SaveAsync(updatedState, CancellationToken.None);

		// Assert
		var loaded = await _sut.LoadAsync<TestSagaState>(sagaId, CancellationToken.None);
		loaded.ShouldNotBeNull();
		loaded.Value.ShouldBe("updated");
	}

	[Fact]
	public async Task ReturnCompletedTask()
	{
		// Arrange
		var state = new TestSagaState { SagaId = Guid.NewGuid(), Value = "test" };

		// Act
		var task = _sut.SaveAsync(state, CancellationToken.None);

		// Assert
		task.IsCompleted.ShouldBeTrue();
		await task; // Should complete immediately
	}

	#endregion

	#region Thread Safety Tests

	[Fact]
	public async Task HandleConcurrentSaves_WithoutDataLoss()
	{
		// Arrange
		var tasks = new List<Task>();
		var sagaIds = new List<Guid>();

		for (var i = 0; i < 100; i++)
		{
			var sagaId = Guid.NewGuid();
			sagaIds.Add(sagaId);
			var state = new TestSagaState { SagaId = sagaId, Value = $"value-{i}" };
			tasks.Add(_sut.SaveAsync(state, CancellationToken.None));
		}

		// Act
		await Task.WhenAll(tasks);

		// Assert - All sagas should be retrievable
		foreach (var sagaId in sagaIds)
		{
			var loaded = await _sut.LoadAsync<TestSagaState>(sagaId, CancellationToken.None);
			loaded.ShouldNotBeNull();
		}
	}

	[Fact]
	public async Task HandleConcurrentLoads_WithoutErrors()
	{
		// Arrange
		var sagaId = Guid.NewGuid();
		var state = new TestSagaState { SagaId = sagaId, Value = "concurrent-test" };
		await _sut.SaveAsync(state, CancellationToken.None);

		var tasks = new List<Task<TestSagaState?>>();
		for (var i = 0; i < 100; i++)
		{
			tasks.Add(_sut.LoadAsync<TestSagaState>(sagaId, CancellationToken.None));
		}

		// Act
		var results = await Task.WhenAll(tasks);

		// Assert - All loads should return the same value
		foreach (var result in results)
		{
			result.ShouldNotBeNull();
			result.Value.ShouldBe("concurrent-test");
		}
	}

	#endregion

	#region Test Types

	internal sealed class TestSagaState : SagaState
	{
		public string? Value { get; set; }
	}

	#endregion
}
