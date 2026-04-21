// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Google;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub.BatchReceiving;

/// <summary>
/// Verifies cross-property validation in <see cref="BatchOptionsValidator"/>.
/// Sprint 746: BatchOptions split added BatchOptionsValidator -- no tests existed.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class BatchOptionsValidatorShould
{
    private readonly BatchOptionsValidator _sut = new();

    #region Happy Path

    [Fact]
    public void Succeed_WithValidDefaults()
    {
        var options = new BatchOptions();
        var result = _sut.Validate(null, options);
        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Succeed_WithCustomValidOptions()
    {
        var options = new BatchOptions
        {
            MaxMessagesPerBatch = 500,
            MinMessagesPerBatch = 10,
            MaxBatchWaitTime = TimeSpan.FromSeconds(1),
            MaxBatchSizeBytes = 1024,
            TargetBatchProcessingTime = TimeSpan.FromMilliseconds(100),
            ConcurrentBatchProcessors = 2,
            Acknowledgment = new BatchAcknowledgmentOptions { AckDeadlineSeconds = 30 },
        };

        var result = _sut.Validate("test", options);
        result.Succeeded.ShouldBeTrue();
    }

    #endregion

    #region MaxMessagesPerBatch Validation

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1001)]
    public void Fail_WhenMaxMessagesPerBatchOutOfRange(int value)
    {
        var options = new BatchOptions { MaxMessagesPerBatch = value };
        var result = _sut.Validate(null, options);
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(BatchOptions.MaxMessagesPerBatch));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1000)]
    public void Succeed_WhenMaxMessagesPerBatchAtBoundary(int value)
    {
        var options = new BatchOptions { MaxMessagesPerBatch = value, MinMessagesPerBatch = 1 };
        var result = _sut.Validate(null, options);
        result.Succeeded.ShouldBeTrue();
    }

    #endregion

    #region MinMessagesPerBatch Validation

    [Fact]
    public void Fail_WhenMinMessagesPerBatchLessThanOne()
    {
        var options = new BatchOptions { MinMessagesPerBatch = 0 };
        var result = _sut.Validate(null, options);
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(BatchOptions.MinMessagesPerBatch));
    }

    [Fact]
    public void Fail_WhenMinMessagesExceedsMax()
    {
        var options = new BatchOptions
        {
            MaxMessagesPerBatch = 100,
            MinMessagesPerBatch = 200,
        };

        var result = _sut.Validate(null, options);
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(BatchOptions.MinMessagesPerBatch));
    }

    [Fact]
    public void Succeed_WhenMinEqualsMax()
    {
        var options = new BatchOptions
        {
            MaxMessagesPerBatch = 100,
            MinMessagesPerBatch = 100,
        };

        var result = _sut.Validate(null, options);
        result.Succeeded.ShouldBeTrue();
    }

    #endregion

    #region TimeSpan Validation

    [Fact]
    public void Fail_WhenMaxBatchWaitTimeZero()
    {
        var options = new BatchOptions { MaxBatchWaitTime = TimeSpan.Zero };
        var result = _sut.Validate(null, options);
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(BatchOptions.MaxBatchWaitTime));
    }

    [Fact]
    public void Fail_WhenMaxBatchWaitTimeNegative()
    {
        var options = new BatchOptions { MaxBatchWaitTime = TimeSpan.FromMilliseconds(-1) };
        var result = _sut.Validate(null, options);
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(BatchOptions.MaxBatchWaitTime));
    }

    [Fact]
    public void Fail_WhenTargetBatchProcessingTimeZero()
    {
        var options = new BatchOptions { TargetBatchProcessingTime = TimeSpan.Zero };
        var result = _sut.Validate(null, options);
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(BatchOptions.TargetBatchProcessingTime));
    }

    [Fact]
    public void Fail_WhenTargetBatchProcessingTimeNegative()
    {
        var options = new BatchOptions { TargetBatchProcessingTime = TimeSpan.FromSeconds(-5) };
        var result = _sut.Validate(null, options);
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(BatchOptions.TargetBatchProcessingTime));
    }

    #endregion

    #region MaxBatchSizeBytes Validation

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Fail_WhenMaxBatchSizeBytesNotPositive(int value)
    {
        var options = new BatchOptions { MaxBatchSizeBytes = value };
        var result = _sut.Validate(null, options);
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(BatchOptions.MaxBatchSizeBytes));
    }

    #endregion

    #region ConcurrentBatchProcessors Validation

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Fail_WhenConcurrentBatchProcessorsNotPositive(int value)
    {
        var options = new BatchOptions { ConcurrentBatchProcessors = value };
        var result = _sut.Validate(null, options);
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(BatchOptions.ConcurrentBatchProcessors));
    }

    #endregion

    #region AckDeadlineSeconds Validation

    [Theory]
    [InlineData(9)]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(601)]
    public void Fail_WhenAckDeadlineSecondsOutOfRange(int value)
    {
        var options = new BatchOptions
        {
            Acknowledgment = new BatchAcknowledgmentOptions { AckDeadlineSeconds = value },
        };

        var result = _sut.Validate(null, options);
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(BatchAcknowledgmentOptions.AckDeadlineSeconds));
    }

    [Theory]
    [InlineData(10)]
    [InlineData(600)]
    public void Succeed_WhenAckDeadlineSecondsAtBoundary(int value)
    {
        var options = new BatchOptions
        {
            Acknowledgment = new BatchAcknowledgmentOptions { AckDeadlineSeconds = value },
        };

        var result = _sut.Validate(null, options);
        result.Succeeded.ShouldBeTrue();
    }

    #endregion

    #region Multiple Failures

    [Fact]
    public void ReportMultipleFailures()
    {
        var options = new BatchOptions
        {
            MaxMessagesPerBatch = 0,
            MinMessagesPerBatch = 0,
            MaxBatchWaitTime = TimeSpan.Zero,
            MaxBatchSizeBytes = 0,
            TargetBatchProcessingTime = TimeSpan.Zero,
            ConcurrentBatchProcessors = 0,
            Acknowledgment = new BatchAcknowledgmentOptions { AckDeadlineSeconds = 5 },
        };

        var result = _sut.Validate(null, options);
        result.Failed.ShouldBeTrue();

        // Should report all failures, not just the first
        result.FailureMessage.ShouldContain(nameof(BatchOptions.MaxMessagesPerBatch));
        result.FailureMessage.ShouldContain(nameof(BatchOptions.MaxBatchWaitTime));
        result.FailureMessage.ShouldContain(nameof(BatchOptions.MaxBatchSizeBytes));
        result.FailureMessage.ShouldContain(nameof(BatchOptions.TargetBatchProcessingTime));
        result.FailureMessage.ShouldContain(nameof(BatchOptions.ConcurrentBatchProcessors));
        result.FailureMessage.ShouldContain(nameof(BatchAcknowledgmentOptions.AckDeadlineSeconds));
    }

    #endregion

    #region Null Guard

    [Fact]
    public void ThrowOnNullOptions()
    {
        Should.Throw<ArgumentNullException>(() => _sut.Validate(null, null!));
    }

    #endregion
}
