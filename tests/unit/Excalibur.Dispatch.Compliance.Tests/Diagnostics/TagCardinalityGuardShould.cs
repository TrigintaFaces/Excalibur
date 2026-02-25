using Excalibur.Dispatch.Compliance.Diagnostics;

namespace Excalibur.Dispatch.Compliance.Tests.Diagnostics;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class TagCardinalityGuardShould
{
	[Fact]
	public void Return_value_within_cardinality_limit()
	{
		var guard = new TagCardinalityGuard(maxCardinality: 5);

		var result = guard.Guard("value1");

		result.ShouldBe("value1");
	}

	[Fact]
	public void Return_overflow_when_limit_exceeded()
	{
		var guard = new TagCardinalityGuard(maxCardinality: 2);

		guard.Guard("v1");
		guard.Guard("v2");
		var result = guard.Guard("v3");

		result.ShouldBe("__other__");
	}

	[Fact]
	public void Return_known_value_after_limit_reached()
	{
		var guard = new TagCardinalityGuard(maxCardinality: 2);

		guard.Guard("v1");
		guard.Guard("v2");
		guard.Guard("v3"); // overflow

		var result = guard.Guard("v1"); // known value should still work

		result.ShouldBe("v1");
	}

	[Fact]
	public void Return_overflow_for_null_value()
	{
		var guard = new TagCardinalityGuard();

		var result = guard.Guard(null);

		result.ShouldBe("__other__");
	}

	[Fact]
	public void Use_custom_overflow_value()
	{
		var guard = new TagCardinalityGuard(maxCardinality: 1, overflowValue: "OVERFLOW");

		guard.Guard("v1");
		var result = guard.Guard("v2");

		result.ShouldBe("OVERFLOW");
	}

	[Fact]
	public void Allow_same_value_multiple_times()
	{
		var guard = new TagCardinalityGuard(maxCardinality: 2);

		guard.Guard("v1");
		guard.Guard("v1");
		guard.Guard("v1");

		var result = guard.Guard("v2");

		result.ShouldBe("v2");
	}

	[Fact]
	public void Handle_default_cardinality()
	{
		var guard = new TagCardinalityGuard();

		for (var i = 0; i < 100; i++)
		{
			var result = guard.Guard($"value-{i}");
			result.ShouldBe($"value-{i}");
		}

		// 101st value should overflow
		var overflow = guard.Guard("value-100");
		overflow.ShouldBe("__other__");
	}

	[Fact]
	public void Be_thread_safe()
	{
		var guard = new TagCardinalityGuard(maxCardinality: 50);

		Parallel.For(0, 100, i =>
		{
			var result = guard.Guard($"thread-{i}");
			// Should be either the original value or __other__, never throw
			result.ShouldNotBeNull();
		});
	}
}
