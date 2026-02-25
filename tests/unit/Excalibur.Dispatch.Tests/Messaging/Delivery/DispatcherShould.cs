// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // Use ValueTasks correctly - FakeItEasy .Returns() stores ValueTask

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Handlers;

using Microsoft.Extensions.Logging.Abstractions;

using MessageResult = Excalibur.Dispatch.Abstractions.MessageResult;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery;

[Trait("Category", "Unit")]
public sealed class DispatcherShould
{
    private readonly IDispatchMiddlewareInvoker _middlewareInvoker = A.Fake<IDispatchMiddlewareInvoker>();
    private readonly FinalDispatchHandler _finalHandler = new(
        A.Fake<IMessageBusProvider>(),
        NullLogger<FinalDispatchHandler>.Instance,
        retryPolicy: null,
        new Dictionary<string, IMessageBusOptions>(StringComparer.Ordinal));

    private Dispatcher CreateSut(
        IDispatchMiddlewareInvoker? invoker = null,
        FinalDispatchHandler? handler = null,
        ITransportContextProvider? transportProvider = null,
        IServiceProvider? serviceProvider = null) =>
        new(invoker ?? _middlewareInvoker, handler ?? _finalHandler, transportProvider, serviceProvider);

    [Fact]
    public async Task ThrowWhenMiddlewareInvokerIsNull()
    {
        var sut = new Dispatcher(middlewareInvoker: null, finalHandler: null);
        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext();

        await Should.ThrowAsync<InvalidOperationException>(
            () => sut.DispatchAsync(message, context, CancellationToken.None));
    }

    [Fact]
    public async Task ThrowWhenFinalHandlerIsNull()
    {
        var sut = new Dispatcher(middlewareInvoker: _middlewareInvoker, finalHandler: null);
        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext();

        await Should.ThrowAsync<InvalidOperationException>(
            () => sut.DispatchAsync(message, context, CancellationToken.None));
    }

