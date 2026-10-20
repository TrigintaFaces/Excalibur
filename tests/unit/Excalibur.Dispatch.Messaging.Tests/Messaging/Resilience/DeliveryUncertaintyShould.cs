// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Exceptions;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Middleware.Resilience;
using Excalibur.Dispatch.Options.Resilience;
using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Telemetry;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Messaging.Resilience;

[Trait("Category", "Unit")]
[Trait("Component", "Dispatch.Core")]
[Trait("Pattern", "Regression")]
public sealed class DeliveryUncertaintyShould
{
    [Theory]
    [InlineData(0, 3)]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    public async Task ApplyBoundedPolicyWithoutOverridingExplicitFilters(int policy, int expected)
    {
        var options = new RetryOptions { MaxRetryAttempts = 2, BaseDelay = TimeSpan.Zero, MaxDelay = TimeSpan.FromMilliseconds(1), UseJitter = false };
        if (policy == 1)
        {
            options.NonRetryableExceptions.Add(typeof(DispatchException));
            options.RetryableExceptions.Add(typeof(DispatchDeliveryUncertainException));
        }
        if (policy == 2) options.RetryableExceptions.Add(typeof(IOException));
        var middleware = new RetryMiddleware(Microsoft.Extensions.Options.Options.Create(options), NullTelemetrySanitizer.Instance,
            NullLogger<RetryMiddleware>.Instance);
        var message = A.Fake<IDispatchEvent>();
        var context = new MessageContext();
        var calls = 0;
        var fault = new DispatchDeliveryUncertainException("Acceptance unestablished.");
        try
        {
            await middleware.InvokeAsync(message, context, (actualMessage, actualContext, _) =>
            {
                actualMessage.ShouldBeSameAs(message);
                actualContext.ShouldBeSameAs(context);
                calls++;
                throw fault;
            }, TestContext.Current.CancellationToken).ConfigureAwait(false);
        }
        catch (DispatchDeliveryUncertainException exception) { exception.ShouldBeSameAs(fault); }
        calls.ShouldBe(expected);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreserveUncertaintyEvidenceIndependentlyOfReplayPermission(bool retryable)
    {
        using var owner = RetryOwnershipScope.Enter(A.Fake<IDispatchEvent>(), new MessageContext());
        var attempt = owner.BeginAttempt();
        var result = MessageResult.Failed(new MessageProblemDetails { Title = "Mixed fanout", Status = 500 });
        var uncertain = new DispatchDeliveryUncertainException("Acceptance unestablished.");
        var aggregate = new AggregateException(uncertain, new ConfigurationException("Rejected destination."));
        RetryOwnershipScope.ReportFanout(attempt, result, retryable, MessageFailureKind.Uncertain, aggregate);
        RetryOwnershipScope.AllowsRetryAfterResult(attempt, result).ShouldBe(retryable);
        RetryOwnershipScope.TryGetKnownFailureKind(attempt, result, out var kind, out var original).ShouldBeTrue();
        kind.ShouldBe(MessageFailureKind.Uncertain);
        original.ShouldBeSameAs(aggregate);
    }

    [Fact]
    public async Task RetryUncertainDestinationOnlyOnTheNextOwnedAttempt()
    {
        var transportFault = new IOException("Original transport evidence.");
        var fault = new DispatchDeliveryUncertainException("Acceptance unestablished.", transportFault);
        new DefaultMessageFailureClassifier().Classify(fault).ShouldBe(MessageFailureKind.Uncertain);
        fault.InnerException.ShouldBeSameAs(transportFault);
        using var owner = RetryOwnershipScope.EnterClassified(A.Fake<IDispatchEvent>(), new MessageContext(), new DefaultMessageFailureClassifier());
        var attempt = owner.BeginAttempt();
        var calls = 0;
        Task Publish() { calls++; return Task.FromException(fault); }
        var first = await owner.PublishDestinationAsync(attempt, "destination", Publish, TestContext.Current.CancellationToken).ConfigureAwait(false);
        var same = await owner.PublishDestinationAsync(attempt, "destination", Publish, TestContext.Current.CancellationToken).ConfigureAwait(false);
        first.ShouldBeSameAs(same);
        first.Retryable.ShouldBeTrue();
        first.FailureKind.ShouldBe(MessageFailureKind.Uncertain);
        calls.ShouldBe(1);
        await owner.PublishDestinationAsync(owner.BeginAttempt(), "destination", Publish, TestContext.Current.CancellationToken).ConfigureAwait(false);
        calls.ShouldBe(2);
    }

    [Fact]
    public async Task HonorCallerCancellationBeforeAnyPublication()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync().ConfigureAwait(false);
        using var owner = RetryOwnershipScope.EnterClassified(A.Fake<IDispatchEvent>(), new MessageContext(), new DefaultMessageFailureClassifier());
        var calls = 0;
        await Should.ThrowAsync<OperationCanceledException>(() => owner.PublishDestinationAsync(owner.BeginAttempt(), "destination",
            () => { calls++; return Task.CompletedTask; }, cancellation.Token)).ConfigureAwait(false);
        calls.ShouldBe(0);
    }
}
