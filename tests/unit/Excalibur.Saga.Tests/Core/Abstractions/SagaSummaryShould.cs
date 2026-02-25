// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Abstractions;

namespace Excalibur.Saga.Tests.Core.Abstractions;

/// <summary>
/// Unit tests for <see cref="SagaSummary"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class SagaSummaryShould
{
	#region Default Values Tests

	[Fact]
	public void HaveEmptySagaIdByDefault()
	{
		// Arrange & Act
		var summary = new SagaSummary();

		// Assert
		summary.SagaId.ShouldBe(string.Empty);
	}

	[Fact]
	public void HaveEmptySagaTypeByDefault()
	{
		// Arrange & Act
		var summary = new SagaSummary();

		// Assert
		summary.SagaType.ShouldBe(string.Empty);
	}

	[Fact]
	public void HaveDefaultStateByDefault()
	{
		// Arrange & Act
		var summary = new SagaSummary();

		// Assert
		summary.State.ShouldBe(default(SagaState));
	}

	[Fact]
	public void HaveDefaultStartedAtByDefault()
	{
		// Arrange & Act
		var summary = new SagaSummary();

		// Assert
		summary.StartedAt.ShouldBe(default(DateTimeOffset));
	}

	[Fact]
	public void HaveNullCompletedAtByDefault()
	{
		// Arrange & Act
		var summary = new SagaSummary();

		// Assert
		summary.CompletedAt.ShouldBeNull();
	}

	[Fact]
	public void HaveZeroCurrentStepByDefault()
	{
		// Arrange & Act
		var summary = new SagaSummary();

		// Assert
		summary.CurrentStep.ShouldBe(0);
	}

	[Fact]
	public void HaveZeroTotalStepsByDefault()
	{
		// Arrange & Act
		var summary = new SagaSummary();

		// Assert
		summary.TotalSteps.ShouldBe(0);
	}

	#endregion Default Values Tests

	#region Property Setting Tests

	[Fact]
	public void AllowSagaIdToBeSet()
	{
		// Arrange & Act
		var summary = new SagaSummary { SagaId = "saga-123" };

		// Assert
		summary.SagaId.ShouldBe("saga-123");
	}

	[Fact]
	public void AllowSagaTypeToBeSet()
	{
		// Arrange & Act
		var summary = new SagaSummary { SagaType = "OrderProcessingSaga" };

		// Assert
		summary.SagaType.ShouldBe("OrderProcessingSaga");
	}

	[Fact]
	public void AllowStateToBeSet()
	{
		// Arrange & Act
		var summary = new SagaSummary { State = SagaState.Running };

		// Assert
		summary.State.ShouldBe(SagaState.Running);
	}

	[Fact]
	public void AllowStartedAtToBeSet()
	{
		// Arrange
		var startedAt = DateTimeOffset.UtcNow;

		// Act
		var summary = new SagaSummary { StartedAt = startedAt };

		// Assert
		summary.StartedAt.ShouldBe(startedAt);
	}

	[Fact]
	public void AllowCompletedAtToBeSet()
	{
		// Arrange
		var completedAt = DateTimeOffset.UtcNow;

		// Act
		var summary = new SagaSummary { CompletedAt = completedAt };

		// Assert
		summary.CompletedAt.ShouldBe(completedAt);
	}

	[Fact]
	public void AllowCurrentStepToBeSet()
	{
		// Arrange & Act
		var summary = new SagaSummary { CurrentStep = 3 };

		// Assert
		summary.CurrentStep.ShouldBe(3);
	}

	[Fact]
	public void AllowTotalStepsToBeSet()
	{
		// Arrange & Act
		var summary = new SagaSummary { TotalSteps = 5 };

		// Assert
		summary.TotalSteps.ShouldBe(5);
	}

	#endregion Property Setting Tests

	#region Comprehensive Configuration Tests

	[Fact]
	public void CreateRunningSagaSummary()
	{
		// Arrange
		var startedAt = DateTimeOffset.UtcNow;

		// Act
		var summary = new SagaSummary
		{
			SagaId = "order-saga-001",
			SagaType = "OrderProcessingSaga",
			State = SagaState.Running,
			StartedAt = startedAt,
			CurrentStep = 2,
			TotalSteps = 5,
		};

		// Assert
		summary.SagaId.ShouldBe("order-saga-001");
		summary.SagaType.ShouldBe("OrderProcessingSaga");
		summary.State.ShouldBe(SagaState.Running);
		summary.StartedAt.ShouldBe(startedAt);
		summary.CompletedAt.ShouldBeNull();
		summary.CurrentStep.ShouldBe(2);
		summary.TotalSteps.ShouldBe(5);
	}

	[Fact]
	public void CreateCompletedSagaSummary()
	{
		// Arrange
		var startedAt = DateTimeOffset.UtcNow.AddMinutes(-10);
		var completedAt = DateTimeOffset.UtcNow;

		// Act
		var summary = new SagaSummary
		{
			SagaId = "payment-saga-002",
			SagaType = "PaymentProcessingSaga",
			State = SagaState.Completed,
			StartedAt = startedAt,
			CompletedAt = completedAt,
			CurrentStep = 3,
			TotalSteps = 3,
		};

		// Assert
		summary.State.ShouldBe(SagaState.Completed);
		summary.CompletedAt.ShouldNotBeNull();
		summary.CurrentStep.ShouldBe(summary.TotalSteps);
	}

	[Fact]
	public void CreateCompensatingSagaSummary()
	{
		// Arrange
		var startedAt = DateTimeOffset.UtcNow.AddMinutes(-5);

		// Act
		var summary = new SagaSummary
		{
			SagaId = "refund-saga-003",
			SagaType = "RefundProcessingSaga",
			State = SagaState.Compensating,
			StartedAt = startedAt,
			CurrentStep = 2,
			TotalSteps = 4,
		};

		// Assert
		summary.State.ShouldBe(SagaState.Compensating);
		summary.CompletedAt.ShouldBeNull();
	}

	[Fact]
	public void CreateCompensatedSuccessfullySagaSummary()
	{
		// Arrange
		var startedAt = DateTimeOffset.UtcNow.AddMinutes(-15);
		var completedAt = DateTimeOffset.UtcNow;

		// Act
		var summary = new SagaSummary
		{
			SagaId = "cancel-saga-004",
			SagaType = "CancellationSaga",
			State = SagaState.CompensatedSuccessfully,
			StartedAt = startedAt,
			CompletedAt = completedAt,
			CurrentStep = 0,
			TotalSteps = 3,
		};

		// Assert
		summary.State.ShouldBe(SagaState.CompensatedSuccessfully);
		summary.CompletedAt.ShouldNotBeNull();
	}

	[Fact]
	public void CreateCancelledSagaSummary()
	{
		// Arrange
		var startedAt = DateTimeOffset.UtcNow.AddMinutes(-2);
		var completedAt = DateTimeOffset.UtcNow;

		// Act
		var summary = new SagaSummary
		{
			SagaId = "cancelled-saga-005",
			SagaType = "ReservationSaga",
			State = SagaState.Cancelled,
			StartedAt = startedAt,
			CompletedAt = completedAt,
			CurrentStep = 1,
			TotalSteps = 5,
		};

		// Assert
		summary.State.ShouldBe(SagaState.Cancelled);
		summary.CurrentStep.ShouldBeLessThan(summary.TotalSteps);
	}

	[Fact]
	public void TrackProgressThroughSteps()
	{
		// Arrange & Act
		var summary = new SagaSummary
		{
			CurrentStep = 3,
			TotalSteps = 10,
		};

		// Assert - progress can be calculated as percentage
		var progress = (double)summary.CurrentStep / summary.TotalSteps * 100;
		progress.ShouldBe(30);
	}

	[Fact]
	public void HandleSingleStepSaga()
	{
		// Arrange & Act
		var summary = new SagaSummary
		{
			SagaId = "single-step-saga",
			SagaType = "SimpleNotificationSaga",
			State = SagaState.Completed,
			CurrentStep = 1,
			TotalSteps = 1,
		};

		// Assert
		summary.CurrentStep.ShouldBe(summary.TotalSteps);
	}

	#endregion Comprehensive Configuration Tests
}
