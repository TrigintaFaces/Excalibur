// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // Use ValueTasks correctly - FakeItEasy .Returns() stores ValueTask

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Delivery.Pipeline;

using MessageResult = Excalibur.Dispatch.Abstractions.MessageResult;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery.Pipeline;

[Trait("Category", "Unit")]
public sealed class DispatchPipelineShould
{
    private static IDispatchMiddleware CreateMiddleware(
        DispatchMiddlewareStage stage = DispatchMiddlewareStage.PreProcessing,
        Func<IDispatchMessage, IMessageContext, DispatchRequestDelegate, CancellationToken, ValueTask<IMessageResult>>? invokeFunc = null)
    {
        var middleware = A.Fake<IDispatchMiddleware>();
        A.CallTo(() => middleware.Stage).Returns(stage);
        A.CallTo(() => middleware.ApplicableMessageKinds).Returns(MessageKinds.All);

        if (invokeFunc != null)
        {
            A.CallTo(() => middleware.InvokeAsync(
                    A<IDispatchMessage>._, A<IMessageContext>._, A<DispatchRequestDelegate>._, A<CancellationToken>._))
                .ReturnsLazily(call => invokeFunc(
                    call.GetArgument<IDispatchMessage>(0)!,
                    call.GetArgument<IMessageContext>(1)!,
                    call.GetArgument<DispatchRequestDelegate>(2)!,
                    call.GetArgument<CancellationToken>(3)));
        }

        return middleware;
    }

