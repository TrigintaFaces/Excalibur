// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Abstractions;

namespace Excalibur.Saga.Tests.Core.Abstractions;

/// <summary>
/// Unit tests for <see cref="CompensationResult"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class CompensationResultShould
{
	#region Default Values Tests

	[Fact]
	public void HaveIsSuccessFalseByDefault()
	{
		// Arrange & Act
		var result = new CompensationResult();

		// Assert
		result.IsSuccess.ShouldBeFalse();
	}

	[Fact]
	public void HaveZeroStepsCompensatedByDefault()
	{
		// Arrange & Act
		var result = new CompensationResult();

		// Assert
		result.StepsCompensated.ShouldBe(0);
	}

	[Fact]
	public void HaveNullErrorMessageByDefault()
	{
		// Arrange & Act
		var result = new CompensationResult();

		// Assert
		result.ErrorMessage.ShouldBeNull();
	}

	[Fact]
	public void HaveNullExceptionByDefault()
	{
		// Arrange & Act
		var result = new CompensationResult();

		// Assert
		result.Exception.ShouldBeNull();
	}

	[Fact]
	public void HaveZeroDurationByDefault()
	{
		// Arrange & Act
		var result = new CompensationResult();

		// Assert
		result.Duration.ShouldBe(TimeSpan.Zero);
	}

	#endregion Default Values Tests

	#region Property Setting Tests

	[Fact]
	public void AllowIsSuccessToBeInitialized()
	{
		// Arrange & Act
		var result = new CompensationResult { IsSuccess = true };

		// Assert
		result.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public void AllowStepsCompensatedToBeInitialized()
	{
		// Arrange & Act
		var result = new CompensationResult { StepsCompensated = 5 };

		// Assert
		result.StepsCompensated.ShouldBe(5);
	}

	[Fact]
	public void AllowErrorMessageToBeInitialized()
	{
		// Arrange & Act
		var result = new CompensationResult { ErrorMessage = "Compensation failed at step 3" };

		// Assert
		result.ErrorMessage.ShouldBe("Compensation failed at step 3");
	}

	[Fact]
	public void AllowExceptionToBeInitialized()
	{
		// Arrange
		var exception = new InvalidOperationException("Test exception");

		// Act
		var result = new CompensationResult { Exception = exception };

		// Assert
		result.Exception.ShouldBeSameAs(exception);
	}

	[Fact]
	public void AllowDurationToBeInitialized()
	{
		// Arrange & Act
		var result = new CompensationResult { Duration = TimeSpan.FromSeconds(15) };

		// Assert
		result.Duration.ShouldBe(TimeSpan.FromSeconds(15));
	}

	#endregion Property Setting Tests

	#region Comprehensive Result Tests

	[Fact]
	public void CreateSuccessfulCompensationResult()
	{
		// Arrange & Act
		var result = new CompensationResult
		{
			IsSuccess = true,
			StepsCompensated = 3,
			Duration = TimeSpan.FromSeconds(5),
		};

		// Assert
		result.IsSuccess.ShouldBeTrue();
		result.StepsCompensated.ShouldBe(3);
		result.ErrorMessage.ShouldBeNull();
		result.Exception.ShouldBeNull();
	}

	[Fact]
	public void CreateFailedCompensationResult()
	{
		// Arrange
		var exception = new InvalidOperationException("Database unavailable");

		// Act
		var result = new CompensationResult
		{
			IsSuccess = false,
			StepsCompensated = 2,
			ErrorMessage = "Failed to compensate step 3",
			Exception = exception,
			Duration = TimeSpan.FromSeconds(3),
		};

		// Assert
		result.IsSuccess.ShouldBeFalse();
		result.StepsCompensated.ShouldBe(2);
		result.ErrorMessage.ShouldBe("Failed to compensate step 3");
		result.Exception.ShouldBeSameAs(exception);
	}

	[Fact]
	public void CreatePartialCompensationResult()
	{
		// Arrange & Act
		var result = new CompensationResult
		{
			IsSuccess = false,
			StepsCompensated = 4,
			ErrorMessage = "3 of 7 steps compensated before failure",
			Duration = TimeSpan.FromSeconds(10),
		};

		// Assert
		result.IsSuccess.ShouldBeFalse();
		result.StepsCompensated.ShouldBe(4);
		result.ErrorMessage.ShouldContain("steps compensated");
	}

	#endregion Comprehensive Result Tests
}
