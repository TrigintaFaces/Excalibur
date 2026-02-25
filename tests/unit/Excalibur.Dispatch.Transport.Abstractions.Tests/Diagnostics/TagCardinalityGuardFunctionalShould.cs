// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Diagnostics;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Diagnostics;

/// <summary>
/// Functional tests for <see cref="TagCardinalityGuard"/> verifying cardinality limiting behavior.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TagCardinalityGuardFunctionalShould
{
	[Fact]
	public void Allow_values_within_cardinality_limit()
	{
		var guard = new TagCardinalityGuard(maxCardinality: 5);

		guard.Guard("queue-a").ShouldBe("queue-a");
		guard.Guard("queue-b").ShouldBe("queue-b");
		guard.Guard("queue-c").ShouldBe("queue-c");
	}

	[Fact]
	public void Return_overflow_when_limit_exceeded()
	{
		var guard = new TagCardinalityGuard(maxCardinality: 2, overflowValue: "__overflow__");

		guard.Guard("first").ShouldBe("first");
		guard.Guard("second").ShouldBe("second");

		// Third value should be overflow
		guard.Guard("third").ShouldBe("__overflow__");
		guard.Guard("fourth").ShouldBe("__overflow__");
	}

	[Fact]
	public void Return_known_values_after_limit_reached()
	{
		var guard = new TagCardinalityGuard(maxCardinality: 2);

		guard.Guard("alpha");
		guard.Guard("beta");

		// Known values still work even after limit
		guard.Guard("alpha").ShouldBe("alpha");
		guard.Guard("beta").ShouldBe("beta");

		// New values get overflow
		guard.Guard("gamma").ShouldBe("__other__");
	}

	[Fact]
	public void Return_overflow_for_null_value()
	{
		var guard = new TagCardinalityGuard(maxCardinality: 10);

		guard.Guard(null).ShouldBe("__other__");
	}

	[Fact]
	public void Use_custom_overflow_value()
	{
		var guard = new TagCardinalityGuard(maxCardinality: 1, overflowValue: "OVERFLOW");

		guard.Guard("first").ShouldBe("first");
		guard.Guard("second").ShouldBe("OVERFLOW");
	}

	[Fact]
	public void Default_overflow_value_is_other()
	{
		var guard = new TagCardinalityGuard(maxCardinality: 1);

		guard.Guard("first");

		guard.Guard("second").ShouldBe("__other__");
	}

	[Fact]
	public void Handle_concurrent_access_safely()
	{
		var guard = new TagCardinalityGuard(maxCardinality: 50);
		var results = new System.Collections.Concurrent.ConcurrentBag<string>();

		Parallel.For(0, 100, i =>
		{
			var result = guard.Guard($"value-{i}");
			results.Add(result);
		});

		// All results should be either a real value or overflow
		results.Count.ShouldBe(100);

		// At most 50 unique non-overflow values (might slightly exceed due to concurrency)
		var nonOverflow = results.Where(r => r != "__other__").Distinct().Count();
		nonOverflow.ShouldBeLessThanOrEqualTo(55); // Allow small overshoot
	}

	[Fact]
	public void Handle_large_cardinality_limit()
	{
		var guard = new TagCardinalityGuard(maxCardinality: 1000);

		for (var i = 0; i < 1000; i++)
		{
			guard.Guard($"value-{i}").ShouldBe($"value-{i}");
		}

		// 1001st value should overflow
		guard.Guard("overflow-value").ShouldBe("__other__");
	}
}
