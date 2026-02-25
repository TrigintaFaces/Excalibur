// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Processing;

namespace Excalibur.Dispatch.Tests.Messaging.Processing;

/// <summary>
/// Unit tests for <see cref="HandlerResult"/>.
/// </summary>
/// <remarks>
/// Tests the handler execution result struct.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Processing")]
[Trait("Priority", "0")]
public sealed class HandlerResultShould
{
	#region Factory Method Tests

	[Fact]
	public void Ok_WithoutBytesWritten_CreatesSuccessResult()
	{
		// Arrange & Act
		var result = HandlerResult.Ok();

		// Assert
		result.Success.ShouldBeTrue();
		result.ErrorCode.ShouldBe(0);
		result.BytesWritten.ShouldBe(0);
	}

	[Fact]
	public void Ok_WithBytesWritten_CreatesSuccessResultWithBytes()
	{
		// Arrange & Act
		var result = HandlerResult.Ok(bytesWritten: 1024);

		// Assert
		result.Success.ShouldBeTrue();
		result.ErrorCode.ShouldBe(0);
		result.BytesWritten.ShouldBe(1024);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(100)]
	[InlineData(int.MaxValue)]
	public void Ok_WithVariousBytesWritten_Works(int bytesWritten)
	{
		// Arrange & Act
		var result = HandlerResult.Ok(bytesWritten);

		// Assert
		result.Success.ShouldBeTrue();
		result.BytesWritten.ShouldBe(bytesWritten);
	}

	[Fact]
	public void Error_WithErrorCode_CreatesFailureResult()
	{
		// Arrange & Act
		var result = HandlerResult.Error(errorCode: 500);

		// Assert
		result.Success.ShouldBeFalse();
		result.ErrorCode.ShouldBe(500);
		result.BytesWritten.ShouldBe(0);
	}

	[Theory]
	[InlineData(1)]
	[InlineData(404)]
	[InlineData(500)]
	[InlineData(-1)]
	[InlineData(int.MaxValue)]
	public void Error_WithVariousErrorCodes_Works(int errorCode)
	{
		// Arrange & Act
		var result = HandlerResult.Error(errorCode);

		// Assert
		result.Success.ShouldBeFalse();
		result.ErrorCode.ShouldBe(errorCode);
	}

	#endregion

	#region Equality Tests

	[Fact]
	public void Equals_SameSuccessResult_ReturnsTrue()
	{
		// Arrange
		var result1 = HandlerResult.Ok(100);
		var result2 = HandlerResult.Ok(100);

		// Act & Assert
		result1.Equals(result2).ShouldBeTrue();
	}

	[Fact]
	public void Equals_SameErrorResult_ReturnsTrue()
	{
		// Arrange
		var result1 = HandlerResult.Error(404);
		var result2 = HandlerResult.Error(404);

		// Act & Assert
		result1.Equals(result2).ShouldBeTrue();
	}

	[Fact]
	public void Equals_DifferentBytesWritten_ReturnsFalse()
	{
		// Arrange
		var result1 = HandlerResult.Ok(100);
		var result2 = HandlerResult.Ok(200);

		// Act & Assert
		result1.Equals(result2).ShouldBeFalse();
	}

	[Fact]
	public void Equals_DifferentErrorCodes_ReturnsFalse()
	{
		// Arrange
		var result1 = HandlerResult.Error(404);
		var result2 = HandlerResult.Error(500);

		// Act & Assert
		result1.Equals(result2).ShouldBeFalse();
	}

	[Fact]
	public void Equals_SuccessAndError_ReturnsFalse()
	{
		// Arrange
		var success = HandlerResult.Ok();
		var error = HandlerResult.Error(1);

		// Act & Assert
		success.Equals(error).ShouldBeFalse();
	}

	[Fact]
	public void Equals_WithObject_SameValues_ReturnsTrue()
	{
		// Arrange
		var result1 = HandlerResult.Ok(50);
		object result2 = HandlerResult.Ok(50);

		// Act & Assert
		result1.Equals(result2).ShouldBeTrue();
	}

	[Fact]
	public void Equals_WithObject_DifferentValues_ReturnsFalse()
	{
		// Arrange
		var result1 = HandlerResult.Ok(50);
		object result2 = HandlerResult.Ok(100);

		// Act & Assert
		result1.Equals(result2).ShouldBeFalse();
	}

