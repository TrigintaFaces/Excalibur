// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;
using Shouldly;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Statistics;

[Trait("Category", "Unit")]
[Trait("Component", "Transport.Abstractions")]
public class LongPollingStatisticsShould
{
    [Fact]
    public void HaveCorrectDefaultValues()
    {
        var stats = new LongPollingStatistics();

        stats.TotalReceives.ShouldBe(0);
        stats.TotalMessages.ShouldBe(0);
        stats.EmptyReceives.ShouldBe(0);
        stats.CurrentLoadFactor.ShouldBe(0);
        stats.CurrentWaitTime.ShouldBe(TimeSpan.Zero);
        stats.ApiCallsSaved.ShouldBe(0);
        stats.LastReceiveTime.ShouldBe(default);
    }

    [Fact]
    public void AllowSettingAllProperties()
    {
        var lastReceive = DateTimeOffset.UtcNow;
        var waitTime = TimeSpan.FromSeconds(20);

        var stats = new LongPollingStatistics
        {
            TotalReceives = 1000,
            TotalMessages = 5000,
            EmptyReceives = 100,
            CurrentLoadFactor = 0.75,
            CurrentWaitTime = waitTime,
            ApiCallsSaved = 4000,
            LastReceiveTime = lastReceive
        };

        stats.TotalReceives.ShouldBe(1000);
        stats.TotalMessages.ShouldBe(5000);
        stats.EmptyReceives.ShouldBe(100);
        stats.CurrentLoadFactor.ShouldBe(0.75);
        stats.CurrentWaitTime.ShouldBe(waitTime);
        stats.ApiCallsSaved.ShouldBe(4000);
        stats.LastReceiveTime.ShouldBe(lastReceive);
    }

    [Fact]
    public void CalculateAverageMessagesPerReceiveCorrectly()
    {
        var stats = new LongPollingStatistics
        {
            TotalReceives = 100,
            TotalMessages = 500
        };

        stats.AverageMessagesPerReceive.ShouldBe(5.0);
    }

    [Fact]
    public void ReturnZeroAverageWhenNoReceives()
    {
        var stats = new LongPollingStatistics
        {
            TotalReceives = 0,
            TotalMessages = 0
        };

        stats.AverageMessagesPerReceive.ShouldBe(0);
    }

    [Fact]
    public void CalculateEmptyReceiveRateCorrectly()
    {
        var stats = new LongPollingStatistics
        {
            TotalReceives = 100,
            EmptyReceives = 25
        };

        stats.EmptyReceiveRate.ShouldBe(0.25);
    }

    [Fact]
    public void ReturnZeroEmptyRateWhenNoReceives()
    {
        var stats = new LongPollingStatistics
        {
            TotalReceives = 0,
            EmptyReceives = 0
        };

        stats.EmptyReceiveRate.ShouldBe(0);
    }

    [Fact]
    public void AllowHighThroughputMetrics()
    {
        var stats = new LongPollingStatistics
        {
            TotalReceives = 1_000_000,
            TotalMessages = 50_000_000,
            EmptyReceives = 100_000,
            ApiCallsSaved = 49_000_000
        };

        stats.TotalReceives.ShouldBe(1_000_000);
        stats.TotalMessages.ShouldBe(50_000_000);
        stats.AverageMessagesPerReceive.ShouldBe(50.0);
    }

    [Fact]
    public void TrackApiCallSavings()
    {
        // Without long polling: 50,000 messages = 50,000 API calls
        // With long polling: 50,000 messages in 1,000 receives = 49,000 calls saved
        var stats = new LongPollingStatistics
        {
            TotalReceives = 1000,
            TotalMessages = 50000,
            ApiCallsSaved = 49000
        };

        stats.ApiCallsSaved.ShouldBe(49000);
    }

    [Fact]
    public void TrackLoadFactor()
    {
        var stats = new LongPollingStatistics
        {
            CurrentLoadFactor = 0.85 // 85% load
        };

        stats.CurrentLoadFactor.ShouldBe(0.85);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void AllowLoadFactorRange(double loadFactor)
    {
        var stats = new LongPollingStatistics
        {
            CurrentLoadFactor = loadFactor
        };

        stats.CurrentLoadFactor.ShouldBe(loadFactor);
    }

    [Fact]
    public void TrackWaitTime()
    {
        var stats = new LongPollingStatistics
        {
            CurrentWaitTime = TimeSpan.FromSeconds(30)
        };

        stats.CurrentWaitTime.TotalSeconds.ShouldBe(30);
    }

    [Fact]
    public void AllowSqsStyleLongPollingMetrics()
    {
        // SQS long polling typically uses 20 second wait
        var stats = new LongPollingStatistics
        {
            TotalReceives = 500,
            TotalMessages = 2500,
            EmptyReceives = 100,
            CurrentWaitTime = TimeSpan.FromSeconds(20),
            ApiCallsSaved = 2000
        };

        stats.CurrentWaitTime.TotalSeconds.ShouldBe(20);
        stats.EmptyReceiveRate.ShouldBe(0.2);
    }

    [Fact]
    public void BeImmutableAsRecordStruct()
    {
        // Record structs should support with expressions
        var original = new LongPollingStatistics
        {
            TotalReceives = 100,
            TotalMessages = 500
        };

        var updated = original with { TotalReceives = 200 };

        original.TotalReceives.ShouldBe(100);
        updated.TotalReceives.ShouldBe(200);
        updated.TotalMessages.ShouldBe(500); // Preserved from original
    }

    [Fact]
    public void SupportEquality()
    {
        var stats1 = new LongPollingStatistics
        {
            TotalReceives = 100,
            TotalMessages = 500
        };

        var stats2 = new LongPollingStatistics
        {
            TotalReceives = 100,
            TotalMessages = 500
        };

        stats1.ShouldBe(stats2);
        (stats1 == stats2).ShouldBeTrue();
    }

    [Fact]
    public void HandleHighEmptyReceiveRate()
    {
        var stats = new LongPollingStatistics
        {
            TotalReceives = 1000,
            TotalMessages = 100,
            EmptyReceives = 900
        };

        stats.EmptyReceiveRate.ShouldBe(0.9);
        stats.AverageMessagesPerReceive.ShouldBe(0.1);
    }
}
