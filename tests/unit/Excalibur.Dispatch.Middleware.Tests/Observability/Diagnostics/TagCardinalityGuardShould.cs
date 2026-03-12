// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Diagnostics;

namespace Excalibur.Dispatch.Middleware.Tests.Observability.Diagnostics;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class TagCardinalityGuardShould
{
	[Fact]
	public void ReturnOriginalValueWithinLimit()
	{
		// Arrange
		var guard = new TagCardinalityGuard(maxCardinality: 5);

		// Act & Assert
		guard.Guard("value1").ShouldBe("value1");
		guard.Guard("value2").ShouldBe("value2");
		guard.Guard("value3").ShouldBe("value3");
	}

	[Fact]
	public void ReturnOverflowWhenLimitExceeded()
	{
		// Arrange
		var guard = new TagCardinalityGuard(maxCardinality: 3);

		guard.Guard("v1");
		guard.Guard("v2");
		guard.Guard("v3");

		// Act - 4th distinct value should overflow
		var result = guard.Guard("v4");

		// Assert
		result.ShouldBe("__other__");
	}

	[Fact]
	public void ReturnKnownValueAfterLimitExceeded()
	{
		// Arrange
		var guard = new TagCardinalityGuard(maxCardinality: 2);

		guard.Guard("v1");
		guard.Guard("v2");
		guard.Guard("v3"); // overflow

		// Act - known value should still pass
		guard.Guard("v1").ShouldBe("v1");
		guard.Guard("v2").ShouldBe("v2");
	}

	[Fact]
	public void ReturnOverflowForNullValue()
	{
		var guard = new TagCardinalityGuard();
		guard.Guard(null).ShouldBe("__other__");
	}

	[Fact]
	public void UseCustomOverflowValue()
	{
		// Arrange
		var guard = new TagCardinalityGuard(maxCardinality: 1, overflowValue: "OVERFLOW");

		guard.Guard("v1");

		// Act
		var result = guard.Guard("v2");

		// Assert
		result.ShouldBe("OVERFLOW");
	}

	[Fact]
	public void HandleConcurrentAccessSafely()
	{
		// Arrange
		var guard = new TagCardinalityGuard(maxCardinality: 100);
		var results = new System.Collections.Concurrent.ConcurrentBag<string>();

		// Act
		Parallel.For(0, 200, i =>
		{
			var result = guard.Guard($"value-{i}");
			results.Add(result);
		});

		// Assert - some should be original values, some should be overflow
		results.Count.ShouldBe(200);
		var overflowCount = results.Count(r => r == "__other__");
		// With 100 max cardinality and 200 values, roughly 100 should overflow
		// Allow for minor overshoot due to concurrency
		overflowCount.ShouldBeGreaterThan(50);
	}

	[Fact]
	public void ReturnSameValueOnRepeatedCalls()
	{
		var guard = new TagCardinalityGuard(maxCardinality: 5);

		// Same value multiple times should always return original
		for (var i = 0; i < 100; i++)
		{
			guard.Guard("repeated").ShouldBe("repeated");
		}
	}
}
