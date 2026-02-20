// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;
using Shouldly;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.DeadLetterQueue;

[Trait("Category", "Unit")]
[Trait("Component", "Transport.Abstractions")]
public class ReprocessOptionsShould
{
    [Fact]
    public void HaveCorrectDefaultValues()
    {
        var options = new ReprocessOptions();

        options.TargetQueue.ShouldBeNull();
        options.MessageFilter.ShouldBeNull();
        options.MessageTransform.ShouldBeNull();
        options.RetryDelay.ShouldBe(TimeSpan.FromMilliseconds(100));
        options.RemoveFromDlq.ShouldBeTrue();
        options.Priority.ShouldBeNull();
        options.TimeToLive.ShouldBeNull();
        options.MaxMessages.ShouldBeNull();
        options.ProcessInParallel.ShouldBeFalse();
        options.MaxDegreeOfParallelism.ShouldBe(4);
    }

    [Theory]
    [InlineData("orders-queue")]
    [InlineData("events-topic/subscriptions/processor")]
    [InlineData(null)]
    public void AllowSettingTargetQueue(string? target)
    {
        var options = new ReprocessOptions { TargetQueue = target };

        options.TargetQueue.ShouldBe(target);
    }

    [Fact]
    public void AllowSettingMessageFilter()
    {
        Func<DeadLetterMessage, bool> filter = msg => msg.DeliveryAttempts < 5;
        var options = new ReprocessOptions { MessageFilter = filter };

        options.MessageFilter.ShouldNotBeNull();
        options.MessageFilter.ShouldBe(filter);
    }

    [Fact]
    public void AllowSettingMessageTransform()
    {
        Func<TransportMessage, TransportMessage> transform = msg =>
        {
            msg.Properties["reprocessed"] = true;
            return msg;
        };
        var options = new ReprocessOptions { MessageTransform = transform };

        options.MessageTransform.ShouldNotBeNull();
        options.MessageTransform.ShouldBe(transform);
    }

    [Fact]
    public void AllowSettingRetryDelay()
    {
        var delay = TimeSpan.FromSeconds(5);
        var options = new ReprocessOptions { RetryDelay = delay };

        options.RetryDelay.ShouldBe(delay);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AllowSettingRemoveFromDlq(bool remove)
    {
        var options = new ReprocessOptions { RemoveFromDlq = remove };

        options.RemoveFromDlq.ShouldBe(remove);
    }

    [Theory]
    [InlineData(MessagePriority.Low)]
    [InlineData(MessagePriority.Normal)]
    [InlineData(MessagePriority.High)]
    [InlineData(MessagePriority.Critical)]
    [InlineData(null)]
    public void AllowSettingPriority(MessagePriority? priority)
    {
        var options = new ReprocessOptions { Priority = priority };

        options.Priority.ShouldBe(priority);
    }

    [Fact]
    public void AllowSettingTimeToLive()
    {
        var ttl = TimeSpan.FromHours(24);
        var options = new ReprocessOptions { TimeToLive = ttl };

        options.TimeToLive.ShouldBe(ttl);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(1000)]
    [InlineData(null)]
    public void AllowSettingMaxMessages(int? max)
    {
        var options = new ReprocessOptions { MaxMessages = max };

        options.MaxMessages.ShouldBe(max);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AllowSettingProcessInParallel(bool parallel)
    {
        var options = new ReprocessOptions { ProcessInParallel = parallel };

        options.ProcessInParallel.ShouldBe(parallel);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(16)]
    public void AllowSettingMaxDegreeOfParallelism(int maxDegree)
    {
        var options = new ReprocessOptions { MaxDegreeOfParallelism = maxDegree };

        options.MaxDegreeOfParallelism.ShouldBe(maxDegree);
    }

    [Fact]
    public void AllowMessageFilterToFilterMessages()
    {
        var options = new ReprocessOptions
        {
            MessageFilter = msg => msg.Reason == "TransientError"
        };

        var transientErrorMsg = new DeadLetterMessage
        {
            OriginalMessage = new TransportMessage(),
            Reason = "TransientError"
        };

        var permanentErrorMsg = new DeadLetterMessage
        {
            OriginalMessage = new TransportMessage(),
            Reason = "PermanentError"
        };

        options.MessageFilter(transientErrorMsg).ShouldBeTrue();
        options.MessageFilter(permanentErrorMsg).ShouldBeFalse();
    }

    [Fact]
    public void AllowMessageTransformToModifyMessages()
    {
        var options = new ReprocessOptions
        {
            MessageTransform = msg =>
            {
                msg.Properties["reprocessedAt"] = DateTimeOffset.UtcNow;
                return msg;
            }
        };

        var message = new TransportMessage();
        var transformed = options.MessageTransform(message);

        transformed.Properties.ContainsKey("reprocessedAt").ShouldBeTrue();
    }

    [Fact]
    public void AllowHighThroughputReprocessingConfiguration()
    {
        var options = new ReprocessOptions
        {
            TargetQueue = "orders-queue",
            RetryDelay = TimeSpan.Zero,
            ProcessInParallel = true,
            MaxDegreeOfParallelism = 16,
            MaxMessages = 10000,
            RemoveFromDlq = true
        };

        options.TargetQueue.ShouldBe("orders-queue");
        options.RetryDelay.ShouldBe(TimeSpan.Zero);
        options.ProcessInParallel.ShouldBeTrue();
        options.MaxDegreeOfParallelism.ShouldBe(16);
        options.MaxMessages.ShouldBe(10000);
        options.RemoveFromDlq.ShouldBeTrue();
    }

    [Fact]
    public void AllowCarefulReprocessingConfiguration()
    {
        var options = new ReprocessOptions
        {
            TargetQueue = "orders-queue",
            RetryDelay = TimeSpan.FromSeconds(1),
            ProcessInParallel = false,
            MaxMessages = 10,
            RemoveFromDlq = false, // Keep messages for review
            Priority = MessagePriority.Low
        };

        options.TargetQueue.ShouldBe("orders-queue");
        options.RetryDelay.ShouldBe(TimeSpan.FromSeconds(1));
        options.ProcessInParallel.ShouldBeFalse();
        options.MaxMessages.ShouldBe(10);
        options.RemoveFromDlq.ShouldBeFalse();
        options.Priority.ShouldBe(MessagePriority.Low);
    }

    [Fact]
    public void AllowFilteredReprocessingConfiguration()
    {
        var options = new ReprocessOptions
        {
            MessageFilter = msg => msg.DeliveryAttempts < 3 && msg.Reason != "ValidationError",
            MessageTransform = msg =>
            {
                msg.Properties["originalDeadLetterReason"] = "Unknown";
                return msg;
            },
            TargetQueue = "retry-queue",
            RemoveFromDlq = true
        };

        options.MessageFilter.ShouldNotBeNull();
        options.MessageTransform.ShouldNotBeNull();
        options.TargetQueue.ShouldBe("retry-queue");
        options.RemoveFromDlq.ShouldBeTrue();
    }
}
