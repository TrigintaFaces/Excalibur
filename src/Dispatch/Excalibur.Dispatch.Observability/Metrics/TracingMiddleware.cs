// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Extensions;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Telemetry;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Observability.Metrics;

/// <summary>
/// Middleware that provides distributed tracing for message processing using OpenTelemetry.
/// </summary>
/// <remarks>
/// <para>
/// This middleware creates spans for each message processed through the dispatch pipeline.
/// It uses the <see cref="DispatchActivitySource"/> to create activities that can be
/// exported to OpenTelemetry-compatible backends.
/// </para>
/// <para>
/// To enable tracing, add the Dispatch activity source to your OpenTelemetry configuration:
/// <code>
/// services.AddOpenTelemetry()
///     .WithTracing(tracing => tracing.AddSource("Excalibur.Dispatch"));
/// </code>
/// </para>
/// </remarks>
/// <param name="observabilityOptions">The observability options controlling detailed timing and sensitive data inclusion.</param>
/// <param name="sanitizer">The telemetry sanitizer for PII protection.</param>
[AppliesTo(MessageKinds.All)]
internal sealed class TracingMiddleware(IOptions<ObservabilityOptions> observabilityOptions, ITelemetrySanitizer sanitizer) : IDispatchMiddleware
{
	private readonly ObservabilityOptions _observabilityOptions = observabilityOptions?.Value ?? throw new ArgumentNullException(nameof(observabilityOptions));
	private readonly ITelemetrySanitizer _sanitizer = sanitizer ?? throw new ArgumentNullException(nameof(sanitizer));
	/// <inheritdoc />
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;

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

		var messageType = message.GetType();
		using var activity = StartDispatchActivity(messageType, context);

		if (activity is null)
		{
			// No listener registered, skip tracing overhead
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		// Set standard attributes
		_ = activity.SetTag("message.type", messageType.Name);
		_ = activity.SetTag("dispatch.operation", "handle");

		// Only include potentially sensitive identifiers when IncludeSensitiveData is true
		if (_observabilityOptions.IncludeSensitiveData)
		{
			_ = activity.SetTag("message.id", context.MessageId ?? string.Empty);
			_ = activity.SetTag("dispatch.correlation.id", context.CorrelationId ?? string.Empty);
		}

		// Add handler type if available
		if (context.GetItem<Type>("HandlerType") is { } handlerType)
		{
			_ = activity.SetTag("handler.type", handlerType.Name);
		}

		// Add message kind
		var messageKind = message switch
		{
			IDispatchAction => "Action",
			IDispatchEvent => "Event",
			IDispatchDocument => "Document",
			_ => "Message"
		};
		_ = activity.SetTag("message.kind", messageKind);

		try
		{
			var result = await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);

			if (result.IsSuccess)
			{
				_ = activity.SetStatus(ActivityStatusCode.Ok);
				_ = activity.SetTag("dispatch.status", "success");
			}
			else
			{
				_ = activity.SetStatus(ActivityStatusCode.Error, result.ProblemDetails?.Detail ?? "Message processing failed");
				_ = activity.SetTag("dispatch.status", "failed");

				if (result.ProblemDetails is { } pd)
				{
					_ = activity.SetTag("error.type", pd.Type ?? "unknown");
					_ = activity.SetTag("error.code", pd.ErrorCode);
				}
			}

			return result;
		}
		catch (Exception ex)
		{
			activity.SetSanitizedErrorStatus(ex, _sanitizer);
			_ = activity.SetTag("dispatch.status", "exception");
			throw;
		}
	}

	/// <summary>
	/// Starts the dispatch span (FR-A3, 3wlvav). When a W3C <c>traceparent</c> was restored from an inbound
	/// message (e.g. an outbox-published message round-tripped through staging → publish), the span continues
	/// that distributed trace as an <see cref="ActivityKind.Consumer"/> child — mirroring
	/// <c>W3CTraceContextMiddleware</c> — instead of starting a new trace root. Fail-open (FR-A5/EC-A2): an
	/// absent or malformed traceparent falls back to the default in-process <see cref="ActivityKind.Internal"/>
	/// span. Returns <see langword="null"/> when no listener is registered (the no-overhead fast path, EC-A3).
	/// </summary>
	private static Activity? StartDispatchActivity(Type messageType, IMessageContext context)
	{
		var name = $"dispatch.{messageType.Name}";
		var restoredTraceParent = context.GetTraceParent();

		// No restored remote parent (or it is malformed): keep the default in-process Internal span, whose
		// parent is the ambient Activity.Current (preserves prior behavior for in-process dispatch).
		if (string.IsNullOrEmpty(restoredTraceParent) ||
			!ActivityContext.TryParse(restoredTraceParent, traceState: null, out var parentContext))
		{
			return DispatchActivitySource.Instance.StartActivity(name, ActivityKind.Internal);
		}

		// A remote parent was restored → Consumer span continuing the trace. OTel: do not override a
		// competing ambient span — when one is already in scope, keep it as the parent and attach the
		// restored context as a link (preserves correlation without hijacking the ambient trace); otherwise
		// (the dominant outbox-consumer case, Activity.Current == null) the restored context is the parent.
		return Activity.Current is null
			? DispatchActivitySource.Instance.StartActivity(name, ActivityKind.Consumer, parentContext)
			: DispatchActivitySource.Instance.StartActivity(
				name,
				ActivityKind.Consumer,
				parentContext: default,
				links: [new ActivityLink(parentContext)]);
	}
}
