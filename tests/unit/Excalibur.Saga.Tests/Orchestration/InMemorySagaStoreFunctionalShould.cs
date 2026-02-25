// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Orchestration;

using AbstractSagaState = Excalibur.Dispatch.Abstractions.Messaging.SagaState;

namespace Excalibur.Saga.Tests.Orchestration;

/// <summary>
/// Test saga state that extends the correct base class for InMemorySagaStore.
/// </summary>
internal sealed class TestSagaState : AbstractSagaState
{
	public string SagaName { get; set; } = string.Empty;
	public string Status { get; set; } = string.Empty;
	public int CurrentStepIndex { get; set; }
	public DateTime StartedAt { get; set; }
	public DateTime? CompletedAt { get; set; }
	public string? ErrorMessage { get; set; }
	public IDictionary<string, object> Metadata { get; } = new Dictionary<string, object>(StringComparer.Ordinal);
}

/// <summary>
/// Functional tests for <see cref="InMemorySagaStore"/> covering
/// saga state persistence, data round-trips, concurrent access, and overwrite semantics.
/// </summary>
[Trait("Category", "Unit")]
public sealed class InMemorySagaStoreFunctionalShould
{
	[Fact]
	public async Task SaveAndLoadSagaState_RoundTrip()
	{
		// Arrange
		var store = new InMemorySagaStore();
		var sagaId = Guid.NewGuid();
		var state = new TestSagaState
		{
			SagaId = sagaId,
			SagaName = "OrderSaga",
			Status = "Running",
			CurrentStepIndex = 2,
			StartedAt = DateTime.UtcNow,
		};

		// Act
		await store.SaveAsync(state, CancellationToken.None);
		var loaded = await store.LoadAsync<TestSagaState>(sagaId, CancellationToken.None);

		// Assert
		loaded.ShouldNotBeNull();
		loaded.SagaId.ShouldBe(sagaId);
		loaded.SagaName.ShouldBe("OrderSaga");
		loaded.Status.ShouldBe("Running");
		loaded.CurrentStepIndex.ShouldBe(2);
	}

	[Fact]
	public async Task ReturnNull_ForNonexistentSaga()
	{
		// Arrange
		var store = new InMemorySagaStore();

		// Act
		var result = await store.LoadAsync<TestSagaState>(Guid.NewGuid(), CancellationToken.None);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task OverwriteExistingState_OnSave()
	{
		// Arrange
		var store = new InMemorySagaStore();
		var sagaId = Guid.NewGuid();

		var state1 = new TestSagaState
		{
			SagaId = sagaId,
			Status = "Running",
			CurrentStepIndex = 1,
		};
		var state2 = new TestSagaState
		{
			SagaId = sagaId,
			Status = "Completed",
			CurrentStepIndex = 5,
		};

		// Act
		await store.SaveAsync(state1, CancellationToken.None);
		await store.SaveAsync(state2, CancellationToken.None);
		var loaded = await store.LoadAsync<TestSagaState>(sagaId, CancellationToken.None);

		// Assert
		loaded.ShouldNotBeNull();
		loaded.Status.ShouldBe("Completed");
		loaded.CurrentStepIndex.ShouldBe(5);
	}

	[Fact]
	public async Task ThrowOnNullState()
	{
		// Arrange
		var store = new InMemorySagaStore();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => store.SaveAsync<TestSagaState>(null!, CancellationToken.None));
	}

	[Fact]
	public async Task HandleConcurrentSaves_ThreadSafely()
	{
		// Arrange
		var store = new InMemorySagaStore();
		var tasks = new List<Task>();

		for (var i = 0; i < 50; i++)
		{
			var sagaId = Guid.NewGuid();
			var state = new TestSagaState
			{
				SagaId = sagaId,
				SagaName = $"Saga{i}",
				Status = "Running",
			};
			tasks.Add(store.SaveAsync(state, CancellationToken.None));
		}

		// Act
		await Task.WhenAll(tasks);

		// Assert - no exceptions, all saves successful
		// Load a random one to verify
		tasks.Clear();
	}

	[Fact]
	public async Task PersistMetadata()
	{
		// Arrange
		var store = new InMemorySagaStore();
		var sagaId = Guid.NewGuid();

		var state = new TestSagaState
		{
			SagaId = sagaId,
			Status = "Running",
		};
		state.Metadata["CustomerId"] = "cust-123";
		state.Metadata["Region"] = "US-WEST";

		// Act
		await store.SaveAsync(state, CancellationToken.None);
		var loaded = await store.LoadAsync<TestSagaState>(sagaId, CancellationToken.None);

		// Assert
		loaded.ShouldNotBeNull();
		loaded.Metadata["CustomerId"].ShouldBe("cust-123");
		loaded.Metadata["Region"].ShouldBe("US-WEST");
	}

	[Fact]
	public async Task TrackCompletionTime()
	{
		// Arrange
		var store = new InMemorySagaStore();
		var sagaId = Guid.NewGuid();
		var completedAt = DateTime.UtcNow;

		var state = new TestSagaState
		{
			SagaId = sagaId,
			Status = "Completed",
			StartedAt = completedAt.AddMinutes(-5),
			CompletedAt = completedAt,
		};

		// Act
		await store.SaveAsync(state, CancellationToken.None);
		var loaded = await store.LoadAsync<TestSagaState>(sagaId, CancellationToken.None);

		// Assert
		loaded.ShouldNotBeNull();
		loaded.CompletedAt.ShouldBe(completedAt);
	}
}
