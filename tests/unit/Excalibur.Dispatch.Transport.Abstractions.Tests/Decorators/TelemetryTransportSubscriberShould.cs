using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Decorators;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Decorators;

public sealed class TelemetryTransportSubscriberShould : IDisposable
{
    private readonly Meter _meter;
    private readonly ActivitySource _activitySource;

    public TelemetryTransportSubscriberShould()
    {
        _meter = new Meter("test.subscriber");
        _activitySource = new ActivitySource("test.subscriber");
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
        var inner = A.Fake<ITransportSubscriber>();

        Should.Throw<ArgumentNullException>(() =>
            new TelemetryTransportSubscriber(inner, null!, _activitySource, "Kafka"));
    }

    [Fact]
    public void Should_Throw_On_Null_ActivitySource()
    {
        var inner = A.Fake<ITransportSubscriber>();

        Should.Throw<ArgumentNullException>(() =>
            new TelemetryTransportSubscriber(inner, _meter, null!, "Kafka"));
    }

    [Fact]
    public void Should_Throw_On_Null_Or_Empty_TransportName()
    {
        var inner = A.Fake<ITransportSubscriber>();

        Should.Throw<ArgumentNullException>(() =>
            new TelemetryTransportSubscriber(inner, _meter, _activitySource, null!));

        Should.Throw<ArgumentException>(() =>
            new TelemetryTransportSubscriber(inner, _meter, _activitySource, ""));
    }

    [Fact]
    public async Task SubscribeAsync_Should_Delegate_To_Inner()
    {
        var inner = A.Fake<ITransportSubscriber>();
        A.CallTo(() => inner.Source).Returns("test-topic");
        Func<TransportReceivedMessage, CancellationToken, Task<MessageAction>> handler =
            (_, _) => Task.FromResult(MessageAction.Acknowledge);

        var subscriber = new TelemetryTransportSubscriber(inner, _meter, _activitySource, "TestTransport");
        await subscriber.SubscribeAsync(handler, CancellationToken.None);

        A.CallTo(() => inner.SubscribeAsync(A<Func<TransportReceivedMessage, CancellationToken, Task<MessageAction>>>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }
}
