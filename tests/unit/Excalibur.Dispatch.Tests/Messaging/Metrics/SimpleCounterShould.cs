using Excalibur.Dispatch.Metrics;

namespace Excalibur.Dispatch.Tests.Messaging.Metrics;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SimpleCounterShould
{
	[Fact]
	public void DefaultToZero()
	{
		var counter = new SimpleCounter();

		counter.Value.ShouldBe(0);
	}

	[Fact]
	public void IncrementByDefault()
	{
		var counter = new SimpleCounter();

		counter.Increment();

		counter.Value.ShouldBe(1);
	}

	[Fact]
	public void IncrementBySpecifiedAmount()
	{
		var counter = new SimpleCounter();

		counter.Increment(5.5);

		counter.Value.ShouldBe(5.5);
	}

	[Fact]
	public void AccumulateMultipleIncrements()
	{
		var counter = new SimpleCounter();

		counter.Increment(10);
		counter.Increment(20);
		counter.Increment(30);

		counter.Value.ShouldBe(60);
	}
}
