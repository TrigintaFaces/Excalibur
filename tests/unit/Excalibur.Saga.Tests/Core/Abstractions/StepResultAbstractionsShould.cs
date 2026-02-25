// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using StepResultAbstractions = Excalibur.Saga.Abstractions.StepResult;

namespace Excalibur.Saga.Tests.Core.Abstractions;

/// <summary>
/// Unit tests for <see cref="Saga.Abstractions.StepResult"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class StepResultAbstractionsShould
{
	#region Default Values Tests

	[Fact]
	public void HaveFalseIsSuccessByDefault()
	{
		// Arrange & Act
		var result = new StepResultAbstractions();

		// Assert
		result.IsSuccess.ShouldBeFalse();
	}

	[Fact]
	public void HaveNullErrorMessageByDefault()
	{
		// Arrange & Act
		var result = new StepResultAbstractions();

		// Assert
		result.ErrorMessage.ShouldBeNull();
	}

	[Fact]
	public void HaveNullExceptionByDefault()
	{
		// Arrange & Act
		var result = new StepResultAbstractions();

		// Assert
		result.Exception.ShouldBeNull();
	}

	[Fact]
	public void HaveNullOutputDataByDefault()
	{
		// Arrange & Act
		var result = new StepResultAbstractions();

		// Assert
		result.OutputData.ShouldBeNull();
	}

	#endregion Default Values Tests

	#region Property Setting Tests

	[Fact]
	public void AllowIsSuccessToBeSet()
	{
		// Arrange & Act
		var result = new StepResultAbstractions { IsSuccess = true };

		// Assert
		result.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public void AllowErrorMessageToBeSet()
	{
		// Arrange & Act
		var result = new StepResultAbstractions { ErrorMessage = "Test error" };

		// Assert
		result.ErrorMessage.ShouldBe("Test error");
	}

	[Fact]
	public void AllowExceptionToBeSet()
	{
		// Arrange
		var exception = new InvalidOperationException("Test exception");

		// Act
		var result = new StepResultAbstractions { Exception = exception };

		// Assert
		result.Exception.ShouldBe(exception);
	}

	[Fact]
	public void AllowOutputDataToBeSet()
	{
		// Arrange
		var outputData = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["key"] = "value",
		};

		// Act
		var result = new StepResultAbstractions { OutputData = outputData };

		// Assert
		result.OutputData.ShouldNotBeNull();
		result.OutputData.ShouldContainKey("key");
	}

	#endregion Property Setting Tests

	#region Factory Method Tests

	[Fact]
	public void CreateSuccessResultWithoutOutputData()
	{
		// Act
		var result = StepResultAbstractions.Success();

		// Assert
		result.IsSuccess.ShouldBeTrue();
		result.ErrorMessage.ShouldBeNull();
		result.Exception.ShouldBeNull();
		result.OutputData.ShouldBeNull();
	}

	[Fact]
	public void CreateSuccessResultWithOutputData()
	{
		// Arrange
		var outputData = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["orderId"] = "ORD-123",
			["status"] = "confirmed",
		};

		// Act
		var result = StepResultAbstractions.Success(outputData);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		result.OutputData.ShouldNotBeNull();
		result.OutputData["orderId"].ShouldBe("ORD-123");
	}

	[Fact]
	public void CreateSuccessResultWithNullOutputData()
	{
		// Act
		var result = StepResultAbstractions.Success(null);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		result.OutputData.ShouldBeNull();
	}

	[Fact]
	public void CreateFailureResultWithErrorMessage()
	{
		// Act
		var result = StepResultAbstractions.Failure("Operation failed");

		// Assert
		result.IsSuccess.ShouldBeFalse();
		result.ErrorMessage.ShouldBe("Operation failed");
		result.Exception.ShouldBeNull();
	}

	[Fact]
	public void CreateFailureResultWithErrorMessageAndException()
	{
		// Arrange
		var exception = new TimeoutException("Request timed out");

		// Act
		var result = StepResultAbstractions.Failure("Service timeout", exception);

		// Assert
		result.IsSuccess.ShouldBeFalse();
		result.ErrorMessage.ShouldBe("Service timeout");
		result.Exception.ShouldBe(exception);
	}

	[Fact]
	public void CreateFailureResultWithNullException()
	{
		// Act
		var result = StepResultAbstractions.Failure("Error message", null);

		// Assert
		result.IsSuccess.ShouldBeFalse();
		result.ErrorMessage.ShouldBe("Error message");
		result.Exception.ShouldBeNull();
	}

	#endregion Factory Method Tests

	#region Immutability Tests

	[Fact]
	public void BeImmutableAfterCreation()
	{
		// Arrange
		var result = StepResultAbstractions.Success();

		// Assert - properties are init-only
		result.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public void PreserveOutputDataReference()
	{
		// Arrange
		var outputData = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["key1"] = "value1",
		};

		// Act
		var result = StepResultAbstractions.Success(outputData);

		// Modify original dictionary
		outputData["key2"] = "value2";

		// Assert - the result shares the same reference
		result.OutputData.Count.ShouldBe(2);
	}

	#endregion Immutability Tests

	#region Scenario Tests

	[Fact]
	public void RepresentSuccessfulDatabaseOperation()
	{
		// Arrange
		var outputData = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["rowsAffected"] = 5,
			["executionTime"] = TimeSpan.FromMilliseconds(150),
		};

		// Act
		var result = StepResultAbstractions.Success(outputData);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		result.OutputData["rowsAffected"].ShouldBe(5);
	}

	[Fact]
	public void RepresentFailedValidation()
	{
		// Act
		var result = StepResultAbstractions.Failure("Invalid email format");

		// Assert
		result.IsSuccess.ShouldBeFalse();
		result.ErrorMessage.ShouldContain("Invalid");
		result.Exception.ShouldBeNull();
	}

	[Fact]
	public void RepresentExceptionDuringExecution()
	{
		// Arrange
		var exception = new InvalidOperationException("State machine in invalid state")
		{
			Data = { ["currentState"] = "Processing" },
		};

		// Act
		var result = StepResultAbstractions.Failure("State error occurred", exception);

		// Assert
		result.IsSuccess.ShouldBeFalse();
		result.Exception.ShouldBeOfType<InvalidOperationException>();
		result.Exception.Data["currentState"].ShouldBe("Processing");
	}

	[Fact]
	public void HandleEmptyOutputData()
	{
		// Arrange
		var outputData = new Dictionary<string, object>(StringComparer.Ordinal);

		// Act
		var result = StepResultAbstractions.Success(outputData);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		result.OutputData.ShouldNotBeNull();
		result.OutputData.ShouldBeEmpty();
	}

	#endregion Scenario Tests
}
