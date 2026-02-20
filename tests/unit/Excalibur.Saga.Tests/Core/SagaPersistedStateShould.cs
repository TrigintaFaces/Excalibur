// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Abstractions;

namespace Excalibur.Saga.Tests.Core;

/// <summary>
/// Unit tests for <see cref="SagaPersistedState{TSagaData}"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class SagaPersistedStateShould
{
	#region Default Values Tests

	[Fact]
	public void HaveEmptySagaIdByDefault()
	{
		// Arrange & Act
		var state = new SagaPersistedState<TestSagaData>();

		// Assert
		state.SagaId.ShouldBe(string.Empty);
	}

	[Fact]
	public void HaveNullDefinitionByDefault()
	{
		// Arrange & Act
		var state = new SagaPersistedState<TestSagaData>();

		// Assert
		state.Definition.ShouldBeNull();
	}

	[Fact]
	public void HaveNullDataByDefault()
	{
		// Arrange & Act
		var state = new SagaPersistedState<TestSagaData>();

		// Assert
		state.Data.ShouldBeNull();
	}

	[Fact]
	public void HaveDefaultStateByDefault()
	{
		// Arrange & Act
		var state = new SagaPersistedState<TestSagaData>();

		// Assert
		state.State.ShouldBe(default(SagaState));
	}

	[Fact]
	public void HaveZeroCurrentStepIndexByDefault()
	{
		// Arrange & Act
		var state = new SagaPersistedState<TestSagaData>();

		// Assert
		state.CurrentStepIndex.ShouldBe(0);
	}

	[Fact]
	public void HaveEmptyActivitiesListByDefault()
	{
		// Arrange & Act
		var state = new SagaPersistedState<TestSagaData>();

		// Assert
		state.Activities.ShouldNotBeNull();
		state.Activities.ShouldBeEmpty();
	}

	[Fact]
	public void HaveDefaultStartedAtByDefault()
	{
		// Arrange & Act
		var state = new SagaPersistedState<TestSagaData>();

		// Assert
		state.StartedAt.ShouldBe(default(DateTimeOffset));
	}

	[Fact]
	public void HaveNullCompletedAtByDefault()
	{
		// Arrange & Act
		var state = new SagaPersistedState<TestSagaData>();

		// Assert
		state.CompletedAt.ShouldBeNull();
	}

	#endregion Default Values Tests

	#region Property Setting Tests

	[Fact]
	public void AllowSagaIdToBeSet()
	{
		// Arrange & Act
		var state = new SagaPersistedState<TestSagaData> { SagaId = "saga-123" };

		// Assert
		state.SagaId.ShouldBe("saga-123");
	}

	[Fact]
	public void AllowDefinitionToBeSet()
	{
		// Arrange
		var definition = new TestSagaDefinition();

		// Act
		var state = new SagaPersistedState<TestSagaData> { Definition = definition };

		// Assert
		state.Definition.ShouldBe(definition);
	}

	[Fact]
	public void AllowDataToBeSet()
	{
		// Arrange
		var data = new TestSagaData { OrderId = "ORD-456" };

		// Act
		var state = new SagaPersistedState<TestSagaData> { Data = data };

		// Assert
		state.Data.ShouldBe(data);
		state.Data.OrderId.ShouldBe("ORD-456");
	}

	[Fact]
	public void AllowStateToBeSet()
	{
		// Arrange & Act
		var state = new SagaPersistedState<TestSagaData> { State = SagaState.Running };

		// Assert
		state.State.ShouldBe(SagaState.Running);
	}

	[Fact]
	public void AllowCurrentStepIndexToBeSet()
	{
		// Arrange & Act
		var state = new SagaPersistedState<TestSagaData> { CurrentStepIndex = 3 };

		// Assert
		state.CurrentStepIndex.ShouldBe(3);
	}

	[Fact]
	public void AllowActivitiesToBeSet()
	{
		// Arrange
		var activities = new List<SagaActivity>
		{
			new() { Message = "Activity 1", Timestamp = DateTimeOffset.UtcNow },
			new() { Message = "Activity 2", Timestamp = DateTimeOffset.UtcNow },
		};

		// Act
		var state = new SagaPersistedState<TestSagaData> { Activities = activities };

		// Assert
		state.Activities.Count.ShouldBe(2);
	}

	[Fact]
	public void AllowStartedAtToBeSet()
	{
		// Arrange
		var startedAt = DateTimeOffset.UtcNow;

		// Act
		var state = new SagaPersistedState<TestSagaData> { StartedAt = startedAt };

		// Assert
		state.StartedAt.ShouldBe(startedAt);
	}

	[Fact]
	public void AllowCompletedAtToBeSet()
	{
		// Arrange
		var completedAt = DateTimeOffset.UtcNow;

		// Act
		var state = new SagaPersistedState<TestSagaData> { CompletedAt = completedAt };

		// Assert
		state.CompletedAt.ShouldBe(completedAt);
	}

	#endregion Property Setting Tests

	#region Comprehensive State Tests

	[Fact]
	public void CreateRunningSagaState()
	{
		// Arrange
		var definition = new TestSagaDefinition();
		var data = new TestSagaData { OrderId = "ORD-001" };
		var startedAt = DateTimeOffset.UtcNow;

		// Act
		var state = new SagaPersistedState<TestSagaData>
		{
			SagaId = "running-saga-001",
			Definition = definition,
			Data = data,
			State = SagaState.Running,
			CurrentStepIndex = 2,
			StartedAt = startedAt,
			Activities =
			[
				new SagaActivity { Message = "Step 1 completed", Timestamp = startedAt.AddSeconds(1) },
				new SagaActivity { Message = "Step 2 started", Timestamp = startedAt.AddSeconds(2) },
			],
		};

		// Assert
		state.State.ShouldBe(SagaState.Running);
		state.CompletedAt.ShouldBeNull();
		state.Activities.Count.ShouldBe(2);
	}

	[Fact]
	public void CreateCompletedSagaState()
	{
		// Arrange
		var definition = new TestSagaDefinition();
		var data = new TestSagaData { OrderId = "ORD-002" };
		var startedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
		var completedAt = DateTimeOffset.UtcNow;

		// Act
		var state = new SagaPersistedState<TestSagaData>
		{
			SagaId = "completed-saga-001",
			Definition = definition,
			Data = data,
			State = SagaState.Completed,
			CurrentStepIndex = 3,
			StartedAt = startedAt,
			CompletedAt = completedAt,
		};

		// Assert
		state.State.ShouldBe(SagaState.Completed);
		state.CompletedAt.ShouldNotBeNull();
	}

	[Fact]
	public void CreateCompensatingSagaState()
	{
		// Arrange
		var definition = new TestSagaDefinition();
		var data = new TestSagaData { OrderId = "ORD-FAILED" };
		var startedAt = DateTimeOffset.UtcNow.AddMinutes(-10);

		// Act
		var state = new SagaPersistedState<TestSagaData>
		{
			SagaId = "compensating-saga-001",
			Definition = definition,
			Data = data,
			State = SagaState.Compensating,
			CurrentStepIndex = 2,
			StartedAt = startedAt,
			Activities =
			[
				new SagaActivity { Message = "Error in step 3", Timestamp = startedAt.AddMinutes(8) },
				new SagaActivity { Message = "Compensation started", Timestamp = startedAt.AddMinutes(8).AddSeconds(1) },
			],
		};

		// Assert
		state.State.ShouldBe(SagaState.Compensating);
	}

	[Fact]
	public void CreateCompensatedSuccessfullySagaState()
	{
		// Arrange
		var definition = new TestSagaDefinition();
		var startedAt = DateTimeOffset.UtcNow.AddMinutes(-15);
		var completedAt = DateTimeOffset.UtcNow;

		// Act
		var state = new SagaPersistedState<TestSagaData>
		{
			SagaId = "compensated-saga-001",
			Definition = definition,
			Data = new TestSagaData(),
			State = SagaState.CompensatedSuccessfully,
			CurrentStepIndex = 0,
			StartedAt = startedAt,
			CompletedAt = completedAt,
		};

		// Assert
		state.State.ShouldBe(SagaState.CompensatedSuccessfully);
	}

	[Fact]
	public void CreateCancelledSagaState()
	{
		// Arrange
		var definition = new TestSagaDefinition();
		var startedAt = DateTimeOffset.UtcNow.AddMinutes(-2);
		var completedAt = DateTimeOffset.UtcNow;

		// Act
		var state = new SagaPersistedState<TestSagaData>
		{
			SagaId = "cancelled-saga-001",
			Definition = definition,
			Data = new TestSagaData(),
			State = SagaState.Cancelled,
			CurrentStepIndex = 1,
			StartedAt = startedAt,
			CompletedAt = completedAt,
		};

		// Assert
		state.State.ShouldBe(SagaState.Cancelled);
	}

	[Fact]
	public void TrackMultipleActivities()
	{
		// Arrange
		var startedAt = DateTimeOffset.UtcNow;
		var activities = new List<SagaActivity>();

		for (var i = 0; i < 10; i++)
		{
			activities.Add(new SagaActivity
			{
				Message = $"Activity {i + 1}",
				Timestamp = startedAt.AddSeconds(i),
			});
		}

		// Act
		var state = new SagaPersistedState<TestSagaData>
		{
			SagaId = "multi-activity-saga",
			Activities = activities,
		};

		// Assert
		state.Activities.Count.ShouldBe(10);
	}

	[Fact]
	public void SupportDifferentSagaDataTypes()
	{
		// Arrange
		var definition = new PaymentSagaDefinition();
		var data = new PaymentSagaData
		{
			PaymentId = "PAY-123",
			Amount = 99.99m,
			Currency = "USD",
		};

		// Act
		var state = new SagaPersistedState<PaymentSagaData>
		{
			SagaId = "payment-saga-001",
			Definition = definition,
			Data = data,
			State = SagaState.Completed,
		};

		// Assert
		state.Data.Amount.ShouldBe(99.99m);
		state.Data.Currency.ShouldBe("USD");
	}

	#endregion Comprehensive State Tests

	#region Test Helper Types

	private sealed class TestSagaData
	{
		public string OrderId { get; init; } = string.Empty;
	}

	private sealed class PaymentSagaData
	{
		public string PaymentId { get; init; } = string.Empty;
		public decimal Amount { get; init; }
		public string Currency { get; init; } = string.Empty;
	}

	private sealed class TestSagaDefinition : ISagaDefinition<TestSagaData>
	{
		public string Name => "TestSaga";
		public TimeSpan Timeout => TimeSpan.FromMinutes(30);
		public IReadOnlyList<ISagaStep<TestSagaData>> Steps => [];
		public IRetryPolicy? RetryPolicy => null;

		public Task OnCompletedAsync(ISagaContext<TestSagaData> context, CancellationToken cancellationToken)
			=> Task.CompletedTask;

		public Task OnFailedAsync(ISagaContext<TestSagaData> context, Exception exception, CancellationToken cancellationToken)
			=> Task.CompletedTask;
	}

	private sealed class PaymentSagaDefinition : ISagaDefinition<PaymentSagaData>
	{
		public string Name => "PaymentSaga";
		public TimeSpan Timeout => TimeSpan.FromMinutes(30);
		public IReadOnlyList<ISagaStep<PaymentSagaData>> Steps => [];
		public IRetryPolicy? RetryPolicy => null;

		public Task OnCompletedAsync(ISagaContext<PaymentSagaData> context, CancellationToken cancellationToken)
			=> Task.CompletedTask;

		public Task OnFailedAsync(ISagaContext<PaymentSagaData> context, Exception exception, CancellationToken cancellationToken)
			=> Task.CompletedTask;
	}

	#endregion Test Helper Types
}
