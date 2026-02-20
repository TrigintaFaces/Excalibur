using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Decorators;
using Excalibur.Dispatch.Transport.Diagnostics;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Decorators;

public class OrderingTransportSenderShould
{
    [Fact]
    public void Should_Throw_On_Null_KeySelector()
    {
        var inner = A.Fake<ITransportSender>();

        Should.Throw<ArgumentNullException>(() => new OrderingTransportSender(inner, null!));
    }

    [Fact]
    public async Task SendAsync_Should_Set_OrderingKey_Property()
    {
        var inner = A.Fake<ITransportSender>();
        A.CallTo(() => inner.SendAsync(A<TransportMessage>._, A<CancellationToken>._))
            .Returns(SendResult.Success("msg-1"));

        var sender = new OrderingTransportSender(inner, msg => "order-key-123");
        var message = new TransportMessage();

        await sender.SendAsync(message, CancellationToken.None);

        message.Properties[TransportTelemetryConstants.PropertyKeys.OrderingKey].ShouldBe("order-key-123");
    }

    [Fact]
    public async Task SendAsync_Should_Not_Set_Property_When_Key_Is_Null()
    {
        var inner = A.Fake<ITransportSender>();
        A.CallTo(() => inner.SendAsync(A<TransportMessage>._, A<CancellationToken>._))
            .Returns(SendResult.Success("msg-1"));

        var sender = new OrderingTransportSender(inner, _ => null);
        var message = new TransportMessage();

        await sender.SendAsync(message, CancellationToken.None);

        message.HasProperties.ShouldBeFalse();
    }

    [Fact]
    public async Task SendAsync_Should_Delegate_To_Inner()
    {
        var inner = A.Fake<ITransportSender>();
        var expected = SendResult.Success("msg-1");
        A.CallTo(() => inner.SendAsync(A<TransportMessage>._, A<CancellationToken>._)).Returns(expected);

        var sender = new OrderingTransportSender(inner, _ => "key");
        var result = await sender.SendAsync(new TransportMessage(), CancellationToken.None);

        result.ShouldBe(expected);
    }

    [Fact]
    public async Task SendBatchAsync_Should_Set_OrderingKey_On_All_Messages()
    {
        var inner = A.Fake<ITransportSender>();
        A.CallTo(() => inner.SendBatchAsync(A<IReadOnlyList<TransportMessage>>._, A<CancellationToken>._))
            .Returns(new BatchSendResult { TotalMessages = 2, SuccessCount = 2 });

        var sender = new OrderingTransportSender(inner, msg => msg.Id);
        var messages = new List<TransportMessage>
        {
            new() { Id = "a" },
            new() { Id = "b" },
        };

        await sender.SendBatchAsync(messages, CancellationToken.None);

        messages[0].Properties[TransportTelemetryConstants.PropertyKeys.OrderingKey].ShouldBe("a");
        messages[1].Properties[TransportTelemetryConstants.PropertyKeys.OrderingKey].ShouldBe("b");
    }
}
