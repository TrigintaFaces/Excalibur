// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Caching;

namespace Excalibur.Dispatch.Middleware.Tests.Caching;

/// <summary>
/// Unit tests for <see cref="DefaultResultCachePolicy"/>.
/// Covers the delegate-based policy evaluation with various inputs.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Caching")]
public sealed class DefaultResultCachePolicyShould : UnitTestBase
{
	[Fact]
	public void ShouldCache_WhenDelegateReturnsTrue_ReturnsTrue()
	{
		// Arrange
		var policy = new DefaultResultCachePolicy((_, _) => true);
		var message = A.Fake<IDispatchMessage>();

		// Act
		var result = policy.ShouldCache(message, "some-result");

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ShouldCache_WhenDelegateReturnsFalse_ReturnsFalse()
	{
		// Arrange
		var policy = new DefaultResultCachePolicy((_, _) => false);
		var message = A.Fake<IDispatchMessage>();

		// Act
		var result = policy.ShouldCache(message, "some-result");

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ShouldCache_WithNullResult_DelegateReceivesNullResult()
	{
		// Arrange
		object? capturedResult = "not-null";
		var policy = new DefaultResultCachePolicy((_, result) =>
		{
			capturedResult = result;
			return true;
		});
		var message = A.Fake<IDispatchMessage>();

		// Act
		policy.ShouldCache(message, null);

		// Assert
		capturedResult.ShouldBeNull();
	}

	[Fact]
	public void ShouldCache_DelegateReceivesCorrectMessageAndResult()
	{
		// Arrange
		IDispatchMessage? capturedMessage = null;
		object? capturedResult = null;
		var policy = new DefaultResultCachePolicy((msg, res) =>
		{
			capturedMessage = msg;
			capturedResult = res;
			return true;
		});
		var message = A.Fake<IDispatchMessage>();
		var resultValue = new { Id = 42 };

		// Act
		policy.ShouldCache(message, resultValue);

		// Assert
		capturedMessage.ShouldBe(message);
		capturedResult.ShouldBe(resultValue);
	}

	[Fact]
	public void ShouldCache_WhenDelegateChecksResultType_FiltersCorrectly()
	{
		// Arrange -- only cache non-null string results
		var policy = new DefaultResultCachePolicy((_, result) => result is string s && s.Length > 0);
		var message = A.Fake<IDispatchMessage>();

		// Act & Assert
		policy.ShouldCache(message, "valid").ShouldBeTrue();
		policy.ShouldCache(message, "").ShouldBeFalse();
		policy.ShouldCache(message, null).ShouldBeFalse();
		policy.ShouldCache(message, 42).ShouldBeFalse();
	}

	[Fact]
	public void ShouldCache_ImplementsIResultCachePolicy()
	{
		// Arrange
		var policy = new DefaultResultCachePolicy((_, _) => true);

		// Assert
		policy.ShouldBeAssignableTo<IResultCachePolicy>();
	}
}
