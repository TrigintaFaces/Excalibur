// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Observability.Http;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Observability.Tests.Http;

/// <summary>
/// Regression locks for <see cref="W3CTraceContextInjectionMiddleware"/> (producer-side send-stage
/// W3C trace-context injection). Bound to committed impl <c>c2a5420be</c> (bd-fa303y).
/// </summary>
/// <remarks>
/// The middleware injects the ambient <see cref="Activity.Current"/> W3C <c>traceparent</c> onto the
/// outgoing context via the BCL <see cref="DistributedContextPropagator"/> so the transport envelope
/// carries it; a caller-set <c>traceparent</c> is preserved (never overwritten); and injection is
/// fail-open — a failure never breaks the send.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
public sealed class W3CTraceContextInjectionMiddlewareShould
{
    private readonly W3CTraceContextInjectionMiddleware _sut =
        new(NullLogger<W3CTraceContextInjectionMiddleware>.Instance);

    /// <summary>
    /// Creates a fake <see cref="IMessageContext"/> backed by real Items/Features dictionaries so the
    /// identity-feature and item extension methods operate on real state.
    /// </summary>
    private static IMessageContext CreateFakeContext(Dictionary<string, object>? items = null)
    {
        var context = A.Fake<IMessageContext>();
        A.CallTo(() => context.Items).Returns(items ?? new Dictionary<string, object>(StringComparer.Ordinal));
        A.CallTo(() => context.Features).Returns(new Dictionary<Type, object>());
        return context;
    }

    private static DispatchRequestDelegate CapturingNext(Action onInvoked) =>
        (m, c, ct) =>
        {
            onInvoked();
            return new ValueTask<IMessageResult>(A.Fake<IMessageResult>());
        };

    /// <summary>
    /// Starts a sampled ambient activity so <see cref="Activity.Current"/> carries a real W3C traceparent.
    /// </summary>
    private static (ActivityListener Listener, ActivitySource Source, Activity Activity) StartAmbientActivity()
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);
        var source = new ActivitySource("W3CTraceContextInjectionMiddlewareShould");
        var activity = source.StartActivity("send");
        activity.ShouldNotBeNull("test harness must establish an ambient activity");
        return (listener, source, activity!);
    }

    [Fact]
    public void HaveSerializationStage()
    {
        _sut.Stage.ShouldBe(DispatchMiddlewareStage.Serialization);
    }

    [Fact]
    public async Task InjectAmbientTraceParent_WhenNoCallerSetValue()
    {
        // Arrange
        var (listener, source, activity) = StartAmbientActivity();
        using (listener)
        using (source)
        using (activity)
        {
            var message = A.Fake<IDispatchMessage>();
            var context = CreateFakeContext();
            var nextCalled = false;

            // Act
            await _sut.InvokeAsync(message, context, CapturingNext(() => nextCalled = true), CancellationToken.None);

            // Assert — the BCL-injected traceparent (matching the ambient activity) is set on the context.
            nextCalled.ShouldBeTrue();
            context.GetTraceParent().ShouldNotBeNullOrEmpty();
            context.GetTraceParent().ShouldBe(activity.Id);
        }
    }

    [Fact]
    public async Task PreserveCallerSetTraceParent_WhenAlreadyPresent()
    {
        // Arrange — caller explicitly set a traceparent; the middleware must NOT overwrite it.
        const string CallerValue = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";
        var (listener, source, activity) = StartAmbientActivity();
        using (listener)
        using (source)
        using (activity)
        {
            var message = A.Fake<IDispatchMessage>();
            var context = CreateFakeContext();
            context.GetOrCreateIdentityFeature().TraceParent = CallerValue;

            // Act
            await _sut.InvokeAsync(message, context, CapturingNext(() => { }), CancellationToken.None);

            // Assert — caller value preserved (not overwritten by the ambient activity).
            context.GetTraceParent().ShouldBe(CallerValue);
            context.GetTraceParent().ShouldNotBe(activity.Id);
        }
    }

    [Fact]
    public async Task NotInject_AndStillSend_WhenNoAmbientActivity()
    {
        // Arrange — no ambient activity established.
        Activity.Current.ShouldBeNull("precondition: no ambient activity for this case");
        var message = A.Fake<IDispatchMessage>();
        var context = CreateFakeContext();
        var nextCalled = false;

        // Act
        await _sut.InvokeAsync(message, context, CapturingNext(() => nextCalled = true), CancellationToken.None);

        // Assert — nothing injected, send unaffected.
        nextCalled.ShouldBeTrue();
        context.GetTraceParent().ShouldBeNull();
    }

    [Fact]
    public async Task Throw_WhenMessageIsNull()
    {
        var context = CreateFakeContext();
        await Should.ThrowAsync<ArgumentNullException>(
            () => _sut.InvokeAsync(null!, context, CapturingNext(() => { }), CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task Throw_WhenContextIsNull()
    {
        var message = A.Fake<IDispatchMessage>();
        await Should.ThrowAsync<ArgumentNullException>(
            () => _sut.InvokeAsync(message, null!, CapturingNext(() => { }), CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task Throw_WhenNextDelegateIsNull()
    {
        var message = A.Fake<IDispatchMessage>();
        var context = CreateFakeContext();
        await Should.ThrowAsync<ArgumentNullException>(
            () => _sut.InvokeAsync(message, context, null!, CancellationToken.None).AsTask());
    }
}
