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

        // A retryable exception exhausted to the cap propagates the ORIGINAL exception. Retry decides how
        // many attempts a fault gets; it does not decide what the fault is, so it does not restate it as a
        // result of its own.
        var thrown = await Should.ThrowAsync<TimeoutException>(
            () => sut.InvokeAsync(
                message, context,
                (_, _, _) => throw new TimeoutException("always fails"),
                CancellationToken.None).AsTask());

        thrown.Message.ShouldBe("always fails");
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

        var callCount = 0;

        var thrown = await Should.ThrowAsync<ArgumentException>(
            () => sut.InvokeAsync(
                message, context,
                (_, _, _) =>
                {
                    callCount++;
                    throw new ArgumentException("bad arg");
                },
                CancellationToken.None).AsTask());

        thrown.Message.ShouldBe("bad arg");
        callCount.ShouldBe(1);
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

        var callCount = 0;

        var thrown = await Should.ThrowAsync<InvalidOperationException>(
            () => sut.InvokeAsync(
                message, context,
                (_, _, _) =>
                {
                    callCount++;
                    throw new InvalidOperationException("invalid");
                },
                CancellationToken.None).AsTask());

        thrown.Message.ShouldBe("invalid");
        callCount.ShouldBe(1);
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
    // These pin the BEHAVIORAL contract on both exhaustion paths, which leave the middleware by different
    // mechanisms and must each stay fail-loud. Exhaustion by repeated EXCEPTION rethrows the original, so
    // the consumer's exception type and message survive for their mapper and typed handler to match on.
    // Exhaustion by repeated FAILED RESULT returns the downstream's own result unchanged, because there is
    // no exception to raise and substituting one would discard what the pipeline below produced.
    //
    // They are NON-VACUOUS: each goes RED if an exhaustion path is mutated to swallow the failure, return
    // Success, or convert the fault into a retry-shaped result of the middleware's own.

    [Fact]
    public async Task ExhaustedRetries_ViaRetryableException_ReturnFailLoudTerminal_NeverSilentDrop()
    {
        // Arrange — a GENUINELY-retryable exception (NOT in the default NonRetryableExceptions floor) so every
        // attempt re-tries to the cap and the exception→exhaustion path actually fires.
        //
        // IMPORTANT (wjp8nb / a6d1ba1e8): the original form of this test allowlisted InvalidOperationException,
        // but that type is in the NonRetryableExceptions FLOOR, and the floor takes PRECEDENCE over the
        // allowlist (a TenantIsolationViolationException : InvalidOperationException must never be retried —
        // a tenant-isolation security invariant). So it was correctly rejected after one attempt and never
        // reached exhaustion; the assertion below was stale. The floor-precedence guarantee is proven by the
        // companion safety arm NonRetryableFloorException_AllowlistedButNeverRetried below — do NOT flip this
        // back to a floor exception (that would require weakening the floor, reopening the isolation hole).
        var options = new RetryOptions { MaxAttempts = 3, BaseDelay = TimeSpan.FromMilliseconds(1) };
        options.RetryableExceptions.Add(typeof(TransientTestException));
        var sut = CreateSut(options);
        var callCount = 0;

        // Act — a transient fault that never recovers across all attempts.
        var thrown = await Should.ThrowAsync<TransientTestException>(
            () => sut.InvokeAsync(
                A.Fake<IDispatchMessage>(), new MessageContext(),
                (_, _, _) =>
                {
                    callCount++;
                    throw new TransientTestException("transient — never recovers");
                },
                CancellationToken.None).AsTask());

        // Assert (LIVENESS) — fail-loud, never silent; the ORIGINAL exception reaches the caller with its
        // type and message intact, and every attempt was used.
        thrown.Message.ShouldBe("transient — never recovers");
        callCount.ShouldBe(options.MaxAttempts);
    }

    [Fact]
    public async Task NonRetryableFloorException_AllowlistedButNeverRetried_FailsLoudAfterOneAttempt()
    {
        // SECURITY SAFETY ARM (guards wjp8nb / a6d1ba1e8 floor-first precedence): a NonRetryableExceptions-floor
        // type — InvalidOperationException, the base of TenantIsolationViolationException — must NEVER be retried,
        // EVEN when a consumer explicitly allowlists it. The floor takes precedence over RetryableExceptions, so
        // retrying a permanent cross-tenant violation can never be enabled by configuration. This turns the
        // near-miss that produced the stale sibling above into a permanent structural guard: if a future change
        // let the allowlist override the floor, this test goes RED (callCount would climb to MaxAttempts).
        var options = new RetryOptions { MaxAttempts = 3, BaseDelay = TimeSpan.FromMilliseconds(1) };
        options.RetryableExceptions.Add(typeof(InvalidOperationException)); // allowlist attempt — the floor must override it
        var sut = CreateSut(options);
        var callCount = 0;

        // Act
        var thrown = await Should.ThrowAsync<InvalidOperationException>(
            () => sut.InvokeAsync(
                A.Fake<IDispatchMessage>(), new MessageContext(),
                (_, _, _) =>
                {
                    callCount++;
                    throw new InvalidOperationException("non-retryable floor exception — must not be retried");
                },
                CancellationToken.None).AsTask());

        // Assert (SAFETY) — declined after ONE attempt; still fail-loud (no silent drop), and the caller sees
        // the original exception rather than a retry-shaped substitute.
        thrown.Message.ShouldBe("non-retryable floor exception — must not be retried");
        callCount.ShouldBe(1); // the floor holds — never retried, even though allowlisted
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

        // Assert — exhaustion never flips a persistent failure to success, and it returns the DOWNSTREAM's
        // own failure rather than substituting a retry-shaped one. There is no exception here to raise, so
        // the result the pipeline below produced is what the caller should see.
        result.Succeeded.ShouldBeFalse();
        result.ProblemDetails.ShouldNotBeNull();
        result.ProblemDetails!.Type.ShouldBe("Error");
        result.ProblemDetails!.Detail.ShouldBe("transient — never recovers");
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
        var callCount = 0;

        var thrown = await Should.ThrowAsync<IOException>(
            () => sut.InvokeAsync(
                message, context,
                (_, _, _) =>
                {
                    callCount++;
                    throw new IOException("io error");
                },
                CancellationToken.None).AsTask());

        thrown.Message.ShouldBe("io error");
        callCount.ShouldBe(1);
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

        var callCount = 0;

        var thrown = await Should.ThrowAsync<TimeoutException>(
            () => sut.InvokeAsync(
                message, context,
                (_, _, _) =>
                {
                    callCount++;
                    throw new TimeoutException("configured as non-retryable");
                },
                CancellationToken.None).AsTask());

        thrown.Message.ShouldBe("configured as non-retryable");
        callCount.ShouldBe(1);
    }

    // A genuinely-retryable test exception: intentionally NOT a subtype of any default NonRetryableExceptions
    // floor type (InvalidOperationException, etc.), so allowlisting it actually makes it retryable to exhaustion.
    // Used to exercise the exception→RetryExhausted path without touching the security floor.
    private sealed class TransientTestException : Exception
    {
        public TransientTestException(string message) : base(message) { }
    }
}
