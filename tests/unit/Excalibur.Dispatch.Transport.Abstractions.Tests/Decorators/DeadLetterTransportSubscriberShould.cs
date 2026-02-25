using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Decorators;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Decorators;

public sealed class DeadLetterTransportSubscriberShould : IDisposable
{
    private readonly Meter _meter;

    public DeadLetterTransportSubscriberShould()
    {
        _meter = new Meter("test.dlq.subscriber");
    }

    public void Dispose()
    {
        _meter.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Should_Throw_On_Null_DeadLetterHandler()
    {
        var inner = A.Fake<ITransportSubscriber>();

        Should.Throw<ArgumentNullException>(() =>
            new DeadLetterTransportSubscriber(inner, null!, "Kafka"));
    }

    [Fact]
    public void Should_Throw_On_Null_TransportName()
    {
        var inner = A.Fake<ITransportSubscriber>();
        Func<TransportReceivedMessage, string?, CancellationToken, Task> handler = (_, _, _) => Task.CompletedTask;

        Should.Throw<ArgumentNullException>(() =>
            new DeadLetterTransportSubscriber(inner, handler, null!));
    }

    [Fact]
    public async Task SubscribeAsync_Should_Delegate_To_Inner()
    {
        var inner = A.Fake<ITransportSubscriber>();
        A.CallTo(() => inner.Source).Returns("test-topic");
        Func<TransportReceivedMessage, string?, CancellationToken, Task> dlqHandler = (_, _, _) => Task.CompletedTask;
        Func<TransportReceivedMessage, CancellationToken, Task<MessageAction>> handler =
            (_, _) => Task.FromResult(MessageAction.Acknowledge);

        var subscriber = new DeadLetterTransportSubscriber(inner, dlqHandler, "TestTransport", _meter);
        await subscriber.SubscribeAsync(handler, CancellationToken.None);

        A.CallTo(() => inner.SubscribeAsync(
            A<Func<TransportReceivedMessage, CancellationToken, Task<MessageAction>>>._,
            A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task SubscribeAsync_Should_Call_DeadLetterHandler_On_Reject()
    {
        var inner = A.Fake<ITransportSubscriber>();
        A.CallTo(() => inner.Source).Returns("test-topic");

        var dlqCalled = false;
        Func<TransportReceivedMessage, string?, CancellationToken, Task> dlqHandler = (_, _, _) =>
        {
            dlqCalled = true;
            return Task.CompletedTask;
        };

        // Capture the handler that gets passed to inner.SubscribeAsync
        Func<TransportReceivedMessage, CancellationToken, Task<MessageAction>>? capturedHandler = null;
        A.CallTo(() => inner.SubscribeAsync(
            A<Func<TransportReceivedMessage, CancellationToken, Task<MessageAction>>>._,
            A<CancellationToken>._))
            .Invokes(call =>
            {
                capturedHandler = call.GetArgument<Func<TransportReceivedMessage, CancellationToken, Task<MessageAction>>>(0);
            })
            .Returns(Task.CompletedTask);

        var subscriber = new DeadLetterTransportSubscriber(inner, dlqHandler, "TestTransport", _meter);

        // User handler always rejects
        Func<TransportReceivedMessage, CancellationToken, Task<MessageAction>> userHandler =
            (_, _) => Task.FromResult(MessageAction.Reject);

        await subscriber.SubscribeAsync(userHandler, CancellationToken.None);

        // Simulate the inner calling the captured handler
        capturedHandler.ShouldNotBeNull();
        var action = await capturedHandler!(new TransportReceivedMessage(), CancellationToken.None);

        action.ShouldBe(MessageAction.Reject);
        dlqCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task SubscribeAsync_Should_Not_Call_DeadLetterHandler_On_Acknowledge()
    {
        var inner = A.Fake<ITransportSubscriber>();
        A.CallTo(() => inner.Source).Returns("test-topic");

        var dlqCalled = false;
        Func<TransportReceivedMessage, string?, CancellationToken, Task> dlqHandler = (_, _, _) =>
        {
            dlqCalled = true;
            return Task.CompletedTask;
        };

        Func<TransportReceivedMessage, CancellationToken, Task<MessageAction>>? capturedHandler = null;
        A.CallTo(() => inner.SubscribeAsync(
            A<Func<TransportReceivedMessage, CancellationToken, Task<MessageAction>>>._,
            A<CancellationToken>._))
            .Invokes(call =>
            {
                capturedHandler = call.GetArgument<Func<TransportReceivedMessage, CancellationToken, Task<MessageAction>>>(0);
            })
            .Returns(Task.CompletedTask);

        var subscriber = new DeadLetterTransportSubscriber(inner, dlqHandler, "TestTransport");

        await subscriber.SubscribeAsync(
            (_, _) => Task.FromResult(MessageAction.Acknowledge),
            CancellationToken.None);

        capturedHandler.ShouldNotBeNull();
        var action = await capturedHandler!(new TransportReceivedMessage(), CancellationToken.None);

        action.ShouldBe(MessageAction.Acknowledge);
        dlqCalled.ShouldBeFalse();
    }
}
