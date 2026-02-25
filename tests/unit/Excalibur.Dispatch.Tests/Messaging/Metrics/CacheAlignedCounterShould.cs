using Excalibur.Dispatch.Metrics;

namespace Excalibur.Dispatch.Tests.Messaging.Metrics;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CacheAlignedCounterShould
{
	[Fact]
	public void DefaultToZero()
	{
		var counter = new CacheAlignedCounter();

		counter.Value.ShouldBe(0);
	}

	[Fact]
	public void CreateWithInitialValue()
	{
		var counter = CacheAlignedCounter.Create(42);

		counter.Value.ShouldBe(42);
	}

	[Fact]
	public void CreateWithDefaultZero()
	{
		var counter = CacheAlignedCounter.Create();

		counter.Value.ShouldBe(0);
	}

	[Fact]
	public void IncrementByOne()
	{
		var counter = CacheAlignedCounter.Create(10);

		var result = counter.Increment();

		result.ShouldBe(11);
		counter.Value.ShouldBe(11);
	}

	[Fact]
	public void DecrementByOne()
	{
		var counter = CacheAlignedCounter.Create(10);

		var result = counter.Decrement();

		result.ShouldBe(9);
		counter.Value.ShouldBe(9);
	}

	[Fact]
	public void AddPositiveValue()
	{
		var counter = CacheAlignedCounter.Create(5);

		var result = counter.Add(10);

		result.ShouldBe(15);
		counter.Value.ShouldBe(15);
	}

	[Fact]
	public void AddNegativeValue()
	{
		var counter = CacheAlignedCounter.Create(10);

		var result = counter.Add(-3);

		result.ShouldBe(7);
		counter.Value.ShouldBe(7);
	}

	[Fact]
	public void ExchangeReturnsPreviousValue()
	{
		var counter = CacheAlignedCounter.Create(42);

		var previous = counter.Exchange(100);

		previous.ShouldBe(42);
		counter.Value.ShouldBe(100);
	}

	[Fact]
	public void CompareExchange_WhenMatches()
	{
		var counter = CacheAlignedCounter.Create(42);

		var original = counter.CompareExchange(100, 42);

		original.ShouldBe(42);
		counter.Value.ShouldBe(100);
	}

	[Fact]
	public void CompareExchange_WhenDoesNotMatch()
	{
		var counter = CacheAlignedCounter.Create(42);

		var original = counter.CompareExchange(100, 999);

		original.ShouldBe(42);
		counter.Value.ShouldBe(42);
	}

	[Fact]
	public void ResetToZero()
	{
		var counter = CacheAlignedCounter.Create(42);

		counter.Reset();

		counter.Value.ShouldBe(0);
	}

	[Fact]
	public void SupportEquality()
	{
		var c1 = CacheAlignedCounter.Create(42);
		var c2 = CacheAlignedCounter.Create(42);
		var c3 = CacheAlignedCounter.Create(99);

		c1.Equals(c2).ShouldBeTrue();
		c1.Equals(c3).ShouldBeFalse();
		(c1 == c2).ShouldBeTrue();
		(c1 != c3).ShouldBeTrue();
	}

	[Fact]
	public void SupportEqualsWithObject()
	{
		var c = CacheAlignedCounter.Create(10);

		c.Equals((object)CacheAlignedCounter.Create(10)).ShouldBeTrue();
		c.Equals(null).ShouldBeFalse();
		c.Equals("not a counter").ShouldBeFalse();
	}

	[Fact]
	public void SupportGetHashCode()
	{
		var c1 = CacheAlignedCounter.Create(42);
		var c2 = CacheAlignedCounter.Create(42);

		c1.GetHashCode().ShouldBe(c2.GetHashCode());
	}
}
