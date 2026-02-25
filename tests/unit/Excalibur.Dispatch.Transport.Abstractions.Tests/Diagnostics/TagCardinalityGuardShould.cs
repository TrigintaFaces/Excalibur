using Excalibur.Dispatch.Transport.Diagnostics;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Diagnostics;

public class TagCardinalityGuardShould
{
    [Fact]
    public void Guard_Should_Return_Value_Within_Limit()
    {
        var guard = new TagCardinalityGuard(maxCardinality: 10);

        guard.Guard("value1").ShouldBe("value1");
    }

    [Fact]
    public void Guard_Should_Return_Overflow_When_Limit_Exceeded()
    {
        var guard = new TagCardinalityGuard(maxCardinality: 2);

        guard.Guard("a").ShouldBe("a");
        guard.Guard("b").ShouldBe("b");
        guard.Guard("c").ShouldBe("__other__");
    }

    [Fact]
    public void Guard_Should_Return_Known_Value_After_Limit_Exceeded()
    {
        var guard = new TagCardinalityGuard(maxCardinality: 2);

        guard.Guard("a");
        guard.Guard("b");
        guard.Guard("c"); // overflow

        // Known values should still work
        guard.Guard("a").ShouldBe("a");
        guard.Guard("b").ShouldBe("b");
    }

    [Fact]
    public void Guard_Should_Return_Overflow_For_Null()
    {
        var guard = new TagCardinalityGuard();

        guard.Guard(null).ShouldBe("__other__");
    }

    [Fact]
    public void Guard_Should_Use_Custom_Overflow_Value()
    {
        var guard = new TagCardinalityGuard(maxCardinality: 1, overflowValue: "_overflow_");

        guard.Guard("a");
        guard.Guard("b").ShouldBe("_overflow_");
    }

    [Fact]
    public void Guard_Should_Default_Max_Cardinality_To_100()
    {
        var guard = new TagCardinalityGuard();

        // Add 100 distinct values - should all be fine
        for (var i = 0; i < 100; i++)
        {
            guard.Guard($"value-{i}").ShouldBe($"value-{i}");
        }

        // 101st should overflow
        guard.Guard("overflow-value").ShouldBe("__other__");
    }

    [Fact]
    public void Guard_Should_Be_Idempotent_For_Same_Value()
    {
        var guard = new TagCardinalityGuard(maxCardinality: 5);

        // Same value multiple times should not consume cardinality slots
        for (var i = 0; i < 20; i++)
        {
            guard.Guard("same-value").ShouldBe("same-value");
        }
    }
}
