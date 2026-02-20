// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Routing;

namespace Excalibur.Dispatch.Abstractions.Tests;

/// <summary>
/// Depth coverage tests for <see cref="MessageResult"/> factory methods.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MessageResultDepthShould
{
	[Fact]
	public void Success_ReturnsSucceededResult()
	{
		var result = MessageResult.Success();
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void SuccessFromCache_ReturnsCacheHitResult()
	{
		var result = MessageResult.SuccessFromCache();
		result.Succeeded.ShouldBeTrue();
		result.CacheHit.ShouldBeTrue();
	}

	[Fact]
	public void SuccessFromCache_Generic_ReturnsValueAndCacheHit()
	{
		var result = MessageResult.SuccessFromCache(42);
		result.Succeeded.ShouldBeTrue();
		result.CacheHit.ShouldBeTrue();
		result.ReturnValue.ShouldBe(42);
	}

	[Fact]
	public void Success_WithValue_ReturnsValueResult()
	{
		var result = MessageResult.Success("hello");
		result.Succeeded.ShouldBeTrue();
		result.ReturnValue.ShouldBe("hello");
	}

	[Fact]
	public void Success_WithFullContext_ReturnsResult()
	{
		var routingDecision = RoutingDecision.Success("target", []);
		var result = MessageResult.Success(routingDecision, "valid", "authorized", cacheHit: true);
		result.Succeeded.ShouldBeTrue();
		result.CacheHit.ShouldBeTrue();
	}

	[Fact]
	public void Success_Generic_WithFullContext_ReturnsResult()
	{
		var result = MessageResult.Success(
			42,
			routingDecision: null,
			validationResult: "valid",
			authorizationResult: "auth",
			cacheHit: true);
		result.Succeeded.ShouldBeTrue();
		result.ReturnValue.ShouldBe(42);
		result.CacheHit.ShouldBeTrue();
	}

	[Fact]
	public void Failed_WithErrorString_ReturnsFailedResult()
	{
		var result = MessageResult.Failed("something went wrong");
		result.Succeeded.ShouldBeFalse();
		result.ErrorMessage.ShouldBe("something went wrong");
	}

	[Fact]
	public void Failed_WithProblemDetails_ReturnsFailedResult()
	{
		var problem = A.Fake<IMessageProblemDetails>();
		A.CallTo(() => problem.Detail).Returns("detail msg");
		var result = MessageResult.Failed(problem);
		result.Succeeded.ShouldBeFalse();
		result.ProblemDetails.ShouldBeSameAs(problem);
	}

	[Fact]
	public void Failed_Generic_ReturnsFailedTypedResult()
	{
		var result = MessageResult.Failed<int>("error msg");
		result.Succeeded.ShouldBeFalse();
		result.ErrorMessage.ShouldBe("error msg");
		result.ReturnValue.ShouldBe(default);
	}

	[Fact]
	public void Failed_Generic_WithProblemDetails_ReturnsResult()
	{
		var problem = A.Fake<IMessageProblemDetails>();
		var result = MessageResult.Failed<string>("err", problem);
		result.Succeeded.ShouldBeFalse();
		result.ProblemDetails.ShouldBeSameAs(problem);
	}

	[Fact]
	public void IsSuccess_AliasMatchesSucceeded()
	{
		var success = MessageResult.Success();
		success.IsSuccess.ShouldBeTrue();

		var failed = MessageResult.Failed("err");
		failed.IsSuccess.ShouldBeFalse();
	}

	[Fact]
	public void Failed_WithProblemDetails_SetsErrorMessageFromDetail()
	{
		var problem = A.Fake<IMessageProblemDetails>();
		A.CallTo(() => problem.Detail).Returns("some detail");
		var result = MessageResult.Failed(problem);
		result.ErrorMessage.ShouldBe("some detail");
	}
}
