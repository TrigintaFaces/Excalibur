using System.Threading.Channels;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Channels;
using Excalibur.Dispatch.Options.Channels;
using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Common.Channels;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class MessagePumpShould
{
    private static int GetPrivateCounter(MessagePump pump, string fieldName)
    {
        var field = typeof(MessagePump).GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (field is null)
        {
            throw new InvalidOperationException($"Expected private field '{fieldName}' was not found.");
        }

        return (int)field.GetValue(pump)!;
    }

    [Fact]
    public void Constructor_Throws_ForNullArguments()
    {
        var channel = Channel.CreateUnbounded<MessageEnvelope>();

        Should.Throw<ArgumentNullException>(() => new MessagePump(null!, channel, _ => Task.CompletedTask));
        Should.Throw<ArgumentNullException>(() => new MessagePump("pump", null!, _ => Task.CompletedTask));
        Should.Throw<ArgumentNullException>(() => new MessagePump("pump", channel, null!));
    }

    [Fact]
    public async Task StartAndStop_TransitionStatus_AndSetStartedTimestamp()
    {
        var channel = Channel.CreateUnbounded<MessageEnvelope>();
        using var pump = new MessagePump("pump", channel, _ => Task.CompletedTask);

        pump.Status.ShouldBe(ChannelMessagePumpStatus.NotStarted);
        pump.IsRunning.ShouldBeFalse();
        pump.Name.ShouldBe("pump");

        await pump.StartAsync(CancellationToken.None);

        pump.IsRunning.ShouldBeTrue();
        pump.Status.ShouldBe(ChannelMessagePumpStatus.Running);
        pump.Metrics.StartedAt.ShouldNotBeNull();
        pump.Reader.ShouldNotBeNull();
        pump.Writer.ShouldNotBeNull();

        await pump.StopAsync(CancellationToken.None);

        pump.IsRunning.ShouldBeFalse();
        pump.Status.ShouldBe(ChannelMessagePumpStatus.Stopped);
    }

    [Fact]
    public async Task StartAsync_Throws_WhenAlreadyRunning()
    {
        var channel = Channel.CreateUnbounded<MessageEnvelope>();
        using var pump = new MessagePump("pump", channel, _ => Task.CompletedTask);
        await pump.StartAsync(CancellationToken.None);

        var ex = await Should.ThrowAsync<InvalidOperationException>(() => pump.StartAsync(CancellationToken.None));
        ex.Message.ShouldContain("already running");

        await pump.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StopAsync_WhenNotRunning_IsNoOp()
    {
        var channel = Channel.CreateUnbounded<MessageEnvelope>();
        using var pump = new MessagePump("pump", channel, _ => Task.CompletedTask);

        await pump.StopAsync(CancellationToken.None);

        pump.Status.ShouldBe(ChannelMessagePumpStatus.NotStarted);
        pump.IsRunning.ShouldBeFalse();
    }

    [Fact]
    public async Task ProcessMessagesAsync_InvokesHandler_ForEachMessage()
    {
        var processed = 0;
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var channel = Channel.CreateUnbounded<MessageEnvelope>();

        using var pump = new MessagePump(
            "pump",
            channel,
            _ =>
            {
                if (Interlocked.Increment(ref processed) == 2)
                {
                    completion.TrySetResult();
                }

                return Task.CompletedTask;
            });

        await pump.StartAsync(CancellationToken.None);
        channel.Writer.TryWrite(new MessageEnvelope());
        channel.Writer.TryWrite(new MessageEnvelope());

        await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(

        	completion.Task,

        	TimeSpan.FromSeconds(5));
        await pump.StopAsync(CancellationToken.None);

        processed.ShouldBe(2);
    }

    [Fact]
    public async Task ProcessMessagesAsync_Continues_WhenHandlerThrows()
    {
        var attempts = 0;
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var channel = Channel.CreateUnbounded<MessageEnvelope>();

        using var pump = new MessagePump(
            "pump",
            channel,
            _ =>
            {
                var attempt = Interlocked.Increment(ref attempts);
                if (attempt == 1)
                {
                    throw new InvalidOperationException("simulated failure");
                }

                completion.TrySetResult();
                return Task.CompletedTask;
            });

        await pump.StartAsync(CancellationToken.None);
        channel.Writer.TryWrite(new MessageEnvelope());
        channel.Writer.TryWrite(new MessageEnvelope());

        await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(

        	completion.Task,

        	TimeSpan.FromSeconds(5));
        await pump.StopAsync(CancellationToken.None);

        attempts.ShouldBe(2);
    }

    [Fact]
    public async Task ProcessMessagesAsync_Completes_WhenInputChannelIsCompleted()
    {
        var processed = 0;
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var channel = Channel.CreateUnbounded<MessageEnvelope>();

        using var pump = new MessagePump(
            "pump",
            channel,
            _ =>
            {
                Interlocked.Increment(ref processed);
                completion.TrySetResult();
                return Task.CompletedTask;
            });

        await pump.StartAsync(CancellationToken.None);
        channel.Writer.TryWrite(new MessageEnvelope()).ShouldBeTrue();
        await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
        	completion.Task,
        	TimeSpan.FromSeconds(5));
        channel.Writer.TryComplete().ShouldBeTrue();

        await pump.StopAsync(CancellationToken.None);

        processed.ShouldBe(1);
    }

    [Fact]
    public void Configure_ReturnsSamePump_ForChaining()
    {
        var channel = Channel.CreateUnbounded<MessageEnvelope>();
        using var pump = new MessagePump("pump", channel, _ => Task.CompletedTask);

        var configured = pump.Configure(options => options.Capacity = 32);

        configured.ShouldBeSameAs(pump);
    }

    [Fact]
    public void Dispose_CompletesUnderlyingInputChannel()
    {
        var channel = Channel.CreateUnbounded<MessageEnvelope>();
        var pump = new MessagePump("pump", channel, _ => Task.CompletedTask);

        pump.Dispose();

        channel.Writer.TryWrite(new MessageEnvelope()).ShouldBeFalse();
    }

    [Fact]
    public async Task DisposeAsync_StopsPump_AndCompletesInternalWriter()
    {
        var channel = Channel.CreateUnbounded<MessageEnvelope>();
        var pump = new MessagePump("pump", channel, _ => Task.CompletedTask);

        await pump.StartAsync(CancellationToken.None);
        await pump.DisposeAsync();

        pump.IsRunning.ShouldBeFalse();
        pump.Status.ShouldBe(ChannelMessagePumpStatus.Stopped);
        pump.Writer.ShouldNotBeNull();
        pump.Writer.TryWrite(new MessageEnvelope()).ShouldBeFalse();
    }

    [Fact]
    public async Task Dispose_WhileRunning_CompletesInputChannel_AndStopsProcessing()
    {
        var channel = Channel.CreateUnbounded<MessageEnvelope>();
        var pump = new MessagePump("pump", channel, _ => Task.CompletedTask);
        await pump.StartAsync(CancellationToken.None);

        pump.Dispose();

        pump.IsRunning.ShouldBeFalse();
        channel.Writer.TryWrite(new MessageEnvelope()).ShouldBeFalse();
    }

    [Fact]
    public async Task ProtectedHooks_CanBeInvoked_ByDerivedPump()
    {
        var channel = Channel.CreateUnbounded<MessageEnvelope>();
        var pump = new TestableMessagePump("pump", channel, _ => Task.CompletedTask);

        pump.MarkConsumed();
        pump.MarkConsumed();
        pump.MarkFailed();
        await pump.InvokeProduceAsync(channel.Writer, CancellationToken.None);

        GetPrivateCounter(pump, "_messagesConsumed").ShouldBe(2);
        GetPrivateCounter(pump, "_messagesFailed").ShouldBe(1);
    }

    private sealed class TestableMessagePump(
        string name,
        Channel<MessageEnvelope> channel,
        Func<MessageEnvelope, Task> messageHandler,
        ChannelMessagePumpOptions? options = null)
        : MessagePump(name, channel, messageHandler, options)
    {
        public void MarkConsumed() => OnMessageConsumed();

        public void MarkFailed() => OnMessageFailed();

        public Task InvokeProduceAsync(ChannelWriter<MessageEnvelope> writer, CancellationToken cancellationToken) =>
            ProduceMessagesAsync(writer, cancellationToken);
    }
}
