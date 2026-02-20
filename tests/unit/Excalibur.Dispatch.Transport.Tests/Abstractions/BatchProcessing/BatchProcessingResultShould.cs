// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;
using Shouldly;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.BatchProcessing;

[Trait("Category", "Unit")]
[Trait("Component", "Transport.Abstractions")]
public class BatchProcessingResultShould
{
    [Fact]
    public void HaveCorrectDefaultValues()
    {
        var result = new BatchProcessingResult();

        result.BatchId.ShouldBe(string.Empty);
        result.SuccessCount.ShouldBe(0);
        result.FailureCount.ShouldBe(0);
        result.SkippedCount.ShouldBe(0);
        result.TotalCount.ShouldBe(0);
        result.ProcessingDuration.ShouldBe(TimeSpan.Zero);
        result.StartedAt.ShouldBe(default);
        result.CompletedAt.ShouldBe(default);
        result.MessageResults.ShouldNotBeNull();
        result.MessageResults.ShouldBeEmpty();
        result.Errors.ShouldNotBeNull();
        result.Errors.ShouldBeEmpty();
        result.IsSuccess.ShouldBeTrue();
        result.IsPartialSuccess.ShouldBeFalse();
        result.SuccessRate.ShouldBe(0);
        result.Metadata.ShouldNotBeNull();
        result.Metadata.ShouldBeEmpty();
    }

    [Fact]
    public void CalculateTotalCountCorrectly()
    {
        var result = new BatchProcessingResult
        {
            SuccessCount = 10,
            FailureCount = 3,
            SkippedCount = 2
        };

        result.TotalCount.ShouldBe(15);
    }

    [Fact]
    public void ReturnIsSuccessTrue_WhenNoFailures()
    {
        var result = new BatchProcessingResult
        {
            SuccessCount = 10,
            FailureCount = 0,
            SkippedCount = 2
        };

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void ReturnIsSuccessFalse_WhenHasFailures()
    {
        var result = new BatchProcessingResult
        {
            SuccessCount = 10,
            FailureCount = 1,
            SkippedCount = 0
        };

        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void ReturnIsPartialSuccessTrue_WhenHasBothSuccessesAndFailures()
    {
        var result = new BatchProcessingResult
        {
            SuccessCount = 8,
            FailureCount = 2,
            SkippedCount = 0
        };

        result.IsPartialSuccess.ShouldBeTrue();
    }

    [Fact]
    public void ReturnIsPartialSuccessFalse_WhenAllFailed()
    {
        var result = new BatchProcessingResult
        {
            SuccessCount = 0,
            FailureCount = 10,
            SkippedCount = 0
        };

        result.IsPartialSuccess.ShouldBeFalse();
    }

    [Fact]
    public void ReturnIsPartialSuccessFalse_WhenAllSucceeded()
    {
        var result = new BatchProcessingResult
        {
            SuccessCount = 10,
            FailureCount = 0,
            SkippedCount = 0
        };

        result.IsPartialSuccess.ShouldBeFalse();
    }

    [Fact]
    public void CalculateSuccessRateCorrectly()
    {
        var result = new BatchProcessingResult
        {
            SuccessCount = 8,
            FailureCount = 2,
            SkippedCount = 0
        };

        result.SuccessRate.ShouldBe(0.8);
    }

    [Fact]
    public void ReturnZeroSuccessRate_WhenNoMessages()
    {
        var result = new BatchProcessingResult
        {
            SuccessCount = 0,
            FailureCount = 0,
            SkippedCount = 0
        };

        result.SuccessRate.ShouldBe(0);
    }

    [Fact]
    public void ReturnFullSuccessRate_WhenAllSucceeded()
    {
        var result = new BatchProcessingResult
        {
            SuccessCount = 10,
            FailureCount = 0,
            SkippedCount = 0
        };

        result.SuccessRate.ShouldBe(1.0);
    }

    [Fact]
    public void IncludeSkippedInSuccessRateCalculation()
    {
        var result = new BatchProcessingResult
        {
            SuccessCount = 5,
            FailureCount = 3,
            SkippedCount = 2
        };

        result.SuccessRate.ShouldBe(0.5);
    }

    [Fact]
    public void AllowAddingMessageResults()
    {
        var result = new BatchProcessingResult();
        var messageResult = new MessageProcessingResult
        {
            MessageId = "msg-1",
            IsSuccess = true
        };

        result.MessageResults.Add(messageResult);

        result.MessageResults.Count.ShouldBe(1);
        result.MessageResults.ShouldContain(messageResult);
    }

    [Fact]
    public void AllowAddingErrors()
    {
        var result = new BatchProcessingResult();
        var error = new ProcessingError
        {
            Code = "ERR001",
            Message = "Test error"
        };

        result.Errors.Add(error);

        result.Errors.Count.ShouldBe(1);
        result.Errors.ShouldContain(error);
    }

    [Fact]
    public void AllowAddingMetadata()
    {
        var result = new BatchProcessingResult();

        result.Metadata["key1"] = "value1";
        result.Metadata["key2"] = 42;

        result.Metadata.Count.ShouldBe(2);
        result.Metadata["key1"].ShouldBe("value1");
        result.Metadata["key2"].ShouldBe(42);
    }

    [Fact]
    public void AllowSettingBatchId()
    {
        var result = new BatchProcessingResult { BatchId = "batch-123" };

        result.BatchId.ShouldBe("batch-123");
    }

    [Fact]
    public void AllowSettingProcessingDuration()
    {
        var duration = TimeSpan.FromSeconds(45);
        var result = new BatchProcessingResult { ProcessingDuration = duration };

        result.ProcessingDuration.ShouldBe(duration);
    }

    [Fact]
    public void AllowSettingTimestamps()
    {
        var started = DateTimeOffset.UtcNow;
        var completed = started.AddSeconds(30);

        var result = new BatchProcessingResult
        {
            StartedAt = started,
            CompletedAt = completed
        };

        result.StartedAt.ShouldBe(started);
        result.CompletedAt.ShouldBe(completed);
    }
}
