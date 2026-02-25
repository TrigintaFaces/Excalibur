// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Observability.Metrics;

namespace Excalibur.Dispatch.Observability.Http;

/// <summary>
/// Middleware that propagates W3C Trace Context (traceparent/tracestate) HTTP headers
/// into the Dispatch <see cref="DispatchActivitySource"/>.
/// </summary>
/// <remarks>
/// <para>
/// Implements the W3C Trace Context specification (https://www.w3.org/TR/trace-context/)
/// to enable distributed tracing across HTTP boundaries. The middleware extracts
/// <c>traceparent</c> and <c>tracestate</c> headers from the message context and creates
/// a child activity linked to the parent trace.
/// </para>
/// <para>
/// The <c>traceparent</c> header format is:
/// <c>{version}-{trace-id}-{parent-id}-{trace-flags}</c>
/// (e.g., <c>00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01</c>).
/// </para>
/// </remarks>
[AppliesTo(MessageKinds.All)]
public sealed class W3CTraceContextMiddleware : IDispatchMiddleware
{
	/// <summary>
	/// The well-known key for the W3C traceparent header in message properties.
	/// </summary>
	public const string TraceparentKey = "traceparent";

	/// <summary>
	/// The well-known key for the W3C tracestate header in message properties.
	/// </summary>
	public const string TracestateKey = "tracestate";

	private static readonly ActivitySource ActivitySource = new(
		W3CTraceContextTelemetryConstants.ActivitySourceName,
		W3CTraceContextTelemetryConstants.Version);

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

		var traceparent = ExtractHeader(context, TraceparentKey);

		if (traceparent is null)
		{
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		var tracestate = ExtractHeader(context, TracestateKey);
		var parentContext = ParseTraceparent(traceparent, tracestate);

		using var activity = ActivitySource.StartActivity(
			"dispatch.w3c.propagate",
			ActivityKind.Consumer,
			parentContext);

		if (activity is not null)
		{
			_ = activity.SetTag("dispatch.trace.propagation", "w3c");
			_ = activity.SetTag("message.type", message.GetType().Name);

			if (tracestate is not null)
			{
				activity.TraceStateString = tracestate;
			}
		}

		return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
	}

	private static string? ExtractHeader(IMessageContext context, string key)
	{
		if (context.GetItem<string>(key) is { Length: > 0 } value)
		{
			return value;
		}

		if (context.GetItem<IDictionary<string, string>>("Headers") is { } headers &&
			headers.TryGetValue(key, out var headerValue))
		{
			return headerValue;
		}

		return null;
	}

	private static ActivityContext ParseTraceparent(string traceparent, string? tracestate)
	{
		// W3C traceparent format: {version}-{trace-id}-{parent-id}-{trace-flags}
		// Example: 00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01
		var parts = traceparent.Split('-');
		if (parts.Length < 4 || parts[1].Length != 32 || parts[2].Length != 16)
		{
			return default;
		}

		try
		{
			var traceId = ActivityTraceId.CreateFromString(parts[1].AsSpan());
			var spanId = ActivitySpanId.CreateFromString(parts[2].AsSpan());

			var flags = parts[3].Length >= 2 &&
						int.TryParse(parts[3], System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var flagsInt)
				? (ActivityTraceFlags)flagsInt
				: ActivityTraceFlags.None;

			return new ActivityContext(traceId, spanId, flags, tracestate);
		}
		catch (ArgumentOutOfRangeException)
		{
			return default;
		}
	}
}
