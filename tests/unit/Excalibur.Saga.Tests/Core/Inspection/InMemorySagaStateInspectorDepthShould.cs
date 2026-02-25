// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Inspection;
using Excalibur.Saga.Models;

using SagaStateModel = Excalibur.Saga.Models.SagaState;

namespace Excalibur.Saga.Tests.Core.Inspection;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InMemorySagaStateInspectorDepthShould
{
	private readonly ISagaStateStore _stateStore = A.Fake<ISagaStateStore>();
	private readonly InMemorySagaStateInspector _sut;

	public InMemorySagaStateInspectorDepthShould()
	{
		_sut = new InMemorySagaStateInspector(_stateStore);
	}

	[Fact]
	public void ThrowWhenStateStoreIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new InMemorySagaStateInspector(null!));
	}

	[Fact]
	public async Task GetStateAsyncDelegatesToStore()
	{
		// Arrange
		var sagaState = new SagaStateModel { SagaName = "TestSaga", Status = SagaStatus.Running };
		A.CallTo(() => _stateStore.GetStateAsync("saga-1", A<CancellationToken>._))
			.Returns(Task.FromResult<SagaStateModel?>(sagaState));

		// Act
		var result = await _sut.GetStateAsync("saga-1", CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
		result.SagaName.ShouldBe("TestSaga");
		result.Status.ShouldBe(SagaStatus.Running);
	}

	[Fact]
	public async Task GetStateAsyncReturnsNullWhenNotFound()
	{
		// Arrange
		A.CallTo(() => _stateStore.GetStateAsync("unknown", A<CancellationToken>._))
			.Returns(Task.FromResult<SagaStateModel?>(null));

		// Act
		var result = await _sut.GetStateAsync("unknown", CancellationToken.None);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task GetStateAsyncThrowsWhenSagaIdIsNull()
	{
		await Should.ThrowAsync<ArgumentException>(async () =>
			await _sut.GetStateAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task GetStateAsyncThrowsWhenSagaIdIsEmpty()
	{
		await Should.ThrowAsync<ArgumentException>(async () =>
			await _sut.GetStateAsync("", CancellationToken.None));
	}

	[Fact]
	public async Task GetHistoryAsyncReturnsStepHistory()
	{
		// Arrange
		var sagaState = new SagaStateModel
		{
			SagaName = "TestSaga",
			Status = SagaStatus.Completed,
		};
		sagaState.StepHistory.Add(new StepExecutionRecord
		{
			StepName = "Step1",
			StartedAt = DateTime.UtcNow.AddMinutes(-5),
			CompletedAt = DateTime.UtcNow.AddMinutes(-4),
		});
		sagaState.StepHistory.Add(new StepExecutionRecord
		{
			StepName = "Step2",
			StartedAt = DateTime.UtcNow.AddMinutes(-3),
			CompletedAt = DateTime.UtcNow.AddMinutes(-2),
		});

		A.CallTo(() => _stateStore.GetStateAsync("saga-1", A<CancellationToken>._))
			.Returns(Task.FromResult<SagaStateModel?>(sagaState));

		// Act
		var result = await _sut.GetHistoryAsync("saga-1", CancellationToken.None);

		// Assert
		result.Count.ShouldBe(2);
		result[0].StepName.ShouldBe("Step1");
		result[1].StepName.ShouldBe("Step2");
	}

	[Fact]
	public async Task GetHistoryAsyncReturnsEmptyWhenSagaNotFound()
	{
		// Arrange
		A.CallTo(() => _stateStore.GetStateAsync("unknown", A<CancellationToken>._))
			.Returns(Task.FromResult<SagaStateModel?>(null));

		// Act
		var result = await _sut.GetHistoryAsync("unknown", CancellationToken.None);

		// Assert
		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task GetHistoryAsyncReturnsEmptyWhenNoStepHistory()
	{
		// Arrange
		var sagaState = new SagaStateModel { SagaName = "TestSaga", Status = SagaStatus.Running };

		A.CallTo(() => _stateStore.GetStateAsync("saga-1", A<CancellationToken>._))
			.Returns(Task.FromResult<SagaStateModel?>(sagaState));

		// Act
		var result = await _sut.GetHistoryAsync("saga-1", CancellationToken.None);

		// Assert
		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task GetHistoryAsyncThrowsWhenSagaIdIsNull()
	{
		await Should.ThrowAsync<ArgumentException>(async () =>
			await _sut.GetHistoryAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task GetHistoryAsyncThrowsWhenSagaIdIsEmpty()
	{
		await Should.ThrowAsync<ArgumentException>(async () =>
			await _sut.GetHistoryAsync("", CancellationToken.None));
	}

	[Fact]
	public async Task GetActiveStepAsyncReturnsActiveStep()
	{
		// Arrange
		var sagaState = new SagaStateModel
		{
			SagaName = "TestSaga",
			Status = SagaStatus.Running,
		};
		sagaState.StepHistory.Add(new StepExecutionRecord
		{
			StepName = "Step1",
			StartedAt = DateTime.UtcNow.AddMinutes(-5),
			CompletedAt = DateTime.UtcNow.AddMinutes(-4),
		});
		sagaState.StepHistory.Add(new StepExecutionRecord
		{
			StepName = "Step2",
			StartedAt = DateTime.UtcNow.AddMinutes(-3),
			CompletedAt = null, // Active - not completed
		});

		A.CallTo(() => _stateStore.GetStateAsync("saga-1", A<CancellationToken>._))
			.Returns(Task.FromResult<SagaStateModel?>(sagaState));

		// Act
		var result = await _sut.GetActiveStepAsync("saga-1", CancellationToken.None);

		// Assert
		result.ShouldBe("Step2");
	}

	[Fact]
	public async Task GetActiveStepAsyncReturnsNullWhenSagaNotFound()
	{
		// Arrange
		A.CallTo(() => _stateStore.GetStateAsync("unknown", A<CancellationToken>._))
			.Returns(Task.FromResult<SagaStateModel?>(null));

		// Act
		var result = await _sut.GetActiveStepAsync("unknown", CancellationToken.None);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task GetActiveStepAsyncReturnsNullWhenSagaIsNotRunning()
	{
		// Arrange
		var sagaState = new SagaStateModel
		{
			SagaName = "TestSaga",
			Status = SagaStatus.Completed,
		};
		sagaState.StepHistory.Add(new StepExecutionRecord
		{
			StepName = "Step1",
			StartedAt = DateTime.UtcNow.AddMinutes(-5),
			CompletedAt = null, // Not completed, but saga is in Completed status
		});

		A.CallTo(() => _stateStore.GetStateAsync("saga-1", A<CancellationToken>._))
			.Returns(Task.FromResult<SagaStateModel?>(sagaState));

		// Act
		var result = await _sut.GetActiveStepAsync("saga-1", CancellationToken.None);

		// Assert - should be null because saga is not Running
		result.ShouldBeNull();
	}

	[Fact]
	public async Task GetActiveStepAsyncReturnsNullWhenAllStepsCompleted()
	{
		// Arrange
		var sagaState = new SagaStateModel
		{
			SagaName = "TestSaga",
			Status = SagaStatus.Running,
		};
		sagaState.StepHistory.Add(new StepExecutionRecord
		{
			StepName = "Step1",
			StartedAt = DateTime.UtcNow.AddMinutes(-5),
			CompletedAt = DateTime.UtcNow.AddMinutes(-4),
		});

		A.CallTo(() => _stateStore.GetStateAsync("saga-1", A<CancellationToken>._))
			.Returns(Task.FromResult<SagaStateModel?>(sagaState));

		// Act
		var result = await _sut.GetActiveStepAsync("saga-1", CancellationToken.None);

		// Assert - all steps completed, no active step
		result.ShouldBeNull();
	}

	[Fact]
	public async Task GetActiveStepAsyncThrowsWhenSagaIdIsNull()
	{
		await Should.ThrowAsync<ArgumentException>(async () =>
			await _sut.GetActiveStepAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task GetActiveStepAsyncThrowsWhenSagaIdIsEmpty()
	{
		await Should.ThrowAsync<ArgumentException>(async () =>
			await _sut.GetActiveStepAsync("", CancellationToken.None));
	}
}
