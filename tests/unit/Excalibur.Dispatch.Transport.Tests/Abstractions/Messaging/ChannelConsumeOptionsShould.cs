// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Threading.Channels;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Transport;
using Shouldly;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Messaging;

[Trait("Category", "Unit")]
[Trait("Component", "Transport.Abstractions")]
public class ChannelConsumeOptionsShould
{
    [Fact]
    public void HaveCorrectDefaultValues()
    {
        var options = new ChannelConsumeOptions();

        // Buffer defaults
        options.Buffer.ChannelOptions.ShouldBeNull();
        options.Buffer.ChannelCapacity.ShouldBe(1000);
        options.Buffer.FullMode.ShouldBe(BoundedChannelFullMode.Wait);
        options.Buffer.AllowSynchronousContinuations.ShouldBeFalse();
        options.Buffer.BatchSize.ShouldBe(10);
        options.Buffer.MaxWaitTime.ShouldBe(TimeSpan.FromSeconds(1));
        options.Buffer.MaxConcurrency.ShouldBe(Environment.ProcessorCount);
        options.Buffer.PrefetchCount.ShouldBe(10);

        // Root defaults
        options.CompleteChannelOnStop.ShouldBeTrue();
        options.PreserveOrdering.ShouldBeFalse();
        options.OrderingKeySelector.ShouldBeNull();

        // Acknowledgment defaults
        options.Acknowledgment.AutoAcknowledge.ShouldBeTrue();
        options.Acknowledgment.VisibilityTimeout.ShouldBeNull();
        options.Acknowledgment.UseBatchAcknowledgment.ShouldBeTrue();
        options.Acknowledgment.AcknowledgmentBatchSize.ShouldBe(10);
        options.Acknowledgment.AcknowledgmentBatchTimeout.ShouldBe(TimeSpan.FromSeconds(5));

        // Retry defaults
        options.Retry.EnableAutoRetry.ShouldBeTrue();
        options.Retry.MaxRetryAttempts.ShouldBe(3);
        options.Retry.RetryDelay.ShouldBe(TimeSpan.FromSeconds(1));
        options.Retry.UseExponentialBackoff.ShouldBeTrue();
        options.Retry.DeadLetterStrategy.ShouldBe(DeadLetterStrategy.MoveToDeadLetterQueue);
    }

    [Fact]
    public void ProvideDefaultStaticProperty()
    {
        var options = ChannelConsumeOptions.Default;

        options.ShouldNotBeNull();
        options.Buffer.ChannelCapacity.ShouldBe(1000);
        options.Buffer.MaxConcurrency.ShouldBe(Environment.ProcessorCount);
    }

    [Fact]
    public void ProvideHighThroughputConfiguration()
    {
        var options = ChannelConsumeOptions.HighThroughput;

        options.Buffer.MaxConcurrency.ShouldBe(Environment.ProcessorCount * 2);
        options.Buffer.PrefetchCount.ShouldBe(100);
        options.Acknowledgment.UseBatchAcknowledgment.ShouldBeTrue();
        options.Acknowledgment.AcknowledgmentBatchSize.ShouldBe(50);
        options.Acknowledgment.AutoAcknowledge.ShouldBeTrue();
        options.Retry.EnableAutoRetry.ShouldBeFalse();
    }

    [Fact]
    public void ProvideOrderedProcessingConfiguration()
    {
        var options = ChannelConsumeOptions.Ordered;

        options.Buffer.MaxConcurrency.ShouldBe(1);
        options.Buffer.PrefetchCount.ShouldBe(1);
        options.PreserveOrdering.ShouldBeTrue();
        options.Acknowledgment.AutoAcknowledge.ShouldBeTrue();
        options.Acknowledgment.UseBatchAcknowledgment.ShouldBeFalse();
    }

    [Fact]
    public void ProvideReliableProcessingConfiguration()
    {
        var options = ChannelConsumeOptions.Reliable;

        options.Buffer.MaxConcurrency.ShouldBe(Environment.ProcessorCount / 2);
        options.Buffer.PrefetchCount.ShouldBe(5);
        options.Acknowledgment.AutoAcknowledge.ShouldBeFalse();
        options.Retry.EnableAutoRetry.ShouldBeTrue();
        options.Retry.MaxRetryAttempts.ShouldBe(5);
        options.Retry.UseExponentialBackoff.ShouldBeTrue();
        options.Retry.DeadLetterStrategy.ShouldBe(DeadLetterStrategy.MoveToDeadLetterQueue);
    }

