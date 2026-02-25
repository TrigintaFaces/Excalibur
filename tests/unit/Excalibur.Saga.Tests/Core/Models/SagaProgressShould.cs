// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Models;

namespace Excalibur.Saga.Tests.Core.Models;

/// <summary>
/// Unit tests for <see cref="SagaProgress"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class SagaProgressShould
{
	#region Default Values Tests

	[Fact]
	public void HaveEmptySagaIdByDefault()
	{
		// Arrange & Act
		var progress = new SagaProgress();

		// Assert
		progress.SagaId.ShouldBe(string.Empty);
	}

	[Fact]
	public void HaveDefaultStatus()
	{
		// Arrange & Act
		var progress = new SagaProgress();

		// Assert
		progress.Status.ShouldBe(default(SagaStatus));
	}

	[Fact]
	public void HaveDefaultStartedAt()
	{
		// Arrange & Act
		var progress = new SagaProgress();

		// Assert
		progress.StartedAt.ShouldBe(default(DateTime));
	}

	[Fact]
	public void HaveNullCompletedAtByDefault()
	{
		// Arrange & Act
		var progress = new SagaProgress();

		// Assert
		progress.CompletedAt.ShouldBeNull();
	}

	[Fact]
	public void HaveNullCurrentStepByDefault()
	{
		// Arrange & Act
		var progress = new SagaProgress();

		// Assert
		progress.CurrentStep.ShouldBeNull();
	}

	[Fact]
	public void HaveNullErrorMessageByDefault()
	{
		// Arrange & Act
		var progress = new SagaProgress();

		// Assert
		progress.ErrorMessage.ShouldBeNull();
	}

	[Fact]
	public void HaveZeroCompletedStepsByDefault()
	{
		// Arrange & Act
		var progress = new SagaProgress();

		// Assert
		progress.CompletedSteps.ShouldBe(0);
	}

	[Fact]
	public void HaveZeroTotalStepsByDefault()
	{
		// Arrange & Act
		var progress = new SagaProgress();

		// Assert
		progress.TotalSteps.ShouldBe(0);
	}

	#endregion Default Values Tests

	#region Property Setting Tests

	[Fact]
	public void AllowSagaIdToBeSet()
	{
		// Arrange & Act
		var progress = new SagaProgress { SagaId = "saga-123" };

		// Assert
		progress.SagaId.ShouldBe("saga-123");
	}

	[Fact]
	public void AllowStatusToBeSet()
	{
		// Arrange & Act
		var progress = new SagaProgress { Status = SagaStatus.Running };

		// Assert
		progress.Status.ShouldBe(SagaStatus.Running);
	}

	[Fact]
	public void AllowStartedAtToBeSet()
	{
		// Arrange
		var startedAt = DateTime.UtcNow;

		// Act
		var progress = new SagaProgress { StartedAt = startedAt };

		// Assert
		progress.StartedAt.ShouldBe(startedAt);
	}

	[Fact]
	public void AllowCompletedAtToBeSet()
	{
		// Arrange
		var completedAt = DateTime.UtcNow;

		// Act
		var progress = new SagaProgress { CompletedAt = completedAt };

		// Assert
		progress.CompletedAt.ShouldBe(completedAt);
	}

	[Fact]
	public void AllowCurrentStepToBeSet()
	{
		// Arrange & Act
		var progress = new SagaProgress { CurrentStep = "ProcessPayment" };

		// Assert
		progress.CurrentStep.ShouldBe("ProcessPayment");
	}

	[Fact]
	public void AllowErrorMessageToBeSet()
	{
		// Arrange & Act
		var progress = new SagaProgress { ErrorMessage = "Payment failed" };

		// Assert
		progress.ErrorMessage.ShouldBe("Payment failed");
	}

	[Fact]
	public void AllowCompletedStepsToBeSet()
	{
		// Arrange & Act
		var progress = new SagaProgress { CompletedSteps = 3 };

		// Assert
		progress.CompletedSteps.ShouldBe(3);
	}

	[Fact]
	public void AllowTotalStepsToBeSet()
	{
		// Arrange & Act
		var progress = new SagaProgress { TotalSteps = 5 };

		// Assert
		progress.TotalSteps.ShouldBe(5);
	}

	#endregion Property Setting Tests

	#region IsSuccess Tests

	[Fact]
	public void ReportIsSuccess_WhenStatusIsCompleted()
	{
		// Arrange & Act
		var progress = new SagaProgress { Status = SagaStatus.Completed };

		// Assert
		progress.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public void ReportIsNotSuccess_WhenStatusIsRunning()
	{
		// Arrange & Act
		var progress = new SagaProgress { Status = SagaStatus.Running };

		// Assert
		progress.IsSuccess.ShouldBeFalse();
	}

	[Fact]
	public void ReportIsNotSuccess_WhenStatusIsFailed()
	{
		// Arrange & Act
		var progress = new SagaProgress { Status = SagaStatus.Failed };

		// Assert
		progress.IsSuccess.ShouldBeFalse();
	}

	[Fact]
	public void ReportIsNotSuccess_WhenStatusIsCompensating()
	{
		// Arrange & Act
		var progress = new SagaProgress { Status = SagaStatus.Compensating };

		// Assert
		progress.IsSuccess.ShouldBeFalse();
	}

	#endregion IsSuccess Tests

	#region Duration Tests

	[Fact]
	public void CalculateDuration_WhenCompleted()
	{
		// Arrange
		var startedAt = DateTime.UtcNow.AddMinutes(-5);
		var completedAt = DateTime.UtcNow;

		// Act
		var progress = new SagaProgress
		{
			StartedAt = startedAt,
			CompletedAt = completedAt,
		};

		// Assert
		progress.Duration.ShouldNotBeNull();
		progress.Duration.Value.TotalMinutes.ShouldBeInRange(4.9, 5.1);
	}

	[Fact]
	public void ReturnNullDuration_WhenNotCompleted()
	{
		// Arrange & Act
		var progress = new SagaProgress
		{
			StartedAt = DateTime.UtcNow.AddMinutes(-5),
			CompletedAt = null,
		};

		// Assert
		progress.Duration.ShouldBeNull();
	}

	[Fact]
	public void CalculateZeroDuration_WhenCompletedImmediately()
	{
		// Arrange
		var timestamp = DateTime.UtcNow;

		// Act
		var progress = new SagaProgress
		{
			StartedAt = timestamp,
			CompletedAt = timestamp,
		};

		// Assert
		progress.Duration.ShouldBe(TimeSpan.Zero);
	}

	#endregion Duration Tests

	#region Comprehensive Progress Tests

	[Fact]
	public void CreateRunningProgress()
	{
		// Arrange & Act
		var progress = new SagaProgress
		{
			SagaId = "order-saga-123",
			Status = SagaStatus.Running,
			StartedAt = DateTime.UtcNow,
			CurrentStep = "ValidateOrder",
			CompletedSteps = 1,
			TotalSteps = 4,
		};

		// Assert
		progress.IsSuccess.ShouldBeFalse();
		progress.Duration.ShouldBeNull();
		progress.ErrorMessage.ShouldBeNull();
	}

	[Fact]
	public void CreateCompletedProgress()
	{
		// Arrange
		var startedAt = DateTime.UtcNow.AddSeconds(-30);
		var completedAt = DateTime.UtcNow;

		// Act
		var progress = new SagaProgress
		{
			SagaId = "order-saga-123",
			Status = SagaStatus.Completed,
			StartedAt = startedAt,
			CompletedAt = completedAt,
			CompletedSteps = 4,
			TotalSteps = 4,
		};

		// Assert
		progress.IsSuccess.ShouldBeTrue();
		progress.Duration.ShouldNotBeNull();
		progress.CompletedSteps.ShouldBe(progress.TotalSteps);
	}

	[Fact]
	public void CreateFailedProgress()
	{
		// Arrange & Act
		var progress = new SagaProgress
		{
			SagaId = "order-saga-123",
			Status = SagaStatus.Failed,
			StartedAt = DateTime.UtcNow.AddSeconds(-10),
			CompletedAt = DateTime.UtcNow,
			CurrentStep = "ProcessPayment",
			ErrorMessage = "Payment gateway unavailable",
			CompletedSteps = 2,
			TotalSteps = 4,
		};

		// Assert
		progress.IsSuccess.ShouldBeFalse();
		progress.ErrorMessage.ShouldNotBeNull();
		progress.CompletedSteps.ShouldBeLessThan(progress.TotalSteps);
	}

	#endregion Comprehensive Progress Tests
}
