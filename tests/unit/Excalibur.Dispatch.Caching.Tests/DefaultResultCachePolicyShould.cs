// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Caching;

namespace Excalibur.Dispatch.Caching.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Caching")]
public sealed class DefaultResultCachePolicyShould
{
	[Fact]
	public void ReturnTrue_WhenPolicyDelegateReturnsTrue()
	{
		// Arrange
		var policy = new DefaultResultCachePolicy(static (_, _) => true);
		var message = A.Fake<IDispatchMessage>();

		// Act
		var result = policy.ShouldCache(message, "some-result");

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ReturnFalse_WhenPolicyDelegateReturnsFalse()
	{
		// Arrange
		var policy = new DefaultResultCachePolicy(static (_, _) => false);
		var message = A.Fake<IDispatchMessage>();

		// Act
		var result = policy.ShouldCache(message, "some-result");

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void PassMessageAndResult_ToPolicyDelegate()
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
		var resultObj = new object();

		// Act
		policy.ShouldCache(message, resultObj);

		// Assert
		capturedMessage.ShouldBeSameAs(message);
		capturedResult.ShouldBeSameAs(resultObj);
	}

	[Fact]
	public void HandleNullResult()
	{
		// Arrange
		var policy = new DefaultResultCachePolicy(static (_, result) => result == null);
		var message = A.Fake<IDispatchMessage>();

		// Act
		var result = policy.ShouldCache(message, null);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void AllowConditionalCaching_BasedOnResultType()
	{
		// Arrange
		var policy = new DefaultResultCachePolicy(static (_, result) => result is string);
		var message = A.Fake<IDispatchMessage>();

		// Act & Assert
		policy.ShouldCache(message, "string-result").ShouldBeTrue();
		policy.ShouldCache(message, 42).ShouldBeFalse();
		policy.ShouldCache(message, null).ShouldBeFalse();
	}
}
