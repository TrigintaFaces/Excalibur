// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Diagnostics;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Diagnostics;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class TransportTelemetryConstantsShould
{
    [Theory]
    [InlineData("Kafka", "Excalibur.Dispatch.Transport.Kafka")]
    [InlineData("RabbitMQ", "Excalibur.Dispatch.Transport.RabbitMQ")]
    [InlineData("AzureServiceBus", "Excalibur.Dispatch.Transport.AzureServiceBus")]
    [InlineData("AwsSqs", "Excalibur.Dispatch.Transport.AwsSqs")]
    [InlineData("GooglePubSub", "Excalibur.Dispatch.Transport.GooglePubSub")]
    public void Format_MeterName_Correctly(string transportName, string expected)
    {
        TransportTelemetryConstants.MeterName(transportName).ShouldBe(expected);
    }

    [Theory]
    [InlineData("Kafka", "Excalibur.Dispatch.Transport.Kafka")]
    [InlineData("RabbitMQ", "Excalibur.Dispatch.Transport.RabbitMQ")]
    public void Format_ActivitySourceName_Correctly(string transportName, string expected)
    {
        TransportTelemetryConstants.ActivitySourceName(transportName).ShouldBe(expected);
    }

    [Fact]
    public void Have_Correct_MetricName_Constants()
    {
        TransportTelemetryConstants.MetricNames.MessagesSent.ShouldBe("dispatch.transport.messages.sent");
        TransportTelemetryConstants.MetricNames.MessagesSendFailed.ShouldBe("dispatch.transport.messages.send_failed");
        TransportTelemetryConstants.MetricNames.MessagesReceived.ShouldBe("dispatch.transport.messages.received");
        TransportTelemetryConstants.MetricNames.MessagesAcknowledged.ShouldBe("dispatch.transport.messages.acknowledged");
        TransportTelemetryConstants.MetricNames.MessagesRejected.ShouldBe("dispatch.transport.messages.rejected");
        TransportTelemetryConstants.MetricNames.MessagesDeadLettered.ShouldBe("dispatch.transport.messages.dead_lettered");
        TransportTelemetryConstants.MetricNames.SendDuration.ShouldBe("dispatch.transport.send.duration");
        TransportTelemetryConstants.MetricNames.ReceiveDuration.ShouldBe("dispatch.transport.receive.duration");
        TransportTelemetryConstants.MetricNames.BatchSize.ShouldBe("dispatch.transport.batch.size");
        TransportTelemetryConstants.MetricNames.MessagesRequeued.ShouldBe("dispatch.transport.messages.requeued");
        TransportTelemetryConstants.MetricNames.HandlerErrors.ShouldBe("dispatch.transport.handler.errors");
        TransportTelemetryConstants.MetricNames.HandlerDuration.ShouldBe("dispatch.transport.handler.duration");
    }

    [Fact]
    public void Have_Correct_Tag_Constants()
    {
        TransportTelemetryConstants.Tags.TransportName.ShouldBe("dispatch.transport.name");
        TransportTelemetryConstants.Tags.Destination.ShouldBe("dispatch.transport.destination");
        TransportTelemetryConstants.Tags.Source.ShouldBe("dispatch.transport.source");
        TransportTelemetryConstants.Tags.Operation.ShouldBe("dispatch.transport.operation");
        TransportTelemetryConstants.Tags.MessageType.ShouldBe("message.type");
        TransportTelemetryConstants.Tags.ErrorType.ShouldBe("error.type");
        TransportTelemetryConstants.Tags.IsRetryable.ShouldBe("error.retryable");
    }

    [Fact]
    public void Have_Correct_PropertyKey_Constants()
    {
        TransportTelemetryConstants.PropertyKeys.OrderingKey.ShouldBe("dispatch.ordering.key");
        TransportTelemetryConstants.PropertyKeys.PartitionKey.ShouldBe("dispatch.partition.key");
        TransportTelemetryConstants.PropertyKeys.DeduplicationId.ShouldBe("dispatch.deduplication.id");
        TransportTelemetryConstants.PropertyKeys.ScheduledTime.ShouldBe("dispatch.scheduled.time");
        TransportTelemetryConstants.PropertyKeys.Priority.ShouldBe("dispatch.priority");
        TransportTelemetryConstants.PropertyKeys.DelaySeconds.ShouldBe("dispatch.delay.seconds");
    }

    [Fact]
    public void Use_Dot_Notation_For_All_MetricNames()
    {
        var metricNames = new[]
        {
            TransportTelemetryConstants.MetricNames.MessagesSent,
            TransportTelemetryConstants.MetricNames.MessagesSendFailed,
            TransportTelemetryConstants.MetricNames.MessagesReceived,
            TransportTelemetryConstants.MetricNames.MessagesAcknowledged,
            TransportTelemetryConstants.MetricNames.MessagesRejected,
            TransportTelemetryConstants.MetricNames.MessagesDeadLettered,
            TransportTelemetryConstants.MetricNames.SendDuration,
            TransportTelemetryConstants.MetricNames.ReceiveDuration,
            TransportTelemetryConstants.MetricNames.BatchSize,
            TransportTelemetryConstants.MetricNames.MessagesRequeued,
            TransportTelemetryConstants.MetricNames.HandlerErrors,
            TransportTelemetryConstants.MetricNames.HandlerDuration,
        };

        foreach (var name in metricNames)
        {
            name.ShouldStartWith("dispatch.transport.");
        }
    }
}
