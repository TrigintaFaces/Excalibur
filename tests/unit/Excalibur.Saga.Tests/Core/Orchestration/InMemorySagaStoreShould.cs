// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Saga.Orchestration;

namespace Excalibur.Saga.Tests.Core.Orchestration;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InMemorySagaStoreShould
{
	private readonly InMemorySagaStore _sut = new();

	[Fact]
	public async Task SaveAsync_PersistState()
	{
		// Arrange
		var state = new TestSagaState { SagaId = Guid.NewGuid() };

		// Act
		await _sut.SaveAsync(state, CancellationToken.None);

		// Assert
		var loaded = await _sut.LoadAsync<TestSagaState>(state.SagaId, CancellationToken.None);
		loaded.ShouldNotBeNull();
		loaded.SagaId.ShouldBe(state.SagaId);
	}

	[Fact]
	public async Task LoadAsync_ReturnNull_WhenNotFound()
	{
		// Act
		var result = await _sut.LoadAsync<TestSagaState>(Guid.NewGuid(), CancellationToken.None);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task SaveAsync_OverwriteExistingState()
	{
		// Arrange
		var sagaId = Guid.NewGuid();
		var state1 = new TestSagaState { SagaId = sagaId, Completed = false };
		var state2 = new TestSagaState { SagaId = sagaId, Completed = true };

		// Act
		await _sut.SaveAsync(state1, CancellationToken.None);
		await _sut.SaveAsync(state2, CancellationToken.None);

		// Assert
		var loaded = await _sut.LoadAsync<TestSagaState>(sagaId, CancellationToken.None);
		loaded.ShouldNotBeNull();
		loaded.Completed.ShouldBeTrue();
	}

	[Fact]
	public async Task SaveAsync_ThrowOnNull()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.SaveAsync<TestSagaState>(null!, CancellationToken.None));
	}

	[Fact]
	public async Task SaveAndLoad_MultipleDistinctSagas()
	{
		// Arrange
		var state1 = new TestSagaState { SagaId = Guid.NewGuid() };
		var state2 = new TestSagaState { SagaId = Guid.NewGuid() };

		// Act
		await _sut.SaveAsync(state1, CancellationToken.None);
		await _sut.SaveAsync(state2, CancellationToken.None);

		// Assert
		var loaded1 = await _sut.LoadAsync<TestSagaState>(state1.SagaId, CancellationToken.None);
		var loaded2 = await _sut.LoadAsync<TestSagaState>(state2.SagaId, CancellationToken.None);
		loaded1.ShouldNotBeNull();
		loaded2.ShouldNotBeNull();
		loaded1.SagaId.ShouldBe(state1.SagaId);
		loaded2.SagaId.ShouldBe(state2.SagaId);
	}

	[Fact]
	public async Task LoadAsync_ReturnCorrectType()
	{
		// Arrange
		var state = new TestSagaState { SagaId = Guid.NewGuid() };
		await _sut.SaveAsync(state, CancellationToken.None);

		// Act
		var loaded = await _sut.LoadAsync<TestSagaState>(state.SagaId, CancellationToken.None);

		// Assert
		loaded.ShouldBeOfType<TestSagaState>();
	}

#pragma warning disable CA1034
	public sealed class TestSagaState : SagaState
	{
		public string OrderId { get; set; } = string.Empty;
	}
#pragma warning restore CA1034
}
