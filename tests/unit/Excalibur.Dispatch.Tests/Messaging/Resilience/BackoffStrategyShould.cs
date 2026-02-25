using Excalibur.Dispatch.Resilience;

namespace Excalibur.Dispatch.Tests.Messaging.Resilience;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class BackoffStrategyShould
{
    [Theory]
    [InlineData(BackoffStrategy.Fixed, 0)]
    [InlineData(BackoffStrategy.Linear, 1)]
    [InlineData(BackoffStrategy.Exponential, 2)]
    [InlineData(BackoffStrategy.ExponentialWithJitter, 3)]
    [InlineData(BackoffStrategy.Fibonacci, 4)]
    public void HaveCorrectEnumValues(BackoffStrategy strategy, int expected)
    {
        ((int)strategy).ShouldBe(expected);
    }

    [Fact]
    public void HaveAllValues()
    {
        var values = Enum.GetValues<BackoffStrategy>();
        values.Length.ShouldBe(5);
    }

    [Fact]
    public void DefaultToFixed()
    {
        var strategy = default(BackoffStrategy);
        strategy.ShouldBe(BackoffStrategy.Fixed);
    }

    [Fact]
    public void BeParsableFromString()
    {
        Enum.Parse<BackoffStrategy>("Exponential").ShouldBe(BackoffStrategy.Exponential);
        Enum.Parse<BackoffStrategy>("ExponentialWithJitter").ShouldBe(BackoffStrategy.ExponentialWithJitter);
        Enum.Parse<BackoffStrategy>("Fibonacci").ShouldBe(BackoffStrategy.Fibonacci);
    }
}
