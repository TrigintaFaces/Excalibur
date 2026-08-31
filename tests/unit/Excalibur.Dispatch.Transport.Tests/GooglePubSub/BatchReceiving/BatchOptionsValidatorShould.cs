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

    #endregion

    #region Multiple Failures

    #endregion

    #region Null Guard

    [Fact]
    public void ThrowOnNullOptions()
    {
        Should.Throw<ArgumentNullException>(() => _sut.Validate(null, null!));
    }

    #endregion
}
