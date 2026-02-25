// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.Dispatch.Observability.Propagation;

namespace Excalibur.Dispatch.Observability.Tests.Propagation;

/// <summary>
/// Deep coverage tests for <see cref="W3CTracingContextPropagator"/> covering invalid trace-id/span-id
/// lengths, empty traceparent, unrecorded flags, non-hex flag parsing, tracestate propagation,
/// and edge case format validation.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
public sealed class W3CTracingContextPropagatorDepthShould
{
	private readonly W3CTracingContextPropagator _propagator = new();

	[Fact]
	public async Task Extract_ReturnDefault_ForWrongTraceIdLength()
	{
		// Arrange — trace ID must be 32 hex chars, this is only 16
		var spanId = ActivitySpanId.CreateRandom();
		var carrier = new Dictionary<string, string>
		{
			["traceparent"] = $"00-463ac35c9f6413ad-{spanId}-01",
		};

		// Act
		var result = await _propagator.ExtractAsync(carrier, CancellationToken.None);

		// Assert — 16-char trace ID fails the 32-char check
		result.ShouldBe(default(ActivityContext));
	}

	[Fact]
	public async Task Extract_ReturnDefault_ForWrongSpanIdLength()
	{
		// Arrange — span ID must be 16 hex chars
		var traceId = ActivityTraceId.CreateRandom();
		var carrier = new Dictionary<string, string>
		{
			["traceparent"] = $"00-{traceId}-abcdef12-01",
		};

		// Act
		var result = await _propagator.ExtractAsync(carrier, CancellationToken.None);

		// Assert — 8-char span ID fails the 16-char check
		result.ShouldBe(default(ActivityContext));
	}

	[Fact]
	public async Task Extract_ReturnDefault_ForEmptyTraceparent()
	{
		// Arrange
		var carrier = new Dictionary<string, string>
		{
			["traceparent"] = "",
		};

		// Act
		var result = await _propagator.ExtractAsync(carrier, CancellationToken.None);

		// Assert
		result.ShouldBe(default(ActivityContext));
	}

	[Fact]
	public async Task Extract_NotRecordedFlags()
	{
		// Arrange — flags = "00" means not recorded
		var traceId = ActivityTraceId.CreateRandom();
		var spanId = ActivitySpanId.CreateRandom();
		var carrier = new Dictionary<string, string>
		{
			["traceparent"] = $"00-{traceId}-{spanId}-00",
		};

		// Act
		var result = await _propagator.ExtractAsync(carrier, CancellationToken.None);

		// Assert
		result.ShouldNotBe(default(ActivityContext));
		result.TraceFlags.ShouldBe(ActivityTraceFlags.None);
	}

	[Fact]
	public async Task Extract_NonHexFlags_FallbackToNone()
	{
		// Arrange — non-parseable flags
		var traceId = ActivityTraceId.CreateRandom();
		var spanId = ActivitySpanId.CreateRandom();
		var carrier = new Dictionary<string, string>
		{
			["traceparent"] = $"00-{traceId}-{spanId}-zz",
		};

		// Act
		var result = await _propagator.ExtractAsync(carrier, CancellationToken.None);

		// Assert — "zz" is not valid hex, flags default to None
		result.ShouldNotBe(default(ActivityContext));
		result.TraceFlags.ShouldBe(ActivityTraceFlags.None);
	}

	[Fact]
	public async Task Extract_ShortFlags_FallbackToNone()
	{
		// Arrange — flags shorter than 2 chars
		var traceId = ActivityTraceId.CreateRandom();
		var spanId = ActivitySpanId.CreateRandom();
		var carrier = new Dictionary<string, string>
		{
			["traceparent"] = $"00-{traceId}-{spanId}-1",
		};

		// Act
		var result = await _propagator.ExtractAsync(carrier, CancellationToken.None);

		// Assert — single char flags → defaults to None (length < 2 check)
		result.ShouldNotBe(default(ActivityContext));
		result.TraceFlags.ShouldBe(ActivityTraceFlags.None);
	}

	[Fact]
	public async Task Extract_WithTracestateOnly_ReturnDefault()
	{
		// Arrange — tracestate without traceparent
		var carrier = new Dictionary<string, string>
		{
			["tracestate"] = "key=value",
		};

		// Act
		var result = await _propagator.ExtractAsync(carrier, CancellationToken.None);

		// Assert — missing traceparent returns default
		result.ShouldBe(default(ActivityContext));
	}

	[Fact]
	public async Task Inject_NotRecordedFlags()
	{
		// Arrange — not recorded
		var context = new ActivityContext(
			ActivityTraceId.CreateRandom(),
			ActivitySpanId.CreateRandom(),
			ActivityTraceFlags.None);
		var carrier = new Dictionary<string, string>();

		// Act
		await _propagator.InjectAsync(context, carrier, CancellationToken.None);

		// Assert — traceparent should end with -00
		carrier["traceparent"].ShouldEndWith("-00");
	}

	[Fact]
	public async Task RoundTrip_WithTracestate()
	{
		// Arrange
		var traceId = ActivityTraceId.CreateRandom();
		var spanId = ActivitySpanId.CreateRandom();
		var original = new ActivityContext(traceId, spanId, ActivityTraceFlags.Recorded, "vendor=data,rojo=abc");
		var carrier = new Dictionary<string, string>();

		// Act
		await _propagator.InjectAsync(original, carrier, CancellationToken.None);
		var extracted = await _propagator.ExtractAsync(carrier, CancellationToken.None);

		// Assert — full roundtrip including tracestate
		extracted.TraceId.ShouldBe(traceId);
		extracted.SpanId.ShouldBe(spanId);
		extracted.TraceFlags.ShouldBe(ActivityTraceFlags.Recorded);
		extracted.TraceState.ShouldBe("vendor=data,rojo=abc");
	}

	[Fact]
	public async Task Extract_ExtraPartsIgnored()
	{
		// Arrange — more than 4 parts (future version extension)
		var traceId = ActivityTraceId.CreateRandom();
		var spanId = ActivitySpanId.CreateRandom();
		var carrier = new Dictionary<string, string>
		{
			["traceparent"] = $"00-{traceId}-{spanId}-01-extradata",
		};

		// Act
		var result = await _propagator.ExtractAsync(carrier, CancellationToken.None);

		// Assert — extra parts should not break parsing
		result.ShouldNotBe(default(ActivityContext));
		result.TraceFlags.ShouldBe(ActivityTraceFlags.Recorded);
	}

	[Fact]
	public async Task Extract_ReturnDefault_ForExactlyThreeParts()
	{
		// Arrange — only 3 parts (version-traceId-spanId, missing flags)
		var traceId = ActivityTraceId.CreateRandom();
		var spanId = ActivitySpanId.CreateRandom();
		var carrier = new Dictionary<string, string>
		{
			["traceparent"] = $"00-{traceId}-{spanId}",
		};

		// Act
		var result = await _propagator.ExtractAsync(carrier, CancellationToken.None);

		// Assert — needs at least 4 parts
		result.ShouldBe(default(ActivityContext));
	}
}
