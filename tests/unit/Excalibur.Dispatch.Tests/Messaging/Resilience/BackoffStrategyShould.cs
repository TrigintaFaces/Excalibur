using Excalibur.Dispatch.Resilience;

namespace Excalibur.Dispatch.Tests.Messaging.Resilience;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class BackoffStrategyShould
{
    [Theory]
    [InlineData(BackoffStrategy.Constant, 0)]
    [InlineData(BackoffStrategy.Linear, 1)]
    [InlineData(BackoffStrategy.Exponential, 2)]
    public void HaveCorrectEnumValues(BackoffStrategy strategy, int expected)
    {
        ((int)strategy).ShouldBe(expected);
    }

    [Fact]
    public void HaveAllValues()
    {
        var values = Enum.GetValues<BackoffStrategy>();
        values.Length.ShouldBe(3);
    }

    [Fact]
    public void DefaultToFixed()
    {
        var strategy = default(BackoffStrategy);
        strategy.ShouldBe(BackoffStrategy.Constant);
    }

    [Fact]
    public void BeParsableFromString()
    {
        Enum.Parse<BackoffStrategy>("Exponential").ShouldBe(BackoffStrategy.Exponential);
    }
}
