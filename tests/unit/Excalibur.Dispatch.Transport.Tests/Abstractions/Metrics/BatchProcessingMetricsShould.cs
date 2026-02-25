// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;
using Shouldly;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Metrics;

[Trait("Category", "Unit")]
[Trait("Component", "Transport.Abstractions")]
public class BatchProcessingMetricsShould
{
    [Fact]
    public void HaveCorrectDefaultValues()
    {
        var metrics = new BatchProcessingMetrics();

        metrics.TotalBatchesProcessed.ShouldBe(0);
        metrics.TotalMessagesProcessed.ShouldBe(0);
        metrics.TotalSuccessfulMessages.ShouldBe(0);
        metrics.TotalFailedMessages.ShouldBe(0);
        metrics.AverageBatchSize.ShouldBe(0);
        metrics.AverageProcessingTime.ShouldBe(TimeSpan.Zero);
        metrics.AverageMessageProcessingTime.ShouldBe(TimeSpan.Zero);
        metrics.CurrentThroughput.ShouldBe(0);
        metrics.PeakThroughput.ShouldBe(0);
        metrics.SuccessRate.ShouldBe(0);
        metrics.ActiveBatches.ShouldBe(0);
        metrics.QueueDepth.ShouldBe(0);
        metrics.StartedAt.ShouldBe(default);
        metrics.LastUpdatedAt.ShouldBe(default);
    }

    [Fact]
    public void AllowSettingAllProperties()
    {
        var startedAt = DateTimeOffset.UtcNow.AddHours(-1);
        var lastUpdated = DateTimeOffset.UtcNow;

        var metrics = new BatchProcessingMetrics
        {
            TotalBatchesProcessed = 1000,
            TotalMessagesProcessed = 50000,
            TotalSuccessfulMessages = 49500,
            TotalFailedMessages = 500,
            AverageBatchSize = 50.0,
            AverageProcessingTime = TimeSpan.FromMilliseconds(100),
            AverageMessageProcessingTime = TimeSpan.FromMilliseconds(2),
            CurrentThroughput = 500.0,
            PeakThroughput = 1000.0,
            SuccessRate = 0.99,
            ActiveBatches = 5,
            QueueDepth = 100,
            StartedAt = startedAt,
            LastUpdatedAt = lastUpdated
        };

        metrics.TotalBatchesProcessed.ShouldBe(1000);
        metrics.TotalMessagesProcessed.ShouldBe(50000);
        metrics.TotalSuccessfulMessages.ShouldBe(49500);
        metrics.TotalFailedMessages.ShouldBe(500);
        metrics.AverageBatchSize.ShouldBe(50.0);
        metrics.AverageProcessingTime.ShouldBe(TimeSpan.FromMilliseconds(100));
        metrics.AverageMessageProcessingTime.ShouldBe(TimeSpan.FromMilliseconds(2));
        metrics.CurrentThroughput.ShouldBe(500.0);
        metrics.PeakThroughput.ShouldBe(1000.0);
        metrics.SuccessRate.ShouldBe(0.99);
        metrics.ActiveBatches.ShouldBe(5);
        metrics.QueueDepth.ShouldBe(100);
        metrics.StartedAt.ShouldBe(startedAt);
        metrics.LastUpdatedAt.ShouldBe(lastUpdated);
    }

    [Fact]
    public void CalculateUptimeCorrectly()
    {
        var startedAt = DateTimeOffset.UtcNow.AddHours(-2);

        var metrics = new BatchProcessingMetrics
        {
            StartedAt = startedAt
        };

        metrics.Uptime.TotalHours.ShouldBeGreaterThanOrEqualTo(2);
        metrics.Uptime.TotalHours.ShouldBeLessThanOrEqualTo(2.1); // Allow some margin
    }

    [Fact]
    public void AllowHighVolumeMetrics()
    {
        var metrics = new BatchProcessingMetrics
        {
            TotalBatchesProcessed = 10_000_000,
            TotalMessagesProcessed = 1_000_000_000,
            TotalSuccessfulMessages = 999_000_000,
            TotalFailedMessages = 1_000_000,
            CurrentThroughput = 100000.0,
            PeakThroughput = 150000.0
        };

        metrics.TotalBatchesProcessed.ShouldBe(10_000_000);
        metrics.TotalMessagesProcessed.ShouldBe(1_000_000_000);
    }

