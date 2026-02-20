using Excalibur.Dispatch.Transport.Diagnostics;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Diagnostics;

public class TransportTelemetryConstantsShould
{
    [Theory]
    [InlineData("Kafka", "Excalibur.Dispatch.Transport.Kafka")]
    [InlineData("RabbitMQ", "Excalibur.Dispatch.Transport.RabbitMQ")]
    [InlineData("AzureServiceBus", "Excalibur.Dispatch.Transport.AzureServiceBus")]
    public void MeterName_Should_Format_Correctly(string transport, string expected)
    {
        TransportTelemetryConstants.MeterName(transport).ShouldBe(expected);
    }

    [Theory]
    [InlineData("Kafka", "Excalibur.Dispatch.Transport.Kafka")]
    [InlineData("GooglePubSub", "Excalibur.Dispatch.Transport.GooglePubSub")]
    public void ActivitySourceName_Should_Format_Correctly(string transport, string expected)
    {
        TransportTelemetryConstants.ActivitySourceName(transport).ShouldBe(expected);
    }

    [Fact]
    public void MetricNames_Should_Have_Correct_Values()
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
    public void Tags_Should_Have_Correct_Values()
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
    public void PropertyKeys_Should_Have_Correct_Values()
    {
        TransportTelemetryConstants.PropertyKeys.OrderingKey.ShouldBe("dispatch.ordering.key");
        TransportTelemetryConstants.PropertyKeys.PartitionKey.ShouldBe("dispatch.partition.key");
        TransportTelemetryConstants.PropertyKeys.DeduplicationId.ShouldBe("dispatch.deduplication.id");
        TransportTelemetryConstants.PropertyKeys.ScheduledTime.ShouldBe("dispatch.scheduled.time");
        TransportTelemetryConstants.PropertyKeys.Priority.ShouldBe("dispatch.priority");
        TransportTelemetryConstants.PropertyKeys.DelaySeconds.ShouldBe("dispatch.delay.seconds");
    }
}
