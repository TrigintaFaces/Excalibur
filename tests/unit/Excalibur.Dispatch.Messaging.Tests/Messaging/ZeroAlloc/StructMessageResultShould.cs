// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.ZeroAlloc;

namespace Excalibur.Dispatch.Tests.Messaging.ZeroAlloc;

/// <summary>
///     Tests for the <see cref="StructMessageResult" /> struct.
/// </summary>
[Trait("Category", "Unit")]
public sealed class StructMessageResultShould
{
	[Fact]
	public void CreateSuccessResultWithSucceededTrue()
	{
		var result = StructMessageResult.Success();
		result.Succeeded.ShouldBeTrue();
		result.ProblemDetails.ShouldBeNull();
		result.CacheHit.ShouldBeFalse();
		result.ErrorMessage.ShouldBeNull();
	}

	[Fact]
	public void CreateFailedResultWithProblemDetails()
	{
		var problem = new MessageProblemDetails { Detail = "Something went wrong" };
		var result = StructMessageResult.Failed(problem);
		result.Succeeded.ShouldBeFalse();
		result.ProblemDetails.ShouldBe(problem);
		result.ErrorMessage.ShouldBe("Something went wrong");
	}

	[Fact]
	public void CreateCacheHitResult()
	{
		var result = StructMessageResult.FromCache();
		result.Succeeded.ShouldBeTrue();
		result.CacheHit.ShouldBeTrue();
	}

	[Fact]
	public void ImplementEquality()
	{
		var a = StructMessageResult.Success();
		var b = StructMessageResult.Success();
		(a == b).ShouldBeTrue();
		(a != b).ShouldBeFalse();
		a.Equals(b).ShouldBeTrue();
		a.Equals((object)b).ShouldBeTrue();
	}

	[Fact]
	public void ImplementInequalityForDifferentStates()
	{
		var success = StructMessageResult.Success();
		var cache = StructMessageResult.FromCache();
		(success == cache).ShouldBeFalse();
		(success != cache).ShouldBeTrue();
	}

	[Fact]
	public void HaveConsistentHashCodes()
	{
		var a = StructMessageResult.Success();
		var b = StructMessageResult.Success();
		a.GetHashCode().ShouldBe(b.GetHashCode());
	}

	[Fact]
	public void NotEqualToNonStructMessageResultObject()
	{
		var result = StructMessageResult.Success();
		result.Equals("not a result").ShouldBeFalse();
	}

	[Fact]
	public void ExposeDefaultRoutingDecision()
	{
		StructMessageResult.RoutingDecision.ShouldNotBeNull();
	}

	[Fact]
	public void ExposeDefaultValidationResult()
	{
		// IValidationResult has static abstract members; test via IMessageResult interface
		IMessageResult result = StructMessageResult.Success();
		result.ValidationResult.ShouldNotBeNull();
	}

	[Fact]
	public void ExposeDefaultAuthorizationResult()
	{
		StructMessageResult.AuthorizationResult.ShouldNotBeNull();
		StructMessageResult.AuthorizationResult!.IsAuthorized.ShouldBeTrue();
	}
}

/// <summary>
///     Tests for the <see cref="StructMessageResult{T}" /> generic struct.
/// </summary>
[Trait("Category", "Unit")]
public sealed class StructMessageResultOfTShould
{
	[Fact]
	public void CreateSuccessResultWithReturnValue()
	{
		var result = StructMessageResult<int>.Success(42);
		result.Succeeded.ShouldBeTrue();
		result.ReturnValue.ShouldBe(42);
		result.CacheHit.ShouldBeFalse();
	}

	[Fact]
	public void CreateFailedResultWithProblemDetails()
	{
		var problem = new MessageProblemDetails { Detail = "Oops" };
		var result = StructMessageResult<string>.Failed(problem);
		result.Succeeded.ShouldBeFalse();
		result.ReturnValue.ShouldBeNull();
		result.ProblemDetails.ShouldBe(problem);
		result.ErrorMessage.ShouldBe("Oops");
	}

	[Fact]
	public void CreateCacheHitResultWithReturnValue()
	{
		var result = StructMessageResult<string>.FromCache("cached-value");
		result.Succeeded.ShouldBeTrue();
		result.CacheHit.ShouldBeTrue();
		result.ReturnValue.ShouldBe("cached-value");
	}

	[Fact]
	public void ImplementEquality()
	{
		var a = StructMessageResult<int>.Success(42);
		var b = StructMessageResult<int>.Success(42);
		(a == b).ShouldBeTrue();
		a.Equals(b).ShouldBeTrue();
		a.Equals((object)b).ShouldBeTrue();
	}

	[Fact]
	public void ImplementInequalityForDifferentValues()
	{
		var a = StructMessageResult<int>.Success(1);
		var b = StructMessageResult<int>.Success(2);
		(a != b).ShouldBeTrue();
	}

	[Fact]
	public void HaveConsistentHashCodes()
	{
		var a = StructMessageResult<string>.Success("hello");
		var b = StructMessageResult<string>.Success("hello");
		a.GetHashCode().ShouldBe(b.GetHashCode());
	}

	[Fact]
	public void NotEqualToNonStructMessageResultObject()
	{
		var result = StructMessageResult<int>.Success(1);
		result.Equals("not a result").ShouldBeFalse();
	}
}
