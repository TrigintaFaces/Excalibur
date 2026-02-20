// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.ZeroAlloc;

namespace Excalibur.Dispatch.Tests.Messaging.ZeroAlloc;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class StructMessageResultShould
{
	// --- StructMessageResult (non-generic) ---

	[Fact]
	public void Success_ReturnsSucceededResult()
	{
		// Act
		var result = StructMessageResult.Success();

		// Assert
		result.Succeeded.ShouldBeTrue();
		result.CacheHit.ShouldBeFalse();
		result.ProblemDetails.ShouldBeNull();
		result.ErrorMessage.ShouldBeNull();
	}

	[Fact]
	public void Failed_ReturnsFailedResult()
	{
		// Arrange
		var problemDetails = A.Fake<IMessageProblemDetails>();
		A.CallTo(() => problemDetails.Detail).Returns("Something went wrong");

		// Act
		var result = StructMessageResult.Failed(problemDetails);

		// Assert
		result.Succeeded.ShouldBeFalse();
		result.ProblemDetails.ShouldBe(problemDetails);
		result.ErrorMessage.ShouldBe("Something went wrong");
	}

	[Fact]
	public void FromCache_ReturnsCacheHitResult()
	{
		// Act
		var result = StructMessageResult.FromCache();

		// Assert
		result.Succeeded.ShouldBeTrue();
		result.CacheHit.ShouldBeTrue();
		result.ProblemDetails.ShouldBeNull();
	}

	[Fact]
	public void RoutingDecision_IsNotNull()
	{
		// Act & Assert
		StructMessageResult.RoutingDecision.ShouldNotBeNull();
	}

	[Fact]
	public void ValidationResult_IsNotNull()
	{
		// Act & Assert — avoid Shouldly generic constraint issue with IValidationResult (CS8920)
		var validationResult = StructMessageResult.ValidationResult;
		(validationResult != null).ShouldBeTrue();
		validationResult!.IsValid.ShouldBeTrue();
	}

	[Fact]
	public void AuthorizationResult_IsNotNull()
	{
		// Act & Assert
		var authResult = StructMessageResult.AuthorizationResult;
		(authResult != null).ShouldBeTrue();
		authResult!.IsAuthorized.ShouldBeTrue();
		authResult.FailureMessage.ShouldBeNull();
	}

	[Fact]
	public void Equality_SameResults_AreEqual()
	{
		// Arrange
		var result1 = StructMessageResult.Success();
		var result2 = StructMessageResult.Success();

		// Assert
		result1.Equals(result2).ShouldBeTrue();
		(result1 == result2).ShouldBeTrue();
		(result1 != result2).ShouldBeFalse();
	}

	[Fact]
	public void Equality_DifferentSucceeded_AreNotEqual()
	{
		// Arrange
		var success = StructMessageResult.Success();
		var failed = StructMessageResult.Failed(A.Fake<IMessageProblemDetails>());

		// Assert
		success.Equals(failed).ShouldBeFalse();
		(success != failed).ShouldBeTrue();
	}

	[Fact]
	public void Equality_DifferentCacheHit_AreNotEqual()
	{
		// Arrange
		var success = StructMessageResult.Success();
		var cached = StructMessageResult.FromCache();

		// Assert
		success.Equals(cached).ShouldBeFalse();
	}

	[Fact]
	public void Equals_WithObject_WorksCorrectly()
	{
		// Arrange
		var result = StructMessageResult.Success();
		object boxed = StructMessageResult.Success();

		// Assert
		result.Equals(boxed).ShouldBeTrue();
		result.Equals("not a result").ShouldBeFalse();
		result.Equals(null).ShouldBeFalse();
	}

	[Fact]
	public void GetHashCode_SameResults_SameHash()
	{
		// Arrange
		var result1 = StructMessageResult.Success();
		var result2 = StructMessageResult.Success();

		// Assert
		result1.GetHashCode().ShouldBe(result2.GetHashCode());
	}

	[Fact]
	public void ExplicitInterfaceImplementation_WorksCorrectly()
	{
		// Arrange
		IMessageResult result = StructMessageResult.Success();

		// Assert — use direct null check to avoid CS8920
		(result.ValidationResult != null).ShouldBeTrue();
		(result.AuthorizationResult != null).ShouldBeTrue();
	}

