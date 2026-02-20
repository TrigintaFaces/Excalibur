// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Models;

namespace Excalibur.Saga.Tests.Core.Models;

/// <summary>
/// Unit tests for <see cref="CompensationStatus"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class CompensationStatusShould
{
	#region Enum Values Tests

	[Fact]
	public void HaveNotRequiredValue()
	{
		// Act
		var status = CompensationStatus.NotRequired;

		// Assert
		status.ShouldBe(CompensationStatus.NotRequired);
		((int)status).ShouldBe(0);
	}

	[Fact]
	public void HavePendingValue()
	{
		// Act
		var status = CompensationStatus.Pending;

		// Assert
		status.ShouldBe(CompensationStatus.Pending);
		((int)status).ShouldBe(1);
	}

	[Fact]
	public void HaveRunningValue()
	{
		// Act
		var status = CompensationStatus.Running;

		// Assert
		status.ShouldBe(CompensationStatus.Running);
		((int)status).ShouldBe(2);
	}

	[Fact]
	public void HaveSucceededValue()
	{
		// Act
		var status = CompensationStatus.Succeeded;

		// Assert
		status.ShouldBe(CompensationStatus.Succeeded);
		((int)status).ShouldBe(3);
	}

	[Fact]
	public void HaveFailedValue()
	{
		// Act
		var status = CompensationStatus.Failed;

		// Assert
		status.ShouldBe(CompensationStatus.Failed);
		((int)status).ShouldBe(4);
	}

	[Fact]
	public void HaveNotCompensableValue()
	{
		// Act
		var status = CompensationStatus.NotCompensable;

		// Assert
		status.ShouldBe(CompensationStatus.NotCompensable);
		((int)status).ShouldBe(5);
	}

	#endregion Enum Values Tests

	#region Default Value Tests

	[Fact]
	public void DefaultToNotRequired()
	{
		// Arrange & Act
		CompensationStatus status = default;

		// Assert
		status.ShouldBe(CompensationStatus.NotRequired);
	}

	#endregion Default Value Tests

	#region Conversion Tests

	[Fact]
	public void ConvertToString()
	{
		// Assert
		CompensationStatus.NotRequired.ToString().ShouldBe("NotRequired");
		CompensationStatus.Pending.ToString().ShouldBe("Pending");
		CompensationStatus.Running.ToString().ShouldBe("Running");
		CompensationStatus.Succeeded.ToString().ShouldBe("Succeeded");
		CompensationStatus.Failed.ToString().ShouldBe("Failed");
		CompensationStatus.NotCompensable.ToString().ShouldBe("NotCompensable");
	}

	[Fact]
	public void ParseFromString()
	{
		// Assert
		Enum.Parse<CompensationStatus>("NotRequired").ShouldBe(CompensationStatus.NotRequired);
		Enum.Parse<CompensationStatus>("Pending").ShouldBe(CompensationStatus.Pending);
		Enum.Parse<CompensationStatus>("Running").ShouldBe(CompensationStatus.Running);
		Enum.Parse<CompensationStatus>("Succeeded").ShouldBe(CompensationStatus.Succeeded);
		Enum.Parse<CompensationStatus>("Failed").ShouldBe(CompensationStatus.Failed);
		Enum.Parse<CompensationStatus>("NotCompensable").ShouldBe(CompensationStatus.NotCompensable);
	}

	#endregion Conversion Tests

	#region Usage Scenario Tests

	[Fact]
	public void RepresentNoCompensationNeeded()
	{
		// Arrange
		var state = new SagaStepState
		{
			Name = "Step1",
			CompensationStatus = CompensationStatus.NotRequired,
		};

		// Assert
		state.CompensationStatus.ShouldBe(CompensationStatus.NotRequired);
	}

	[Fact]
	public void RepresentCompensationPending()
	{
		// Arrange
		var state = new SagaStepState
		{
			Name = "Step1",
			CompensationStatus = CompensationStatus.Pending,
		};

		// Assert
		state.CompensationStatus.ShouldBe(CompensationStatus.Pending);
	}

	[Fact]
	public void RepresentCompensationInProgress()
	{
		// Arrange
		var state = new SagaStepState
		{
			Name = "Step1",
			CompensationStatus = CompensationStatus.Running,
			CompensationStartedAt = DateTime.UtcNow,
		};

		// Assert
		state.CompensationStatus.ShouldBe(CompensationStatus.Running);
	}

	[Fact]
	public void RepresentSuccessfulCompensation()
	{
		// Arrange
		var state = new SagaStepState
		{
			Name = "Step1",
			CompensationStatus = CompensationStatus.Succeeded,
			CompensationStartedAt = DateTime.UtcNow.AddSeconds(-5),
			CompensationCompletedAt = DateTime.UtcNow,
		};

		// Assert
		state.CompensationStatus.ShouldBe(CompensationStatus.Succeeded);
	}

	[Fact]
	public void RepresentFailedCompensation()
	{
		// Arrange
		var state = new SagaStepState
		{
			Name = "Step1",
			CompensationStatus = CompensationStatus.Failed,
			CompensationError = "Unable to rollback changes",
		};

		// Assert
		state.CompensationStatus.ShouldBe(CompensationStatus.Failed);
	}

	[Fact]
	public void RepresentNotCompensableStep()
	{
		// Arrange
		var state = new SagaStepState
		{
			Name = "Step1",
			Status = StepStatus.Succeeded,
			CompensationStatus = CompensationStatus.NotCompensable,
		};

		// Assert
		state.CompensationStatus.ShouldBe(CompensationStatus.NotCompensable);
	}

	#endregion Usage Scenario Tests
}
