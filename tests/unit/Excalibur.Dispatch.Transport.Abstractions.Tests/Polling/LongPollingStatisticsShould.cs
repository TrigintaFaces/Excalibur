using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Polling;

public class LongPollingStatisticsShould
{
    [Fact]
    public void AverageMessagesPerReceive_Should_Calculate_Correctly()
    {
        var stats = new LongPollingStatistics
        {
            TotalReceives = 10,
            TotalMessages = 50,
        };

        stats.AverageMessagesPerReceive.ShouldBe(5.0);
    }

    [Fact]
    public void AverageMessagesPerReceive_Should_Be_Zero_When_No_Receives()
    {
        var stats = new LongPollingStatistics
        {
            TotalReceives = 0,
            TotalMessages = 0,
        };

        stats.AverageMessagesPerReceive.ShouldBe(0);
    }

    [Fact]
    public void EmptyReceiveRate_Should_Calculate_Correctly()
    {
        var stats = new LongPollingStatistics
        {
            TotalReceives = 100,
            EmptyReceives = 25,
        };

        stats.EmptyReceiveRate.ShouldBe(0.25);
    }

    [Fact]
    public void EmptyReceiveRate_Should_Be_Zero_When_No_Receives()
    {
        var stats = new LongPollingStatistics
        {
            TotalReceives = 0,
            EmptyReceives = 0,
        };

        stats.EmptyReceiveRate.ShouldBe(0);
    }

    [Fact]
    public void Should_Be_Readonly_Record_Struct()
    {
        var stats = new LongPollingStatistics
        {
            TotalReceives = 10,
            TotalMessages = 100,
            EmptyReceives = 2,
            CurrentLoadFactor = 0.8,
            CurrentWaitTime = TimeSpan.FromMilliseconds(500),
            ApiCallsSaved = 50,
            LastReceiveTime = DateTimeOffset.UtcNow,
        };

        stats.TotalReceives.ShouldBe(10);
        stats.TotalMessages.ShouldBe(100);
        stats.EmptyReceives.ShouldBe(2);
        stats.CurrentLoadFactor.ShouldBe(0.8);
        stats.CurrentWaitTime.ShouldBe(TimeSpan.FromMilliseconds(500));
        stats.ApiCallsSaved.ShouldBe(50);
    }

    [Fact]
    public void Should_Support_Equality()
    {
        var stats1 = new LongPollingStatistics { TotalReceives = 10, TotalMessages = 50 };
        var stats2 = new LongPollingStatistics { TotalReceives = 10, TotalMessages = 50 };

        stats1.ShouldBe(stats2);
    }
}
