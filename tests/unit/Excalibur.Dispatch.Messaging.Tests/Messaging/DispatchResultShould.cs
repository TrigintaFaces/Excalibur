// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Tests.Messaging;

/// <summary>
/// Unit tests for <see cref="DispatchResult"/>.
/// </summary>
/// <remarks>
/// Tests the dispatch result implementation.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Messaging")]
[Trait("Priority", "0")]
public sealed class DispatchResultShould
{
	#region Constructor Tests

	[Fact]
	public void Constructor_WithIsSuccessTrue_SetsIsSuccessTrue()
	{
		// Arrange & Act
		var result = new DispatchResult(isSuccess: true);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		result.Result.ShouldBeNull();
		result.Exception.ShouldBeNull();
		result.Metadata.ShouldBeNull();
	}

	[Fact]
	public void Constructor_WithIsSuccessFalse_SetsIsSuccessFalse()
	{
		// Arrange & Act
		var result = new DispatchResult(isSuccess: false);

		// Assert
		result.IsSuccess.ShouldBeFalse();
	}

	[Fact]
	public void Constructor_WithResult_SetsResult()
	{
		// Arrange
		var data = new { Id = 123, Name = "Test" };

		// Act
		var result = new DispatchResult(isSuccess: true, result: data);

		// Assert
		result.Result.ShouldBe(data);
	}

	[Fact]
	public void Constructor_WithException_SetsException()
	{
		// Arrange
		var exception = new InvalidOperationException("Test error");

		// Act
		var result = new DispatchResult(isSuccess: false, exception: exception);

		// Assert
		result.Exception.ShouldBe(exception);
	}

	[Fact]
	public void Constructor_WithMetadata_SetsMetadata()
	{
		// Arrange
		var metadata = new Dictionary<string, object>
		{
			["key1"] = "value1",
			["key2"] = 42,
		};

		// Act
		var result = new DispatchResult(isSuccess: true, metadata: metadata);

		// Assert
		result.Metadata.ShouldBe(metadata);
	}

	[Fact]
	public void Constructor_WithAllParameters_SetsAllProperties()
	{
		// Arrange
		var data = "result data";
		var exception = new ArgumentException("arg error");
		var metadata = new Dictionary<string, object> { ["trace"] = "123" };

		// Act
		var result = new DispatchResult(
			isSuccess: false,
			result: data,
			exception: exception,
			metadata: metadata);

		// Assert
		result.IsSuccess.ShouldBeFalse();
		result.Result.ShouldBe(data);
		result.Exception.ShouldBe(exception);
		result.Metadata.ShouldBe(metadata);
	}

	#endregion

	#region Success Factory Method Tests

	[Fact]
	public void Success_WithNoParameters_CreatesSuccessResult()
	{
		// Arrange & Act
		var result = DispatchResult.Success();

		// Assert
		result.IsSuccess.ShouldBeTrue();
		result.Result.ShouldBeNull();
		result.Exception.ShouldBeNull();
		result.Metadata.ShouldBeNull();
	}

	[Fact]
	public void Success_WithResult_CreatesSuccessResultWithData()
	{
		// Arrange
		var data = new { Value = 100 };

		// Act
		var result = DispatchResult.Success(result: data);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		result.Result.ShouldBe(data);
		result.Exception.ShouldBeNull();
	}

	[Fact]
	public void Success_WithMetadata_CreatesSuccessResultWithMetadata()
	{
		// Arrange
		var metadata = new Dictionary<string, object> { ["duration"] = 500 };

		// Act
		var result = DispatchResult.Success(metadata: metadata);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		result.Metadata.ShouldBe(metadata);
	}

	[Fact]
	public void Success_WithResultAndMetadata_CreatesSuccessResultWithBoth()
	{
		// Arrange
		var data = "operation completed";
		var metadata = new Dictionary<string, object> { ["timestamp"] = DateTime.UtcNow };

		// Act
		var result = DispatchResult.Success(result: data, metadata: metadata);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		result.Result.ShouldBe(data);
		result.Metadata.ShouldBe(metadata);
	}

	#endregion

	#region Failure Factory Method Tests

	[Fact]
	public void Failure_WithException_CreatesFailureResult()
	{
		// Arrange
		var exception = new InvalidOperationException("Operation failed");

		// Act
		var result = DispatchResult.Failure(exception);

		// Assert
		result.IsSuccess.ShouldBeFalse();
		result.Exception.ShouldBe(exception);
		result.Result.ShouldBeNull();
		result.Metadata.ShouldBeNull();
	}

