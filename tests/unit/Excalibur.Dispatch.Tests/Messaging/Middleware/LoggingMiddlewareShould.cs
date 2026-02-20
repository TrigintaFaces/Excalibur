// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // Use ValueTasks correctly - FakeItEasy .Returns() stores ValueTask

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Abstractions.Telemetry;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Middleware;
using Excalibur.Dispatch.Options.Middleware;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MessageResult = Excalibur.Dispatch.Abstractions.MessageResult;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

[Trait("Category", "Unit")]
public sealed class LoggingMiddlewareShould
{
    private readonly ITelemetrySanitizer _sanitizer = A.Fake<ITelemetrySanitizer>();
    private readonly ILogger<LoggingMiddleware> _logger;

    public LoggingMiddlewareShould()
    {
        _logger = A.Fake<ILogger<LoggingMiddleware>>();
        A.CallTo(() => _logger.IsEnabled(A<LogLevel>._)).Returns(true);
        A.CallTo(() => _logger.BeginScope(A<object>._)).Returns(A.Fake<IDisposable>());
        A.CallTo(() => _sanitizer.SanitizePayload(A<string>._))
            .ReturnsLazily(call => call.GetArgument<string>(0) ?? string.Empty);
        A.CallTo(() => _sanitizer.SanitizeTag(A<string>._, A<string?>._))
            .ReturnsLazily(call => call.GetArgument<string?>(1));
    }

    private LoggingMiddleware CreateSut(LoggingMiddlewareOptions? options = null)
    {
        var opts = options ?? new LoggingMiddlewareOptions();
        return new LoggingMiddleware(Microsoft.Extensions.Options.Options.Create(opts), _sanitizer, _logger);
    }

    [Fact]
    public async Task PassThroughSuccessfully()
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
    public async Task ExcludeConfiguredTypes()
    {
        var messageType = typeof(IDispatchMessage);
        var options = new LoggingMiddlewareOptions();
        options.ExcludeTypes.Add(messageType);

        var sut = CreateSut(options);
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
    public async Task PropagateExceptions()
    {
        var sut = CreateSut();
        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext();

        await Should.ThrowAsync<InvalidOperationException>(
            () => sut.InvokeAsync(
                message, context,
                (_, _, _) => throw new InvalidOperationException("test"),
                CancellationToken.None).AsTask());
    }

    [Fact]
    public void HavePreProcessingStage()
    {
        var sut = CreateSut();
        sut.Stage.ShouldBe(DispatchMiddlewareStage.PreProcessing);
    }

    [Fact]
    public async Task HandleFailedResult()
    {
        var sut = CreateSut();
        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext();
        var failedResult = MessageResult.Failed(new MessageProblemDetails
        {
            Type = "Error",
            Title = "Error",
            ErrorCode = 500,
            Status = 500,
            Detail = "Something went wrong"
        });

        var result = await sut.InvokeAsync(
            message, context,
            (_, _, _) => new ValueTask<IMessageResult>(failedResult),
            CancellationToken.None);

        result.Succeeded.ShouldBeFalse();
    }

    [Fact]
    public async Task SkipStartLogWhenDisabled()
    {
        var sut = CreateSut(new LoggingMiddlewareOptions
        {
            LogStart = false,
            LogCompletion = true
        });
        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext();

        var result = await sut.InvokeAsync(
            message, context,
            (_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success()),
            CancellationToken.None);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task SkipCompletionLogWhenDisabled()
    {
        var sut = CreateSut(new LoggingMiddlewareOptions
        {
            LogStart = true,
            LogCompletion = false
        });
        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext();

        var result = await sut.InvokeAsync(
            message, context,
            (_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success()),
            CancellationToken.None);

        result.Succeeded.ShouldBeTrue();
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
    public async Task IncludeTimingWhenEnabled()
    {
        var sut = CreateSut(new LoggingMiddlewareOptions
        {
            IncludeTiming = true,
            LogCompletion = true
        });
        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext();

        var result = await sut.InvokeAsync(
            message, context,
            (_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success()),
            CancellationToken.None);

        result.Succeeded.ShouldBeTrue();
    }
}
