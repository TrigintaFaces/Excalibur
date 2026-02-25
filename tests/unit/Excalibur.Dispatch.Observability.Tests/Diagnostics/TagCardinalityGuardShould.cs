// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch.Observability.Diagnostics;

namespace Excalibur.Dispatch.Observability.Tests.Diagnostics;

/// <summary>
/// Unit tests for <see cref="TagCardinalityGuard"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Diagnostics")]
public sealed class TagCardinalityGuardShould
{
	[Fact]
	public void ReturnOriginalValue_WhenWithinCardinalityLimit()
	{
		// Arrange
		var guard = CreateGuard();

		// Act
		var result = guard.Guard("test-value");

		// Assert
		result.ShouldBe("test-value");
	}

	[Fact]
	public void ReturnOverflowSentinel_WhenNullValue()
	{
		// Arrange
		var guard = CreateGuard();

		// Act
		var result = guard.Guard(null);

		// Assert
		result.ShouldBe("__other__");
	}

	[Fact]
	public void ReturnOverflowSentinel_WhenCardinalityLimitExceeded()
	{
		// Arrange
		var guard = CreateGuard(maxCardinality: 3);

		// Act — fill up cardinality limit
		guard.Guard("value1");
		guard.Guard("value2");
		guard.Guard("value3");
		var result = guard.Guard("value4-overflow");

		// Assert
		result.ShouldBe("__other__");
	}

	[Fact]
	public void ReturnOriginalValue_WhenAlreadyTracked()
	{
		// Arrange
		var guard = CreateGuard(maxCardinality: 2);
		guard.Guard("value1");
		guard.Guard("value2");

		// Act — re-request existing value after limit reached
		var result = guard.Guard("value1");

		// Assert
		result.ShouldBe("value1");
	}

	[Fact]
	public void UseCustomOverflowValue()
	{
		// Arrange
		var guard = CreateGuard(maxCardinality: 1, overflowValue: "OVERFLOW");
		guard.Guard("only-one");

		// Act
		var result = guard.Guard("second-value");

		// Assert
		result.ShouldBe("OVERFLOW");
	}

	[Fact]
	public void BeThreadSafe_UnderConcurrentAccess()
	{
		// Arrange
		var guard = CreateGuard(maxCardinality: 50);
		var results = new string[200];

		// Act
		Parallel.For(0, 200, i =>
		{
			results[i] = guard.Guard($"value-{i}");
		});

		// Assert — first 50 (approximately) should be their own values, rest overflow
		var overflowCount = results.Count(r => r == "__other__");
		overflowCount.ShouldBeGreaterThan(0);
	}

	/// <summary>
	/// Creates an instance of the internal TagCardinalityGuard via reflection.
	/// </summary>
	private static TagCardinalityGuard CreateGuard(int maxCardinality = 100, string overflowValue = "__other__")
	{
		// TagCardinalityGuard is internal, use assembly access (InternalsVisibleTo covers this,
		// but if not, we can use reflection). The test project should have access.
		return new TagCardinalityGuard(maxCardinality, overflowValue);
	}
}
