// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;
using Shouldly;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.DeadLetterQueue;

[Trait("Category", "Unit")]
[Trait("Component", "Transport.Abstractions")]
public class ReprocessResultShould
{
    [Fact]
    public void HaveCorrectDefaultValues()
    {
        var result = new ReprocessResult();

        result.SuccessCount.ShouldBe(0);
        result.FailureCount.ShouldBe(0);
        result.SkippedCount.ShouldBe(0);
        result.TotalCount.ShouldBe(0);
        result.Failures.ShouldNotBeNull();
        result.Failures.ShouldBeEmpty();
        result.ProcessingTime.ShouldBe(TimeSpan.Zero);
        result.IsSuccess.ShouldBeTrue(); // No failures = success
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(100)]
    public void AllowSettingSuccessCount(int count)
    {
        var result = new ReprocessResult { SuccessCount = count };

        result.SuccessCount.ShouldBe(count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(50)]
    public void AllowSettingFailureCount(int count)
    {
        var result = new ReprocessResult { FailureCount = count };

        result.FailureCount.ShouldBe(count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(25)]
    public void AllowSettingSkippedCount(int count)
    {
        var result = new ReprocessResult { SkippedCount = count };

        result.SkippedCount.ShouldBe(count);
    }

    [Fact]
    public void ComputeTotalCount()
    {
        var result = new ReprocessResult
        {
            SuccessCount = 50,
            FailureCount = 10,
            SkippedCount = 5
        };

        result.TotalCount.ShouldBe(65);
    }

    [Fact]
    public void AllowSettingProcessingTime()
    {
        var time = TimeSpan.FromSeconds(30);
        var result = new ReprocessResult { ProcessingTime = time };

        result.ProcessingTime.ShouldBe(time);
    }

    [Fact]
    public void ComputeIsSuccess_WhenNoFailures()
    {
        var result = new ReprocessResult
        {
            SuccessCount = 100,
            FailureCount = 0,
            SkippedCount = 5
        };

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void ComputeIsSuccess_WhenHasFailures()
    {
        var result = new ReprocessResult
        {
            SuccessCount = 95,
            FailureCount = 5,
            SkippedCount = 0
        };

        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void AllowSettingFailures()
    {
        var failures = new List<ReprocessFailure>
        {
            new()
            {
                Message = new DeadLetterMessage { OriginalMessage = new TransportMessage { Id = "msg-1" } },
                Reason = "Connection timeout",
                Exception = new TimeoutException("Connection timed out")
            },
            new()
            {
                Message = new DeadLetterMessage { OriginalMessage = new TransportMessage { Id = "msg-2" } },
                Reason = "Invalid format"
            }
        };

        var result = new ReprocessResult { Failures = failures };

        result.Failures.Count.ShouldBe(2);
        result.Failures.First().Reason.ShouldBe("Connection timeout");
        result.Failures.Last().Reason.ShouldBe("Invalid format");
    }

    [Fact]
    public void AllowSuccessfulReprocessResult()
    {
        var result = new ReprocessResult
        {
            SuccessCount = 100,
            FailureCount = 0,
            SkippedCount = 0,
            ProcessingTime = TimeSpan.FromSeconds(5)
        };

        result.TotalCount.ShouldBe(100);
        result.IsSuccess.ShouldBeTrue();
        result.Failures.ShouldBeEmpty();
    }

    [Fact]
    public void AllowPartiallySuccessfulReprocessResult()
    {
        var result = new ReprocessResult
        {
            SuccessCount = 90,
            FailureCount = 5,
            SkippedCount = 5,
            ProcessingTime = TimeSpan.FromSeconds(10),
            Failures =
            [
                new ReprocessFailure
                {
                    Message = new DeadLetterMessage { OriginalMessage = new TransportMessage() },
                    Reason = "Transient error"
                }
            ]
        };

        result.TotalCount.ShouldBe(100);
        result.IsSuccess.ShouldBeFalse();
        result.SuccessCount.ShouldBe(90);
        result.FailureCount.ShouldBe(5);
        result.SkippedCount.ShouldBe(5);
    }

    [Fact]
    public void AllowCompleteFailureReprocessResult()
    {
        var result = new ReprocessResult
        {
            SuccessCount = 0,
            FailureCount = 50,
            SkippedCount = 0,
            ProcessingTime = TimeSpan.FromSeconds(2)
        };

        result.TotalCount.ShouldBe(50);
        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void AllowAllSkippedReprocessResult()
    {
        var result = new ReprocessResult
        {
            SuccessCount = 0,
            FailureCount = 0,
            SkippedCount = 100,
            ProcessingTime = TimeSpan.FromMilliseconds(500)
        };

        result.TotalCount.ShouldBe(100);
        result.IsSuccess.ShouldBeTrue(); // No failures means success
        result.SkippedCount.ShouldBe(100);
    }

    [Fact]
    public void AllowEmptyReprocessResult()
    {
        var result = new ReprocessResult
        {
            SuccessCount = 0,
            FailureCount = 0,
            SkippedCount = 0,
            ProcessingTime = TimeSpan.Zero
        };

        result.TotalCount.ShouldBe(0);
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void AllowLongRunningReprocessResult()
    {
        var result = new ReprocessResult
        {
            SuccessCount = 10000,
            FailureCount = 5,
            SkippedCount = 100,
            ProcessingTime = TimeSpan.FromMinutes(30)
        };

        result.TotalCount.ShouldBe(10105);
        result.ProcessingTime.ShouldBe(TimeSpan.FromMinutes(30));
    }
}
