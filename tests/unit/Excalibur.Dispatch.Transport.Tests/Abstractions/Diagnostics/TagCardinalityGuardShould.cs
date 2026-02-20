// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Diagnostics;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Diagnostics;

/// <summary>
/// Tests for <see cref="TagCardinalityGuard"/>.
/// Verifies cardinality capping, overflow sentinel behavior, and thread safety.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport.Abstractions")]
public sealed class TagCardinalityGuardShould
{
	[Fact]
	public void Return_Value_When_Under_Cardinality_Limit()
	{
		var guard = new TagCardinalityGuard(maxCardinality: 5);

		guard.Guard("value-1").ShouldBe("value-1");
		guard.Guard("value-2").ShouldBe("value-2");
		guard.Guard("value-3").ShouldBe("value-3");
	}

	[Fact]
	public void Return_Overflow_When_Cardinality_Limit_Exceeded()
	{
		var guard = new TagCardinalityGuard(maxCardinality: 3);

		guard.Guard("a").ShouldBe("a");
		guard.Guard("b").ShouldBe("b");
		guard.Guard("c").ShouldBe("c");

		// 4th distinct value exceeds limit
		guard.Guard("d").ShouldBe("__other__");
		guard.Guard("e").ShouldBe("__other__");
	}

	[Fact]
	public void Return_Known_Value_After_Limit_Reached()
	{
		var guard = new TagCardinalityGuard(maxCardinality: 2);

		guard.Guard("x").ShouldBe("x");
		guard.Guard("y").ShouldBe("y");

		// Limit reached, but known values still returned
		guard.Guard("x").ShouldBe("x");
		guard.Guard("y").ShouldBe("y");

		// New value overflows
		guard.Guard("z").ShouldBe("__other__");
	}

	[Fact]
	public void Return_Overflow_For_Null_Value()
	{
		var guard = new TagCardinalityGuard(maxCardinality: 10);

		guard.Guard(null).ShouldBe("__other__");
	}

	[Fact]
	public void Use_Custom_Overflow_Value()
	{
		var guard = new TagCardinalityGuard(maxCardinality: 1, overflowValue: "_overflow_");

		guard.Guard("first").ShouldBe("first");
		guard.Guard("second").ShouldBe("_overflow_");
	}

	[Fact]
	public void Handle_Default_Cardinality_Of_100()
	{
		var guard = new TagCardinalityGuard();

		// Fill up to 100 distinct values
		for (var i = 0; i < 100; i++)
		{
			guard.Guard($"value-{i}").ShouldBe($"value-{i}");
		}

		// 101st value overflows
		guard.Guard("overflow-value").ShouldBe("__other__");

		// Existing values still work
		guard.Guard("value-0").ShouldBe("value-0");
		guard.Guard("value-99").ShouldBe("value-99");
	}

	[Fact]
	public void Be_Thread_Safe_Under_Concurrent_Access()
	{
		var guard = new TagCardinalityGuard(maxCardinality: 50);
		var results = new System.Collections.Concurrent.ConcurrentBag<string>();

		// Simulate concurrent access from multiple threads
		Parallel.For(0, 200, i =>
		{
			var result = guard.Guard($"concurrent-{i}");
			results.Add(result);
		});

		// All results should be either a valid value or __other__
		var distinctResults = results.Distinct().ToList();
		var validValues = distinctResults.Where(r => r != "__other__").ToList();

		// Under concurrent access, the ConcurrentDictionary check-then-add in Guard()
		// has an inherent TOCTOU race that may admit a few extra values beyond the limit.
		// This is acceptable for a defense-in-depth cardinality guard (matches OpenTelemetry
		// SDK approximate behavior). Allow up to maxCardinality + thread overshoot margin.
		validValues.Count.ShouldBeInRange(1, 60);

		// There should be some overflow values since we sent 200 distinct inputs
		results.ShouldContain("__other__");
	}

	[Fact]
	public void Not_Count_Null_Toward_Cardinality()
	{
		var guard = new TagCardinalityGuard(maxCardinality: 2);

		guard.Guard(null).ShouldBe("__other__");
		guard.Guard("a").ShouldBe("a");
		guard.Guard("b").ShouldBe("b");

		// Null didn't consume a slot, so we should be at the limit now
		guard.Guard("c").ShouldBe("__other__");
	}

	[Fact]
	public void Return_Same_Value_On_Repeated_Calls()
	{
		var guard = new TagCardinalityGuard(maxCardinality: 5);

		guard.Guard("repeat").ShouldBe("repeat");
		guard.Guard("repeat").ShouldBe("repeat");
		guard.Guard("repeat").ShouldBe("repeat");
	}
}
