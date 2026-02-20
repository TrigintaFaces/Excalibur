using Excalibur.Dispatch.Metrics;

namespace Excalibur.Dispatch.Tests.Messaging.Metrics;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class HistogramConfigurationShould
{
	[Fact]
	public void CreateWithBuckets()
	{
		var config = new HistogramConfiguration(1.0, 5.0, 10.0);

		config.Buckets.Length.ShouldBe(3);
		config.Buckets[0].ShouldBe(1.0);
		config.Buckets[1].ShouldBe(5.0);
		config.Buckets[2].ShouldBe(10.0);
	}

	[Fact]
	public void SortBucketsAutomatically()
	{
		var config = new HistogramConfiguration(10.0, 1.0, 5.0);

		config.Buckets[0].ShouldBe(1.0);
		config.Buckets[1].ShouldBe(5.0);
		config.Buckets[2].ShouldBe(10.0);
	}

	[Fact]
	public void ThrowOnNullBuckets()
	{
		Should.Throw<ArgumentException>(() => new HistogramConfiguration(null!));
	}

	[Fact]
	public void ThrowOnEmptyBuckets()
	{
		Should.Throw<ArgumentException>(() => new HistogramConfiguration([]));
	}

	[Fact]
	public void CreateDefaultLatency()
	{
		var config = HistogramConfiguration.DefaultLatency;

		config.Buckets.Length.ShouldBe(14);
		config.Buckets[0].ShouldBe(0.005);
		config.Buckets[^1].ShouldBe(10);
	}

	[Fact]
	public void CreateDefaultSize()
	{
		var config = HistogramConfiguration.DefaultSize;

		config.Buckets.Length.ShouldBe(10);
		config.Buckets[0].ShouldBe(100);
	}

	[Fact]
	public void CreateExponentialBuckets()
	{
		var config = HistogramConfiguration.Exponential(100, 2, 5);

		config.Buckets.Length.ShouldBe(5);
		config.Buckets[0].ShouldBe(100);
		config.Buckets[1].ShouldBe(200);
		config.Buckets[2].ShouldBe(400);
		config.Buckets[3].ShouldBe(800);
		config.Buckets[4].ShouldBe(1600);
	}

	[Fact]
	public void Exponential_ThrowOnZeroStart()
	{
		Should.Throw<ArgumentException>(() => HistogramConfiguration.Exponential(0, 2, 5));
	}

	[Fact]
	public void Exponential_ThrowOnNegativeStart()
	{
		Should.Throw<ArgumentException>(() => HistogramConfiguration.Exponential(-1, 2, 5));
	}

	[Fact]
	public void Exponential_ThrowOnFactorLessThanOrEqualToOne()
	{
		Should.Throw<ArgumentException>(() => HistogramConfiguration.Exponential(100, 1, 5));
		Should.Throw<ArgumentException>(() => HistogramConfiguration.Exponential(100, 0.5, 5));
	}

	[Fact]
	public void Exponential_ThrowOnZeroCount()
	{
		Should.Throw<ArgumentException>(() => HistogramConfiguration.Exponential(100, 2, 0));
	}

	[Fact]
	public void CreateLinearBuckets()
	{
		var config = HistogramConfiguration.Linear(10, 5, 4);

		config.Buckets.Length.ShouldBe(4);
		config.Buckets[0].ShouldBe(10);
		config.Buckets[1].ShouldBe(15);
		config.Buckets[2].ShouldBe(20);
		config.Buckets[3].ShouldBe(25);
	}

	[Fact]
	public void Linear_ThrowOnZeroWidth()
	{
		Should.Throw<ArgumentException>(() => HistogramConfiguration.Linear(10, 0, 5));
	}

	[Fact]
	public void Linear_ThrowOnNegativeWidth()
	{
		Should.Throw<ArgumentException>(() => HistogramConfiguration.Linear(10, -1, 5));
	}

	[Fact]
	public void Linear_ThrowOnZeroCount()
	{
		Should.Throw<ArgumentException>(() => HistogramConfiguration.Linear(10, 5, 0));
	}
}
