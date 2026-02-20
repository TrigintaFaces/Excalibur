// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Processing;

namespace Excalibur.Dispatch.Tests.Messaging.Processing;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DispatchResultShould
{
	[Fact]
	public void SuccessResult_IndicateSuccess()
	{
		// Act
		var result = DispatchResult.SuccessResult;

		// Assert
		result.Success.ShouldBeTrue();
		result.FailedHandlerIndex.ShouldBe(-1);
		result.ErrorCode.ShouldBe(0);
	}

	[Fact]
	public void ExceptionThrown_IndicateFailure()
	{
		// Act
		var result = DispatchResult.ExceptionThrown;

		// Assert
		result.Success.ShouldBeFalse();
		result.FailedHandlerIndex.ShouldBe(-1);
		result.ErrorCode.ShouldBe(-1);
	}

	[Fact]
	public void HandlerFailed_SetHandlerIndexAndErrorCode()
	{
		// Act
		var result = DispatchResult.HandlerFailed(handlerIndex: 2, errorCode: 42);

		// Assert
		result.Success.ShouldBeFalse();
		result.FailedHandlerIndex.ShouldBe(2);
		result.ErrorCode.ShouldBe(42);
	}

	[Fact]
	public void Equality_TwoSuccessResults_AreEqual()
	{
		// Act
		var result1 = DispatchResult.SuccessResult;
		var result2 = DispatchResult.SuccessResult;

		// Assert
		result1.ShouldBe(result2);
		(result1 == result2).ShouldBeTrue();
		(result1 != result2).ShouldBeFalse();
	}

	[Fact]
	public void Equality_SuccessAndFailure_AreNotEqual()
	{
		// Act
		var success = DispatchResult.SuccessResult;
		var failure = DispatchResult.ExceptionThrown;

		// Assert
		success.ShouldNotBe(failure);
		(success == failure).ShouldBeFalse();
		(success != failure).ShouldBeTrue();
	}

	[Fact]
	public void Equality_DifferentHandlerFailures_AreNotEqual()
	{
		// Act
		var failure1 = DispatchResult.HandlerFailed(1, 100);
		var failure2 = DispatchResult.HandlerFailed(2, 100);

		// Assert
		failure1.ShouldNotBe(failure2);
	}

	[Fact]
	public void GetHashCode_SameResults_ReturnSameHash()
	{
		// Act
		var result1 = DispatchResult.HandlerFailed(1, 42);
		var result2 = DispatchResult.HandlerFailed(1, 42);

		// Assert
		result1.GetHashCode().ShouldBe(result2.GetHashCode());
	}

	[Fact]
	public void Equals_WithObjectParam_ReturnFalseForNonDispatchResult()
	{
		// Arrange
		var result = DispatchResult.SuccessResult;

		// Act & Assert
		result.Equals("not a dispatch result").ShouldBeFalse();
	}

	[Fact]
	public void Equals_WithObjectParam_ReturnTrueForBoxedEqual()
	{
		// Arrange
		var result1 = DispatchResult.SuccessResult;
		object boxed = DispatchResult.SuccessResult;

		// Act & Assert
		result1.Equals(boxed).ShouldBeTrue();
	}
}
