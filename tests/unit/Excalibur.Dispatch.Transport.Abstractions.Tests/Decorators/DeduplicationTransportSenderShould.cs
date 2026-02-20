using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Decorators;
using Excalibur.Dispatch.Transport.Diagnostics;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Decorators;

public class DeduplicationTransportSenderShould
{
    [Fact]
    public void Should_Throw_On_Null_IdSelector()
    {
        var inner = A.Fake<ITransportSender>();

        Should.Throw<ArgumentNullException>(() => new DeduplicationTransportSender(inner, null!));
    }

    [Fact]
    public async Task SendAsync_Should_Set_DeduplicationId_Property()
    {
        var inner = A.Fake<ITransportSender>();
        A.CallTo(() => inner.SendAsync(A<TransportMessage>._, A<CancellationToken>._))
            .Returns(SendResult.Success("msg-1"));

        var sender = new DeduplicationTransportSender(inner, _ => "dedup-001");
        var message = new TransportMessage();

        await sender.SendAsync(message, CancellationToken.None);

        message.Properties[TransportTelemetryConstants.PropertyKeys.DeduplicationId].ShouldBe("dedup-001");
    }

    [Fact]
    public async Task SendAsync_Should_Not_Set_Property_When_Id_Is_Null()
    {
        var inner = A.Fake<ITransportSender>();
        A.CallTo(() => inner.SendAsync(A<TransportMessage>._, A<CancellationToken>._))
            .Returns(SendResult.Success("msg-1"));

        var sender = new DeduplicationTransportSender(inner, _ => null);
        var message = new TransportMessage();

        await sender.SendAsync(message, CancellationToken.None);

        message.HasProperties.ShouldBeFalse();
    }

    [Fact]
    public async Task SendBatchAsync_Should_Set_DeduplicationId_On_All_Messages()
    {
        var inner = A.Fake<ITransportSender>();
        A.CallTo(() => inner.SendBatchAsync(A<IReadOnlyList<TransportMessage>>._, A<CancellationToken>._))
            .Returns(new BatchSendResult { TotalMessages = 2, SuccessCount = 2 });

        var sender = new DeduplicationTransportSender(inner, msg => $"dedup-{msg.Id}");
        var messages = new List<TransportMessage>
        {
            new() { Id = "1" },
            new() { Id = "2" },
        };

        await sender.SendBatchAsync(messages, CancellationToken.None);

        messages[0].Properties[TransportTelemetryConstants.PropertyKeys.DeduplicationId].ShouldBe("dedup-1");
        messages[1].Properties[TransportTelemetryConstants.PropertyKeys.DeduplicationId].ShouldBe("dedup-2");
    }
}
