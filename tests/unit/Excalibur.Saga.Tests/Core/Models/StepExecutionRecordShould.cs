// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Models;

namespace Excalibur.Saga.Tests.Core.Models;

/// <summary>
/// Unit tests for <see cref="StepExecutionRecord"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class StepExecutionRecordShould
{
	#region Default Values Tests

	[Fact]
	public void HaveEmptyStepNameByDefault()
	{
		// Arrange & Act
		var record = new StepExecutionRecord();

		// Assert
		record.StepName.ShouldBe(string.Empty);
	}

	[Fact]
	public void HaveZeroStepIndexByDefault()
	{
		// Arrange & Act
		var record = new StepExecutionRecord();

		// Assert
		record.StepIndex.ShouldBe(0);
	}

	[Fact]
	public void HaveDefaultStartedAt()
	{
		// Arrange & Act
		var record = new StepExecutionRecord();

		// Assert
		record.StartedAt.ShouldBe(default(DateTime));
	}

	[Fact]
	public void HaveNullCompletedAtByDefault()
	{
		// Arrange & Act
		var record = new StepExecutionRecord();

		// Assert
		record.CompletedAt.ShouldBeNull();
	}

	[Fact]
	public void HaveIsSuccessFalseByDefault()
	{
		// Arrange & Act
		var record = new StepExecutionRecord();

		// Assert
		record.IsSuccess.ShouldBeFalse();
	}

	[Fact]
	public void HaveWasCompensatedFalseByDefault()
	{
		// Arrange & Act
		var record = new StepExecutionRecord();

		// Assert
		record.WasCompensated.ShouldBeFalse();
	}

	[Fact]
	public void HaveNullErrorMessageByDefault()
	{
		// Arrange & Act
		var record = new StepExecutionRecord();

		// Assert
		record.ErrorMessage.ShouldBeNull();
	}

	[Fact]
	public void HaveZeroRetryCountByDefault()
	{
		// Arrange & Act
		var record = new StepExecutionRecord();

		// Assert
		record.RetryCount.ShouldBe(0);
	}

	#endregion Default Values Tests

	#region Property Setting Tests

	[Fact]
	public void AllowStepNameToBeSet()
	{
		// Arrange & Act
		var record = new StepExecutionRecord { StepName = "ValidateOrder" };

		// Assert
		record.StepName.ShouldBe("ValidateOrder");
	}

	[Fact]
	public void AllowStepIndexToBeSet()
	{
		// Arrange & Act
		var record = new StepExecutionRecord { StepIndex = 2 };

		// Assert
		record.StepIndex.ShouldBe(2);
	}

	[Fact]
	public void AllowStartedAtToBeSet()
	{
		// Arrange
		var startedAt = DateTime.UtcNow;

		// Act
		var record = new StepExecutionRecord { StartedAt = startedAt };

		// Assert
		record.StartedAt.ShouldBe(startedAt);
	}

	[Fact]
	public void AllowCompletedAtToBeSet()
	{
		// Arrange
		var completedAt = DateTime.UtcNow;

		// Act
		var record = new StepExecutionRecord { CompletedAt = completedAt };

		// Assert
		record.CompletedAt.ShouldBe(completedAt);
	}

	[Fact]
	public void AllowIsSuccessToBeSet()
	{
		// Arrange & Act
		var record = new StepExecutionRecord { IsSuccess = true };

		// Assert
		record.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public void AllowWasCompensatedToBeSet()
	{
		// Arrange & Act
		var record = new StepExecutionRecord { WasCompensated = true };

		// Assert
		record.WasCompensated.ShouldBeTrue();
	}

	[Fact]
	public void AllowErrorMessageToBeSet()
	{
		// Arrange & Act
		var record = new StepExecutionRecord { ErrorMessage = "Step failed due to timeout" };

		// Assert
		record.ErrorMessage.ShouldBe("Step failed due to timeout");
	}

	[Fact]
	public void AllowRetryCountToBeSet()
	{
		// Arrange & Act
		var record = new StepExecutionRecord { RetryCount = 3 };

		// Assert
		record.RetryCount.ShouldBe(3);
	}

	#endregion Property Setting Tests

	#region Duration Tests

	[Fact]
	public void CalculateDuration_WhenCompleted()
	{
		// Arrange
		var startedAt = DateTime.UtcNow.AddSeconds(-30);
		var completedAt = DateTime.UtcNow;

		// Act
		var record = new StepExecutionRecord
		{
			StartedAt = startedAt,
			CompletedAt = completedAt,
		};

		// Assert
		record.Duration.ShouldNotBeNull();
		record.Duration.Value.TotalSeconds.ShouldBeInRange(29.9, 30.1);
	}

	[Fact]
	public void ReturnNullDuration_WhenNotCompleted()
	{
		// Arrange & Act
		var record = new StepExecutionRecord
		{
			StartedAt = DateTime.UtcNow,
			CompletedAt = null,
		};

		// Assert
		record.Duration.ShouldBeNull();
	}

	[Fact]
	public void CalculateZeroDuration_WhenCompletedImmediately()
	{
		// Arrange
		var timestamp = DateTime.UtcNow;

		// Act
		var record = new StepExecutionRecord
		{
			StartedAt = timestamp,
			CompletedAt = timestamp,
		};

		// Assert
		record.Duration.ShouldBe(TimeSpan.Zero);
	}

	#endregion Duration Tests

	#region Comprehensive Record Tests

	[Fact]
	public void CreateSuccessfulStepRecord()
	{
		// Arrange
		var startedAt = DateTime.UtcNow.AddSeconds(-5);
		var completedAt = DateTime.UtcNow;

		// Act
		var record = new StepExecutionRecord
		{
			StepName = "ProcessPayment",
			StepIndex = 2,
			StartedAt = startedAt,
			CompletedAt = completedAt,
			IsSuccess = true,
			WasCompensated = false,
			RetryCount = 0,
		};

		// Assert
		record.IsSuccess.ShouldBeTrue();
		record.WasCompensated.ShouldBeFalse();
		record.ErrorMessage.ShouldBeNull();
		record.Duration.ShouldNotBeNull();
	}

	[Fact]
	public void CreateFailedStepRecord()
	{
		// Arrange
		var startedAt = DateTime.UtcNow.AddSeconds(-10);
		var completedAt = DateTime.UtcNow;

		// Act
		var record = new StepExecutionRecord
		{
			StepName = "ProcessPayment",
			StepIndex = 2,
			StartedAt = startedAt,
			CompletedAt = completedAt,
			IsSuccess = false,
			WasCompensated = false,
			ErrorMessage = "Payment gateway returned error 503",
			RetryCount = 3,
		};

		// Assert
		record.IsSuccess.ShouldBeFalse();
		record.ErrorMessage.ShouldNotBeNull();
		record.RetryCount.ShouldBe(3);
	}

	[Fact]
	public void CreateCompensatedStepRecord()
	{
		// Arrange
		var startedAt = DateTime.UtcNow.AddSeconds(-20);
		var completedAt = DateTime.UtcNow.AddSeconds(-15);

		// Act
		var record = new StepExecutionRecord
		{
			StepName = "ReserveInventory",
			StepIndex = 1,
			StartedAt = startedAt,
			CompletedAt = completedAt,
			IsSuccess = true,
			WasCompensated = true,
			RetryCount = 0,
		};

		// Assert
		record.IsSuccess.ShouldBeTrue();
		record.WasCompensated.ShouldBeTrue();
	}

	[Fact]
	public void CreateInProgressStepRecord()
	{
		// Arrange & Act
		var record = new StepExecutionRecord
		{
			StepName = "SendConfirmation",
			StepIndex = 4,
			StartedAt = DateTime.UtcNow,
			CompletedAt = null,
			IsSuccess = false,
			RetryCount = 0,
		};

		// Assert
		record.CompletedAt.ShouldBeNull();
		record.Duration.ShouldBeNull();
		record.IsSuccess.ShouldBeFalse();
	}

	#endregion Comprehensive Record Tests
}
