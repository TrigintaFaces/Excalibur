using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Decorators;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Decorators;

public sealed class TelemetryTransportReceiverShould : IDisposable
{
    private readonly Meter _meter;
    private readonly ActivitySource _activitySource;

    public TelemetryTransportReceiverShould()
    {
        _meter = new Meter("test.receiver");
        _activitySource = new ActivitySource("test.receiver");
    }

    public void Dispose()
    {
        _meter.Dispose();
        _activitySource.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Should_Throw_On_Null_Meter()
    {
        var inner = A.Fake<ITransportReceiver>();

        Should.Throw<ArgumentNullException>(() =>
            new TelemetryTransportReceiver(inner, null!, _activitySource, "Kafka"));
    }

    [Fact]
    public void Should_Throw_On_Null_ActivitySource()
    {
        var inner = A.Fake<ITransportReceiver>();

        Should.Throw<ArgumentNullException>(() =>
            new TelemetryTransportReceiver(inner, _meter, null!, "Kafka"));
    }

    [Fact]
    public void Should_Throw_On_Null_TransportName()
    {
        var inner = A.Fake<ITransportReceiver>();

        Should.Throw<ArgumentNullException>(() =>
            new TelemetryTransportReceiver(inner, _meter, _activitySource, null!));
    }

    [Fact]
    public async Task ReceiveAsync_Should_Delegate_To_Inner()
    {
        var inner = A.Fake<ITransportReceiver>();
        A.CallTo(() => inner.Source).Returns("test-queue");
        var expected = new List<TransportReceivedMessage> { new() { Id = "msg-1" } };
        A.CallTo(() => inner.ReceiveAsync(10, A<CancellationToken>._)).Returns(expected);

        var receiver = new TelemetryTransportReceiver(inner, _meter, _activitySource, "TestTransport");
        var result = await receiver.ReceiveAsync(10, CancellationToken.None);

        result.ShouldBe(expected);
    }

    [Fact]
    public async Task AcknowledgeAsync_Should_Delegate_To_Inner()
    {
        var inner = A.Fake<ITransportReceiver>();
        A.CallTo(() => inner.Source).Returns("test-queue");
        var message = new TransportReceivedMessage { Id = "msg-1" };

        var receiver = new TelemetryTransportReceiver(inner, _meter, _activitySource, "TestTransport");
        await receiver.AcknowledgeAsync(message, CancellationToken.None);

        A.CallTo(() => inner.AcknowledgeAsync(message, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task RejectAsync_Should_Delegate_To_Inner()
    {
        var inner = A.Fake<ITransportReceiver>();
        A.CallTo(() => inner.Source).Returns("test-queue");
        var message = new TransportReceivedMessage { Id = "msg-1" };

        var receiver = new TelemetryTransportReceiver(inner, _meter, _activitySource, "TestTransport");
        await receiver.RejectAsync(message, "bad", false, CancellationToken.None);

        A.CallTo(() => inner.RejectAsync(message, "bad", false, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ReceiveAsync_Should_Handle_Empty_Result()
    {
        var inner = A.Fake<ITransportReceiver>();
        A.CallTo(() => inner.Source).Returns("test-queue");
        A.CallTo(() => inner.ReceiveAsync(A<int>._, A<CancellationToken>._))
            .Returns(new List<TransportReceivedMessage>());

        var receiver = new TelemetryTransportReceiver(inner, _meter, _activitySource, "TestTransport");
        var result = await receiver.ReceiveAsync(10, CancellationToken.None);

        result.Count.ShouldBe(0);
    }
}
