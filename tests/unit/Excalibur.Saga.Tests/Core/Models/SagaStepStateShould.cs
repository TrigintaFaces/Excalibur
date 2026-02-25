// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Models;

namespace Excalibur.Saga.Tests.Core.Models;

/// <summary>
/// Unit tests for <see cref="SagaStepState"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class SagaStepStateShould
{
	#region Default Values Tests

	[Fact]
	public void HaveEmptyNameByDefault()
	{
		// Arrange & Act
		var state = new SagaStepState();

		// Assert
		state.Name.ShouldBe(string.Empty);
	}

	[Fact]
	public void HaveDefaultStatus()
	{
		// Arrange & Act
		var state = new SagaStepState();

		// Assert
		state.Status.ShouldBe(default(StepStatus));
	}

	[Fact]
	public void HaveNullStartedAtByDefault()
	{
		// Arrange & Act
		var state = new SagaStepState();

		// Assert
		state.StartedAt.ShouldBeNull();
	}

	[Fact]
	public void HaveNullCompletedAtByDefault()
	{
		// Arrange & Act
		var state = new SagaStepState();

		// Assert
		state.CompletedAt.ShouldBeNull();
	}

	[Fact]
	public void HaveZeroAttemptsByDefault()
	{
		// Arrange & Act
		var state = new SagaStepState();

		// Assert
		state.Attempts.ShouldBe(0);
	}

	[Fact]
	public void HaveNullErrorMessageByDefault()
	{
		// Arrange & Act
		var state = new SagaStepState();

		// Assert
		state.ErrorMessage.ShouldBeNull();
	}

	[Fact]
	public void HaveDefaultCompensationStatus()
	{
		// Arrange & Act
		var state = new SagaStepState();

		// Assert
		state.CompensationStatus.ShouldBe(default(CompensationStatus));
	}

	[Fact]
	public void HaveNullCompensationStartedAtByDefault()
	{
		// Arrange & Act
		var state = new SagaStepState();

		// Assert
		state.CompensationStartedAt.ShouldBeNull();
	}

	[Fact]
	public void HaveNullCompensationCompletedAtByDefault()
	{
		// Arrange & Act
		var state = new SagaStepState();

		// Assert
		state.CompensationCompletedAt.ShouldBeNull();
	}

	[Fact]
	public void HaveNullCompensationErrorByDefault()
	{
		// Arrange & Act
		var state = new SagaStepState();

		// Assert
		state.CompensationError.ShouldBeNull();
	}

	[Fact]
	public void HaveNullStepDataJsonByDefault()
	{
		// Arrange & Act
		var state = new SagaStepState();

		// Assert
		state.StepDataJson.ShouldBeNull();
	}

	#endregion Default Values Tests

	#region Property Setting Tests

	[Fact]
	public void AllowNameToBeSet()
	{
		// Arrange & Act
		var state = new SagaStepState { Name = "ProcessPayment" };

		// Assert
		state.Name.ShouldBe("ProcessPayment");
	}

	[Fact]
	public void AllowStatusToBeSet()
	{
		// Arrange & Act
		var state = new SagaStepState { Status = StepStatus.Succeeded };

		// Assert
		state.Status.ShouldBe(StepStatus.Succeeded);
	}

	[Fact]
	public void AllowStartedAtToBeSet()
	{
		// Arrange
		var startedAt = DateTime.UtcNow;

		// Act
		var state = new SagaStepState { StartedAt = startedAt };

		// Assert
		state.StartedAt.ShouldBe(startedAt);
	}

	[Fact]
	public void AllowCompletedAtToBeSet()
	{
		// Arrange
		var completedAt = DateTime.UtcNow;

		// Act
		var state = new SagaStepState { CompletedAt = completedAt };

		// Assert
		state.CompletedAt.ShouldBe(completedAt);
	}

	[Fact]
	public void AllowAttemptsToBeSet()
	{
		// Arrange & Act
		var state = new SagaStepState { Attempts = 3 };

		// Assert
		state.Attempts.ShouldBe(3);
	}

	[Fact]
	public void AllowErrorMessageToBeSet()
	{
		// Arrange & Act
		var state = new SagaStepState { ErrorMessage = "Connection timeout" };

		// Assert
		state.ErrorMessage.ShouldBe("Connection timeout");
	}

	[Fact]
	public void AllowCompensationStatusToBeSet()
	{
		// Arrange & Act
		var state = new SagaStepState { CompensationStatus = CompensationStatus.Succeeded };

		// Assert
		state.CompensationStatus.ShouldBe(CompensationStatus.Succeeded);
	}

	[Fact]
	public void AllowCompensationStartedAtToBeSet()
	{
		// Arrange
		var timestamp = DateTime.UtcNow;

		// Act
		var state = new SagaStepState { CompensationStartedAt = timestamp };

		// Assert
		state.CompensationStartedAt.ShouldBe(timestamp);
	}

	[Fact]
	public void AllowCompensationCompletedAtToBeSet()
	{
		// Arrange
		var timestamp = DateTime.UtcNow;

		// Act
		var state = new SagaStepState { CompensationCompletedAt = timestamp };

		// Assert
		state.CompensationCompletedAt.ShouldBe(timestamp);
	}

	[Fact]
	public void AllowCompensationErrorToBeSet()
	{
		// Arrange & Act
		var state = new SagaStepState { CompensationError = "Failed to rollback" };

		// Assert
		state.CompensationError.ShouldBe("Failed to rollback");
	}

	[Fact]
	public void AllowStepDataJsonToBeSet()
	{
		// Arrange & Act
		var state = new SagaStepState { StepDataJson = "{\"orderId\":\"123\"}" };

		// Assert
		state.StepDataJson.ShouldBe("{\"orderId\":\"123\"}");
	}

	#endregion Property Setting Tests

	#region Comprehensive State Tests

	[Fact]
	public void CreateNotStartedStepState()
	{
		// Arrange & Act
		var state = new SagaStepState
		{
			Name = "ValidateOrder",
			Status = StepStatus.NotStarted,
			Attempts = 0,
		};

		// Assert
		state.Status.ShouldBe(StepStatus.NotStarted);
		state.StartedAt.ShouldBeNull();
		state.CompletedAt.ShouldBeNull();
		state.Attempts.ShouldBe(0);
	}

	[Fact]
	public void CreateRunningStepState()
	{
		// Arrange & Act
		var state = new SagaStepState
		{
			Name = "ProcessPayment",
			Status = StepStatus.Running,
			StartedAt = DateTime.UtcNow,
			Attempts = 1,
		};

		// Assert
		state.Status.ShouldBe(StepStatus.Running);
		state.StartedAt.ShouldNotBeNull();
		state.CompletedAt.ShouldBeNull();
	}

	[Fact]
	public void CreateSucceededStepState()
	{
		// Arrange
		var startedAt = DateTime.UtcNow.AddSeconds(-5);
		var completedAt = DateTime.UtcNow;

		// Act
		var state = new SagaStepState
		{
			Name = "SendConfirmation",
			Status = StepStatus.Succeeded,
			StartedAt = startedAt,
			CompletedAt = completedAt,
			Attempts = 1,
		};

		// Assert
		state.Status.ShouldBe(StepStatus.Succeeded);
		state.StartedAt.ShouldNotBeNull();
		state.CompletedAt.ShouldNotBeNull();
		state.ErrorMessage.ShouldBeNull();
	}

	[Fact]
	public void CreateFailedStepState()
	{
		// Arrange
		var startedAt = DateTime.UtcNow.AddSeconds(-10);
		var completedAt = DateTime.UtcNow;

		// Act
		var state = new SagaStepState
		{
			Name = "ChargeCard",
			Status = StepStatus.Failed,
			StartedAt = startedAt,
			CompletedAt = completedAt,
			Attempts = 3,
			ErrorMessage = "Card declined",
		};

		// Assert
		state.Status.ShouldBe(StepStatus.Failed);
		state.Attempts.ShouldBe(3);
		state.ErrorMessage.ShouldNotBeNull();
	}

	[Fact]
	public void CreateSkippedStepState()
	{
		// Arrange & Act
		var state = new SagaStepState
		{
			Name = "OptionalNotification",
			Status = StepStatus.Skipped,
			Attempts = 0,
		};

		// Assert
		state.Status.ShouldBe(StepStatus.Skipped);
		state.Attempts.ShouldBe(0);
	}

	[Fact]
	public void CreateTimedOutStepState()
	{
		// Arrange & Act
		var state = new SagaStepState
		{
			Name = "ExternalServiceCall",
			Status = StepStatus.TimedOut,
			StartedAt = DateTime.UtcNow.AddSeconds(-30),
			CompletedAt = DateTime.UtcNow,
			Attempts = 1,
			ErrorMessage = "Operation timed out after 30 seconds",
		};

		// Assert
		state.Status.ShouldBe(StepStatus.TimedOut);
		state.ErrorMessage.ShouldContain("timed out");
	}

	[Fact]
	public void CreateCompensatedStepState()
	{
		// Arrange
		var stepStarted = DateTime.UtcNow.AddSeconds(-20);
		var stepCompleted = DateTime.UtcNow.AddSeconds(-15);
		var compStarted = DateTime.UtcNow.AddSeconds(-10);
		var compCompleted = DateTime.UtcNow;

		// Act
		var state = new SagaStepState
		{
			Name = "ReserveInventory",
			Status = StepStatus.Succeeded,
			StartedAt = stepStarted,
			CompletedAt = stepCompleted,
			Attempts = 1,
			CompensationStatus = CompensationStatus.Succeeded,
			CompensationStartedAt = compStarted,
			CompensationCompletedAt = compCompleted,
		};

		// Assert
		state.CompensationStatus.ShouldBe(CompensationStatus.Succeeded);
		state.CompensationStartedAt.ShouldNotBeNull();
		state.CompensationCompletedAt.ShouldNotBeNull();
		state.CompensationError.ShouldBeNull();
	}

	[Fact]
	public void CreateFailedCompensationStepState()
	{
		// Arrange
		var compStarted = DateTime.UtcNow.AddSeconds(-5);
		var compCompleted = DateTime.UtcNow;

		// Act
		var state = new SagaStepState
		{
			Name = "DebitAccount",
			Status = StepStatus.Succeeded,
			CompensationStatus = CompensationStatus.Failed,
			CompensationStartedAt = compStarted,
			CompensationCompletedAt = compCompleted,
			CompensationError = "Unable to credit account - account closed",
		};

		// Assert
		state.CompensationStatus.ShouldBe(CompensationStatus.Failed);
		state.CompensationError.ShouldNotBeNull();
	}

	[Fact]
	public void CreateStepStateWithCustomData()
	{
		// Arrange & Act
		var state = new SagaStepState
		{
			Name = "ShipOrder",
			Status = StepStatus.Succeeded,
			StepDataJson = "{\"trackingNumber\":\"1Z999AA10123456784\",\"carrier\":\"UPS\"}",
		};

		// Assert
		state.StepDataJson.ShouldNotBeNull();
		state.StepDataJson.ShouldContain("trackingNumber");
	}

	#endregion Comprehensive State Tests
}
