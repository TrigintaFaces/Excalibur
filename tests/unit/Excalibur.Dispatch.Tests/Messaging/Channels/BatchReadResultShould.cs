using Excalibur.Dispatch.Channels;

namespace Excalibur.Dispatch.Tests.Messaging.Channels;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class BatchReadResultShould
{
    [Fact]
    public void CreateWithItemsAndHasItemsTrue()
    {
        var items = new List<int> { 1, 2, 3 };

        var result = new BatchReadResult<int>(items, true);

        result.Items.ShouldBeSameAs(items);
        result.HasItems.ShouldBeTrue();
        result.Count.ShouldBe(3);
    }

    [Fact]
    public void CreateWithEmptyListAndHasItemsFalse()
    {
        var items = new List<int>();

        var result = new BatchReadResult<int>(items, false);

        result.Items.ShouldBeEmpty();
        result.HasItems.ShouldBeFalse();
        result.Count.ShouldBe(0);
    }

    [Fact]
    public void ReturnZeroCountForNullItems()
    {
        var result = new BatchReadResult<string>(null!, false);

        result.Count.ShouldBe(0);
    }

    [Fact]
    public void ImplementEqualityByReferenceAndHasItems()
    {
        var items = new List<int> { 1, 2 };
        var result1 = new BatchReadResult<int>(items, true);
        var result2 = new BatchReadResult<int>(items, true);

        result1.Equals(result2).ShouldBeTrue();
        (result1 == result2).ShouldBeTrue();
        (result1 != result2).ShouldBeFalse();
    }

    [Fact]
    public void DetectInequalityWithDifferentHasItems()
    {
        var items = new List<int> { 1 };
        var result1 = new BatchReadResult<int>(items, true);
        var result2 = new BatchReadResult<int>(items, false);

        result1.Equals(result2).ShouldBeFalse();
        (result1 != result2).ShouldBeTrue();
    }

    [Fact]
    public void DetectInequalityWithDifferentItemsReference()
    {
        var items1 = new List<int> { 1 };
        var items2 = new List<int> { 1 };
        var result1 = new BatchReadResult<int>(items1, true);
        var result2 = new BatchReadResult<int>(items2, true);

        result1.Equals(result2).ShouldBeFalse();
    }

    [Fact]
    public void ImplementObjectEquals()
    {
        var items = new List<int> { 1 };
        var result = new BatchReadResult<int>(items, true);

        result.Equals((object)result).ShouldBeTrue();
        result.Equals((object)"not a result").ShouldBeFalse();
        result.Equals(null).ShouldBeFalse();
    }

    [Fact]
    public void ProduceConsistentHashCode()
    {
        var items = new List<int> { 1, 2, 3 };
        var result1 = new BatchReadResult<int>(items, true);
        var result2 = new BatchReadResult<int>(items, true);

        result1.GetHashCode().ShouldBe(result2.GetHashCode());
    }
}
