// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Delivery.Pipeline;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery.Pipeline;

/// <summary>
/// Unit tests for the <see cref="MiddlewareResult"/> struct.
/// </summary>
/// <remarks>
/// Sprint 460 - Task S460.4: Middleware Pipeline Tests.
/// Tests the result struct used for middleware execution outcomes.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Priority", "0")]
public sealed class MiddlewareResultShould
{
	#region Factory Method Tests

	[Fact]
	public void Continue_ReturnsContinueExecutionTrue()
	{
		// Act
		var result = MiddlewareResult.Continue();

		// Assert
		result.ContinueExecution.ShouldBeTrue();
		result.Success.ShouldBeTrue();
		result.Error.ShouldBeNull();
	}

	[Fact]
	public void StopWithSuccess_ReturnsContinueExecutionFalse()
	{
		// Act
		var result = MiddlewareResult.StopWithSuccess();

		// Assert
		result.ContinueExecution.ShouldBeFalse();
		result.Success.ShouldBeTrue();
		result.Error.ShouldBeNull();
	}

	[Fact]
	public void StopWithError_ReturnsContinueExecutionFalseWithError()
	{
		// Arrange
		const string errorMessage = "Test error message";

		// Act
		var result = MiddlewareResult.StopWithError(errorMessage);

		// Assert
		result.ContinueExecution.ShouldBeFalse();
		result.Success.ShouldBeFalse();
		result.Error.ShouldBe(errorMessage);
	}

	#endregion

	#region Constructor Tests

	[Fact]
	public void Constructor_SetsAllProperties()
	{
		// Arrange & Act
		var result = new MiddlewareResult(
			continueExecution: true,
			success: false,
			error: "custom error");

		// Assert
		result.ContinueExecution.ShouldBeTrue();
		result.Success.ShouldBeFalse();
		result.Error.ShouldBe("custom error");
	}

	[Fact]
	public void Constructor_DefaultError_IsNull()
	{
		// Act
		var result = new MiddlewareResult(
			continueExecution: true,
			success: true);

		// Assert
		result.Error.ShouldBeNull();
	}

	#endregion

	#region Equality Tests

	[Fact]
	public void Equals_SameValues_ReturnsTrue()
	{
		// Arrange
		var result1 = MiddlewareResult.Continue();
		var result2 = MiddlewareResult.Continue();

		// Assert
		result1.Equals(result2).ShouldBeTrue();
		(result1 == result2).ShouldBeTrue();
		(result1 != result2).ShouldBeFalse();
	}

	[Fact]
	public void Equals_DifferentContinueExecution_ReturnsFalse()
	{
		// Arrange
		var result1 = MiddlewareResult.Continue();
		var result2 = MiddlewareResult.StopWithSuccess();

		// Assert
		result1.Equals(result2).ShouldBeFalse();
		(result1 == result2).ShouldBeFalse();
		(result1 != result2).ShouldBeTrue();
	}

	[Fact]
	public void Equals_DifferentSuccess_ReturnsFalse()
	{
		// Arrange
		var result1 = MiddlewareResult.StopWithSuccess();
		var result2 = MiddlewareResult.StopWithError("error");

		// Assert
		result1.Equals(result2).ShouldBeFalse();
	}

	[Fact]
	public void Equals_DifferentError_ReturnsFalse()
	{
		// Arrange
		var result1 = MiddlewareResult.StopWithError("error1");
		var result2 = MiddlewareResult.StopWithError("error2");

		// Assert
		result1.Equals(result2).ShouldBeFalse();
	}

	[Fact]
	public void Equals_SameError_ReturnsTrue()
	{
		// Arrange
		var result1 = MiddlewareResult.StopWithError("same error");
		var result2 = MiddlewareResult.StopWithError("same error");

		// Assert
		result1.Equals(result2).ShouldBeTrue();
	}

	[Fact]
	public void Equals_Object_WorksCorrectly()
	{
		// Arrange
		var result1 = MiddlewareResult.Continue();
		object result2 = MiddlewareResult.Continue();
		object notAResult = "not a result";

		// Assert
		result1.Equals(result2).ShouldBeTrue();
		result1.Equals(notAResult).ShouldBeFalse();
		result1.Equals(null).ShouldBeFalse();
	}

	#endregion

	#region HashCode Tests

	[Fact]
	public void GetHashCode_SameValues_ReturnsSameHash()
	{
		// Arrange
		var result1 = MiddlewareResult.Continue();
		var result2 = MiddlewareResult.Continue();

		// Assert
		result1.GetHashCode().ShouldBe(result2.GetHashCode());
	}

	[Fact]
	public void GetHashCode_DifferentValues_ReturnsDifferentHash()
	{
		// Arrange
		var result1 = MiddlewareResult.Continue();
		var result2 = MiddlewareResult.StopWithError("error");

		// Assert - Different values should have different hashes (usually)
		// Note: Hash collisions are possible but unlikely for different values
		result1.GetHashCode().ShouldNotBe(result2.GetHashCode());
	}

	[Fact]
	public void GetHashCode_ErrorVariants_ReturnsDifferentHash()
	{
		// Arrange
		var result1 = MiddlewareResult.StopWithError("error1");
		var result2 = MiddlewareResult.StopWithError("error2");

		// Assert
		result1.GetHashCode().ShouldNotBe(result2.GetHashCode());
	}

	#endregion

	#region Struct Behavior Tests

	[Fact]
	public void DefaultValue_HasDefaultProperties()
	{
		// Arrange & Act
		var result = default(MiddlewareResult);

		// Assert
		result.ContinueExecution.ShouldBeFalse();
		result.Success.ShouldBeFalse();
		result.Error.ShouldBeNull();
	}

	[Fact]
	public void ValueSemantics_CopyIsIndependent()
	{
		// Arrange
		var original = MiddlewareResult.StopWithError("original error");
		var copy = original;

		// Act - create a new result with different values
		var newResult = MiddlewareResult.Continue();

		// Assert - original and copy should still be equal
		original.Equals(copy).ShouldBeTrue();
		original.Equals(newResult).ShouldBeFalse();
	}

	#endregion
}
