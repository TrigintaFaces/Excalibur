// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Abstractions;

namespace Excalibur.Saga.Tests.Abstractions;

/// <summary>
/// Unit tests for <see cref="SagaSummary"/>.
/// Verifies saga summary model behavior.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class SagaSummaryShould
{
	#region Default Value Tests

	[Fact]
	public void HaveEmptyStringDefaults()
	{
		// Act
		var summary = new SagaSummary();

		// Assert
		summary.SagaId.ShouldBe(string.Empty);
		summary.SagaType.ShouldBe(string.Empty);
	}

	[Fact]
	public void HaveDefaultState()
	{
		// Act
		var summary = new SagaSummary();

		// Assert
		summary.State.ShouldBe(default);
	}

	[Fact]
	public void HaveDefaultStartedAt()
	{
		// Act
		var summary = new SagaSummary();

		// Assert
		summary.StartedAt.ShouldBe(default);
	}

	[Fact]
	public void HaveNullCompletedAt()
	{
		// Act
		var summary = new SagaSummary();

		// Assert
		summary.CompletedAt.ShouldBeNull();
	}

	[Fact]
	public void HaveZeroCurrentStep()
	{
		// Act
		var summary = new SagaSummary();

		// Assert
		summary.CurrentStep.ShouldBe(0);
	}

	[Fact]
	public void HaveZeroTotalSteps()
	{
		// Act
		var summary = new SagaSummary();

		// Assert
		summary.TotalSteps.ShouldBe(0);
	}

	#endregion

	#region Init Property Tests

	[Fact]
	public void AllowSagaIdToBeInitialized()
	{
		// Act
		var summary = new SagaSummary { SagaId = "saga-123" };

		// Assert
		summary.SagaId.ShouldBe("saga-123");
	}

	[Fact]
	public void AllowSagaTypeToBeInitialized()
	{
		// Act
		var summary = new SagaSummary { SagaType = "OrderSaga" };

		// Assert
		summary.SagaType.ShouldBe("OrderSaga");
	}

	[Fact]
	public void AllowStateToBeInitialized()
	{
		// Act
		var summary = new SagaSummary { State = SagaState.Running };

		// Assert
		summary.State.ShouldBe(SagaState.Running);
	}

	[Fact]
	public void AllowStartedAtToBeInitialized()
	{
		// Arrange
		var startTime = new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero);

		// Act
		var summary = new SagaSummary { StartedAt = startTime };

		// Assert
		summary.StartedAt.ShouldBe(startTime);
	}

	[Fact]
	public void AllowCompletedAtToBeInitialized()
	{
		// Arrange
		var completedTime = new DateTimeOffset(2026, 1, 15, 11, 0, 0, TimeSpan.Zero);

		// Act
		var summary = new SagaSummary { CompletedAt = completedTime };

		// Assert
		summary.CompletedAt.ShouldBe(completedTime);
	}

	[Fact]
	public void AllowCurrentStepToBeInitialized()
	{
		// Act
		var summary = new SagaSummary { CurrentStep = 3 };

		// Assert
		summary.CurrentStep.ShouldBe(3);
	}

	[Fact]
	public void AllowTotalStepsToBeInitialized()
	{
		// Act
		var summary = new SagaSummary { TotalSteps = 5 };

		// Assert
		summary.TotalSteps.ShouldBe(5);
	}

	#endregion

	#region Object Initialization Tests

	[Fact]
	public void SupportFullObjectInitialization()
	{
		// Arrange
		var startTime = new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);
		var completedTime = new DateTimeOffset(2026, 1, 15, 11, 0, 0, TimeSpan.Zero);

		// Act
		var summary = new SagaSummary
		{
			SagaId = "saga-complete",
			SagaType = "OrderProcessingSaga",
			State = SagaState.Completed,
			StartedAt = startTime,
			CompletedAt = completedTime,
			CurrentStep = 5,
			TotalSteps = 5
		};

		// Assert
		summary.SagaId.ShouldBe("saga-complete");
		summary.SagaType.ShouldBe("OrderProcessingSaga");
		summary.State.ShouldBe(SagaState.Completed);
		summary.StartedAt.ShouldBe(startTime);
		summary.CompletedAt.ShouldBe(completedTime);
		summary.CurrentStep.ShouldBe(5);
		summary.TotalSteps.ShouldBe(5);
	}

	[Fact]
	public void SupportPartialObjectInitialization()
	{
		// Act
		var summary = new SagaSummary
		{
			SagaId = "saga-partial",
			State = SagaState.Running
		};

		// Assert
		summary.SagaId.ShouldBe("saga-partial");
		summary.SagaType.ShouldBe(string.Empty);
		summary.State.ShouldBe(SagaState.Running);
		summary.CompletedAt.ShouldBeNull();
	}

	#endregion

	#region SagaState Enum Coverage

	[Theory]
	[InlineData(SagaState.Created)]
	[InlineData(SagaState.Running)]
	[InlineData(SagaState.Compensating)]
	[InlineData(SagaState.Completed)]
	[InlineData(SagaState.CompensatedSuccessfully)]
	[InlineData(SagaState.CompensationFailed)]
	[InlineData(SagaState.Cancelled)]
	public void AcceptAllSagaStateValues(SagaState state)
	{
		// Act
		var summary = new SagaSummary { State = state };

		// Assert
		summary.State.ShouldBe(state);
	}

	#endregion
}
