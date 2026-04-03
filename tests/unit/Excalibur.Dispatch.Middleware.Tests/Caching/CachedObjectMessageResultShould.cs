// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Caching;

namespace Excalibur.Dispatch.Middleware.Tests.Caching;

[Trait("Category", "Unit")]
[Trait("Component", "Caching")]
public sealed class CachedObjectMessageResultShould : UnitTestBase
{
	[Fact]
	public void SetSucceededToTrue()
	{
		var result = new CachedObjectMessageResult("test-value");

		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void SetCacheHitToTrue()
	{
		var result = new CachedObjectMessageResult("test-value");

		result.CacheHit.ShouldBeTrue();
	}

	[Fact]
	public void StoreReturnValue()
	{
		var value = new { Name = "test", Count = 42 };
		var result = new CachedObjectMessageResult(value);

		result.ReturnValue.ShouldBeSameAs(value);
	}

	[Fact]
	public void AcceptNullValue()
	{
		var result = new CachedObjectMessageResult(null);

		result.ReturnValue.ShouldBeNull();
		result.Succeeded.ShouldBeTrue();
		result.CacheHit.ShouldBeTrue();
	}

	[Fact]
	public void HaveNullProblemDetails()
	{
		var result = new CachedObjectMessageResult("test");

		result.ProblemDetails.ShouldBeNull();
	}

	[Fact]
	public void HaveNullErrorMessage()
	{
		var result = new CachedObjectMessageResult("test");

		result.ErrorMessage.ShouldBeNull();
	}

	[Fact]
	public void HaveNullValidationResult()
	{
		IMessageResult messageResult = new CachedObjectMessageResult("test");

		messageResult.ValidationResult.ShouldBeNull();
	}

	[Fact]
	public void HaveNullAuthorizationResult()
	{
		IMessageResult messageResult = new CachedObjectMessageResult("test");

		messageResult.AuthorizationResult.ShouldBeNull();
	}

	[Fact]
	public void ImplementIMessageResult()
	{
		var result = new CachedObjectMessageResult("test");

		result.ShouldBeAssignableTo<IMessageResult>();
	}

	[Fact]
	public void StoreStringValue()
	{
		var result = new CachedObjectMessageResult("cached-string");

		result.ReturnValue.ShouldBe("cached-string");
	}

	[Fact]
	public void StoreIntValue()
	{
		var result = new CachedObjectMessageResult(42);

		result.ReturnValue.ShouldBe(42);
	}
}