    [Fact]
    public void AllowSettingAllProperties()
    {
        var channelOptions = new UnboundedChannelOptions();
        var visibilityTimeout = TimeSpan.FromMinutes(5);
        Func<MessageEnvelope, string?> keySelector = _ => "key";

        var options = new ChannelConsumeOptions
        {
            Buffer =
            {
                ChannelOptions = channelOptions,
                ChannelCapacity = 5000,
                FullMode = BoundedChannelFullMode.DropOldest,
                AllowSynchronousContinuations = true,
                BatchSize = 50,
                MaxWaitTime = TimeSpan.FromSeconds(5),
                MaxConcurrency = 16,
                PrefetchCount = 100,
            },
            Acknowledgment =
            {
                AutoAcknowledge = false,
                VisibilityTimeout = visibilityTimeout,
                UseBatchAcknowledgment = false,
                AcknowledgmentBatchSize = 25,
                AcknowledgmentBatchTimeout = TimeSpan.FromSeconds(10),
            },
            CompleteChannelOnStop = false,
            PreserveOrdering = true,
            OrderingKeySelector = keySelector,
            Retry =
            {
                EnableAutoRetry = false,
                MaxRetryAttempts = 10,
                RetryDelay = TimeSpan.FromSeconds(5),
                UseExponentialBackoff = false,
                DeadLetterStrategy = DeadLetterStrategy.Drop,
            },
        };

        options.Buffer.ChannelOptions.ShouldBeSameAs(channelOptions);
        options.Buffer.ChannelCapacity.ShouldBe(5000);
        options.Buffer.FullMode.ShouldBe(BoundedChannelFullMode.DropOldest);
        options.Buffer.AllowSynchronousContinuations.ShouldBeTrue();
        options.Buffer.BatchSize.ShouldBe(50);
        options.Buffer.MaxWaitTime.ShouldBe(TimeSpan.FromSeconds(5));
        options.CompleteChannelOnStop.ShouldBeFalse();
        options.Buffer.MaxConcurrency.ShouldBe(16);
        options.Buffer.PrefetchCount.ShouldBe(100);
        options.Acknowledgment.AutoAcknowledge.ShouldBeFalse();
        options.Acknowledgment.VisibilityTimeout.ShouldBe(visibilityTimeout);
        options.Acknowledgment.UseBatchAcknowledgment.ShouldBeFalse();
        options.Acknowledgment.AcknowledgmentBatchSize.ShouldBe(25);
        options.Acknowledgment.AcknowledgmentBatchTimeout.ShouldBe(TimeSpan.FromSeconds(10));
        options.PreserveOrdering.ShouldBeTrue();
        options.OrderingKeySelector.ShouldBeSameAs(keySelector);
        options.Retry.EnableAutoRetry.ShouldBeFalse();
        options.Retry.MaxRetryAttempts.ShouldBe(10);
        options.Retry.RetryDelay.ShouldBe(TimeSpan.FromSeconds(5));
        options.Retry.UseExponentialBackoff.ShouldBeFalse();
        options.Retry.DeadLetterStrategy.ShouldBe(DeadLetterStrategy.Drop);
    }

    [Theory]
    [InlineData(BoundedChannelFullMode.Wait)]
    [InlineData(BoundedChannelFullMode.DropNewest)]
    [InlineData(BoundedChannelFullMode.DropOldest)]
    [InlineData(BoundedChannelFullMode.DropWrite)]
    public void AllowSettingFullMode(BoundedChannelFullMode fullMode)
    {
        var options = new ChannelConsumeOptions { Buffer = { FullMode = fullMode } };

        options.Buffer.FullMode.ShouldBe(fullMode);
    }

