using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Decorators;
using Excalibur.Dispatch.Transport.Diagnostics;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Decorators;

public class SchedulingTransportSenderShould
{
    [Fact]
    public void Should_Throw_On_Null_TimeSelector()
    {
        var inner = A.Fake<ITransportSender>();

        Should.Throw<ArgumentNullException>(() => new SchedulingTransportSender(inner, null!));
    }

    [Fact]
    public async Task SendAsync_Should_Set_ScheduledTime_Property()
    {
        var inner = A.Fake<ITransportSender>();
        A.CallTo(() => inner.SendAsync(A<TransportMessage>._, A<CancellationToken>._))
            .Returns(SendResult.Success("msg-1"));

        var scheduledTime = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
        var sender = new SchedulingTransportSender(inner, _ => scheduledTime);
        var message = new TransportMessage();

        await sender.SendAsync(message, CancellationToken.None);

        var value = message.Properties[TransportTelemetryConstants.PropertyKeys.ScheduledTime] as string;
        value.ShouldNotBeNull();
        value.ShouldBe(scheduledTime.ToString("O"));
    }

    [Fact]
    public async Task SendAsync_Should_Not_Set_Property_When_Time_Is_Null()
    {
        var inner = A.Fake<ITransportSender>();
        A.CallTo(() => inner.SendAsync(A<TransportMessage>._, A<CancellationToken>._))
            .Returns(SendResult.Success("msg-1"));

        var sender = new SchedulingTransportSender(inner, _ => null);
        var message = new TransportMessage();

        await sender.SendAsync(message, CancellationToken.None);

        message.HasProperties.ShouldBeFalse();
    }

    [Fact]
    public async Task SendBatchAsync_Should_Set_ScheduledTime_On_All_Messages()
    {
        var inner = A.Fake<ITransportSender>();
        A.CallTo(() => inner.SendBatchAsync(A<IReadOnlyList<TransportMessage>>._, A<CancellationToken>._))
            .Returns(new BatchSendResult { TotalMessages = 2, SuccessCount = 2 });

        var time = new DateTimeOffset(2026, 6, 15, 10, 0, 0, TimeSpan.Zero);
        var sender = new SchedulingTransportSender(inner, _ => time);
        var messages = new List<TransportMessage> { new(), new() };

        await sender.SendBatchAsync(messages, CancellationToken.None);

        foreach (var msg in messages)
        {
            msg.Properties[TransportTelemetryConstants.PropertyKeys.ScheduledTime]
                .ShouldBe(time.ToString("O"));
        }
    }

    [Fact]
    public async Task SendAsync_Should_Delegate_To_Inner()
    {
        var inner = A.Fake<ITransportSender>();
        var expected = SendResult.Success("msg-1");
        A.CallTo(() => inner.SendAsync(A<TransportMessage>._, A<CancellationToken>._)).Returns(expected);

        var sender = new SchedulingTransportSender(inner, _ => DateTimeOffset.UtcNow.AddHours(1));
        var result = await sender.SendAsync(new TransportMessage(), CancellationToken.None);

        result.ShouldBe(expected);
    }
}
