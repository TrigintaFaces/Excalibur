// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Abstractions;

namespace Excalibur.Saga.Tests.Core.Abstractions;

/// <summary>
/// Unit tests for <see cref="SagaResult{TSagaData}"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class SagaResultShould
{
	#region Default Values Tests

	[Fact]
	public void HaveEmptySagaIdByDefault()
	{
		// Arrange & Act
		var result = new SagaResult<TestSagaData>();

		// Assert
		result.SagaId.ShouldBe(string.Empty);
	}

	[Fact]
	public void HaveDefaultFinalState()
	{
		// Arrange & Act
		var result = new SagaResult<TestSagaData>();

		// Assert
		result.FinalState.ShouldBe(default(SagaState));
	}

	[Fact]
	public void HaveEmptyActivitiesListByDefault()
	{
		// Arrange & Act
		var result = new SagaResult<TestSagaData>();

		// Assert
		result.Activities.ShouldBeEmpty();
	}

	[Fact]
	public void HaveNullErrorMessageByDefault()
	{
		// Arrange & Act
		var result = new SagaResult<TestSagaData>();

		// Assert
		result.ErrorMessage.ShouldBeNull();
	}

	[Fact]
	public void HaveNullExceptionByDefault()
	{
		// Arrange & Act
		var result = new SagaResult<TestSagaData>();

		// Assert
		result.Exception.ShouldBeNull();
	}

	[Fact]
	public void HaveZeroDurationByDefault()
	{
		// Arrange & Act
		var result = new SagaResult<TestSagaData>();

		// Assert
		result.Duration.ShouldBe(TimeSpan.Zero);
	}

	#endregion Default Values Tests

	#region IsSuccess Tests

	[Fact]
	public void ReportIsSuccess_WhenFinalStateIsCompleted()
	{
		// Arrange & Act
		var result = new SagaResult<TestSagaData>
		{
			FinalState = SagaState.Completed,
		};

		// Assert
		result.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public void ReportIsNotSuccess_WhenFinalStateIsCompensationFailed()
	{
		// Arrange & Act
		var result = new SagaResult<TestSagaData>
		{
			FinalState = SagaState.CompensationFailed,
		};

		// Assert
		result.IsSuccess.ShouldBeFalse();
	}

	[Fact]
	public void ReportIsNotSuccess_WhenFinalStateIsCompensatedSuccessfully()
	{
		// Arrange & Act
		var result = new SagaResult<TestSagaData>
		{
			FinalState = SagaState.CompensatedSuccessfully,
		};

		// Assert
		result.IsSuccess.ShouldBeFalse();
	}

	[Fact]
	public void ReportIsNotSuccess_WhenFinalStateIsCreated()
	{
		// Arrange & Act
		var result = new SagaResult<TestSagaData>
		{
			FinalState = SagaState.Created,
		};

		// Assert
		result.IsSuccess.ShouldBeFalse();
	}

	[Fact]
	public void ReportIsNotSuccess_WhenFinalStateIsRunning()
	{
		// Arrange & Act
		var result = new SagaResult<TestSagaData>
		{
			FinalState = SagaState.Running,
		};

		// Assert
		result.IsSuccess.ShouldBeFalse();
	}

	[Fact]
	public void ReportIsNotSuccess_WhenFinalStateIsCompensating()
	{
		// Arrange & Act
		var result = new SagaResult<TestSagaData>
		{
			FinalState = SagaState.Compensating,
		};

		// Assert
		result.IsSuccess.ShouldBeFalse();
	}

	[Fact]
	public void ReportIsNotSuccess_WhenFinalStateIsCancelled()
	{
		// Arrange & Act
		var result = new SagaResult<TestSagaData>
		{
			FinalState = SagaState.Cancelled,
		};

		// Assert
		result.IsSuccess.ShouldBeFalse();
	}

	#endregion IsSuccess Tests

	#region Property Setting Tests

	[Fact]
	public void AllowSagaIdToBeInitialized()
	{
		// Arrange & Act
		var result = new SagaResult<TestSagaData>
		{
			SagaId = "saga-123",
		};

		// Assert
		result.SagaId.ShouldBe("saga-123");
	}

	[Fact]
	public void AllowDataToBeInitialized()
	{
		// Arrange
		var data = new TestSagaData { OrderId = "order-456" };

		// Act
		var result = new SagaResult<TestSagaData>
		{
			Data = data,
		};

		// Assert
		result.Data.ShouldBeSameAs(data);
		result.Data.OrderId.ShouldBe("order-456");
	}

	[Fact]
	public void AllowErrorMessageToBeInitialized()
	{
		// Arrange & Act
		var result = new SagaResult<TestSagaData>
		{
			ErrorMessage = "Something went wrong",
		};

		// Assert
		result.ErrorMessage.ShouldBe("Something went wrong");
	}

	[Fact]
	public void AllowExceptionToBeInitialized()
	{
		// Arrange
		var exception = new InvalidOperationException("Test exception");

		// Act
		var result = new SagaResult<TestSagaData>
		{
			Exception = exception,
		};

		// Assert
		result.Exception.ShouldBeSameAs(exception);
	}

	[Fact]
	public void AllowDurationToBeInitialized()
	{
		// Arrange & Act
		var result = new SagaResult<TestSagaData>
		{
			Duration = TimeSpan.FromSeconds(45),
		};

		// Assert
		result.Duration.ShouldBe(TimeSpan.FromSeconds(45));
	}

	[Fact]
	public void AllowActivitiesToBeInitialized()
	{
		// Arrange
		var activities = new List<SagaActivity>
		{
			new() { Message = "Step 1 completed", Timestamp = DateTimeOffset.UtcNow },
			new() { Message = "Step 2 completed", Timestamp = DateTimeOffset.UtcNow },
		};

		// Act
		var result = new SagaResult<TestSagaData>
		{
			Activities = activities,
		};

		// Assert
		result.Activities.Count.ShouldBe(2);
	}

	#endregion Property Setting Tests

	#region Comprehensive Result Tests

	[Fact]
	public void CreateSuccessfulResult()
	{
		// Arrange
		var data = new TestSagaData { OrderId = "order-789" };

		// Act
		var result = new SagaResult<TestSagaData>
		{
			SagaId = "saga-success-1",
			FinalState = SagaState.Completed,
			Data = data,
			Duration = TimeSpan.FromSeconds(30),
			Activities =
			[
				new() { Message = "Order validated" },
				new() { Message = "Payment processed" },
			],
		};

		// Assert
		result.IsSuccess.ShouldBeTrue();
		result.SagaId.ShouldBe("saga-success-1");
		result.FinalState.ShouldBe(SagaState.Completed);
		result.Data.OrderId.ShouldBe("order-789");
		result.ErrorMessage.ShouldBeNull();
		result.Exception.ShouldBeNull();
		result.Activities.Count.ShouldBe(2);
	}

	[Fact]
	public void CreateCompensationFailedResult()
	{
		// Arrange
		var exception = new InvalidOperationException("Payment failed");

		// Act
		var result = new SagaResult<TestSagaData>
		{
			SagaId = "saga-failed-1",
			FinalState = SagaState.CompensationFailed,
			ErrorMessage = "Payment processing failed",
			Exception = exception,
			Duration = TimeSpan.FromSeconds(10),
		};

		// Assert
		result.IsSuccess.ShouldBeFalse();
		result.FinalState.ShouldBe(SagaState.CompensationFailed);
		result.ErrorMessage.ShouldBe("Payment processing failed");
		result.Exception.ShouldBeSameAs(exception);
	}

	[Fact]
	public void CreateCompensatedSuccessfullyResult()
	{
		// Arrange & Act
		var result = new SagaResult<TestSagaData>
		{
			SagaId = "saga-compensated-1",
			FinalState = SagaState.CompensatedSuccessfully,
			ErrorMessage = "Saga rolled back due to failure",
			Activities =
			[
				new() { Message = "Order validated" },
				new() { Message = "Payment failed" },
				new() { Message = "Order validation compensated" },
			],
		};

		// Assert
		result.IsSuccess.ShouldBeFalse();
		result.FinalState.ShouldBe(SagaState.CompensatedSuccessfully);
		result.Activities.Count.ShouldBe(3);
	}

	#endregion Comprehensive Result Tests

	/// <summary>
	/// Test saga data class.
	/// </summary>
	private sealed class TestSagaData
	{
		public string OrderId { get; set; } = string.Empty;
	}
}