    [Theory]
    [InlineData(DeadLetterStrategy.Drop)]
    [InlineData(DeadLetterStrategy.MoveToDeadLetterQueue)]
    [InlineData(DeadLetterStrategy.RetryIndefinitely)]
    public void AllowSettingDeadLetterStrategy(DeadLetterStrategy strategy)
    {
        var options = new ChannelConsumeOptions { Retry = { DeadLetterStrategy = strategy } };

        options.Retry.DeadLetterStrategy.ShouldBe(strategy);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AllowSettingAutoAcknowledge(bool autoAck)
    {
        var options = new ChannelConsumeOptions { Acknowledgment = { AutoAcknowledge = autoAck } };

        options.Acknowledgment.AutoAcknowledge.ShouldBe(autoAck);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AllowSettingPreserveOrdering(bool preserveOrdering)
    {
        var options = new ChannelConsumeOptions { PreserveOrdering = preserveOrdering };

        options.PreserveOrdering.ShouldBe(preserveOrdering);
    }

    [Fact]
    public void AllowNullVisibilityTimeout()
    {
        var options = new ChannelConsumeOptions { Acknowledgment = { VisibilityTimeout = null } };

        options.Acknowledgment.VisibilityTimeout.ShouldBeNull();
    }

    [Fact]
    public void AllowSettingVisibilityTimeout()
    {
        var timeout = TimeSpan.FromMinutes(5);
        var options = new ChannelConsumeOptions { Acknowledgment = { VisibilityTimeout = timeout } };

        options.Acknowledgment.VisibilityTimeout.ShouldBe(timeout);
    }

    [Fact]
    public void AllowSqsStyleConfiguration()
    {
        var options = new ChannelConsumeOptions
        {
            Buffer = { MaxConcurrency = 10, PrefetchCount = 10 },
            Acknowledgment = { VisibilityTimeout = TimeSpan.FromSeconds(30) },
            Retry = { MaxRetryAttempts = 5, DeadLetterStrategy = DeadLetterStrategy.MoveToDeadLetterQueue },
        };

        options.Acknowledgment.VisibilityTimeout.ShouldBe(TimeSpan.FromSeconds(30));
        options.Retry.DeadLetterStrategy.ShouldBe(DeadLetterStrategy.MoveToDeadLetterQueue);
    }

    [Fact]
    public void AllowKafkaStyleConfiguration()
    {
        var options = new ChannelConsumeOptions
        {
            Buffer = { MaxConcurrency = Environment.ProcessorCount * 2, BatchSize = 100, MaxWaitTime = TimeSpan.FromMilliseconds(500) },
            PreserveOrdering = false,
            Acknowledgment = { UseBatchAcknowledgment = true },
        };

        options.Buffer.BatchSize.ShouldBe(100);
        options.Buffer.MaxWaitTime.ShouldBe(TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public void AllowRabbitMqStyleConfiguration()
    {
        var options = new ChannelConsumeOptions
        {
            Buffer = { PrefetchCount = 50 },
            Acknowledgment = { AutoAcknowledge = false },
            Retry = { EnableAutoRetry = true, MaxRetryAttempts = 3 },
        };

        options.Buffer.PrefetchCount.ShouldBe(50);
        options.Acknowledgment.AutoAcknowledge.ShouldBeFalse();
    }

    [Fact]
    public void AllowCustomOrderingKeySelector()
    {
        var options = new ChannelConsumeOptions
        {
            PreserveOrdering = true,
            OrderingKeySelector = envelope => envelope.CorrelationId
        };

        options.PreserveOrdering.ShouldBeTrue();
        options.OrderingKeySelector.ShouldNotBeNull();
    }

    [Fact]
    public void AllowRetryConfiguration()
    {
        var options = new ChannelConsumeOptions
        {
            Retry =
            {
                EnableAutoRetry = true,
                MaxRetryAttempts = 10,
                RetryDelay = TimeSpan.FromSeconds(2),
                UseExponentialBackoff = true,
            },
        };

        options.Retry.EnableAutoRetry.ShouldBeTrue();
        options.Retry.MaxRetryAttempts.ShouldBe(10);
        options.Retry.RetryDelay.ShouldBe(TimeSpan.FromSeconds(2));
        options.Retry.UseExponentialBackoff.ShouldBeTrue();
    }

    [Fact]
    public void AllowBatchAcknowledgmentConfiguration()
    {
        var options = new ChannelConsumeOptions
        {
            Acknowledgment =
            {
                UseBatchAcknowledgment = true,
                AcknowledgmentBatchSize = 100,
                AcknowledgmentBatchTimeout = TimeSpan.FromSeconds(30),
            },
        };

        options.Acknowledgment.UseBatchAcknowledgment.ShouldBeTrue();
        options.Acknowledgment.AcknowledgmentBatchSize.ShouldBe(100);
        options.Acknowledgment.AcknowledgmentBatchTimeout.ShouldBe(TimeSpan.FromSeconds(30));
    }
}
