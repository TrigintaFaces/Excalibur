// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Inspection;
using Excalibur.Saga.Models;

using SagaStateModel = Excalibur.Saga.Models.SagaState;

namespace Excalibur.Saga.Tests.Inspection;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InMemorySagaStateInspectorShould
{
	private readonly ISagaStateStore _stateStore = A.Fake<ISagaStateStore>();

	[Fact]
	public void ThrowWhenStateStoreIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new InMemorySagaStateInspector(null!));
	}

	[Fact]
	public async Task ReturnNullWhenSagaNotFound()
	{
		// Arrange
		A.CallTo(() => _stateStore.GetStateAsync("unknown", CancellationToken.None))
			.Returns(Task.FromResult<SagaStateModel?>(null));
		var sut = new InMemorySagaStateInspector(_stateStore);

		// Act
		var result = await sut.GetStateAsync("unknown", CancellationToken.None);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task ReturnStateFromStore()
	{
		// Arrange
		var state = new SagaStateModel { SagaId = "saga-1", Status = SagaStatus.Running };
		A.CallTo(() => _stateStore.GetStateAsync("saga-1", CancellationToken.None))
			.Returns(Task.FromResult<SagaStateModel?>(state));
		var sut = new InMemorySagaStateInspector(_stateStore);

		// Act
		var result = await sut.GetStateAsync("saga-1", CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
		result.SagaId.ShouldBe("saga-1");
	}

	[Fact]
	public async Task ReturnEmptyHistoryWhenSagaNotFound()
	{
		// Arrange
		A.CallTo(() => _stateStore.GetStateAsync("unknown", CancellationToken.None))
			.Returns(Task.FromResult<SagaStateModel?>(null));
		var sut = new InMemorySagaStateInspector(_stateStore);

		// Act
		var history = await sut.GetHistoryAsync("unknown", CancellationToken.None);

		// Assert
		history.ShouldBeEmpty();
	}

	[Fact]
	public async Task ReturnHistoryFromState()
	{
		// Arrange
		var state = new SagaStateModel { SagaId = "saga-1", Status = SagaStatus.Running };
		state.StepHistory.Add(new StepExecutionRecord { StepName = "Step1", StartedAt = DateTime.UtcNow });
		A.CallTo(() => _stateStore.GetStateAsync("saga-1", CancellationToken.None))
			.Returns(Task.FromResult<SagaStateModel?>(state));
		var sut = new InMemorySagaStateInspector(_stateStore);

		// Act
		var history = await sut.GetHistoryAsync("saga-1", CancellationToken.None);

		// Assert
		history.Count.ShouldBe(1);
		history[0].StepName.ShouldBe("Step1");
	}

	[Fact]
	public async Task ReturnNullActiveStepWhenSagaNotFound()
	{
		// Arrange
		A.CallTo(() => _stateStore.GetStateAsync("unknown", CancellationToken.None))
			.Returns(Task.FromResult<SagaStateModel?>(null));
		var sut = new InMemorySagaStateInspector(_stateStore);

		// Act
		var activeStep = await sut.GetActiveStepAsync("unknown", CancellationToken.None);

		// Assert
		activeStep.ShouldBeNull();
	}

	[Fact]
	public async Task ReturnNullActiveStepWhenSagaNotRunning()
	{
		// Arrange
		var state = new SagaStateModel { SagaId = "saga-1", Status = SagaStatus.Completed };
		A.CallTo(() => _stateStore.GetStateAsync("saga-1", CancellationToken.None))
			.Returns(Task.FromResult<SagaStateModel?>(state));
		var sut = new InMemorySagaStateInspector(_stateStore);

		// Act
		var activeStep = await sut.GetActiveStepAsync("saga-1", CancellationToken.None);

		// Assert
		activeStep.ShouldBeNull();
	}

	[Fact]
	public async Task ReturnActiveStepNameForRunningSaga()
	{
		// Arrange
		var state = new SagaStateModel { SagaId = "saga-1", Status = SagaStatus.Running };
		state.StepHistory.Add(new StepExecutionRecord
		{
			StepName = "Step1",
			StartedAt = DateTime.UtcNow,
			CompletedAt = DateTime.UtcNow
		});
		state.StepHistory.Add(new StepExecutionRecord
		{
			StepName = "Step2",
			StartedAt = DateTime.UtcNow,
			CompletedAt = null // still active
		});
		A.CallTo(() => _stateStore.GetStateAsync("saga-1", CancellationToken.None))
			.Returns(Task.FromResult<SagaStateModel?>(state));
		var sut = new InMemorySagaStateInspector(_stateStore);

		// Act
		var activeStep = await sut.GetActiveStepAsync("saga-1", CancellationToken.None);

		// Assert
		activeStep.ShouldBe("Step2");
	}

	[Fact]
	public async Task ThrowOnNullSagaIdForGetState()
	{
		// Arrange
		var sut = new InMemorySagaStateInspector(_stateStore);

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(
			() => sut.GetStateAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowOnEmptySagaIdForGetHistory()
	{
		// Arrange
		var sut = new InMemorySagaStateInspector(_stateStore);

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(
			() => sut.GetHistoryAsync("", CancellationToken.None));
	}

	[Fact]
	public void ImplementISagaStateInspector()
	{
		// Arrange & Act
		var sut = new InMemorySagaStateInspector(_stateStore);

		// Assert
		sut.ShouldBeAssignableTo<ISagaStateInspector>();
	}
}
