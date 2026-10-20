using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Decorators;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Decorators;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
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

    [Fact]
    [Trait("Pattern", "Regression")]
    public Task Persist_Quarantine_Before_Rejecting_Source() => VerifyWriteBeforeRejectAsync(failWrite: false);

    [Fact]
    [Trait("Pattern", "Regression")]
    public Task Preserve_Source_When_Quarantine_Write_Fails() => VerifyWriteBeforeRejectAsync(failWrite: true);

    [Fact]
    [Trait("Pattern", "Regression")]
    public async Task Preserve_Quarantine_When_Source_Reject_Fails()
    {
        var records = new Dictionary<string, TransportReceivedMessage>();
        var inner = A.Fake<ITransportReceiver>();
        var message = new TransportReceivedMessage { Id = "terminal-failure" };
        var terminalFailure = new InvalidOperationException("source rejection failed");
        A.CallTo(() => inner.RejectAsync(message, "poison", false, A<CancellationToken>._))
            .ReturnsLazily(() =>
            {
                records[message.Id].ShouldBeSameAs(message);
                return Task.FromException(terminalFailure);
            });
        var receiver = new DeadLetterTransportReceiver(inner, (received, _, _) =>
        {
            records.TryAdd(received.Id, received);
            return Task.CompletedTask;
        }, "TestTransport");

        var observed = await Should.ThrowAsync<InvalidOperationException>(() =>
            receiver.RejectAsync(message, "poison", false, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5)));

        observed.ShouldBeSameAs(terminalFailure);
        records.Count.ShouldBe(1);
        records[message.Id].ShouldBeSameAs(message);
        A.CallTo(() => inner.RejectAsync(message, "poison", false, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    private static async Task VerifyWriteBeforeRejectAsync(bool failWrite)
    {
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var records = new Dictionary<string, TransportReceivedMessage>();
        var inner = A.Fake<ITransportReceiver>();
        var message = new TransportReceivedMessage { Id = "quarantine-ordering" };
        var writeFailure = new InvalidOperationException("quarantine write failed");
        A.CallTo(() => inner.RejectAsync(message, "poison", false, A<CancellationToken>._))
            .ReturnsLazily(() =>
            {
                records[message.Id].ShouldBeSameAs(message);
                return Task.CompletedTask;
            });
        var receiver = new DeadLetterTransportReceiver(inner, async (received, _, _) =>
        {
            entered.TrySetResult(true);
            await release.Task.ConfigureAwait(false);
            if (failWrite)
            {
                throw writeFailure;
            }

            records.Add(received.Id, received);
        }, "TestTransport");
        var operation = receiver.RejectAsync(message, "poison", false, CancellationToken.None);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            operation.IsCompleted.ShouldBeFalse();
            A.CallTo(() => inner.RejectAsync(message, "poison", false, A<CancellationToken>._))
                .MustNotHaveHappened();
            release.TrySetResult(true);
            if (failWrite)
            {
                var observed = await Should.ThrowAsync<InvalidOperationException>(() =>
                    operation.WaitAsync(TimeSpan.FromSeconds(5)));
                observed.ShouldBeSameAs(writeFailure);
                records.ShouldBeEmpty();
                A.CallTo(() => inner.RejectAsync(message, "poison", false, A<CancellationToken>._))
                    .MustNotHaveHappened();
            }
            else
            {
                await operation.WaitAsync(TimeSpan.FromSeconds(5));
                records.Count.ShouldBe(1);
                records[message.Id].ShouldBeSameAs(message);
                A.CallTo(() => inner.RejectAsync(message, "poison", false, A<CancellationToken>._))
                    .MustHaveHappenedOnceExactly();
            }
        }
        finally
        {
            release.TrySetResult(true);
            try
            {
                await operation.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (InvalidOperationException exception) when (ReferenceEquals(exception, writeFailure))
            {
                // The injected failure is asserted above; drain the operation before leaving the test.
            }
        }
    }
}
