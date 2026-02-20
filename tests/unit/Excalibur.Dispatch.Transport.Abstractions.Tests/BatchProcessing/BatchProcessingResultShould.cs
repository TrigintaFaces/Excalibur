using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.BatchProcessing;

public class BatchProcessingResultShould
{
    [Fact]
    public void TotalCount_Should_Be_Sum_Of_Success_Failure_Skipped()
    {
        var result = new BatchProcessingResult
        {
            SuccessCount = 5,
            FailureCount = 2,
            SkippedCount = 1,
        };

        result.TotalCount.ShouldBe(8);
    }

    [Fact]
    public void IsSuccess_Should_Be_True_When_No_Failures()
    {
        var result = new BatchProcessingResult
        {
            SuccessCount = 10,
            FailureCount = 0,
        };

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void IsSuccess_Should_Be_False_When_Any_Failure()
    {
        var result = new BatchProcessingResult
        {
            SuccessCount = 9,
            FailureCount = 1,
        };

        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void IsPartialSuccess_Should_Be_True_When_Both_Success_And_Failure()
    {
        var result = new BatchProcessingResult
        {
            SuccessCount = 5,
            FailureCount = 3,
        };

        result.IsPartialSuccess.ShouldBeTrue();
    }

    [Fact]
    public void IsPartialSuccess_Should_Be_False_When_All_Succeed()
    {
        var result = new BatchProcessingResult
        {
            SuccessCount = 10,
            FailureCount = 0,
        };

        result.IsPartialSuccess.ShouldBeFalse();
    }

    [Fact]
    public void SuccessRate_Should_Calculate_Correctly()
    {
        var result = new BatchProcessingResult
        {
            SuccessCount = 8,
            FailureCount = 2,
        };

        result.SuccessRate.ShouldBe(0.8);
    }

    [Fact]
    public void SuccessRate_Should_Be_Zero_When_TotalCount_Is_Zero()
    {
        var result = new BatchProcessingResult();

        result.SuccessRate.ShouldBe(0);
    }

    [Fact]
    public void Should_Default_Collections_To_Empty()
    {
        var result = new BatchProcessingResult();

        result.MessageResults.ShouldNotBeNull();
        result.MessageResults.Count.ShouldBe(0);
        result.Errors.ShouldNotBeNull();
        result.Errors.Count.ShouldBe(0);
        result.Metadata.ShouldNotBeNull();
        result.Metadata.Count.ShouldBe(0);
    }

    [Fact]
    public void Should_Default_BatchId_To_Empty()
    {
        var result = new BatchProcessingResult();

        result.BatchId.ShouldBe(string.Empty);
    }

    [Fact]
    public void Should_Store_Processing_Timestamps_And_Duration()
    {
        var startedAt = DateTimeOffset.UtcNow.AddSeconds(-5);
        var completedAt = DateTimeOffset.UtcNow;
        var duration = completedAt - startedAt;

        var result = new BatchProcessingResult
        {
            StartedAt = startedAt,
            CompletedAt = completedAt,
            ProcessingDuration = duration,
        };

        result.StartedAt.ShouldBe(startedAt);
        result.CompletedAt.ShouldBe(completedAt);
        result.ProcessingDuration.ShouldBe(duration);
    }
}
