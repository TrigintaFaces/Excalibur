// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Observability.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Observability.Http;

/// <summary>
/// Producer-side middleware that injects the ambient W3C trace context
/// (<c>traceparent</c>/<c>tracestate</c>) onto the outgoing message context so the
/// transport-serialized envelope carries it and the distributed trace continues
/// producer → consumer. Symmetric with the inbound <see cref="W3CTraceContextMiddleware"/>.
/// </summary>
/// <remarks>
/// <para>
/// The injection mechanism is the BCL <see cref="DistributedContextPropagator"/> (W3C
/// propagator) — the trace context is never hand-formatted. The injected value is taken
/// from <see cref="Activity.Current"/>; when a caller has already set the context
/// <c>traceparent</c> (explicit override), that value is preserved.
/// </para>
/// <para>
/// Propagation is optional cross-cutting infrastructure and <strong>fails open</strong>:
/// any failure while injecting is logged and skipped, never breaking the send.
/// </para>
/// </remarks>
[AppliesTo(MessageKinds.All)]
internal sealed partial class W3CTraceContextInjectionMiddleware : IDispatchMiddleware
{
    private static readonly DistributedContextPropagator Propagator =
        DistributedContextPropagator.CreateW3CPropagator();

    private const string TraceparentKey = "traceparent";
    private const string TracestateKey = "tracestate";

    private readonly ILogger<W3CTraceContextInjectionMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="W3CTraceContextInjectionMiddleware"/> class.
    /// </summary>
    /// <param name="logger"> Logger for fail-open diagnostics. </param>
    public W3CTraceContextInjectionMiddleware(ILogger<W3CTraceContextInjectionMiddleware> logger)
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
        // Explicit caller-set traceparent wins (do not overwrite an override).
        if (context.GetTraceParent() is { Length: > 0 })
        {
            return;
        }

        var activity = Activity.Current;
        if (activity is null)
        {
            return;
        }

        try
        {
            Propagator.Inject(activity, context, static (carrier, key, value) =>
            {
                if (carrier is not IMessageContext ctx || string.IsNullOrEmpty(value))
                {
                    return;
                }

                if (string.Equals(key, TraceparentKey, StringComparison.OrdinalIgnoreCase))
                {
                    ctx.GetOrCreateIdentityFeature().TraceParent = value;
                }
                else if (string.Equals(key, TracestateKey, StringComparison.OrdinalIgnoreCase))
                {
                    ctx.SetItem(TracestateKey, value);
                }
            });
        }
        catch (Exception ex)
        {
            // Fail-open: trace propagation is optional; never break the send.
            LogInjectionFailed(ex);
        }
    }

    [LoggerMessage(
        EventId = ObservabilityEventId.W3CTraceContextInjectionFailed,
        Level = LogLevel.Debug,
        Message = "W3C trace-context injection skipped due to a propagation error; the send continues unaffected.")]
    private partial void LogInjectionFailed(Exception exception);
}
