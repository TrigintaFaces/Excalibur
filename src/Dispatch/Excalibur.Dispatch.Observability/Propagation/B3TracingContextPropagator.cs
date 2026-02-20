// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

namespace Excalibur.Dispatch.Observability.Propagation;

/// <summary>
/// Propagates trace context using the Zipkin B3 format (single-header variant).
/// </summary>
/// <remarks>
/// <para>
/// Implements the B3 single-header propagation format used by Zipkin:
/// <c>{TraceId}-{SpanId}-{SamplingState}-{ParentSpanId}</c>.
/// Also supports the multi-header variant with <c>X-B3-TraceId</c>,
/// <c>X-B3-SpanId</c>, <c>X-B3-Sampled</c>, and <c>X-B3-ParentSpanId</c>.
/// </para>
/// </remarks>
public sealed class B3TracingContextPropagator : ITracingContextPropagator
{
	private const string B3SingleHeader = "b3";
	private const string B3TraceIdHeader = "X-B3-TraceId";
	private const string B3SpanIdHeader = "X-B3-SpanId";
	private const string B3SampledHeader = "X-B3-Sampled";

	/// <inheritdoc />
	public string FormatName => "b3";

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

		var sampled = context.TraceFlags.HasFlag(ActivityTraceFlags.Recorded) ? "1" : "0";

		// Inject single-header format
		carrier[B3SingleHeader] = $"{context.TraceId}-{context.SpanId}-{sampled}";

		// Also inject multi-header format for broad compatibility
		carrier[B3TraceIdHeader] = context.TraceId.ToString();
		carrier[B3SpanIdHeader] = context.SpanId.ToString();
		carrier[B3SampledHeader] = sampled;

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task<ActivityContext> ExtractAsync(
		IDictionary<string, string> carrier,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(carrier);

		// Try single-header format first
		if (carrier.TryGetValue(B3SingleHeader, out var b3Single) && !string.IsNullOrEmpty(b3Single))
		{
			var result = ParseSingleHeader(b3Single);
			if (result != default)
			{
				return Task.FromResult(result);
			}
		}

		// Fall back to multi-header format
		var multiResult = ParseMultiHeader(carrier);
		return Task.FromResult(multiResult);
	}

	private static ActivityContext ParseSingleHeader(string b3)
	{
		// Format: {TraceId}-{SpanId}-{SamplingState}[-{ParentSpanId}]
		// Also supports just "0" (deny) or "d" (debug)
		if (b3 == "0")
		{
			return default;
		}

		var parts = b3.Split('-');
		if (parts.Length < 2 || parts[1].Length != 16)
		{
			return default;
		}

		try
		{
			var traceId = ActivityTraceId.CreateFromString(PadTraceId(parts[0]));
			var spanId = ActivitySpanId.CreateFromString(parts[1].AsSpan());

			var flags = ActivityTraceFlags.None;
			if (parts.Length >= 3 && (parts[2] == "1" || parts[2] == "d"))
			{
				flags = ActivityTraceFlags.Recorded;
			}

			return new ActivityContext(traceId, spanId, flags);
		}
		catch (ArgumentOutOfRangeException)
		{
			return default;
		}
	}

	private static ActivityContext ParseMultiHeader(IDictionary<string, string> carrier)
	{
		if (!carrier.TryGetValue(B3TraceIdHeader, out var traceIdStr) ||
			!carrier.TryGetValue(B3SpanIdHeader, out var spanIdStr))
		{
			return default;
		}

		if (spanIdStr.Length != 16)
		{
			return default;
		}

		try
		{
			var traceId = ActivityTraceId.CreateFromString(PadTraceId(traceIdStr));
			var spanId = ActivitySpanId.CreateFromString(spanIdStr.AsSpan());

			var flags = ActivityTraceFlags.None;
			if (carrier.TryGetValue(B3SampledHeader, out var sampled) && (sampled == "1" || sampled == "d"))
			{
				flags = ActivityTraceFlags.Recorded;
			}

			return new ActivityContext(traceId, spanId, flags);
		}
		catch (ArgumentOutOfRangeException)
		{
			return default;
		}
	}

	/// <summary>
	/// Pads a 16-character (64-bit) trace ID to 32 characters (128-bit) for compatibility.
	/// </summary>
	private static ReadOnlySpan<char> PadTraceId(string traceId)
	{
		if (traceId.Length == 16)
		{
			return string.Concat("0000000000000000", traceId).AsSpan();
		}

		return traceId.AsSpan();
	}
}
