// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.Dispatch.Observability.Propagation;

namespace Excalibur.Dispatch.Observability.Tests.Propagation;

/// <summary>
/// Deep coverage tests for <see cref="B3TracingContextPropagator"/> covering 64-bit trace ID padding,
/// debug "d" flag, invalid formats, parent span ID handling, and multi-header edge cases.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
public sealed class B3TracingContextPropagatorDepthShould
{
	private readonly B3TracingContextPropagator _propagator = new();

	[Fact]
	public async Task ExtractSingleHeader_With64BitTraceId()
	{
		// Arrange — 16-char (64-bit) trace ID should be padded to 32 chars
		var shortTraceId = "463ac35c9f6413ad";
		var spanId = ActivitySpanId.CreateRandom();
		var carrier = new Dictionary<string, string>
		{
			["b3"] = $"{shortTraceId}-{spanId}-1",
		};

		// Act
		var result = await _propagator.ExtractAsync(carrier, CancellationToken.None);

		// Assert — should succeed with padded trace ID (0000000000000000 + shortTraceId)
		result.ShouldNotBe(default(ActivityContext));
		result.TraceId.ToString().ShouldEndWith(shortTraceId);
		result.TraceFlags.ShouldBe(ActivityTraceFlags.Recorded);
	}

	[Fact]
	public async Task ExtractSingleHeader_WithDebugFlag()
	{
		// Arrange — "d" flag means debug/recorded
		var traceId = ActivityTraceId.CreateRandom();
		var spanId = ActivitySpanId.CreateRandom();
		var carrier = new Dictionary<string, string>
		{
			["b3"] = $"{traceId}-{spanId}-d",
		};

		// Act
		var result = await _propagator.ExtractAsync(carrier, CancellationToken.None);

		// Assert — "d" should be treated as Recorded
		result.ShouldNotBe(default(ActivityContext));
		result.TraceFlags.ShouldBe(ActivityTraceFlags.Recorded);
	}

	[Fact]
	public async Task ExtractSingleHeader_NotSampled()
	{
		// Arrange — "0" sampling state
		var traceId = ActivityTraceId.CreateRandom();
		var spanId = ActivitySpanId.CreateRandom();
		var carrier = new Dictionary<string, string>
		{
			["b3"] = $"{traceId}-{spanId}-0",
		};

		// Act
		var result = await _propagator.ExtractAsync(carrier, CancellationToken.None);

		// Assert — "0" should not set Recorded flag
		result.ShouldNotBe(default(ActivityContext));
		result.TraceFlags.ShouldBe(ActivityTraceFlags.None);
	}

	[Fact]
	public async Task ExtractSingleHeader_WithParentSpanId()
	{
		// Arrange — 4-part format: {TraceId}-{SpanId}-{SamplingState}-{ParentSpanId}
		var traceId = ActivityTraceId.CreateRandom();
		var spanId = ActivitySpanId.CreateRandom();
		var parentSpanId = ActivitySpanId.CreateRandom();
		var carrier = new Dictionary<string, string>
		{
			["b3"] = $"{traceId}-{spanId}-1-{parentSpanId}",
		};

		// Act
		var result = await _propagator.ExtractAsync(carrier, CancellationToken.None);

		// Assert — should parse successfully (parent span ID is optional but accepted)
		result.ShouldNotBe(default(ActivityContext));
		result.TraceId.ShouldBe(traceId);
		result.SpanId.ShouldBe(spanId);
	}

	[Fact]
	public async Task ExtractSingleHeader_ReturnDefault_ForInvalidSpanIdLength()
	{
		// Arrange — span ID must be 16 characters
		var traceId = ActivityTraceId.CreateRandom();
		var carrier = new Dictionary<string, string>
		{
			["b3"] = $"{traceId}-shortspan-1",
		};

		// Act
		var result = await _propagator.ExtractAsync(carrier, CancellationToken.None);

		// Assert — invalid span ID length returns default
		result.ShouldBe(default(ActivityContext));
	}

	[Fact]
	public async Task ExtractSingleHeader_ReturnDefault_ForSinglePart()
	{
		// Arrange — less than 2 parts (only trace ID)
		var carrier = new Dictionary<string, string>
		{
			["b3"] = "463ac35c9f6413ad463ac35c9f6413ad",
		};

		// Act
		var result = await _propagator.ExtractAsync(carrier, CancellationToken.None);

		// Assert
		result.ShouldBe(default(ActivityContext));
	}

	[Fact]
	public async Task ExtractSingleHeader_ReturnDefault_ForEmptyValue()
	{
		// Arrange
		var carrier = new Dictionary<string, string>
		{
			["b3"] = "",
		};

		// Act
		var result = await _propagator.ExtractAsync(carrier, CancellationToken.None);

		// Assert — empty b3 header falls through to multi-header which also fails
		result.ShouldBe(default(ActivityContext));
	}

