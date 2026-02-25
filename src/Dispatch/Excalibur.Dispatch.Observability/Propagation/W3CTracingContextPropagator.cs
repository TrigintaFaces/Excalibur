// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Globalization;

namespace Excalibur.Dispatch.Observability.Propagation;

/// <summary>
/// Propagates trace context using the W3C Trace Context format.
/// </summary>
/// <remarks>
/// <para>
/// Implements the W3C Trace Context specification (https://www.w3.org/TR/trace-context/).
/// Injects/extracts <c>traceparent</c> and <c>tracestate</c> headers.
/// </para>
/// <para>
/// The <c>traceparent</c> header format is:
/// <c>{version}-{trace-id}-{parent-id}-{trace-flags}</c>.
/// </para>
/// </remarks>
public sealed class W3CTracingContextPropagator : ITracingContextPropagator
{
	private const string TraceparentHeader = "traceparent";
	private const string TracestateHeader = "tracestate";
	private const string Version = "00";

	/// <inheritdoc />
	public string FormatName => "w3c";

	/// <inheritdoc />
	public Task InjectAsync(
		ActivityContext context,
		IDictionary<string, string> carrier,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(carrier);

		if (context == default)
		{
			return Task.CompletedTask;
		}

		var flags = ((int)context.TraceFlags).ToString("x2", CultureInfo.InvariantCulture);
		var traceparent = $"{Version}-{context.TraceId}-{context.SpanId}-{flags}";
		carrier[TraceparentHeader] = traceparent;

		if (!string.IsNullOrEmpty(context.TraceState))
		{
			carrier[TracestateHeader] = context.TraceState;
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task<ActivityContext> ExtractAsync(
		IDictionary<string, string> carrier,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(carrier);

		if (!carrier.TryGetValue(TraceparentHeader, out var traceparent) ||
			string.IsNullOrEmpty(traceparent))
		{
			return Task.FromResult(default(ActivityContext));
		}

		var parts = traceparent.Split('-');
		if (parts.Length < 4)
		{
			return Task.FromResult(default(ActivityContext));
		}

		// Validate trace-id is 32 hex chars and span-id is 16 hex chars
		if (parts[1].Length != 32 || parts[2].Length != 16)
		{
			return Task.FromResult(default(ActivityContext));
		}

		ActivityTraceId traceId;
		ActivitySpanId spanId;

		try
		{
			traceId = ActivityTraceId.CreateFromString(parts[1].AsSpan());
			spanId = ActivitySpanId.CreateFromString(parts[2].AsSpan());
		}
		catch (ArgumentOutOfRangeException)
		{
			return Task.FromResult(default(ActivityContext));
		}

		var flags = parts[3].Length >= 2 &&
					int.TryParse(parts[3], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var flagsInt)
			? (ActivityTraceFlags)flagsInt
			: ActivityTraceFlags.None;

		carrier.TryGetValue(TracestateHeader, out var tracestate);

		var context = new ActivityContext(traceId, spanId, flags, tracestate);
		return Task.FromResult(context);
	}
}
