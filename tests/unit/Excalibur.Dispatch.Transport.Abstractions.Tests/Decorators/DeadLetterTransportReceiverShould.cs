using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Decorators;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Decorators;

public sealed class DeadLetterTransportReceiverShould : IDisposable
{
    private readonly Meter _meter;

    public DeadLetterTransportReceiverShould()
    {
        _meter = new Meter("test.dlq.receiver");
    }

    public void Dispose()
    {
        _meter.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Should_Throw_On_Null_DeadLetterHandler()
    {
        var inner = A.Fake<ITransportReceiver>();

        Should.Throw<ArgumentNullException>(() =>
            new DeadLetterTransportReceiver(inner, null!, "Kafka"));
    }

    [Fact]
    public void Should_Throw_On_Null_TransportName()
    {
        var inner = A.Fake<ITransportReceiver>();
        Func<TransportReceivedMessage, string?, CancellationToken, Task> handler = (_, _, _) => Task.CompletedTask;

        Should.Throw<ArgumentNullException>(() =>
            new DeadLetterTransportReceiver(inner, handler, null!));
    }

    [Fact]
    public async Task RejectAsync_Should_Call_DeadLetterHandler_When_Not_Requeuing()
    {
        var inner = A.Fake<ITransportReceiver>();
        A.CallTo(() => inner.Source).Returns("test-queue");
        var dlqCalled = false;
        Func<TransportReceivedMessage, string?, CancellationToken, Task> handler = (_, _, _) =>
        {
            dlqCalled = true;
            return Task.CompletedTask;
        };

        var receiver = new DeadLetterTransportReceiver(inner, handler, "TestTransport", _meter);
        var message = new TransportReceivedMessage { Id = "msg-1" };

        await receiver.RejectAsync(message, "bad format", false, CancellationToken.None);

        dlqCalled.ShouldBeTrue();
        A.CallTo(() => inner.RejectAsync(message, "bad format", false, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task RejectAsync_Should_Not_Call_DeadLetterHandler_When_Requeuing()
    {
        var inner = A.Fake<ITransportReceiver>();
        A.CallTo(() => inner.Source).Returns("test-queue");
        var dlqCalled = false;
        Func<TransportReceivedMessage, string?, CancellationToken, Task> handler = (_, _, _) =>
        {
            dlqCalled = true;
            return Task.CompletedTask;
        };

        var receiver = new DeadLetterTransportReceiver(inner, handler, "TestTransport");
        var message = new TransportReceivedMessage { Id = "msg-1" };

        await receiver.RejectAsync(message, "retry", true, CancellationToken.None);

        dlqCalled.ShouldBeFalse();
        A.CallTo(() => inner.RejectAsync(message, "retry", true, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task RejectAsync_Should_Pass_Reason_To_DeadLetterHandler()
    {
        var inner = A.Fake<ITransportReceiver>();
        A.CallTo(() => inner.Source).Returns("test-queue");
        string? capturedReason = null;
        Func<TransportReceivedMessage, string?, CancellationToken, Task> handler = (_, reason, _) =>
        {
            capturedReason = reason;
            return Task.CompletedTask;
        };

        var receiver = new DeadLetterTransportReceiver(inner, handler, "TestTransport");
        await receiver.RejectAsync(new TransportReceivedMessage(), "parse error", false, CancellationToken.None);

        capturedReason.ShouldBe("parse error");
    }

    [Fact]
    public async Task RejectAsync_Should_Work_Without_Meter()
    {
        var inner = A.Fake<ITransportReceiver>();
        A.CallTo(() => inner.Source).Returns("test-queue");
        Func<TransportReceivedMessage, string?, CancellationToken, Task> handler = (_, _, _) => Task.CompletedTask;

        var receiver = new DeadLetterTransportReceiver(inner, handler, "TestTransport");

        // Should not throw even without a meter
        await receiver.RejectAsync(new TransportReceivedMessage(), "reason", false, CancellationToken.None);
    }
}
