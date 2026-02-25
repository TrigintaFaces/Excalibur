using Excalibur.Dispatch.Buffers;

namespace Excalibur.Dispatch.Tests.Messaging.Buffers;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class BufferStatisticsShould
{
    [Fact]
    public void HaveDefaultValues()
    {
        var stats = new BufferStatistics();

        stats.TotalRented.ShouldBe(0);
        stats.TotalReturned.ShouldBe(0);
        stats.OutstandingBuffers.ShouldBe(0);
        stats.SizeDistribution.ShouldNotBeNull();
        stats.SizeDistribution.Count.ShouldBe(0);
    }

    [Fact]
    public void AllowSettingProperties()
    {
        var stats = new BufferStatistics
        {
            TotalRented = 100,
            TotalReturned = 95,
            OutstandingBuffers = 5,
        };

        stats.TotalRented.ShouldBe(100);
        stats.TotalReturned.ShouldBe(95);
        stats.OutstandingBuffers.ShouldBe(5);
    }

    [Fact]
    public void AllowPopulatingSizeDistribution()
    {
        var stats = new BufferStatistics
        {
            SizeDistribution = new Dictionary<int, long>
            {
                { 256, 10 },
                { 1024, 20 },
                { 4096, 5 },
            },
        };

        stats.SizeDistribution.Count.ShouldBe(3);
        stats.SizeDistribution[256].ShouldBe(10);
        stats.SizeDistribution[1024].ShouldBe(20);
        stats.SizeDistribution[4096].ShouldBe(5);
    }
}
