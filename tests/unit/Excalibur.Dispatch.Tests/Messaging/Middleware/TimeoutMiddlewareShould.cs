// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // Use ValueTasks correctly - FakeItEasy .Returns() stores ValueTask

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Middleware;
using Excalibur.Dispatch.Options.Middleware;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using MessageResult = Excalibur.Dispatch.Abstractions.MessageResult;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

[Trait("Category", "Unit")]
public sealed class TimeoutMiddlewareShould : IAsyncDisposable
{
    private TimeoutMiddleware? _sut;

    private TimeoutMiddleware CreateSut(TimeoutOptions? options = null)
    {
        var opts = options ?? new TimeoutOptions { Enabled = true, DefaultTimeout = TimeSpan.FromSeconds(5) };
        _sut = new TimeoutMiddleware(NullLogger<TimeoutMiddleware>.Instance, Microsoft.Extensions.Options.Options.Create(opts));
        return _sut;
    }

    public async ValueTask DisposeAsync()
    {
        if (_sut != null)
        {
            await _sut.DisposeAsync().ConfigureAwait(false);
        }
    }

    [Fact]
    public async Task PassThroughWhenDisabled()
    {
        var sut = CreateSut(new TimeoutOptions { Enabled = false });
        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext();
        var expectedResult = MessageResult.Success();

        var result = await sut.InvokeAsync(
            message, context,
            (_, _, _) => new ValueTask<IMessageResult>(expectedResult),
            CancellationToken.None);

        result.ShouldBe(expectedResult);
    }

    [Fact]
    public async Task CompleteWithinTimeout()
    {
        var sut = CreateSut(new TimeoutOptions { Enabled = true, DefaultTimeout = TimeSpan.FromSeconds(10) });
        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext();

        var result = await sut.InvokeAsync(
            message, context,
            (_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success()),
            CancellationToken.None);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task ThrowMessageTimeoutExceptionWhenTimedOutAndThrowOnTimeoutEnabled()
    {
        // Fake IDispatchMessage is classified as Action kind by GetMessageKind fallback,
        // so ActionTimeout is used instead of DefaultTimeout
        var sut = CreateSut(new TimeoutOptions
        {
            Enabled = true,
            DefaultTimeout = TimeSpan.FromMilliseconds(500),
            ActionTimeout = TimeSpan.FromMilliseconds(500),
            ThrowOnTimeout = true
        });
        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext();

        await Should.ThrowAsync<MessageTimeoutException>(
            () => sut.InvokeAsync(
                message, context,
                async (_, _, ct) =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), ct).ConfigureAwait(false);
                    return MessageResult.Success();
                },
                CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task ReturnTimeoutResultWhenTimedOutAndThrowOnTimeoutDisabled()
    {
        // Fake IDispatchMessage is classified as Action kind by GetMessageKind fallback,
        // so ActionTimeout is used instead of DefaultTimeout
        var sut = CreateSut(new TimeoutOptions
        {
            Enabled = true,
            DefaultTimeout = TimeSpan.FromMilliseconds(500),
            ActionTimeout = TimeSpan.FromMilliseconds(500),
            ThrowOnTimeout = false
        });
        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext();

        var result = await sut.InvokeAsync(
            message, context,
            async (_, _, ct) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(30), ct).ConfigureAwait(false);
                return MessageResult.Success();
            },
            CancellationToken.None);

        result.Succeeded.ShouldBeFalse();
        result.ShouldBeOfType<TimeoutMessageResult>();
    }

    [Fact]
    public async Task UseContextOverrideTimeout()
    {
        var sut = CreateSut(new TimeoutOptions
        {
            Enabled = true,
            DefaultTimeout = TimeSpan.FromMilliseconds(50),
            ThrowOnTimeout = false
        });
        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext();
        context.SetItem("Timeout.Override", TimeSpan.FromSeconds(30));

        var result = await sut.InvokeAsync(
            message, context,
            (_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success()),
            CancellationToken.None);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task UseMessageTypeSpecificTimeout()
    {
        var message = A.Fake<IDispatchMessage>();
        var options = new TimeoutOptions
        {
            Enabled = true,
            DefaultTimeout = TimeSpan.FromMilliseconds(10),
            ThrowOnTimeout = false,
        };
        options.MessageTypeTimeouts[message.GetType().Name] = TimeSpan.FromSeconds(30);

        var sut = CreateSut(options);
        var context = new MessageContext();

        var result = await sut.InvokeAsync(
            message, context,
            (_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success()),
            CancellationToken.None);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void HaveProcessingStage()
    {
        var sut = CreateSut();
        sut.Stage.ShouldBe(DispatchMiddlewareStage.Processing);
    }

    [Fact]
    public void ApplyToActionsAndEvents()
    {
        var sut = CreateSut();
        sut.ApplicableMessageKinds.ShouldBe(MessageKinds.Action | MessageKinds.Event);
    }

    [Fact]
    public async Task PropagateExternalCancellation()
    {
        var sut = CreateSut(new TimeoutOptions { Enabled = true, DefaultTimeout = TimeSpan.FromSeconds(30) });
        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(
            () => sut.InvokeAsync(
                message, context,
                async (_, _, ct) =>
                {
                    ct.ThrowIfCancellationRequested();
                    await Task.Delay(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);
                    return MessageResult.Success();
                },
                cts.Token).AsTask());
    }

    [Fact]
    public async Task SetTimeoutExceededInContextOnTimeout()
    {
        // Fake IDispatchMessage is classified as Action kind by GetMessageKind fallback,
        // so ActionTimeout is used instead of DefaultTimeout
        var sut = CreateSut(new TimeoutOptions
        {
            Enabled = true,
            DefaultTimeout = TimeSpan.FromMilliseconds(500),
            ActionTimeout = TimeSpan.FromMilliseconds(500),
            ThrowOnTimeout = false
        });
        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext();

        await sut.InvokeAsync(
            message, context,
            async (_, _, ct) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(30), ct).ConfigureAwait(false);
                return MessageResult.Success();
            },
            CancellationToken.None);

        context.GetItem<bool>("Timeout.Exceeded").ShouldBeTrue();
    }
}
