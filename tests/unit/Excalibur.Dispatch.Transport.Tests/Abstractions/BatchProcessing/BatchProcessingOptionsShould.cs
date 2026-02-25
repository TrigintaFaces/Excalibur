// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;
using Shouldly;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.BatchProcessing;

[Trait("Category", "Unit")]
[Trait("Component", "Transport.Abstractions")]
public class BatchProcessingOptionsShould
{
    [Fact]
    public void HaveCorrectDefaultValues()
    {
        var options = new BatchProcessingOptions();

        options.MaxBatchSize.ShouldBe(100);
        options.BatchTimeout.ShouldBe(TimeSpan.FromSeconds(30));
        options.ProcessInParallel.ShouldBeTrue();
        options.MaxDegreeOfParallelism.ShouldBe(Environment.ProcessorCount);
        options.ContinueOnError.ShouldBeTrue();
        options.RetryPolicy.ShouldNotBeNull();
        options.EnableMetrics.ShouldBeTrue();
        options.EnableDeadLetter.ShouldBeTrue();
        options.CompletionStrategy.ShouldBe(BatchCompletionStrategy.Size);
        options.MinBatchSize.ShouldBe(1);
        options.CollectionTimeout.ShouldBe(TimeSpan.FromSeconds(5));
        options.PreserveOrder.ShouldBeFalse();
        options.DefaultPriority.ShouldBe(BatchPriority.Normal);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(50)]
    [InlineData(500)]
    [InlineData(1000)]
    public void AllowSettingMaxBatchSize(int maxBatchSize)
    {
        var options = new BatchProcessingOptions { MaxBatchSize = maxBatchSize };

        options.MaxBatchSize.ShouldBe(maxBatchSize);
    }

    [Fact]
    public void AllowSettingBatchTimeout()
    {
        var timeout = TimeSpan.FromMinutes(5);
        var options = new BatchProcessingOptions { BatchTimeout = timeout };

        options.BatchTimeout.ShouldBe(timeout);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AllowSettingProcessInParallel(bool processInParallel)
    {
        var options = new BatchProcessingOptions { ProcessInParallel = processInParallel };

        options.ProcessInParallel.ShouldBe(processInParallel);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(16)]
    public void AllowSettingMaxDegreeOfParallelism(int degree)
    {
        var options = new BatchProcessingOptions { MaxDegreeOfParallelism = degree };

        options.MaxDegreeOfParallelism.ShouldBe(degree);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AllowSettingContinueOnError(bool continueOnError)
    {
        var options = new BatchProcessingOptions { ContinueOnError = continueOnError };

        options.ContinueOnError.ShouldBe(continueOnError);
    }

    [Fact]
    public void AllowSettingRetryPolicy()
    {
        var retryPolicy = new RetryPolicy { MaxRetries = 5, BackoffMultiplier = 3.0 };
        var options = new BatchProcessingOptions { RetryPolicy = retryPolicy };

        options.RetryPolicy.ShouldBe(retryPolicy);
        options.RetryPolicy.MaxRetries.ShouldBe(5);
        options.RetryPolicy.BackoffMultiplier.ShouldBe(3.0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AllowSettingEnableMetrics(bool enableMetrics)
    {
        var options = new BatchProcessingOptions { EnableMetrics = enableMetrics };

        options.EnableMetrics.ShouldBe(enableMetrics);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AllowSettingEnableDeadLetter(bool enableDeadLetter)
    {
        var options = new BatchProcessingOptions { EnableDeadLetter = enableDeadLetter };

        options.EnableDeadLetter.ShouldBe(enableDeadLetter);
    }

    [Theory]
    [InlineData(BatchCompletionStrategy.Size)]
    [InlineData(BatchCompletionStrategy.Time)]
    [InlineData(BatchCompletionStrategy.SizeOrTime)]
    public void AllowSettingCompletionStrategy(BatchCompletionStrategy strategy)
    {
        var options = new BatchProcessingOptions { CompletionStrategy = strategy };

        options.CompletionStrategy.ShouldBe(strategy);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(50)]
    public void AllowSettingMinBatchSize(int minBatchSize)
    {
        var options = new BatchProcessingOptions { MinBatchSize = minBatchSize };

        options.MinBatchSize.ShouldBe(minBatchSize);
    }

    [Fact]
    public void AllowSettingCollectionTimeout()
    {
        var timeout = TimeSpan.FromSeconds(10);
        var options = new BatchProcessingOptions { CollectionTimeout = timeout };

        options.CollectionTimeout.ShouldBe(timeout);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AllowSettingPreserveOrder(bool preserveOrder)
    {
        var options = new BatchProcessingOptions { PreserveOrder = preserveOrder };

        options.PreserveOrder.ShouldBe(preserveOrder);
    }

    [Theory]
    [InlineData(BatchPriority.Low)]
    [InlineData(BatchPriority.Normal)]
    [InlineData(BatchPriority.High)]
    [InlineData(BatchPriority.Critical)]
    public void AllowSettingDefaultPriority(BatchPriority priority)
    {
        var options = new BatchProcessingOptions { DefaultPriority = priority };

        options.DefaultPriority.ShouldBe(priority);
    }

    [Fact]
    public void AllowChainingAllSettings()
    {
        var options = new BatchProcessingOptions
        {
            MaxBatchSize = 200,
            BatchTimeout = TimeSpan.FromMinutes(2),
            ProcessInParallel = false,
            MaxDegreeOfParallelism = 8,
            ContinueOnError = false,
            EnableMetrics = false,
            EnableDeadLetter = false,
            CompletionStrategy = BatchCompletionStrategy.Time,
            MinBatchSize = 5,
            CollectionTimeout = TimeSpan.FromSeconds(15),
            PreserveOrder = true,
            DefaultPriority = BatchPriority.High
        };

        options.MaxBatchSize.ShouldBe(200);
        options.BatchTimeout.ShouldBe(TimeSpan.FromMinutes(2));
        options.ProcessInParallel.ShouldBeFalse();
        options.MaxDegreeOfParallelism.ShouldBe(8);
        options.ContinueOnError.ShouldBeFalse();
        options.EnableMetrics.ShouldBeFalse();
        options.EnableDeadLetter.ShouldBeFalse();
        options.CompletionStrategy.ShouldBe(BatchCompletionStrategy.Time);
        options.MinBatchSize.ShouldBe(5);
        options.CollectionTimeout.ShouldBe(TimeSpan.FromSeconds(15));
        options.PreserveOrder.ShouldBeTrue();
        options.DefaultPriority.ShouldBe(BatchPriority.High);
    }
}
