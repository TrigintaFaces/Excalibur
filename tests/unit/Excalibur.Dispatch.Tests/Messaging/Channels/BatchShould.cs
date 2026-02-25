using Excalibur.Dispatch.Channels;

namespace Excalibur.Dispatch.Tests.Messaging.Channels;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class BatchShould
{
    [Fact]
    public void CreateWithValidItems()
    {
        var items = new List<string> { "a", "b", "c" };

        var batch = new Batch<string>(items);

        batch.Items.ShouldBeSameAs(items);
        batch.Count.ShouldBe(3);
        batch.Timestamp.ShouldBeGreaterThan(DateTimeOffset.MinValue);
    }

    [Fact]
    public void ThrowOnNullItems()
    {
        Should.Throw<ArgumentNullException>(() => new Batch<string>(null!));
    }

    [Fact]
    public void AllowEmptyItems()
    {
        var items = new List<int>();

        var batch = new Batch<int>(items);

        batch.Count.ShouldBe(0);
        batch.Items.ShouldBeEmpty();
    }

    [Fact]
    public void ImplementEqualityByReferenceAndTimestamp()
    {
        var items = new List<string> { "x" };
        var batch1 = new Batch<string>(items);

        // Same reference and same timestamp means equal
        batch1.Equals(batch1).ShouldBeTrue();
    }

    [Fact]
    public void DetectInequalityWithDifferentItems()
    {
        var items1 = new List<string> { "x" };
        var items2 = new List<string> { "x" };
        var batch1 = new Batch<string>(items1);
        var batch2 = new Batch<string>(items2);

        // Different references
        batch1.Equals(batch2).ShouldBeFalse();
    }

    [Fact]
    public void ImplementOperatorEquality()
    {
        var items = new List<string> { "x" };
        var batch1 = new Batch<string>(items);
        var batch2 = batch1;

#pragma warning disable CS1718 // Comparison made to same variable - intentional test of operator==
        (batch1 == batch1).ShouldBeTrue();
#pragma warning restore CS1718
        (batch1 == batch2).ShouldBeTrue();
    }

    [Fact]
    public void ImplementOperatorInequality()
    {
        var items1 = new List<string> { "x" };
        var items2 = new List<string> { "y" };
        var batch1 = new Batch<string>(items1);
        var batch2 = new Batch<string>(items2);

        (batch1 != batch2).ShouldBeTrue();
    }

    [Fact]
    public void ImplementObjectEquals()
    {
        var items = new List<string> { "x" };
        var batch = new Batch<string>(items);

        batch.Equals((object)batch).ShouldBeTrue();
        batch.Equals((object)"not a batch").ShouldBeFalse();
        batch.Equals(null).ShouldBeFalse();
    }

    [Fact]
    public void ProduceHashCode()
    {
        var items = new List<string> { "x" };
        var batch = new Batch<string>(items);

        // Just verify it doesn't throw and returns a value
        batch.GetHashCode().ShouldBeOfType<int>();
    }
}