	[Fact]
	public void Equals_WithNull_ReturnsFalse()
	{
		// Arrange
		var result = HandlerResult.Ok();

		// Act & Assert
		result.Equals(null).ShouldBeFalse();
	}

	[Fact]
	public void Equals_WithWrongType_ReturnsFalse()
	{
		// Arrange
		var result = HandlerResult.Ok();

		// Act & Assert
		result.Equals("not a handler result").ShouldBeFalse();
	}

	#endregion

	#region GetHashCode Tests

	[Fact]
	public void GetHashCode_SameValues_ReturnsSameHashCode()
	{
		// Arrange
		var result1 = HandlerResult.Ok(100);
		var result2 = HandlerResult.Ok(100);

		// Act & Assert
		result1.GetHashCode().ShouldBe(result2.GetHashCode());
	}

	[Fact]
	public void GetHashCode_DifferentValues_ReturnsDifferentHashCodes()
	{
		// Arrange
		var result1 = HandlerResult.Ok(100);
		var result2 = HandlerResult.Error(100);

		// Act & Assert
		result1.GetHashCode().ShouldNotBe(result2.GetHashCode());
	}

	#endregion

	#region Operator Tests

	[Fact]
	public void OperatorEquals_SameValues_ReturnsTrue()
	{
		// Arrange
		var result1 = HandlerResult.Ok(50);
		var result2 = HandlerResult.Ok(50);

		// Act & Assert
		(result1 == result2).ShouldBeTrue();
	}

	[Fact]
	public void OperatorEquals_DifferentValues_ReturnsFalse()
	{
		// Arrange
		var result1 = HandlerResult.Ok(50);
		var result2 = HandlerResult.Ok(100);

		// Act & Assert
		(result1 == result2).ShouldBeFalse();
	}

	[Fact]
	public void OperatorNotEquals_SameValues_ReturnsFalse()
	{
		// Arrange
		var result1 = HandlerResult.Ok(50);
		var result2 = HandlerResult.Ok(50);

		// Act & Assert
		(result1 != result2).ShouldBeFalse();
	}

	[Fact]
	public void OperatorNotEquals_DifferentValues_ReturnsTrue()
	{
		// Arrange
		var result1 = HandlerResult.Ok(50);
		var result2 = HandlerResult.Error(500);

		// Act & Assert
		(result1 != result2).ShouldBeTrue();
	}

	#endregion

	#region Property Tests

	[Fact]
	public void Success_ForOkResult_IsTrue()
	{
		// Arrange & Act
		var result = HandlerResult.Ok();

		// Assert
		result.Success.ShouldBeTrue();
	}

	[Fact]
	public void Success_ForErrorResult_IsFalse()
	{
		// Arrange & Act
		var result = HandlerResult.Error(1);

		// Assert
		result.Success.ShouldBeFalse();
	}

	[Fact]
	public void ErrorCode_ForOkResult_IsZero()
	{
		// Arrange & Act
		var result = HandlerResult.Ok(1000);

		// Assert
		result.ErrorCode.ShouldBe(0);
	}

	[Fact]
	public void BytesWritten_ForErrorResult_IsZero()
	{
		// Arrange & Act
		var result = HandlerResult.Error(500);

		// Assert
		result.BytesWritten.ShouldBe(0);
	}

	#endregion

	#region Typical Usage Scenarios

	[Fact]
	public void SuccessfulHandlerExecution_WithResponseSize()
	{
		// Arrange & Act
		var result = HandlerResult.Ok(bytesWritten: 4096);

		// Assert
		result.Success.ShouldBeTrue();
		result.BytesWritten.ShouldBe(4096);
		result.ErrorCode.ShouldBe(0);
	}

	[Fact]
	public void FailedHandlerExecution_WithNotFoundError()
	{
		// Arrange & Act
		var result = HandlerResult.Error(errorCode: 404);

		// Assert
		result.Success.ShouldBeFalse();
		result.ErrorCode.ShouldBe(404);
		result.BytesWritten.ShouldBe(0);
	}

	[Fact]
	public void FailedHandlerExecution_WithInternalError()
	{
		// Arrange & Act
		var result = HandlerResult.Error(errorCode: 500);

		// Assert
		result.Success.ShouldBeFalse();
		result.ErrorCode.ShouldBe(500);
	}

	#endregion
}
