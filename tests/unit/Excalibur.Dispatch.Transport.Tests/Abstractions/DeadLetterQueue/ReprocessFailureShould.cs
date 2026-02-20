// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;
using Shouldly;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.DeadLetterQueue;

[Trait("Category", "Unit")]
[Trait("Component", "Transport.Abstractions")]
public class ReprocessFailureShould
{
    [Fact]
    public void HaveCorrectDefaultValues()
    {
        var before = DateTimeOffset.UtcNow;
        var failure = new ReprocessFailure();
        var after = DateTimeOffset.UtcNow;

        failure.Message.ShouldBeNull();
        failure.Reason.ShouldBe(string.Empty);
        failure.Exception.ShouldBeNull();
        failure.FailedAt.ShouldBeGreaterThanOrEqualTo(before);
        failure.FailedAt.ShouldBeLessThanOrEqualTo(after);
    }

    [Fact]
    public void AllowSettingMessage()
    {
        var deadLetterMsg = new DeadLetterMessage
        {
            OriginalMessage = new TransportMessage { Id = "msg-123" },
            Reason = "TransientError"
        };
        var failure = new ReprocessFailure { Message = deadLetterMsg };

        failure.Message.ShouldNotBeNull();
        failure.Message.OriginalMessage.Id.ShouldBe("msg-123");
        failure.Message.Reason.ShouldBe("TransientError");
    }

    [Theory]
    [InlineData("")]
    [InlineData("Connection timeout")]
    [InlineData("The message could not be delivered after 5 retries")]
    public void AllowSettingReason(string reason)
    {
        var failure = new ReprocessFailure { Reason = reason };

        failure.Reason.ShouldBe(reason);
    }

    [Fact]
    public void AllowSettingException()
    {
        var exception = new InvalidOperationException("Something went wrong");
        var failure = new ReprocessFailure { Exception = exception };

        failure.Exception.ShouldNotBeNull();
        failure.Exception.Message.ShouldBe("Something went wrong");
        failure.Exception.ShouldBeOfType<InvalidOperationException>();
    }

    [Fact]
    public void AllowSettingFailedAt()
    {
        var failedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        var failure = new ReprocessFailure { FailedAt = failedAt };

        failure.FailedAt.ShouldBe(failedAt);
    }

    [Fact]
    public void AllowTimeoutException()
    {
        var failure = new ReprocessFailure
        {
            Message = new DeadLetterMessage
            {
                OriginalMessage = new TransportMessage { Id = "msg-timeout" }
            },
            Reason = "Connection timeout while sending to target queue",
            Exception = new TimeoutException("The operation timed out after 30 seconds"),
            FailedAt = DateTimeOffset.UtcNow
        };

        failure.Exception.ShouldBeOfType<TimeoutException>();
        failure.Reason.ShouldContain("timeout");
    }

    [Fact]
    public void AllowNetworkException()
    {
        var failure = new ReprocessFailure
        {
            Message = new DeadLetterMessage
            {
                OriginalMessage = new TransportMessage { Id = "msg-network" }
            },
            Reason = "Network error during message delivery",
            Exception = new HttpRequestException("Unable to connect to remote server"),
            FailedAt = DateTimeOffset.UtcNow
        };

        failure.Exception.ShouldBeOfType<HttpRequestException>();
        failure.Reason.ShouldContain("Network");
    }

    [Fact]
    public void AllowValidationException()
    {
        var failure = new ReprocessFailure
        {
            Message = new DeadLetterMessage
            {
                OriginalMessage = new TransportMessage { Id = "msg-validation" }
            },
            Reason = "Message failed validation after transform",
            Exception = new ArgumentException("Message body is invalid"),
            FailedAt = DateTimeOffset.UtcNow
        };

        failure.Exception.ShouldBeOfType<ArgumentException>();
        failure.Reason.ShouldContain("validation");
    }

    [Fact]
    public void AllowFailureWithoutException()
    {
        var failure = new ReprocessFailure
        {
            Message = new DeadLetterMessage
            {
                OriginalMessage = new TransportMessage { Id = "msg-skipped" }
            },
            Reason = "Message was filtered out by the reprocessing filter",
            Exception = null,
            FailedAt = DateTimeOffset.UtcNow
        };

        failure.Exception.ShouldBeNull();
        failure.Reason.ShouldNotBeEmpty();
    }

    [Fact]
    public void AllowAggregateException()
    {
        var innerExceptions = new Exception[]
        {
            new InvalidOperationException("First error"),
            new TimeoutException("Second error")
        };
        var aggregateEx = new AggregateException("Multiple errors occurred", innerExceptions);

        var failure = new ReprocessFailure
        {
            Message = new DeadLetterMessage
            {
                OriginalMessage = new TransportMessage { Id = "msg-aggregate" }
            },
            Reason = "Multiple errors during parallel reprocessing",
            Exception = aggregateEx,
            FailedAt = DateTimeOffset.UtcNow
        };

        failure.Exception.ShouldBeOfType<AggregateException>();
        ((AggregateException)failure.Exception).InnerExceptions.Count.ShouldBe(2);
    }

    [Fact]
    public void AllowMessageWithOriginalDeadLetterContext()
    {
        var originalMsg = new TransportMessage
        {
            Id = "msg-original",
            Body = "test body"u8.ToArray(),
            ContentType = "application/json",
            CorrelationId = "corr-123"
        };

        var deadLetterMsg = new DeadLetterMessage
        {
            OriginalMessage = originalMsg,
            Reason = "MaxRetriesExceeded",
            Exception = new InvalidOperationException("Processing failed"),
            DeadLetteredAt = DateTimeOffset.UtcNow.AddHours(-1),
            DeliveryAttempts = 5
        };

        var failure = new ReprocessFailure
        {
            Message = deadLetterMsg,
            Reason = "Failed to reprocess: target queue does not exist",
            Exception = new InvalidOperationException("Queue not found"),
            FailedAt = DateTimeOffset.UtcNow
        };

        failure.Message.OriginalMessage.Id.ShouldBe("msg-original");
        failure.Message.DeliveryAttempts.ShouldBe(5);
        failure.Message.Reason.ShouldBe("MaxRetriesExceeded");
        failure.Reason.ShouldContain("target queue does not exist");
    }

    [Fact]
    public void AllowHistoricalFailedAt()
    {
        var historicalDate = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);
        var failure = new ReprocessFailure
        {
            Message = new DeadLetterMessage { OriginalMessage = new TransportMessage() },
            Reason = "Historical failure",
            FailedAt = historicalDate
        };

        failure.FailedAt.ShouldBe(historicalDate);
    }

    [Fact]
    public void AllowFutureFailedAtForScheduledReprocessing()
    {
        var futureDate = DateTimeOffset.UtcNow.AddHours(1);
        var failure = new ReprocessFailure
        {
            Message = new DeadLetterMessage { OriginalMessage = new TransportMessage() },
            Reason = "Scheduled reprocessing failed",
            FailedAt = futureDate // Scheduled for future but already recorded as failed
        };

        failure.FailedAt.ShouldBe(futureDate);
    }
}