    [Fact]
    public void TrackSuccessRate()
    {
        var metrics = new BatchProcessingMetrics
        {
            TotalMessagesProcessed = 1000,
            TotalSuccessfulMessages = 990,
            TotalFailedMessages = 10,
            SuccessRate = 0.99 // 99%
        };

        metrics.SuccessRate.ShouldBe(0.99);
    }

    [Fact]
    public void AllowZeroSuccessRate()
    {
        var metrics = new BatchProcessingMetrics
        {
            TotalMessagesProcessed = 100,
            TotalSuccessfulMessages = 0,
            TotalFailedMessages = 100,
            SuccessRate = 0.0
        };

        metrics.SuccessRate.ShouldBe(0.0);
    }

    [Fact]
    public void AllowPerfectSuccessRate()
    {
        var metrics = new BatchProcessingMetrics
        {
            TotalMessagesProcessed = 10000,
            TotalSuccessfulMessages = 10000,
            TotalFailedMessages = 0,
            SuccessRate = 1.0
        };

        metrics.SuccessRate.ShouldBe(1.0);
    }

    [Fact]
    public void TrackBatchSizeMetrics()
    {
        var metrics = new BatchProcessingMetrics
        {
            TotalBatchesProcessed = 100,
            TotalMessagesProcessed = 5000,
            AverageBatchSize = 50.0
        };

        metrics.AverageBatchSize.ShouldBe(50.0);
    }

    [Fact]
    public void TrackProcessingTimeMetrics()
    {
        var metrics = new BatchProcessingMetrics
        {
            AverageProcessingTime = TimeSpan.FromMilliseconds(500),
            AverageMessageProcessingTime = TimeSpan.FromMilliseconds(10)
        };

        metrics.AverageProcessingTime.TotalMilliseconds.ShouldBe(500);
        metrics.AverageMessageProcessingTime.TotalMilliseconds.ShouldBe(10);
    }

    [Fact]
    public void TrackThroughputMetrics()
    {
        var metrics = new BatchProcessingMetrics
        {
            CurrentThroughput = 500.0, // 500 messages/second
            PeakThroughput = 1000.0    // 1000 messages/second peak
        };

        metrics.CurrentThroughput.ShouldBe(500.0);
        metrics.PeakThroughput.ShouldBe(1000.0);
        metrics.PeakThroughput.ShouldBeGreaterThanOrEqualTo(metrics.CurrentThroughput);
    }

    [Fact]
    public void TrackActiveBatchesAndQueueDepth()
    {
        var metrics = new BatchProcessingMetrics
        {
            ActiveBatches = 10,
            QueueDepth = 500
        };

        metrics.ActiveBatches.ShouldBe(10);
        metrics.QueueDepth.ShouldBe(500);
    }

    [Fact]
    public void AllowKafkaStyleBatchMetrics()
    {
        var metrics = new BatchProcessingMetrics
        {
            TotalBatchesProcessed = 5000,
            TotalMessagesProcessed = 500000,
            AverageBatchSize = 100.0,
            AverageProcessingTime = TimeSpan.FromMilliseconds(50),
            CurrentThroughput = 10000.0,
            ActiveBatches = 10
        };

        metrics.AverageBatchSize.ShouldBe(100.0);
        metrics.CurrentThroughput.ShouldBe(10000.0);
    }

    [Fact]
    public void AllowSqsBatchMetrics()
    {
        // SQS batches are limited to 10 messages
        var metrics = new BatchProcessingMetrics
        {
            TotalBatchesProcessed = 10000,
            TotalMessagesProcessed = 100000,
            AverageBatchSize = 10.0,
            AverageProcessingTime = TimeSpan.FromMilliseconds(200)
        };

        metrics.AverageBatchSize.ShouldBe(10.0);
    }

    [Fact]
    public void TrackTimestamps()
    {
        var startedAt = DateTimeOffset.UtcNow.AddHours(-1);
        var lastUpdated = DateTimeOffset.UtcNow;

        var metrics = new BatchProcessingMetrics
        {
            StartedAt = startedAt,
            LastUpdatedAt = lastUpdated
        };

        metrics.StartedAt.ShouldBeLessThan(metrics.LastUpdatedAt);
    }

    [Fact]
    public void AllowFractionalAverageBatchSize()
    {
        var metrics = new BatchProcessingMetrics
        {
            TotalBatchesProcessed = 3,
            TotalMessagesProcessed = 10,
            AverageBatchSize = 3.33
        };

        metrics.AverageBatchSize.ShouldBe(3.33);
    }
}
