// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Extensions;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Observability.Diagnostics;

using Microsoft.Extensions.Logging;

using OpenTelemetry;
using OpenTelemetry.Context.Propagation;

using B3Propagator = OpenTelemetry.Extensions.Propagators.B3Propagator;

namespace Excalibur.Dispatch.Observability.Http;

/// <summary>
/// Producer-side middleware that injects the B3 trace-context headers (<c>b3</c> single-header
/// form / <c>x-b3-*</c> multi-header form) onto the outgoing message context so the
/// transport-serialized envelope carries them and the distributed trace continues
/// producer → consumer for consumers that speak the B3 propagation format. Symmetric with the
/// W3C injection performed by <see cref="W3CTraceContextInjectionMiddleware"/>.
/// </summary>
/// <remarks>
/// <para>
/// B3 is not expressible via the BCL W3C propagator, so injection uses the OpenTelemetry
/// <see cref="B3Propagator"/> — the trace context is never hand-formatted. The injected value is
/// derived from the enqueue-time trace context captured on the message context
/// (<see cref="TraceContextExtensions.GetTraceParentOrCurrent"/>); when no enqueue-time context is stored,
/// the ambient <see cref="Activity.Current"/> is used. When a caller has already set a B3 header
/// (explicit override), that value is preserved.
/// </para>
/// <para>
/// Propagation is optional cross-cutting infrastructure and <strong>fails open</strong>:
/// any failure while injecting is logged and skipped, never breaking the send.
/// </para>
/// </remarks>
[AppliesTo(MessageKinds.All)]
internal sealed partial class B3TraceContextInjectionMiddleware : IDispatchMiddleware
{
    private static readonly TextMapPropagator Propagator = new B3Propagator();

    // B3 carrier keys, matching the exact casing the B3 propagator emits.
    private const string B3SingleHeaderKey = "b3";
    private const string B3TraceIdHeaderKey = "x-b3-traceid";
    private const string TracestateKey = "tracestate";

    private readonly ILogger<B3TraceContextInjectionMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="B3TraceContextInjectionMiddleware"/> class.
    /// </summary>
    /// <param name="logger"> Logger for fail-open diagnostics. </param>
    public B3TraceContextInjectionMiddleware(ILogger<B3TraceContextInjectionMiddleware> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc />
    public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Serialization;

    /// <inheritdoc />
    public async ValueTask<IMessageResult> InvokeAsync(
        IDispatchMessage message,
        IMessageContext context,
        DispatchRequestDelegate nextDelegate,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(nextDelegate);

        Inject(context);

        return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types",
        Justification = "Fail-open propagation: an injection failure must never break the send (Microsoft skip-pattern).")]
    private void Inject(IMessageContext context)
    {
        // Explicit caller-set B3 header wins (do not overwrite an override).
        if (HasExplicitB3Header(context))
        {
            return;
        }

        var propagationContext = ResolvePropagationContext(context);
        if (propagationContext.ActivityContext == default)
        {
            return;
        }

        try
        {
            Propagator.Inject(propagationContext, context, static (carrier, key, value) =>
            {
                if (carrier is null || string.IsNullOrEmpty(value))
                {
                    return;
                }

                carrier.SetItem(key, value);
            });
        }
        catch (Exception ex)
        {
            // Fail-open: trace propagation is optional; never break the send.
            LogInjectionFailed(ex);
        }
    }

    private static bool HasExplicitB3Header(IMessageContext context) =>
        context.GetItem<string>(B3SingleHeaderKey) is { Length: > 0 }
        || context.GetItem<string>(B3TraceIdHeaderKey) is { Length: > 0 };

    /// <summary>
    /// Derives the propagation context from the enqueue-time trace state stored on the message
    /// context, falling back to the ambient activity when no stored context is available.
    /// </summary>
    private static PropagationContext ResolvePropagationContext(IMessageContext context)
    {
        // Deferred-publish: prefer the enqueue-time captured trace context stored on the message
        // context (its W3C traceparent) over Activity.Current, which at flush time may belong to an
        // unrelated ambient operation.
        var traceParent = context.GetTraceParent();
        if (!string.IsNullOrEmpty(traceParent))
        {
            var traceState = context.GetItem<string>(TracestateKey);
            if (ActivityContext.TryParse(traceParent, traceState, out var parsed))
            {
                return new PropagationContext(parsed, Baggage.Current);
            }
        }

        var activity = Activity.Current;
        return activity is not null
            ? new PropagationContext(activity.Context, Baggage.Current)
            : default;
    }

    [LoggerMessage(
        EventId = ObservabilityEventId.B3TraceContextInjectionFailed,
        Level = LogLevel.Debug,
        Message = "B3 trace-context injection skipped due to a propagation error; the send continues unaffected.")]
    private partial void LogInjectionFailed(Exception exception);
}