    [Fact]
    public async Task DispatchMessageThroughMiddlewarePipeline()
    {
        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext();
        var expectedResult = MessageResult.Success();

        A.CallTo(_middlewareInvoker)
            .WithReturnType<ValueTask<IMessageResult>>()
            .Returns(new ValueTask<IMessageResult>(expectedResult));

        var sut = CreateSut();

        var result = await sut.DispatchAsync(message, context, CancellationToken.None);

        result.ShouldBe(expectedResult);
        A.CallTo(_middlewareInvoker)
            .WithReturnType<ValueTask<IMessageResult>>()
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task PopulateCorrelationIdWhenNotSet()
    {
        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext { CorrelationId = null };

        A.CallTo(_middlewareInvoker)
            .WithReturnType<ValueTask<IMessageResult>>()
            .Returns(new ValueTask<IMessageResult>(MessageResult.Success()));

        var sut = CreateSut();
        await sut.DispatchAsync(message, context, CancellationToken.None);

        context.CorrelationId.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task PopulateCausationIdFromCorrelationIdWhenNotSet()
    {
        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext { CorrelationId = "test-correlation", CausationId = null };

        A.CallTo(_middlewareInvoker)
            .WithReturnType<ValueTask<IMessageResult>>()
            .Returns(new ValueTask<IMessageResult>(MessageResult.Success()));

        var sut = CreateSut();
        await sut.DispatchAsync(message, context, CancellationToken.None);

        context.CausationId.ShouldBe("test-correlation");
    }

    [Fact]
    public async Task SetMessageTypeInContext()
    {
        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext();

        A.CallTo(_middlewareInvoker)
            .WithReturnType<ValueTask<IMessageResult>>()
            .Returns(new ValueTask<IMessageResult>(MessageResult.Success()));

        var sut = CreateSut();
        await sut.DispatchAsync(message, context, CancellationToken.None);

        context.MessageType.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task StoreMessageInContext()
    {
        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext();

        A.CallTo(_middlewareInvoker)
            .WithReturnType<ValueTask<IMessageResult>>()
            .Returns(new ValueTask<IMessageResult>(MessageResult.Success()));

        var sut = CreateSut();
        await sut.DispatchAsync(message, context, CancellationToken.None);

        context.Message.ShouldBe(message);
    }

    [Fact]
    public async Task SetAmbientContextDuringDispatch()
    {
        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext();
        IMessageContext? capturedContext = null;

        A.CallTo(_middlewareInvoker)
            .WithReturnType<ValueTask<IMessageResult>>()
            .Invokes(call =>
            {
                capturedContext = MessageContextHolder.Current;
            })
            .Returns(new ValueTask<IMessageResult>(MessageResult.Success()));

        var sut = CreateSut();
        await sut.DispatchAsync(message, context, CancellationToken.None);

        capturedContext.ShouldBe(context);
    }

    [Fact]
    public async Task RestoreAmbientContextAfterDispatch()
    {
        var previousContext = new MessageContext();
        MessageContextHolder.Current = previousContext;

        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext();

        A.CallTo(_middlewareInvoker)
            .WithReturnType<ValueTask<IMessageResult>>()
            .Returns(new ValueTask<IMessageResult>(MessageResult.Success()));

        var sut = CreateSut();
        await sut.DispatchAsync(message, context, CancellationToken.None);

        MessageContextHolder.Current.ShouldBe(previousContext);
        MessageContextHolder.Current = null;
    }

    [Fact]
    public async Task ThrowWhenMessageIsNull()
    {
        var sut = CreateSut();
        var context = new MessageContext();

        await Should.ThrowAsync<ArgumentNullException>(
            () => sut.DispatchAsync<IDispatchMessage>(null!, context, CancellationToken.None));
    }

    [Fact]
    public async Task ThrowWhenContextIsNull()
    {
        var sut = CreateSut();
        var message = A.Fake<IDispatchMessage>();

        await Should.ThrowAsync<ArgumentNullException>(
            () => sut.DispatchAsync(message, null!, CancellationToken.None));
    }

    [Fact]
    public async Task ResolveTransportBindingBeforeMiddleware()
    {
        var transportProvider = A.Fake<ITransportContextProvider>();
        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext();
        var binding = A.Fake<ITransportBinding>();

        A.CallTo(() => transportProvider.GetTransportBinding(A<IMessageContext>._))
            .Returns(binding);
        A.CallTo(_middlewareInvoker)
            .WithReturnType<ValueTask<IMessageResult>>()
            .Returns(new ValueTask<IMessageResult>(MessageResult.Success()));

        var sut = CreateSut(transportProvider: transportProvider);
        await sut.DispatchAsync(message, context, CancellationToken.None);

        context.Items.ShouldContainKey("Excalibur.Dispatch.TransportBinding");
    }

    [Fact]
    public void ExposeServiceProvider()
    {
        var sp = A.Fake<IServiceProvider>();
        var sut = CreateSut(serviceProvider: sp);

        sut.ServiceProvider.ShouldBe(sp);
    }

    [Fact]
    public async Task PreserveExistingCorrelationId()
    {
        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext { CorrelationId = "existing-correlation" };

        A.CallTo(_middlewareInvoker)
            .WithReturnType<ValueTask<IMessageResult>>()
            .Returns(new ValueTask<IMessageResult>(MessageResult.Success()));

        var sut = CreateSut();
        await sut.DispatchAsync(message, context, CancellationToken.None);

        context.CorrelationId.ShouldBe("existing-correlation");
    }

    [Fact]
    public async Task RestoreAmbientContextEvenOnException()
    {
        var previousContext = new MessageContext();
        MessageContextHolder.Current = previousContext;

        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext();

        A.CallTo(_middlewareInvoker)
            .WithReturnType<ValueTask<IMessageResult>>()
            .ThrowsAsync(new InvalidOperationException("test"));

        var sut = CreateSut();

        try { await sut.DispatchAsync(message, context, CancellationToken.None); }
        catch (InvalidOperationException) { }

        MessageContextHolder.Current.ShouldBe(previousContext);
        MessageContextHolder.Current = null;
    }
}
