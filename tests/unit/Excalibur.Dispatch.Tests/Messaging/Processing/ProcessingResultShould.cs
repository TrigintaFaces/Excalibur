// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Processing;

namespace Excalibur.Dispatch.Tests.Messaging.Processing;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ProcessingResultShould
{
	[Fact]
	public void Ok_CreateSuccessResult()
	{
		// Act
		var result = ProcessingResult.Ok();

		// Assert
		result.Success.ShouldBeTrue();
		result.ResponseLength.ShouldBe(0);
		result.ErrorCode.ShouldBe(0);
	}

	[Fact]
	public void Ok_WithResponseLength_SetResponseLength()
	{
		// Act
		var result = ProcessingResult.Ok(responseLength: 1024);

		// Assert
		result.Success.ShouldBeTrue();
		result.ResponseLength.ShouldBe(1024);
		result.ErrorCode.ShouldBe(0);
	}

	[Fact]
	public void Error_CreateFailureResult()
	{
		// Act
		var result = ProcessingResult.Error(errorCode: 500);

		// Assert
		result.Success.ShouldBeFalse();
		result.ResponseLength.ShouldBe(0);
		result.ErrorCode.ShouldBe(500);
	}

	[Fact]
	public void Equality_SameValues_AreEqual()
	{
		// Arrange
		var result1 = ProcessingResult.Ok(100);
		var result2 = ProcessingResult.Ok(100);

		// Assert
		result1.ShouldBe(result2);
		(result1 == result2).ShouldBeTrue();
	}

	[Fact]
	public void Equality_DifferentValues_AreNotEqual()
	{
		// Arrange
		var success = ProcessingResult.Ok();
		var error = ProcessingResult.Error(1);

		// Assert
		success.ShouldNotBe(error);
		(success != error).ShouldBeTrue();
	}

	[Fact]
	public void GetHashCode_SameValues_ReturnSameHash()
	{
		// Arrange
		var result1 = ProcessingResult.Error(42);
		var result2 = ProcessingResult.Error(42);

		// Assert
		result1.GetHashCode().ShouldBe(result2.GetHashCode());
	}

	[Fact]
	public void Equals_WithObjectParam_ReturnFalseForDifferentType()
	{
		// Arrange
		var result = ProcessingResult.Ok();

		// Act & Assert
		result.Equals("not a processing result").ShouldBeFalse();
	}
}
