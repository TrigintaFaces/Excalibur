// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;
using Shouldly;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.DeadLetterQueue;

[Trait("Category", "Unit")]
[Trait("Component", "Transport.Abstractions")]
public class DeadLetterMessageShould
{
    [Fact]
    public void HaveCorrectDefaultValues()
    {
        var message = new DeadLetterMessage();

        message.OriginalMessage.ShouldBeNull();
        message.OriginalEnvelope.ShouldBeNull();
        message.OriginalContext.ShouldBeNull();
        message.Reason.ShouldBe(string.Empty);
        message.Exception.ShouldBeNull();
        message.DeliveryAttempts.ShouldBe(0);
        message.OriginalSource.ShouldBeNull();
        message.DeadLetteredAt.ShouldBe(default);
        message.Metadata.ShouldNotBeNull();
        message.Metadata.ShouldBeEmpty();
    }

    [Fact]
    public void AllowSettingOriginalMessage()
    {
        var originalMessage = new TransportMessage { Id = "msg-123" };
        var message = new DeadLetterMessage { OriginalMessage = originalMessage };

        message.OriginalMessage.ShouldBe(originalMessage);
        message.OriginalMessage.Id.ShouldBe("msg-123");
    }

    [Fact]
    public void AllowSettingReason()
    {
        var message = new DeadLetterMessage { Reason = "Max retries exceeded" };

        message.Reason.ShouldBe("Max retries exceeded");
    }

    [Fact]
    public void AllowSettingException()
    {
        var ex = new InvalidOperationException("Processing failed");
        var message = new DeadLetterMessage { Exception = ex };

        message.Exception.ShouldBe(ex);
        message.Exception.ShouldBeOfType<InvalidOperationException>();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(10)]
    public void AllowSettingDeliveryAttempts(int attempts)
    {
        var message = new DeadLetterMessage { DeliveryAttempts = attempts };

        message.DeliveryAttempts.ShouldBe(attempts);
    }

    [Fact]
    public void AllowSettingOriginalSource()
    {
        var message = new DeadLetterMessage { OriginalSource = "orders-queue" };

        message.OriginalSource.ShouldBe("orders-queue");
    }

    [Fact]
    public void AllowSettingDeadLetteredAt()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var message = new DeadLetterMessage { DeadLetteredAt = timestamp };

        message.DeadLetteredAt.ShouldBe(timestamp);
    }

    [Fact]
    public void AllowSettingMetadata()
    {
        var message = new DeadLetterMessage
        {
            Metadata = { ["key1"] = "value1", ["key2"] = "value2" }
        };

        message.Metadata.Count.ShouldBe(2);
        message.Metadata["key1"].ShouldBe("value1");
        message.Metadata["key2"].ShouldBe("value2");
    }

    [Fact]
    public void AllowCreatingMetadataViaInitSyntax()
    {
        var message = new DeadLetterMessage
        {
            Metadata =
            {
                ["correlationId"] = "corr-123",
                ["tenantId"] = "tenant-456"
            }
        };

        message.Metadata.Count.ShouldBe(2);
        message.Metadata["correlationId"].ShouldBe("corr-123");
        message.Metadata["tenantId"].ShouldBe("tenant-456");
    }

    [Fact]
    public void AllowSettingAllPropertiesForTypicalDeadLetter()
    {
        var originalMessage = new TransportMessage
        {
            Id = "msg-order-123",
            Subject = "order.created"
        };
        var ex = new TimeoutException("Database connection timed out");
        var timestamp = DateTimeOffset.UtcNow;

        var message = new DeadLetterMessage
        {
            OriginalMessage = originalMessage,
            Reason = "Database connection timeout after 5 retries",
            Exception = ex,
            DeliveryAttempts = 5,
            OriginalSource = "orders-queue",
            DeadLetteredAt = timestamp,
            Metadata =
            {
                ["correlationId"] = "corr-123",
                ["orderId"] = "order-456"
            }
        };

        message.OriginalMessage.Id.ShouldBe("msg-order-123");
        message.OriginalMessage.Subject.ShouldBe("order.created");
        message.Reason.ShouldBe("Database connection timeout after 5 retries");
        message.Exception.ShouldBeOfType<TimeoutException>();
        message.DeliveryAttempts.ShouldBe(5);
        message.OriginalSource.ShouldBe("orders-queue");
        message.DeadLetteredAt.ShouldBe(timestamp);
        message.Metadata["correlationId"].ShouldBe("corr-123");
        message.Metadata["orderId"].ShouldBe("order-456");
    }

    [Fact]
    public void AllowSettingPoisonMessageScenario()
    {
        var originalMessage = new TransportMessage
        {
            Id = "msg-poison",
            Body = new byte[] { 0xFF, 0xFE }
        };

        var message = new DeadLetterMessage
        {
            OriginalMessage = originalMessage,
            Reason = "Message could not be deserialized",
            DeliveryAttempts = 1,
            OriginalSource = "events-queue",
            DeadLetteredAt = DateTimeOffset.UtcNow
        };

        message.OriginalMessage.Id.ShouldBe("msg-poison");
        message.Reason.ShouldBe("Message could not be deserialized");
        message.DeliveryAttempts.ShouldBe(1);
        message.Exception.ShouldBeNull();
    }
}
