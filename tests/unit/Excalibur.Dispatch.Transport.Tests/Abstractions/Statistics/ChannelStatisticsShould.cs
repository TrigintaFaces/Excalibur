// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;
using Shouldly;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Statistics;

[Trait("Category", "Unit")]
[Trait("Component", "Transport.Abstractions")]
public class ChannelStatisticsShould
{
    [Fact]
    public void HaveCorrectDefaultValues()
    {
        var stats = new ChannelStatistics();

        stats.CurrentCount.ShouldBe(0);
        stats.Capacity.ShouldBe(0);
        stats.TotalWritten.ShouldBe(0);
        stats.TotalRead.ShouldBe(0);
        stats.FullCount.ShouldBe(0);
        stats.IsWriterCompleted.ShouldBeFalse();
    }

    [Fact]
    public void AllowSettingAllProperties()
    {
        var stats = new ChannelStatistics
        {
            CurrentCount = 50,
            Capacity = 100,
            TotalWritten = 10000,
            TotalRead = 9950,
            FullCount = 25,
            IsWriterCompleted = true
        };

        stats.CurrentCount.ShouldBe(50);
        stats.Capacity.ShouldBe(100);
        stats.TotalWritten.ShouldBe(10000);
        stats.TotalRead.ShouldBe(9950);
        stats.FullCount.ShouldBe(25);
        stats.IsWriterCompleted.ShouldBeTrue();
    }

    [Fact]
    public void CalculateUtilizationPercentageCorrectly()
    {
        var stats = new ChannelStatistics
        {
            CurrentCount = 50,
            Capacity = 100
        };

        stats.UtilizationPercentage.ShouldBe(50.0);
    }

    [Fact]
    public void ReturnZeroUtilizationWhenCapacityIsZero()
    {
        var stats = new ChannelStatistics
        {
            CurrentCount = 50,
            Capacity = 0
        };

        stats.UtilizationPercentage.ShouldBe(0);
    }

    [Fact]
    public void CalculateFullUtilization()
    {
        var stats = new ChannelStatistics
        {
            CurrentCount = 100,
            Capacity = 100
        };

        stats.UtilizationPercentage.ShouldBe(100.0);
    }

    [Fact]
    public void CalculateLowUtilization()
    {
        var stats = new ChannelStatistics
        {
            CurrentCount = 1,
            Capacity = 1000
        };

        stats.UtilizationPercentage.ShouldBe(0.1);
    }

    [Fact]
    public void AllowHighThroughputMetrics()
    {
        var stats = new ChannelStatistics
        {
            CurrentCount = 500,
            Capacity = 1000,
            TotalWritten = 1_000_000_000,
            TotalRead = 999_999_500,
            FullCount = 10000
        };

        stats.TotalWritten.ShouldBe(1_000_000_000);
        stats.TotalRead.ShouldBe(999_999_500);
        stats.FullCount.ShouldBe(10000);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AllowSettingIsWriterCompleted(bool isCompleted)
    {
        var stats = new ChannelStatistics { IsWriterCompleted = isCompleted };

        stats.IsWriterCompleted.ShouldBe(isCompleted);
    }

    [Fact]
    public void AllowEmptyChannelStats()
    {
        var stats = new ChannelStatistics
        {
            CurrentCount = 0,
            Capacity = 1000,
            TotalWritten = 0,
            TotalRead = 0,
            FullCount = 0,
            IsWriterCompleted = false
        };

        stats.CurrentCount.ShouldBe(0);
        stats.UtilizationPercentage.ShouldBe(0);
    }

    [Fact]
    public void AllowActiveChannelStats()
    {
        var stats = new ChannelStatistics
        {
            CurrentCount = 750,
            Capacity = 1000,
            TotalWritten = 5000,
            TotalRead = 4250,
            FullCount = 5,
            IsWriterCompleted = false
        };

        stats.UtilizationPercentage.ShouldBe(75.0);
        // Current = TotalWritten - TotalRead
        (stats.TotalWritten - stats.TotalRead).ShouldBe(stats.CurrentCount);
    }

    [Fact]
    public void AllowCompletedChannelStats()
    {
        var stats = new ChannelStatistics
        {
            CurrentCount = 0,
            Capacity = 100,
            TotalWritten = 1000,
            TotalRead = 1000,
            FullCount = 50,
            IsWriterCompleted = true
        };

        stats.IsWriterCompleted.ShouldBeTrue();
        stats.TotalWritten.ShouldBe(stats.TotalRead);
    }

    [Fact]
    public void CalculateFractionalUtilization()
    {
        var stats = new ChannelStatistics
        {
            CurrentCount = 33,
            Capacity = 100
        };

        stats.UtilizationPercentage.ShouldBe(33.0);
    }
}
