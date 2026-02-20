using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.BatchProcessing;

public class BatchProcessingMetricsShould
{
    [Fact]
    public void Uptime_Should_Calculate_From_StartedAt()
    {
        var metrics = new BatchProcessingMetrics
        {
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
        };

        metrics.Uptime.TotalMinutes.ShouldBeGreaterThanOrEqualTo(4.9);
        metrics.Uptime.TotalMinutes.ShouldBeLessThanOrEqualTo(5.5);
    }

    [Fact]
    public void Should_Default_All_Numeric_Fields_To_Zero()
    {
        var metrics = new BatchProcessingMetrics();

        metrics.TotalBatchesProcessed.ShouldBe(0);
        metrics.TotalMessagesProcessed.ShouldBe(0);
        metrics.TotalSuccessfulMessages.ShouldBe(0);
        metrics.TotalFailedMessages.ShouldBe(0);
        metrics.AverageBatchSize.ShouldBe(0);
        metrics.CurrentThroughput.ShouldBe(0);
        metrics.PeakThroughput.ShouldBe(0);
        metrics.SuccessRate.ShouldBe(0);
        metrics.ActiveBatches.ShouldBe(0);
        metrics.QueueDepth.ShouldBe(0);
    }

    [Fact]
    public void Should_Allow_Setting_All_Properties()
    {
        var now = DateTimeOffset.UtcNow;
        var metrics = new BatchProcessingMetrics
        {
            TotalBatchesProcessed = 100,
            TotalMessagesProcessed = 5000,
            TotalSuccessfulMessages = 4900,
            TotalFailedMessages = 100,
            AverageBatchSize = 50,
            AverageProcessingTime = TimeSpan.FromMilliseconds(200),
            AverageMessageProcessingTime = TimeSpan.FromMilliseconds(4),
            CurrentThroughput = 250.5,
            PeakThroughput = 500.0,
            SuccessRate = 0.98,
            ActiveBatches = 3,
            QueueDepth = 42,
            StartedAt = now,
            LastUpdatedAt = now,
        };

        metrics.TotalBatchesProcessed.ShouldBe(100);
        metrics.TotalMessagesProcessed.ShouldBe(5000);
        metrics.CurrentThroughput.ShouldBe(250.5);
        metrics.PeakThroughput.ShouldBe(500.0);
    }
}
