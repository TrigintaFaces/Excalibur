// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // Use ValueTasks correctly - FakeItEasy .Returns() stores ValueTask

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Abstractions.Telemetry;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Middleware;
using Excalibur.Dispatch.Options.Resilience;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using MessageResult = Excalibur.Dispatch.Abstractions.MessageResult;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

[Trait("Category", "Unit")]
public sealed class CircuitBreakerMiddlewareShould
{
    private static readonly ITelemetrySanitizer Sanitizer = A.Fake<ITelemetrySanitizer>();

    private static CircuitBreakerMiddleware CreateSut(CircuitBreakerOptions? options = null)
    {
        var opts = options ?? new CircuitBreakerOptions();
        A.CallTo(() => Sanitizer.SanitizeTag(A<string>._, A<string?>._))
            .ReturnsLazily(call => call.GetArgument<string?>(1));
        return new CircuitBreakerMiddleware(
            Microsoft.Extensions.Options.Options.Create(opts),
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
    public async Task RecordFailureOnException()
    {
        var sut = CreateSut(new CircuitBreakerOptions { FailureThreshold = 5 });
        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext();

        var result = await sut.InvokeAsync(
            message, context,
            (_, _, _) => throw new InvalidOperationException("test failure"),
            CancellationToken.None);

        result.Succeeded.ShouldBeFalse();
        result.ProblemDetails.ShouldNotBeNull();
        result.ProblemDetails!.Type.ShouldBe("CircuitBreakerFailure");
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

        // Cause failures to exceed threshold
        for (var i = 0; i < 3; i++)
        {
            await sut.InvokeAsync(
                message, new MessageContext(),
                (_, _, _) => throw new InvalidOperationException("fail"),
                CancellationToken.None);
        }

        // The circuit should now be open - next request should be rejected
        var result = await sut.InvokeAsync(
            message, new MessageContext(),
            (_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success()),
            CancellationToken.None);

        result.Succeeded.ShouldBeFalse();
        result.ProblemDetails!.Type.ShouldBeOneOf("CircuitBreakerOpen", "CircuitBreakerFailure");
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