	// --- StructMessageResult<T> (generic) ---

	[Fact]
	public void Generic_Success_ReturnsSucceededResultWithValue()
	{
		// Act
		var result = StructMessageResult<int>.Success(42);

		// Assert
		result.Succeeded.ShouldBeTrue();
		result.ReturnValue.ShouldBe(42);
		result.CacheHit.ShouldBeFalse();
		result.ProblemDetails.ShouldBeNull();
		result.ErrorMessage.ShouldBeNull();
	}

	[Fact]
	public void Generic_Failed_ReturnsFailedResult()
	{
		// Arrange
		var problemDetails = A.Fake<IMessageProblemDetails>();
		A.CallTo(() => problemDetails.Detail).Returns("Error occurred");

		// Act
		var result = StructMessageResult<string>.Failed(problemDetails);

		// Assert
		result.Succeeded.ShouldBeFalse();
		result.ReturnValue.ShouldBeNull();
		result.ProblemDetails.ShouldBe(problemDetails);
		result.ErrorMessage.ShouldBe("Error occurred");
	}

	[Fact]
	public void Generic_FromCache_ReturnsCacheHitWithValue()
	{
		// Act
		var result = StructMessageResult<string>.FromCache("cached-value");

		// Assert
		result.Succeeded.ShouldBeTrue();
		result.CacheHit.ShouldBeTrue();
		result.ReturnValue.ShouldBe("cached-value");
	}

	[Fact]
	public void Generic_Equality_SameResults_AreEqual()
	{
		// Arrange
		var result1 = StructMessageResult<int>.Success(42);
		var result2 = StructMessageResult<int>.Success(42);

		// Assert
		result1.Equals(result2).ShouldBeTrue();
		(result1 == result2).ShouldBeTrue();
		(result1 != result2).ShouldBeFalse();
	}

	[Fact]
	public void Generic_Equality_DifferentValues_AreNotEqual()
	{
		// Arrange
		var result1 = StructMessageResult<int>.Success(42);
		var result2 = StructMessageResult<int>.Success(99);

		// Assert
		result1.Equals(result2).ShouldBeFalse();
		(result1 != result2).ShouldBeTrue();
	}

	[Fact]
	public void Generic_Equals_WithObject_WorksCorrectly()
	{
		// Arrange
		var result = StructMessageResult<int>.Success(42);
		object boxed = StructMessageResult<int>.Success(42);

		// Assert
		result.Equals(boxed).ShouldBeTrue();
		result.Equals("not a result").ShouldBeFalse();
		result.Equals(null).ShouldBeFalse();
	}

	[Fact]
	public void Generic_GetHashCode_SameResults_SameHash()
	{
		// Arrange
		var result1 = StructMessageResult<int>.Success(42);
		var result2 = StructMessageResult<int>.Success(42);

		// Assert
		result1.GetHashCode().ShouldBe(result2.GetHashCode());
	}

	[Fact]
	public void Generic_RoutingDecision_IsNotNull()
	{
		// Arrange
		var result = StructMessageResult<int>.Success(1);

		// Assert
		result.RoutingDecision.ShouldNotBeNull();
	}

	[Fact]
	public void Generic_ValidationResult_IsValid()
	{
		// Arrange
		var result = StructMessageResult<int>.Success(1);

		// Assert — use direct null check to avoid CS8920 with IValidationResult
		var validationResult = result.ValidationResult;
		(validationResult != null).ShouldBeTrue();
		validationResult!.IsValid.ShouldBeTrue();
	}

	[Fact]
	public void Generic_AuthorizationResult_IsAuthorized()
	{
		// Arrange
		var result = StructMessageResult<int>.Success(1);

		// Assert
		var authResult = result.AuthorizationResult;
		(authResult != null).ShouldBeTrue();
		authResult!.IsAuthorized.ShouldBeTrue();
	}

	[Fact]
	public void Generic_ExplicitInterfaceImplementation_WorksCorrectly()
	{
		// Arrange
		IMessageResult result = new StructMessageResult<int>(42);

		// Assert — use direct null check to avoid CS8920
		(result.ValidationResult != null).ShouldBeTrue();
		(result.AuthorizationResult != null).ShouldBeTrue();
	}
}
