// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // Use ValueTasks correctly - FakeItEasy .Returns() stores ValueTask

using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Telemetry;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Middleware;
using Excalibur.Dispatch.Middleware.Resilience;
using Excalibur.Dispatch.Options.Resilience;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using MessageResult = Excalibur.Dispatch.MessageResult;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Dispatch.Core")]
public sealed class CircuitBreakerMiddlewareShould
{

    // A registry per middleware, deliberately. GetOrCreate honours the options it is given only
    // when it FIRST creates a circuit for that key, so a shared registry would hand every test the
    // thresholds of whichever test ran first.
    private static ITransportCircuitBreakerRegistry NewRegistry() => new TransportCircuitBreakerRegistry();

    private static readonly ITelemetrySanitizer Sanitizer = A.Fake<ITelemetrySanitizer>();

    private static CircuitBreakerMiddleware CreateSut(CircuitBreakerOptions? options = null)
    {
        var opts = options ?? new CircuitBreakerOptions();
        A.CallTo(() => Sanitizer.SanitizeTag(A<string>._, A<string?>._))
            .ReturnsLazily(call => call.GetArgument<string?>(1));
        return new CircuitBreakerMiddleware(
            Microsoft.Extensions.Options.Options.Create(opts),
            NewRegistry(),
            Sanitizer,
            NullLogger<CircuitBreakerMiddleware>.Instance);
    }

    [Fact]
    public async Task PassThroughWhenCircuitIsClosed()
    {
        var sut = CreateSut();
        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext();
        var expectedResult = MessageResult.Success();

        var result = await sut.InvokeAsync(
            message, context,
            (_, _, _) => new ValueTask<IMessageResult>(expectedResult),
            CancellationToken.None);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task RecordFailureOnException_AndRethrowTheOriginal()
    {
        var sut = CreateSut(new CircuitBreakerOptions { FailureThreshold = 5 });
        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext();

        // The breaker observes the fault and lets it through; the recording is proven by
        // OpenCircuitAfterFailureThresholdExceeded below, which only opens if these were counted.
        var thrown = await Should.ThrowAsync<InvalidOperationException>(
            () => sut.InvokeAsync(
                message, context,
                (_, _, _) => throw new InvalidOperationException("test failure"),
                CancellationToken.None).AsTask());

        thrown.Message.ShouldBe("test failure");
    }

    [Fact]
    public async Task OpenCircuitAfterFailureThresholdExceeded()
    {
        var options = new CircuitBreakerOptions
        {
            FailureThreshold = 2,
            OpenDuration = TimeSpan.FromSeconds(60)
        };
        var sut = CreateSut(options);
        var message = A.Fake<IDispatchMessage>();

        // Reach the threshold. Each fault propagates and each one is still recorded; the call AFTER the
        // threshold is the one the open circuit rejects, and it is asserted below rather than in this loop.
        for (var i = 0; i < 2; i++)
        {
            _ = await Should.ThrowAsync<InvalidOperationException>(
                () => sut.InvokeAsync(
                    message, new MessageContext(),
                    (_, _, _) => throw new InvalidOperationException("fail"),
                    CancellationToken.None).AsTask());
        }

        // The circuit should now be open - next request should be rejected
        var result = await sut.InvokeAsync(
            message, new MessageContext(),
            (_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success()),
            CancellationToken.None);

        result.Succeeded.ShouldBeFalse();
        result.ProblemDetails!.Type.ShouldBe("CircuitBreakerOpen");
    }

    [Fact]
    public async Task RecordSuccessOnSuccessfulResult()
    {
        var sut = CreateSut();
        var message = A.Fake<IDispatchMessage>();

        var result = await sut.InvokeAsync(
            message, new MessageContext(),
            (_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success()),
            CancellationToken.None);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void HaveErrorHandlingStage()
    {
        var sut = CreateSut();
        sut.Stage.ShouldBe(DispatchMiddlewareStage.ErrorHandling);
    }

    [Fact]
    public async Task UseCircuitKeySelectorWhenProvided()
    {
        var options = new CircuitBreakerOptions
        {
            CircuitKeySelector = _ => "custom-key"
        };
        var sut = CreateSut(options);
        var message = A.Fake<IDispatchMessage>();

        var result = await sut.InvokeAsync(
            message, new MessageContext(),
            (_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success()),
            CancellationToken.None);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task RecordFailureOnFailedResult()
    {
        var sut = CreateSut();
        var message = A.Fake<IDispatchMessage>();
        var failedResult = MessageResult.Failed(new MessageProblemDetails
        {
            Type = "Error", Title = "Error", ErrorCode = 500, Status = 500, Detail = "Failed"
        });

        var result = await sut.InvokeAsync(
            message, new MessageContext(),
            (_, _, _) => new ValueTask<IMessageResult>(failedResult),
            CancellationToken.None);

        result.Succeeded.ShouldBeFalse();
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
}
