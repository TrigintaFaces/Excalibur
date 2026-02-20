// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // Use ValueTasks correctly - FakeItEasy .Returns() stores ValueTask

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.ErrorHandling;
using Excalibur.Dispatch.Options.ErrorHandling;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MessageResult = Excalibur.Dispatch.Abstractions.MessageResult;

namespace Excalibur.Dispatch.Tests.Messaging.ErrorHandling;

[Trait("Category", "Unit")]
public sealed class PoisonMessageMiddlewareShould : IDisposable
{
    private readonly IPoisonMessageDetector _detector = A.Fake<IPoisonMessageDetector>();
    private readonly IPoisonMessageHandler _handler = A.Fake<IPoisonMessageHandler>();
    private readonly ILogger<PoisonMessageMiddleware> _logger;
    private PoisonMessageMiddleware? _sut;

    public PoisonMessageMiddlewareShould()
    {
        _logger = A.Fake<ILogger<PoisonMessageMiddleware>>();
        A.CallTo(() => _logger.IsEnabled(A<LogLevel>._)).Returns(true);
        A.CallTo(() => _logger.BeginScope(A<object>._)).Returns(A.Fake<IDisposable>());
    }

    private PoisonMessageMiddleware CreateSut(PoisonMessageOptions? options = null)
    {
        var opts = options ?? new PoisonMessageOptions { Enabled = true };
        _sut = new PoisonMessageMiddleware(_detector, _handler, Microsoft.Extensions.Options.Options.Create(opts), _logger);
        return _sut;
    }

    [Fact]
    public async Task PassThroughWhenDisabled()
    {
        var sut = CreateSut(new PoisonMessageOptions { Enabled = false });
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
    public async Task PassThroughOnSuccess()
    {
        var sut = CreateSut();
        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext();

        var result = await sut.InvokeAsync(
            message, context,
            (_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success()),
            CancellationToken.None);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task DetectAndHandlePoisonMessage()
    {
        A.CallTo(() => _detector.IsPoisonMessageAsync(
                A<IDispatchMessage>._, A<IMessageContext>._, A<MessageProcessingInfo>._, A<Exception?>._))
            .Returns(Task.FromResult(PoisonDetectionResult.Poison("Too many failures", "TestDetector")));

        A.CallTo(() => _handler.HandlePoisonMessageAsync(
                A<IDispatchMessage>._, A<IMessageContext>._, A<string>._, A<CancellationToken>._, A<Exception?>._))
            .Returns(Task.CompletedTask);

        var sut = CreateSut();
        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext();

        var result = await sut.InvokeAsync(
            message, context,
            (_, _, _) => throw new InvalidOperationException("processing failure"),
            CancellationToken.None);

        result.Succeeded.ShouldBeFalse();
        result.ProblemDetails.ShouldNotBeNull();
        result.ProblemDetails!.Type.ShouldBe("PoisonMessage");
    }

    [Fact]
    public async Task RethrowWhenNotPoisonMessage()
    {
        A.CallTo(() => _detector.IsPoisonMessageAsync(
                A<IDispatchMessage>._, A<IMessageContext>._, A<MessageProcessingInfo>._, A<Exception?>._))
            .Returns(Task.FromResult(PoisonDetectionResult.NotPoison()));

        var sut = CreateSut();
        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext();

        await Should.ThrowAsync<InvalidOperationException>(
            () => sut.InvokeAsync(
                message, context,
                (_, _, _) => throw new InvalidOperationException("not poison"),
                CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task IncrementProcessingAttempts()
    {
        var sut = CreateSut();
        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext();

        await sut.InvokeAsync(
            message, context,
            (_, ctx, _) =>
            {
                ctx.Items["ProcessingAttempts"].ShouldBe(1);
                return new ValueTask<IMessageResult>(MessageResult.Success());
            },
            CancellationToken.None);
    }

    [Fact]
    public void HavePreProcessingStage()
    {
        var sut = CreateSut();
        sut.Stage.ShouldBe(DispatchMiddlewareStage.PreProcessing);
    }

    [Fact]
    public async Task ThrowWhenMessageIsNull()
    {
        var sut = CreateSut();
        await Should.ThrowAsync<ArgumentNullException>(
            () => sut.InvokeAsync(
                null!, new MessageContext(),
                (_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success()),
                CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task RethrowOriginalWhenPoisonHandlerFails()
    {
        A.CallTo(() => _detector.IsPoisonMessageAsync(
                A<IDispatchMessage>._, A<IMessageContext>._, A<MessageProcessingInfo>._, A<Exception?>._))
            .Returns(Task.FromResult(PoisonDetectionResult.Poison("Too many failures", "TestDetector")));

        A.CallTo(() => _handler.HandlePoisonMessageAsync(
                A<IDispatchMessage>._, A<IMessageContext>._, A<string>._, A<CancellationToken>._, A<Exception?>._))
            .ThrowsAsync(new IOException("handler broke"));

        var sut = CreateSut();
        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext();

        // When the poison handler fails, its exception propagates (throw; rethrows the handler exception)
        await Should.ThrowAsync<IOException>(
            () => sut.InvokeAsync(
                message, context,
                (_, _, _) => throw new InvalidOperationException("original failure"),
                CancellationToken.None).AsTask());
    }

    [Fact]
    public void DisposeDoesNotThrow()
    {
        var sut = CreateSut();
        Should.NotThrow(() => sut.Dispose());
    }

    [Fact]
    public void DoubleDisposeDoesNotThrow()
    {
        var sut = CreateSut();
        Should.NotThrow(() =>
        {
            sut.Dispose();
            sut.Dispose();
        });
    }

    public void Dispose()
    {
        _sut?.Dispose();
    }
}