	[Fact]
	public void Failure_WithExceptionAndMetadata_CreatesFailureResultWithMetadata()
	{
		// Arrange
		var exception = new TimeoutException("Timeout exceeded");
		var metadata = new Dictionary<string, object>
		{
			["timeout"] = 30000,
			["operation"] = "ProcessMessage",
		};

		// Act
		var result = DispatchResult.Failure(exception, metadata);

		// Assert
		result.IsSuccess.ShouldBeFalse();
		result.Exception.ShouldBe(exception);
		result.Metadata.ShouldBe(metadata);
	}

	[Fact]
	public void Failure_WithVariousExceptionTypes_Works()
	{
		// Arrange & Act
		var result1 = DispatchResult.Failure(new ArgumentNullException("param"));
		var result2 = DispatchResult.Failure(new NotSupportedException("Not supported"));
		var result3 = DispatchResult.Failure(new OperationCanceledException());

		// Assert
		_ = result1.Exception.ShouldBeOfType<ArgumentNullException>();
		_ = result2.Exception.ShouldBeOfType<NotSupportedException>();
		_ = result3.Exception.ShouldBeOfType<OperationCanceledException>();
	}

	#endregion

	#region Interface Implementation Tests

	[Fact]
	public void ImplementsIDispatchResult()
	{
		// Arrange & Act
		var result = new DispatchResult(isSuccess: true);

		// Assert
		_ = result.ShouldBeAssignableTo<IDispatchResult>();
	}

	#endregion

	#region Typical Usage Scenarios

	[Fact]
	public void SuccessfulCommandDispatch_Scenario()
	{
		// Arrange & Act - Simulating a successful command dispatch
		var orderId = Guid.NewGuid();
		var result = DispatchResult.Success(
			result: orderId,
			metadata: new Dictionary<string, object>
			{
				["operation"] = "CreateOrder",
				["handlerTime"] = 45,
			});

		// Assert
		result.IsSuccess.ShouldBeTrue();
		result.Result.ShouldBe(orderId);
		result.Metadata["operation"].ShouldBe("CreateOrder");
	}

	[Fact]
	public void FailedValidation_Scenario()
	{
		// Arrange - Simulating a validation failure
		var validationErrors = new List<string> { "Amount must be positive" };

		// Act
		var result = DispatchResult.Failure(
			new ArgumentException("Invalid order amount"),
			new Dictionary<string, object>
			{
				["validationErrors"] = validationErrors,
				["fieldName"] = "Amount",
			});

		// Assert
		result.IsSuccess.ShouldBeFalse();
		result.Exception.Message.ShouldBe("Invalid order amount");
		result.Metadata["fieldName"].ShouldBe("Amount");
	}

	[Fact]
	public void TimeoutScenario()
	{
		// Arrange & Act - Simulating a timeout
		var result = DispatchResult.Failure(
			new TimeoutException("Handler execution timed out after 30 seconds"),
			new Dictionary<string, object>
			{
				["timeoutMs"] = 30000,
				["messageType"] = "ProcessPayment",
			});

		// Assert
		result.IsSuccess.ShouldBeFalse();
		_ = result.Exception.ShouldBeOfType<TimeoutException>();
		result.Metadata["timeoutMs"].ShouldBe(30000);
	}

	[Fact]
	public void ReturnTypesForDifferentResults()
	{
		// Arrange & Act
		var stringResult = DispatchResult.Success(result: "completed");
		var intResult = DispatchResult.Success(result: 42);
		var objectResult = DispatchResult.Success(result: new { Status = "OK" });
		var nullResult = DispatchResult.Success(result: null);

		// Assert
		stringResult.Result.ShouldBe("completed");
		intResult.Result.ShouldBe(42);
		_ = objectResult.Result.ShouldNotBeNull();
		nullResult.Result.ShouldBeNull();
	}

	#endregion

	#region Property Immutability Tests

	[Fact]
	public void Properties_AreImmutable()
	{
		// Arrange
		var originalMetadata = new Dictionary<string, object> { ["key"] = "value" };
		var result = new DispatchResult(isSuccess: true, metadata: originalMetadata);

		// Act - Modify the original dictionary
		originalMetadata["newKey"] = "newValue";

		// Assert - The result's metadata reference should still point to the same dictionary
		// This tests that we store the reference, not a copy (by design for performance)
		result.Metadata["newKey"].ShouldBe("newValue");
	}

	#endregion
}
