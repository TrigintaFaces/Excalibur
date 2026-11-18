// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Middleware.Resilience;
using Excalibur.Dispatch.Options.Resilience;
using Excalibur.Dispatch.Telemetry;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

[Trait("Category", "Unit")]
[Trait("Component", "Dispatch.Core")]
[Trait("Pattern", "Regression")]
public sealed class RetryMiddlewareUnknownFailureShould
{
    [Fact]
    public async Task PropagateAnUnclassifiedBusinessFailureAfterOneAttemptWithoutLosingItsIdentity()
    {
        var calls = 0;
        var fault = new UnclassifiedBusinessException("unclassified controlled business fault");
        var caught = await Should.ThrowAsync<UnclassifiedBusinessException>(() => CreateMiddleware().InvokeAsync(
            new ProbeAction(), new MessageContext(), (_, _, _) =>
            {
                calls++;
                return ValueTask.FromException<IMessageResult>(fault);
            }, TestContext.Current.CancellationToken).AsTask());

        caught.ShouldBeSameAs(fault);
        calls.ShouldBe(1, "a failure with no known retryability must not become transient by default");
    }

    [Fact]
    public async Task RecoverFromAKnownTransientTimeoutOnTheSecondAttempt()
    {
        var calls = 0;
        var fault = new TimeoutException("controlled transient timeout");
        var result = await CreateMiddleware().InvokeAsync(new ProbeAction(), new MessageContext(), (_, _, _) =>
        {
            calls++;
            return calls == 1
                ? ValueTask.FromException<IMessageResult>(fault)
                : ValueTask.FromResult<IMessageResult>(MessageResult.Success());
        }, TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeTrue();
        calls.ShouldBe(2, "declining unknown failures must preserve known transient recovery");
    }

    private static RetryMiddleware CreateMiddleware() => new(
        global::Microsoft.Extensions.Options.Options.Create(new RetryOptions
        {
            MaxRetryAttempts = 2,
            BaseDelay = TimeSpan.Zero,
            MaxDelay = TimeSpan.Zero,
            UseJitter = false,
        }),
        A.Fake<ITelemetrySanitizer>(),
        NullLogger<RetryMiddleware>.Instance);

    private sealed record ProbeAction : IDispatchAction;
    private sealed class UnclassifiedBusinessException(string message) : Exception(message);
}
