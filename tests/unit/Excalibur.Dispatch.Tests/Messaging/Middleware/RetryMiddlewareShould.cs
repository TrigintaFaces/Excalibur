// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // Use ValueTasks correctly - FakeItEasy .Returns() stores ValueTask

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Telemetry;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Middleware;
using Excalibur.Dispatch.Middleware.Resilience;
using Excalibur.Dispatch.Options.Resilience;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MessageResult = Excalibur.Dispatch.MessageResult;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Dispatch.Core")]
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
        // jj9gon/qu3182 (S852) F-5 flip: a retryable exception (TimeoutException → classifier Transient)
        // exhausted to the cap now converges on the distinct, reachable RetryExhausted terminal — it no
        // longer falls through to the generic "RetryError". STRENGTHENED to assert the new contract.
        result.ProblemDetails!.Type.ShouldBe("RetryExhausted");
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

    // ---------------------------------------------------------------------------------------------
    // 7u16zu (AC-4) — in-process leg of the cross-path terminal-failure contract: when in-process
    // retries are EXHAUSTED, RetryMiddleware surfaces a fail-loud TERMINAL failure to the caller —
    // never a silent drop / flip-to-success. (The durable legs — Outbox/Inbox DLQ + terminal status
    // — are regression-locked in their own subsystem tests; this guards the in-process leg.)
    //
    // These pin the BEHAVIORAL contract (Succeeded == false after genuine attempt-cap exhaustion) AND,
    // since the jj9gon/qu3182 (S852) restructure, the distinct terminal Type. They are NON-VACUOUS: each
    // goes RED if the exhaustion path is mutated to swallow the failure, return Success, or revert to the
    // generic "RetryError".
    //
    // jj9gon (S852, RetryMiddleware.cs:212-241): the `Type = "RetryExhausted"` terminal is now the SINGLE
    // REACHABLE exhaustion terminal — BOTH the retryable-exception path and the transient-failed-result
    // path break out of the loop and converge on it (it also emits dispatch.retry.exhausted exactly once,
    // fixing the qu3182 exception-path undercount). The old "currently unreachable / dead branch" note is
    // obsolete. So these locks now assert the reachable `RetryExhausted` Type on both code paths.

    [Fact]
    public async Task ExhaustedRetries_ViaRetryableException_ReturnFailLoudTerminal_NeverSilentDrop()
    {
        // Arrange — make the exception retryable so every attempt re-tries to the cap.
        var options = new RetryOptions { MaxAttempts = 3, BaseDelay = TimeSpan.FromMilliseconds(1) };
        options.RetryableExceptions.Add(typeof(InvalidOperationException));
        var sut = CreateSut(options);
        var callCount = 0;

        // Act — a transient fault that never recovers across all attempts.
        var result = await sut.InvokeAsync(
            A.Fake<IDispatchMessage>(), new MessageContext(),
            (_, _, _) =>
            {
                callCount++;
                throw new InvalidOperationException("transient — never recovers");
            },
            CancellationToken.None);

        // Assert — fail-loud terminal, never silent; genuine exhaustion (every attempt used).
        result.Succeeded.ShouldBeFalse();
        result.ProblemDetails.ShouldNotBeNull();
        result.ProblemDetails!.Type.ShouldBe("RetryExhausted"); // jj9gon: reachable distinct terminal (exception path)
        callCount.ShouldBe(options.MaxAttempts);
    }

    [Fact]
    public async Task ExhaustedRetries_ViaTransientFailedResult_ReturnFailLoudTerminal_NeverSilentDrop()
    {
        // Arrange — a transient (500) failed result is retried until the attempt cap.
        var sut = CreateSut(new RetryOptions { MaxAttempts = 3, BaseDelay = TimeSpan.FromMilliseconds(1) });
        var callCount = 0;

        // Act
        var result = await sut.InvokeAsync(
            A.Fake<IDispatchMessage>(), new MessageContext(),
            (_, _, _) =>
            {
                callCount++;
                return new ValueTask<IMessageResult>(MessageResult.Failed(new MessageProblemDetails
                {
                    Type = "Error",
                    Title = "Error",
                    ErrorCode = 500,
                    Status = 500,
                    Detail = "transient — never recovers",
                }));
            },
            CancellationToken.None);

        // Assert — exhaustion never flips a persistent failure to success.
        result.Succeeded.ShouldBeFalse();
        result.ProblemDetails.ShouldNotBeNull();
        result.ProblemDetails!.Type.ShouldBe("RetryExhausted"); // jj9gon: reachable distinct terminal (failed-result path)
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
