// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Threading.Channels;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Channels;
using Excalibur.Dispatch.Options.Channels;
using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Common;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class MessagePumpShould
{
    private static Channel<MessageEnvelope> CreateChannel() => Channel.CreateUnbounded<MessageEnvelope>();

    /// <summary>
    /// Helper that stops the pump gracefully by completing the channel first,
    /// which causes ReadAllAsync to end without throwing OperationCanceledException.
    /// </summary>
    private static async Task StopPumpGracefully(MessagePump pump, Channel<MessageEnvelope> channel)
    {
        // Complete the channel so ReadAllAsync finishes naturally
        channel.Writer.TryComplete();
        // Give processing loop time to exit
        await Task.Delay(50).ConfigureAwait(false);
        // Now stop - the processing task should already be finished or finishing
        try
        {
            await pump.StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // StopAsync cancels the CTS; ReadAllAsync may throw OCE before
            // it observes channel completion. This is expected behavior.
        }
    }

    [Fact]
    public void Throw_When_Name_Is_Null()
    {
        var channel = CreateChannel();
        Should.Throw<ArgumentNullException>(() =>
            new MessagePump(null!, channel, _ => Task.CompletedTask));
    }

    [Fact]
    public void Throw_When_Channel_Is_Null()
    {
        Should.Throw<ArgumentNullException>(() =>
            new MessagePump("test", null!, _ => Task.CompletedTask));
    }

    [Fact]
    public void Throw_When_MessageHandler_Is_Null()
    {
        var channel = CreateChannel();
        Should.Throw<ArgumentNullException>(() =>
            new MessagePump("test", channel, null!));
    }

    [Fact]
    public void Set_Name_From_Constructor()
    {
        var channel = CreateChannel();
        using var sut = new MessagePump("my-pump", channel, _ => Task.CompletedTask);
        sut.Name.ShouldBe("my-pump");
    }

    [Fact]
    public void Have_NotStarted_Status_Initially()
    {
        var channel = CreateChannel();
        using var sut = new MessagePump("test", channel, _ => Task.CompletedTask);
        sut.Status.ShouldBe(ChannelMessagePumpStatus.NotStarted);
    }

    [Fact]
    public void Not_Be_Running_Initially()
    {
        var channel = CreateChannel();
        using var sut = new MessagePump("test", channel, _ => Task.CompletedTask);
        sut.IsRunning.ShouldBeFalse();
    }

    [Fact]
    public void Have_NonNull_Metrics_Initially()
    {
        var channel = CreateChannel();
        using var sut = new MessagePump("test", channel, _ => Task.CompletedTask);
        sut.Metrics.ShouldNotBeNull();
    }

    [Fact]
    public void Expose_Reader()
    {
        var channel = CreateChannel();
        using var sut = new MessagePump("test", channel, _ => Task.CompletedTask);
        sut.Reader.ShouldNotBeNull();
    }

    [Fact]
    public void Expose_Writer()
    {
        var channel = CreateChannel();
        using var sut = new MessagePump("test", channel, _ => Task.CompletedTask);
        sut.Writer.ShouldNotBeNull();
    }

    [Fact]
    public async Task Start_And_Set_Running_Status()
    {
        var channel = CreateChannel();
        using var cts = new CancellationTokenSource();
        var sut = new MessagePump("test", channel, _ => Task.CompletedTask);

        await sut.StartAsync(cts.Token);

        sut.Status.ShouldBe(ChannelMessagePumpStatus.Running);
        sut.IsRunning.ShouldBeTrue();
        sut.Metrics.StartedAt.ShouldNotBeNull();

        await StopPumpGracefully(sut, channel);
    }

    [Fact]
    public async Task Throw_When_Starting_Already_Running_Pump()
    {
        var channel = CreateChannel();
        using var cts = new CancellationTokenSource();
        var sut = new MessagePump("test", channel, _ => Task.CompletedTask);

        await sut.StartAsync(cts.Token);

        var ex = Should.Throw<InvalidOperationException>(() =>
            sut.StartAsync(cts.Token));
        ex.Message.ShouldContain("already running");

        await StopPumpGracefully(sut, channel);
    }

    [Fact]
    public async Task Stop_And_Set_Stopped_Status()
    {
        var channel = CreateChannel();
        using var cts = new CancellationTokenSource();
        var sut = new MessagePump("test", channel, _ => Task.CompletedTask);

        await sut.StartAsync(cts.Token);
        await StopPumpGracefully(sut, channel);

        sut.Status.ShouldBe(ChannelMessagePumpStatus.Stopped);
        sut.IsRunning.ShouldBeFalse();
    }

    [Fact]
    public async Task Stop_When_Not_Running_Is_Noop()
    {
        var channel = CreateChannel();
        using var sut = new MessagePump("test", channel, _ => Task.CompletedTask);

        // Should not throw - just returns
        await sut.StopAsync(CancellationToken.None);
        sut.IsRunning.ShouldBeFalse();
    }

    [Fact]
    public async Task Process_Messages_Written_To_Channel()
    {
        var processed = new List<string>();
        var channel = Channel.CreateUnbounded<MessageEnvelope>();
        var sut = new MessagePump("test", channel, envelope =>
        {
            processed.Add(envelope.MessageId!);
            return Task.CompletedTask;
        });

        using var cts = new CancellationTokenSource();
        await sut.StartAsync(cts.Token);

        var envelope = new MessageEnvelope { MessageId = "msg-1" };
        await channel.Writer.WriteAsync(envelope);

        // Give processing loop time to pick up the message
        await Task.Delay(200);

        processed.ShouldContain("msg-1");

        await StopPumpGracefully(sut, channel);
    }

    [Fact]
    public void Configure_Returns_Self()
    {
        var channel = CreateChannel();
        using var sut = new MessagePump("test", channel, _ => Task.CompletedTask);

        var result = sut.Configure(opts => opts.Capacity = 200);
        result.ShouldBeSameAs(sut);
    }

    [Fact]
    public void Configure_With_Null_Action_Returns_Self()
    {
        var channel = CreateChannel();
        using var sut = new MessagePump("test", channel, _ => Task.CompletedTask);

        var result = sut.Configure(null);
        result.ShouldBeSameAs(sut);
    }

    [Fact]
    public async Task DisposeAsync_Completes_Without_Start()
    {
        var channel = CreateChannel();
        var sut = new MessagePump("test", channel, _ => Task.CompletedTask);

        await sut.DisposeAsync();

        sut.IsRunning.ShouldBeFalse();
    }

    [Fact]
    public void Dispose_Synchronously_Without_Throwing()
    {
        var channel = CreateChannel();
        var sut = new MessagePump("test", channel, _ => Task.CompletedTask);
        Should.NotThrow(() => sut.Dispose());
    }

    [Fact]
    public void Use_Default_Options_When_Null()
    {
        var channel = CreateChannel();
        using var sut = new MessagePump("test", channel, _ => Task.CompletedTask, options: null);
        sut.ShouldNotBeNull();
    }

    [Fact]
    public void Use_Custom_Options()
    {
        var channel = CreateChannel();
        var options = new ChannelMessagePumpOptions
        {
            Capacity = 50,
            SingleReader = true,
        };

        using var sut = new MessagePump("test", channel, _ => Task.CompletedTask, options);
        sut.ShouldNotBeNull();
    }

    [Fact]
    public void Implement_IChannelMessagePump()
    {
        var channel = CreateChannel();
        using var sut = new MessagePump("test", channel, _ => Task.CompletedTask);
        sut.ShouldBeAssignableTo<IChannelMessagePump>();
    }

    [Fact]
    public void Implement_IDisposable()
    {
        var channel = CreateChannel();
        using var sut = new MessagePump("test", channel, _ => Task.CompletedTask);
        sut.ShouldBeAssignableTo<IDisposable>();
    }

    [Fact]
    public void Have_DisposeAsync_Method()
    {
        // MessagePump has a public DisposeAsync method but doesn't explicitly
        // declare IAsyncDisposable on the type. Verify the method exists.
        var channel = CreateChannel();
        var sut = new MessagePump("test", channel, _ => Task.CompletedTask);

        var method = sut.GetType().GetMethod("DisposeAsync");
        method.ShouldNotBeNull();
        method.ReturnType.ShouldBe(typeof(ValueTask));

        sut.Dispose();
    }

    [Fact]
    public async Task Handle_Exceptions_In_MessageHandler_Without_Crashing()
    {
        var channel = Channel.CreateUnbounded<MessageEnvelope>();
        var processedAfterError = false;

        var sut = new MessagePump("test", channel, envelope =>
        {
            if (envelope.MessageId == "fail")
            {
                throw new InvalidOperationException("Test error");
            }

            processedAfterError = true;
            return Task.CompletedTask;
        });

        using var cts = new CancellationTokenSource();
        await sut.StartAsync(cts.Token);

        // Send a message that throws
        await channel.Writer.WriteAsync(new MessageEnvelope { MessageId = "fail" });
        await Task.Delay(100);

        // Send a second message to verify the pump is still running
        await channel.Writer.WriteAsync(new MessageEnvelope { MessageId = "ok" });
        await Task.Delay(200);

        processedAfterError.ShouldBeTrue();
        sut.IsRunning.ShouldBeTrue();

        await StopPumpGracefully(sut, channel);
    }

    [Fact]
    public async Task Set_StartedAt_In_Metrics_On_Start()
    {
        var channel = CreateChannel();
        var sut = new MessagePump("test", channel, _ => Task.CompletedTask);

        sut.Metrics.StartedAt.ShouldBeNull();

        using var cts = new CancellationTokenSource();
        await sut.StartAsync(cts.Token);

        sut.Metrics.StartedAt.ShouldNotBeNull();
        sut.Metrics.StartedAt!.Value.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddMinutes(-1));

        await StopPumpGracefully(sut, channel);
    }
}
