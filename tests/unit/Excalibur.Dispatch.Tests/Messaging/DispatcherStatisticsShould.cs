using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Tests.Messaging;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DispatcherStatisticsShould
{
	[Fact]
	public void HaveDefaultValues()
	{
		var stats = new DispatcherStatistics();

		stats.TotalMessages.ShouldBe(0L);
		stats.TotalBatches.ShouldBe(0L);
		stats.AverageBatchSize.ShouldBe(0.0);
		stats.Throughput.ShouldBe(0.0);
		stats.QueueDepth.ShouldBe(0);
		stats.ElapsedMilliseconds.ShouldBe(0.0);
	}

	[Fact]
	public void AllowSettingViaInitProperties()
	{
		var stats = new DispatcherStatistics
		{
			TotalMessages = 1000,
			TotalBatches = 50,
			AverageBatchSize = 20.0,
			Throughput = 500.5,
			QueueDepth = 10,
			ElapsedMilliseconds = 2000.0,
		};

		stats.TotalMessages.ShouldBe(1000L);
		stats.TotalBatches.ShouldBe(50L);
		stats.AverageBatchSize.ShouldBe(20.0);
		stats.Throughput.ShouldBe(500.5);
		stats.QueueDepth.ShouldBe(10);
		stats.ElapsedMilliseconds.ShouldBe(2000.0);
	}

	[Fact]
	public void SupportEqualityComparison()
	{
		var stats1 = new DispatcherStatistics { TotalMessages = 100, TotalBatches = 5 };
		var stats2 = new DispatcherStatistics { TotalMessages = 100, TotalBatches = 5 };
		var stats3 = new DispatcherStatistics { TotalMessages = 200, TotalBatches = 5 };

		(stats1 == stats2).ShouldBeTrue();
		(stats1 != stats3).ShouldBeTrue();
		stats1.Equals(stats2).ShouldBeTrue();
		stats1.Equals(stats3).ShouldBeFalse();
	}

	[Fact]
	public void SupportEqualsWithObject()
	{
		var stats = new DispatcherStatistics { TotalMessages = 100 };

		stats.Equals((object)stats).ShouldBeTrue();
		stats.Equals(null).ShouldBeFalse();
		stats.Equals("not a stats").ShouldBeFalse();
	}

	[Fact]
	public void SupportGetHashCode()
	{
		var stats1 = new DispatcherStatistics { TotalMessages = 100, TotalBatches = 5 };
		var stats2 = new DispatcherStatistics { TotalMessages = 100, TotalBatches = 5 };

		stats1.GetHashCode().ShouldBe(stats2.GetHashCode());
	}
}