	[Fact]
	public async Task ExtractMultiHeader_WithDebugFlag()
	{
		// Arrange
		var traceId = ActivityTraceId.CreateRandom();
		var spanId = ActivitySpanId.CreateRandom();
		var carrier = new Dictionary<string, string>
		{
			["X-B3-TraceId"] = traceId.ToString(),
			["X-B3-SpanId"] = spanId.ToString(),
			["X-B3-Sampled"] = "d",
		};

		// Act
		var result = await _propagator.ExtractAsync(carrier, CancellationToken.None);

		// Assert — "d" in multi-header should set Recorded
		result.ShouldNotBe(default(ActivityContext));
		result.TraceFlags.ShouldBe(ActivityTraceFlags.Recorded);
	}

	[Fact]
	public async Task ExtractMultiHeader_WithoutSampledHeader()
	{
		// Arrange — no X-B3-Sampled header
		var traceId = ActivityTraceId.CreateRandom();
		var spanId = ActivitySpanId.CreateRandom();
		var carrier = new Dictionary<string, string>
		{
			["X-B3-TraceId"] = traceId.ToString(),
			["X-B3-SpanId"] = spanId.ToString(),
		};

		// Act
		var result = await _propagator.ExtractAsync(carrier, CancellationToken.None);

		// Assert — should succeed with no Recorded flag
		result.ShouldNotBe(default(ActivityContext));
		result.TraceFlags.ShouldBe(ActivityTraceFlags.None);
	}

	[Fact]
	public async Task ExtractMultiHeader_With64BitTraceId()
	{
		// Arrange — 16-char trace ID in multi-header
		var shortTraceId = "463ac35c9f6413ad";
		var spanId = ActivitySpanId.CreateRandom();
		var carrier = new Dictionary<string, string>
		{
			["X-B3-TraceId"] = shortTraceId,
			["X-B3-SpanId"] = spanId.ToString(),
			["X-B3-Sampled"] = "1",
		};

		// Act
		var result = await _propagator.ExtractAsync(carrier, CancellationToken.None);

		// Assert — 64-bit trace ID padded to 128-bit
		result.ShouldNotBe(default(ActivityContext));
		result.TraceId.ToString().ShouldEndWith(shortTraceId);
	}

	[Fact]
	public async Task ExtractMultiHeader_ReturnDefault_ForInvalidSpanIdLength()
	{
		// Arrange — span ID must be 16 characters
		var traceId = ActivityTraceId.CreateRandom();
		var carrier = new Dictionary<string, string>
		{
			["X-B3-TraceId"] = traceId.ToString(),
			["X-B3-SpanId"] = "tooshort",
		};

		// Act
		var result = await _propagator.ExtractAsync(carrier, CancellationToken.None);

		// Assert
		result.ShouldBe(default(ActivityContext));
	}

	[Fact]
	public async Task ExtractMultiHeader_ReturnDefault_WhenMissingTraceId()
	{
		// Arrange — only span ID present
		var spanId = ActivitySpanId.CreateRandom();
		var carrier = new Dictionary<string, string>
		{
			["X-B3-SpanId"] = spanId.ToString(),
		};

		// Act
		var result = await _propagator.ExtractAsync(carrier, CancellationToken.None);

		// Assert
		result.ShouldBe(default(ActivityContext));
	}

	[Fact]
	public async Task ExtractMultiHeader_ReturnDefault_WhenMissingSpanId()
	{
		// Arrange — only trace ID present
		var traceId = ActivityTraceId.CreateRandom();
		var carrier = new Dictionary<string, string>
		{
			["X-B3-TraceId"] = traceId.ToString(),
		};

		// Act
		var result = await _propagator.ExtractAsync(carrier, CancellationToken.None);

		// Assert
		result.ShouldBe(default(ActivityContext));
	}

	[Fact]
	public async Task PreferSingleHeader_OverMultiHeader()
	{
		// Arrange — both present, single-header should win
		var traceId1 = ActivityTraceId.CreateRandom();
		var spanId1 = ActivitySpanId.CreateRandom();
		var traceId2 = ActivityTraceId.CreateRandom();
		var spanId2 = ActivitySpanId.CreateRandom();
		var carrier = new Dictionary<string, string>
		{
			["b3"] = $"{traceId1}-{spanId1}-1",
			["X-B3-TraceId"] = traceId2.ToString(),
			["X-B3-SpanId"] = spanId2.ToString(),
			["X-B3-Sampled"] = "0",
		};

		// Act
		var result = await _propagator.ExtractAsync(carrier, CancellationToken.None);

		// Assert — single-header values should be used
		result.TraceId.ShouldBe(traceId1);
		result.SpanId.ShouldBe(spanId1);
	}

	[Fact]
	public async Task InjectAndExtract_Roundtrip()
	{
		// Arrange
		var traceId = ActivityTraceId.CreateRandom();
		var spanId = ActivitySpanId.CreateRandom();
		var context = new ActivityContext(traceId, spanId, ActivityTraceFlags.Recorded);
		var carrier = new Dictionary<string, string>();

		// Act — inject then extract
		await _propagator.InjectAsync(context, carrier, CancellationToken.None);
		var extracted = await _propagator.ExtractAsync(carrier, CancellationToken.None);

		// Assert — roundtrip should produce identical context
		extracted.TraceId.ShouldBe(traceId);
		extracted.SpanId.ShouldBe(spanId);
		extracted.TraceFlags.ShouldBe(ActivityTraceFlags.Recorded);
	}
}