    [Fact]
    public async Task ExecuteFinalDelegateWhenNoMiddleware()
    {
        var pipeline = new DispatchPipeline(Enumerable.Empty<IDispatchMiddleware>());
        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext();
        var called = false;

        ValueTask<IMessageResult> FinalDelegate(IDispatchMessage msg, IMessageContext ctx, CancellationToken ct)
        {
            called = true;
            return new ValueTask<IMessageResult>(MessageResult.Success());
        }

        await pipeline.ExecuteAsync(message, context, FinalDelegate, CancellationToken.None);

        called.ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteMiddlewareInStageOrder()
    {
        var executionOrder = new List<string>();

        var errorHandling = CreateMiddleware(DispatchMiddlewareStage.ErrorHandling,
            async (msg, ctx, next, ct) =>
            {
                executionOrder.Add("ErrorHandling");
                return await next(msg, ctx, ct).ConfigureAwait(false);
            });

        var preProcessing = CreateMiddleware(DispatchMiddlewareStage.PreProcessing,
            async (msg, ctx, next, ct) =>
            {
                executionOrder.Add("PreProcessing");
                return await next(msg, ctx, ct).ConfigureAwait(false);
            });

        var validation = CreateMiddleware(DispatchMiddlewareStage.Validation,
            async (msg, ctx, next, ct) =>
            {
                executionOrder.Add("Validation");
                return await next(msg, ctx, ct).ConfigureAwait(false);
            });

        // Provide in wrong order; pipeline should sort by stage
        var pipeline = new DispatchPipeline(new[] { validation, errorHandling, preProcessing });
        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext();

        await pipeline.ExecuteAsync(message, context,
            (_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success()),
            CancellationToken.None);

        // PreProcessing (10) < Validation (20) < ErrorHandling (80)
        executionOrder[0].ShouldBe("PreProcessing");
        executionOrder[1].ShouldBe("Validation");
        executionOrder[2].ShouldBe("ErrorHandling");
    }

    [Fact]
    public async Task AllowMiddlewareToShortCircuitPipeline()
    {
        var secondMiddlewareCalled = false;
        var failedResult = MessageResult.Failed(new MessageProblemDetails
        {
            Type = "Test",
            Title = "ShortCircuit",
            ErrorCode = 400,
            Status = 400,
            Detail = "Validation failed"
        });

        var first = CreateMiddleware(DispatchMiddlewareStage.Validation,
            (_, _, _, _) => new ValueTask<IMessageResult>(failedResult));

        var second = CreateMiddleware(DispatchMiddlewareStage.Processing,
            async (msg, ctx, next, ct) =>
            {
                secondMiddlewareCalled = true;
                return await next(msg, ctx, ct).ConfigureAwait(false);
            });

        var pipeline = new DispatchPipeline(new[] { first, second });
        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext();

        var result = await pipeline.ExecuteAsync(message, context,
            (_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success()),
            CancellationToken.None);

        result.Succeeded.ShouldBeFalse();
        secondMiddlewareCalled.ShouldBeFalse();
    }

    [Fact]
    public async Task PassMessageAndContextThroughPipeline()
    {
        IDispatchMessage? capturedMessage = null;
        IMessageContext? capturedContext = null;

        var middleware = CreateMiddleware(DispatchMiddlewareStage.PreProcessing,
            async (msg, ctx, next, ct) =>
            {
                capturedMessage = msg;
                capturedContext = ctx;
                return await next(msg, ctx, ct).ConfigureAwait(false);
            });

        var pipeline = new DispatchPipeline(new[] { middleware });
        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext();

        await pipeline.ExecuteAsync(message, context,
            (_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success()),
            CancellationToken.None);

        capturedMessage.ShouldBe(message);
        capturedContext.ShouldBe(context);
    }

    [Fact]
    public async Task ThrowWhenMessageIsNull()
    {
        var pipeline = new DispatchPipeline(Enumerable.Empty<IDispatchMiddleware>());

        await Should.ThrowAsync<ArgumentNullException>(
            () => pipeline.ExecuteAsync(
                null!,
                new MessageContext(),
                (_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success()),
                CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task ThrowWhenContextIsNull()
    {
        var pipeline = new DispatchPipeline(Enumerable.Empty<IDispatchMiddleware>());
        var message = A.Fake<IDispatchMessage>();

        await Should.ThrowAsync<ArgumentNullException>(
            () => pipeline.ExecuteAsync(
                message,
                null!,
                (_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success()),
                CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task ThrowWhenFinalDelegateIsNull()
    {
        var pipeline = new DispatchPipeline(Enumerable.Empty<IDispatchMiddleware>());
        var message = A.Fake<IDispatchMessage>();

        await Should.ThrowAsync<ArgumentNullException>(
            () => pipeline.ExecuteAsync(message, new MessageContext(), null!, CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task CacheFilteredMiddlewareByMessageType()
    {
        var callCount = 0;
        var middleware = CreateMiddleware(DispatchMiddlewareStage.PreProcessing,
            async (msg, ctx, next, ct) =>
            {
                callCount++;
                return await next(msg, ctx, ct).ConfigureAwait(false);
            });

        var pipeline = new DispatchPipeline(new[] { middleware });
        var message = A.Fake<IDispatchMessage>();

        // Dispatch same message type twice
        await pipeline.ExecuteAsync(message, new MessageContext(),
            (_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success()), CancellationToken.None);
        await pipeline.ExecuteAsync(message, new MessageContext(),
            (_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success()), CancellationToken.None);

        callCount.ShouldBe(2);
    }

    [Fact]
    public void ClearCacheSuccessfully()
    {
        var pipeline = new DispatchPipeline(Enumerable.Empty<IDispatchMiddleware>());

        // Should not throw
        pipeline.ClearCache();
    }

    [Fact]
    public async Task ExecuteMultipleMiddlewareInCorrectOrder()
    {
        var order = new List<int>();

        var m1 = CreateMiddleware(DispatchMiddlewareStage.PreProcessing,
            async (msg, ctx, next, ct) => { order.Add(1); return await next(msg, ctx, ct).ConfigureAwait(false); });
        var m2 = CreateMiddleware(DispatchMiddlewareStage.Authentication,
            async (msg, ctx, next, ct) => { order.Add(2); return await next(msg, ctx, ct).ConfigureAwait(false); });
        var m3 = CreateMiddleware(DispatchMiddlewareStage.Validation,
            async (msg, ctx, next, ct) => { order.Add(3); return await next(msg, ctx, ct).ConfigureAwait(false); });
        var m4 = CreateMiddleware(DispatchMiddlewareStage.Processing,
            async (msg, ctx, next, ct) => { order.Add(4); return await next(msg, ctx, ct).ConfigureAwait(false); });

        var pipeline = new DispatchPipeline(new[] { m4, m2, m1, m3 });
        var message = A.Fake<IDispatchMessage>();

        await pipeline.ExecuteAsync(message, new MessageContext(),
            (_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success()), CancellationToken.None);

        order.ShouldBe([1, 2, 3, 4]);
    }
}
