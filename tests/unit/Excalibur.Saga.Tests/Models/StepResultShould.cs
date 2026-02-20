// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Models;

namespace Excalibur.Saga.Tests.Models;

/// <summary>
/// Unit tests for <see cref="StepResult"/> (Models namespace).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class StepResultShould
{
	#region Default Values Tests

	[Fact]
	public void HaveFalseIsSuccessByDefault()
	{
		// Arrange & Act
		var result = new StepResult();

		// Assert
		result.IsSuccess.ShouldBeFalse();
	}

	[Fact]
	public void HaveNullErrorMessageByDefault()
	{
		// Arrange & Act
		var result = new StepResult();

		// Assert
		result.ErrorMessage.ShouldBeNull();
	}

	[Fact]
	public void HaveNullExceptionByDefault()
	{
		// Arrange & Act
		var result = new StepResult();

		// Assert
		result.Exception.ShouldBeNull();
	}

	[Fact]
	public void HaveFalseShouldRetryByDefault()
	{
		// Arrange & Act
		var result = new StepResult();

		// Assert
		result.ShouldRetry.ShouldBeFalse();
	}

	[Fact]
	public void HaveNullRetryDelayByDefault()
	{
		// Arrange & Act
		var result = new StepResult();

		// Assert
		result.RetryDelay.ShouldBeNull();
	}

	[Fact]
	public void HaveEmptyOutputDataByDefault()
	{
		// Arrange & Act
		var result = new StepResult();

		// Assert
		result.OutputData.ShouldNotBeNull();
		result.OutputData.ShouldBeEmpty();
	}

	#endregion Default Values Tests

	#region Property Setting Tests

	[Fact]
	public void AllowIsSuccessToBeSet()
	{
		// Arrange & Act
		var result = new StepResult { IsSuccess = true };

		// Assert
		result.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public void AllowErrorMessageToBeSet()
	{
		// Arrange & Act
		var result = new StepResult { ErrorMessage = "Something went wrong" };

		// Assert
		result.ErrorMessage.ShouldBe("Something went wrong");
	}

	[Fact]
	public void AllowExceptionToBeSet()
	{
		// Arrange
		var exception = new InvalidOperationException("Test exception");

		// Act
		var result = new StepResult { Exception = exception };

		// Assert
		result.Exception.ShouldBe(exception);
	}

	[Fact]
	public void AllowShouldRetryToBeSet()
	{
		// Arrange & Act
		var result = new StepResult { ShouldRetry = true };

		// Assert
		result.ShouldRetry.ShouldBeTrue();
	}

	[Fact]
	public void AllowRetryDelayToBeSet()
	{
		// Arrange & Act
		var result = new StepResult { RetryDelay = TimeSpan.FromSeconds(5) };

		// Assert
		result.RetryDelay.ShouldBe(TimeSpan.FromSeconds(5));
	}

	[Fact]
	public void AllowOutputDataToBePopulated()
	{
		// Arrange
		var result = new StepResult();

		// Act
		result.OutputData["key1"] = "value1";
		result.OutputData["key2"] = 42;

		// Assert
		result.OutputData.ShouldContainKey("key1");
		result.OutputData["key1"].ShouldBe("value1");
		result.OutputData["key2"].ShouldBe(42);
	}

	#endregion Property Setting Tests

	#region Factory Method Tests

	[Fact]
	public void CreateSuccessResult()
	{
		// Act
		var result = StepResult.Success();

		// Assert
		result.IsSuccess.ShouldBeTrue();
		result.ErrorMessage.ShouldBeNull();
		result.Exception.ShouldBeNull();
		result.ShouldRetry.ShouldBeFalse();
	}

	[Fact]
	public void CreateSuccessResultWithOutputData()
	{
		// Arrange
		var outputData = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["orderId"] = "ORD-123",
			["amount"] = 99.99m,
		};

		// Act
		var result = StepResult.Success(outputData);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		result.OutputData.ShouldContainKey("orderId");
		result.OutputData["orderId"].ShouldBe("ORD-123");
		result.OutputData["amount"].ShouldBe(99.99m);
	}

	[Fact]
	public void CreateSuccessResultWithOutputDataThrowsOnNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => StepResult.Success(null!));
	}

	[Fact]
	public void CreateFailureResult()
	{
		// Act
		var result = StepResult.Failure("Payment declined");

		// Assert
		result.IsSuccess.ShouldBeFalse();
		result.ErrorMessage.ShouldBe("Payment declined");
		result.Exception.ShouldBeNull();
		result.ShouldRetry.ShouldBeFalse();
	}

	[Fact]
	public void CreateFailureResultWithException()
	{
		// Arrange
		var exception = new TimeoutException("Connection timed out");

		// Act
		var result = StepResult.Failure("Service unavailable", exception);

		// Assert
		result.IsSuccess.ShouldBeFalse();
		result.ErrorMessage.ShouldBe("Service unavailable");
		result.Exception.ShouldBe(exception);
		result.Exception.ShouldBeOfType<TimeoutException>();
	}

	[Fact]
	public void CreateRetryResult()
	{
		// Act
		var result = StepResult.Retry(TimeSpan.FromSeconds(30), "Rate limit exceeded");

		// Assert
		result.IsSuccess.ShouldBeFalse();
		result.ShouldRetry.ShouldBeTrue();
		result.RetryDelay.ShouldBe(TimeSpan.FromSeconds(30));
		result.ErrorMessage.ShouldBe("Rate limit exceeded");
	}

	[Fact]
	public void CreateRetryResultWithMinimalDelay()
	{
		// Act
		var result = StepResult.Retry(TimeSpan.FromMilliseconds(100), "Temporary failure");

		// Assert
		result.ShouldRetry.ShouldBeTrue();
		result.RetryDelay.ShouldBe(TimeSpan.FromMilliseconds(100));
	}

	[Fact]
	public void CreateRetryResultWithLongDelay()
	{
		// Act
		var result = StepResult.Retry(TimeSpan.FromMinutes(5), "Service maintenance");

		// Assert
		result.ShouldRetry.ShouldBeTrue();
		result.RetryDelay.ShouldBe(TimeSpan.FromMinutes(5));
	}

	#endregion Factory Method Tests

	#region Comprehensive Scenario Tests

	[Fact]
	public void RepresentPaymentSuccessScenario()
	{
		// Arrange & Act
		var outputData = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["transactionId"] = "TXN-98765",
			["chargedAmount"] = 150.00m,
			["timestamp"] = DateTimeOffset.UtcNow,
		};

		var result = StepResult.Success(outputData);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		result.OutputData["transactionId"].ShouldBe("TXN-98765");
	}

	[Fact]
	public void RepresentPaymentFailureScenario()
	{
		// Arrange
		var exception = new InvalidOperationException("Insufficient funds");

		// Act
		var result = StepResult.Failure("Payment failed: Insufficient funds", exception);

		// Assert
		result.IsSuccess.ShouldBeFalse();
		result.ErrorMessage.ShouldContain("Insufficient funds");
		result.Exception.ShouldNotBeNull();
	}

	[Fact]
	public void RepresentNetworkRetryScenario()
	{
		// Act
		var result = StepResult.Retry(
			TimeSpan.FromSeconds(10),
			"Network timeout, will retry");

		// Assert
		result.ShouldRetry.ShouldBeTrue();
		result.RetryDelay!.Value.TotalSeconds.ShouldBe(10);
	}

	[Fact]
	public void OutputDataIsCaseSensitive()
	{
		// Arrange
		var result = new StepResult();

		// Act
		result.OutputData["Key"] = "value1";
		result.OutputData["key"] = "value2";

		// Assert - keys are different due to Ordinal comparer
		result.OutputData.Count.ShouldBe(2);
		result.OutputData["Key"].ShouldBe("value1");
		result.OutputData["key"].ShouldBe("value2");
	}

	[Fact]
	public void AllowMultipleOutputDataEntries()
	{
		// Arrange
		var outputData = new Dictionary<string, object>(StringComparer.Ordinal);
		for (var i = 0; i < 100; i++)
		{
			outputData[$"key_{i}"] = i;
		}

		// Act
		var result = StepResult.Success(outputData);

		// Assert
		result.OutputData.Count.ShouldBe(100);
	}

	#endregion Comprehensive Scenario Tests
}
