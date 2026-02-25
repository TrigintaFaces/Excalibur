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
using Microsoft.Extensions.Options;

using MessageResult = Excalibur.Dispatch.Abstractions.MessageResult;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

[Trait("Category", "Unit")]
public sealed class RetryMiddlewareShould
{
    private static readonly ITelemetrySanitizer Sanitizer = A.Fake<ITelemetrySanitizer>();
    private readonly ILogger<RetryMiddleware> _logger;

    public RetryMiddlewareShould()
    {
        _logger = A.Fake<ILogger<RetryMiddleware>>();
        A.CallTo(() => _logger.IsEnabled(A<LogLevel>._)).Returns(true);
        A.CallTo(() => _logger.BeginScope(A<object>._)).Returns(A.Fake<IDisposable>());
        A.CallTo(() => Sanitizer.SanitizeTag(A<string>._, A<string?>._))
            .ReturnsLazily(call => call.GetArgument<string?>(1));
    }

    private RetryMiddleware CreateSut(RetryOptions? options = null)
    {
        var opts = options ?? new RetryOptions { MaxAttempts = 3, BaseDelay = TimeSpan.FromMilliseconds(1) };
        return new RetryMiddleware(Microsoft.Extensions.Options.Options.Create(opts), Sanitizer, _logger);
    }

    [Fact]
    public async Task SucceedOnFirstAttempt()
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
    public async Task RetryOnTransientException()
    {
        var sut = CreateSut(new RetryOptions
        {
            MaxAttempts = 3,
            BaseDelay = TimeSpan.FromMilliseconds(1)
        });
        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext();
        var callCount = 0;

        var result = await sut.InvokeAsync(
            message, context,
            (_, _, _) =>
            {
                callCount++;
                if (callCount < 3)
                {
                    throw new TimeoutException("transient");
                }

                return new ValueTask<IMessageResult>(MessageResult.Success());
            },
            CancellationToken.None);

        result.Succeeded.ShouldBeTrue();
        callCount.ShouldBe(3);
    }

    [Fact]
    public async Task StopRetryingAfterMaxAttempts()
    {
        var sut = CreateSut(new RetryOptions
        {
            MaxAttempts = 2,
            BaseDelay = TimeSpan.FromMilliseconds(1)
        });
        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext();

        var result = await sut.InvokeAsync(
            message, context,
            (_, _, _) => throw new TimeoutException("always fails"),
            CancellationToken.None);

        result.Succeeded.ShouldBeFalse();
        result.ProblemDetails.ShouldNotBeNull();
        result.ProblemDetails!.Type.ShouldBe("RetryError");
    }

    [Fact]
    public async Task NotRetryArgumentException()
    {
        var sut = CreateSut(new RetryOptions
        {
            MaxAttempts = 3,
            BaseDelay = TimeSpan.FromMilliseconds(1)
        });
        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext();

        var result = await sut.InvokeAsync(
            message, context,
            (_, _, _) => throw new ArgumentException("bad arg"),
            CancellationToken.None);

        result.Succeeded.ShouldBeFalse();
        result.ProblemDetails!.Type.ShouldBe("RetryError");
    }

    [Fact]
    public async Task NotRetryInvalidOperationException()
    {
        var sut = CreateSut(new RetryOptions
        {
            MaxAttempts = 3,
            BaseDelay = TimeSpan.FromMilliseconds(1)
        });
        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext();

        var result = await sut.InvokeAsync(
            message, context,
            (_, _, _) => throw new InvalidOperationException("invalid"),
            CancellationToken.None);

        result.Succeeded.ShouldBeFalse();
        result.ProblemDetails!.Type.ShouldBe("RetryError");
    }

    [Fact]
    public async Task RetryOnFailedResult()
    {
        var sut = CreateSut(new RetryOptions
        {
            MaxAttempts = 3,
            BaseDelay = TimeSpan.FromMilliseconds(1)
        });
        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext();
        var callCount = 0;

        var result = await sut.InvokeAsync(
            message, context,
            (_, _, _) =>
            {
                callCount++;
                if (callCount < 3)
                {
                    return new ValueTask<IMessageResult>(MessageResult.Failed(new MessageProblemDetails
                    {
                        Type = "Error",
                        Title = "Error",
                        ErrorCode = 500,
                        Status = 500,
                        Detail = "transient error"
                    }));
                }

                return new ValueTask<IMessageResult>(MessageResult.Success());
            },
            CancellationToken.None);

        result.Succeeded.ShouldBeTrue();
        callCount.ShouldBe(3);
    }

    [Fact]
    public void HaveErrorHandlingStage()
    {
        var sut = CreateSut();
        sut.Stage.ShouldBe(DispatchMiddlewareStage.ErrorHandling);
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
    public async Task OnlyRetryConfiguredExceptions()
    {
        var options = new RetryOptions
        {
            MaxAttempts = 3,
            BaseDelay = TimeSpan.FromMilliseconds(1)
        };
        options.RetryableExceptions.Add(typeof(TimeoutException));

        var sut = CreateSut(options);
        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext();

        // IOException is not in the retryable list, so should not retry
        var result = await sut.InvokeAsync(
            message, context,
            (_, _, _) => throw new IOException("io error"),
            CancellationToken.None);

        result.Succeeded.ShouldBeFalse();
        result.ProblemDetails!.Type.ShouldBe("RetryError");
    }

    [Fact]
    public async Task RespectNonRetryableExceptions()
    {
        var options = new RetryOptions
        {
            MaxAttempts = 3,
            BaseDelay = TimeSpan.FromMilliseconds(1)
        };
        options.NonRetryableExceptions.Add(typeof(TimeoutException));

        var sut = CreateSut(options);
        var message = A.Fake<IDispatchMessage>();
        var context = new MessageContext();

        var result = await sut.InvokeAsync(
            message, context,
            (_, _, _) => throw new TimeoutException("configured as non-retryable"),
            CancellationToken.None);

        result.Succeeded.ShouldBeFalse();
        result.ProblemDetails!.Type.ShouldBe("RetryError");
    }
}
