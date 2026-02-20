// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.Dispatch.Observability.Propagation;

namespace Excalibur.Dispatch.Observability.Tests.Propagation;

/// <summary>
/// Functional tests for <see cref="B3TracingContextPropagator"/> verifying inject/extract round-trip.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Propagation")]
public sealed class B3PropagatorFunctionalShould
{
	private readonly B3TracingContextPropagator _propagator = new();

	[Fact]
	public void HaveCorrectFormatName()
	{
		_propagator.FormatName.ShouldBe("b3");
	}

	[Fact]
	public async Task InjectAndExtract_RoundTrip_SingleHeader()
	{
		var traceId = ActivityTraceId.CreateRandom();
		var spanId = ActivitySpanId.CreateRandom();
		var context = new ActivityContext(traceId, spanId, ActivityTraceFlags.Recorded);
		var carrier = new Dictionary<string, string>();

		await _propagator.InjectAsync(context, carrier, CancellationToken.None);

		carrier.ShouldContainKey("b3");
		carrier["b3"].ShouldContain(traceId.ToString());
		carrier["b3"].ShouldContain(spanId.ToString());
		carrier["b3"].ShouldEndWith("-1"); // Recorded flag

		var extracted = await _propagator.ExtractAsync(carrier, CancellationToken.None);

		extracted.TraceId.ShouldBe(traceId);
		extracted.SpanId.ShouldBe(spanId);
		extracted.TraceFlags.ShouldBe(ActivityTraceFlags.Recorded);
	}

	[Fact]
	public async Task InjectAndExtract_RoundTrip_MultiHeader()
	{
		var traceId = ActivityTraceId.CreateRandom();
		var spanId = ActivitySpanId.CreateRandom();
		var context = new ActivityContext(traceId, spanId, ActivityTraceFlags.Recorded);
		var carrier = new Dictionary<string, string>();

		await _propagator.InjectAsync(context, carrier, CancellationToken.None);

		// Multi-header should also be populated
		carrier.ShouldContainKey("X-B3-TraceId");
		carrier.ShouldContainKey("X-B3-SpanId");
		carrier.ShouldContainKey("X-B3-Sampled");
		carrier["X-B3-TraceId"].ShouldBe(traceId.ToString());
		carrier["X-B3-SpanId"].ShouldBe(spanId.ToString());
		carrier["X-B3-Sampled"].ShouldBe("1");

		// Extract using only multi-header (remove single header)
		var multiOnlyCarrier = new Dictionary<string, string>
		{
			["X-B3-TraceId"] = carrier["X-B3-TraceId"],
			["X-B3-SpanId"] = carrier["X-B3-SpanId"],
			["X-B3-Sampled"] = carrier["X-B3-Sampled"],
		};

		var extracted = await _propagator.ExtractAsync(multiOnlyCarrier, CancellationToken.None);
		extracted.TraceId.ShouldBe(traceId);
		extracted.SpanId.ShouldBe(spanId);
		extracted.TraceFlags.ShouldBe(ActivityTraceFlags.Recorded);
	}

	[Fact]
	public async Task Inject_NotSampled_SetsZeroFlag()
	{
		var traceId = ActivityTraceId.CreateRandom();
		var spanId = ActivitySpanId.CreateRandom();
		var context = new ActivityContext(traceId, spanId, ActivityTraceFlags.None);
		var carrier = new Dictionary<string, string>();

		await _propagator.InjectAsync(context, carrier, CancellationToken.None);

		carrier["b3"].ShouldEndWith("-0");
		carrier["X-B3-Sampled"].ShouldBe("0");
	}

	[Fact]
	public async Task Inject_DefaultContext_DoesNothing()
	{
		var carrier = new Dictionary<string, string>();

		await _propagator.InjectAsync(default, carrier, CancellationToken.None);

		carrier.ShouldBeEmpty();
	}

	[Fact]
	public async Task Extract_EmptyCarrier_ReturnsDefault()
	{
		var carrier = new Dictionary<string, string>();
		var result = await _propagator.ExtractAsync(carrier, CancellationToken.None);
		result.ShouldBe(default);
	}

	[Fact]
	public async Task Extract_DenyMarker_ReturnsDefault()
	{
		var carrier = new Dictionary<string, string> { ["b3"] = "0" };
		var result = await _propagator.ExtractAsync(carrier, CancellationToken.None);
		result.ShouldBe(default);
	}

	[Fact]
	public async Task Extract_InvalidSingleHeader_FallsBackToMulti()
	{
		var traceId = ActivityTraceId.CreateRandom();
		var spanId = ActivitySpanId.CreateRandom();
		var carrier = new Dictionary<string, string>
		{
			["b3"] = "invalid-data",
			["X-B3-TraceId"] = traceId.ToString(),
			["X-B3-SpanId"] = spanId.ToString(),
			["X-B3-Sampled"] = "1",
		};

		var result = await _propagator.ExtractAsync(carrier, CancellationToken.None);
		result.TraceId.ShouldBe(traceId);
		result.SpanId.ShouldBe(spanId);
	}

	[Fact]
	public async Task Extract_64BitTraceId_PadsTo128Bit()
	{
		// 64-bit trace IDs (16 hex chars) should be padded with leading zeros
		var shortTraceId = "463ac35c9f6413ad";
		var spanId = ActivitySpanId.CreateRandom();
		var carrier = new Dictionary<string, string>
		{
			["b3"] = $"{shortTraceId}-{spanId}-1",
		};

		var result = await _propagator.ExtractAsync(carrier, CancellationToken.None);
		result.TraceId.ToString().ShouldBe("0000000000000000463ac35c9f6413ad");
		result.TraceFlags.ShouldBe(ActivityTraceFlags.Recorded);
	}

	[Fact]
	public async Task Extract_DebugFlag_TreatsAsRecorded()
	{
		var traceId = ActivityTraceId.CreateRandom();
		var spanId = ActivitySpanId.CreateRandom();
		var carrier = new Dictionary<string, string>
		{
			["b3"] = $"{traceId}-{spanId}-d",
		};

		var result = await _propagator.ExtractAsync(carrier, CancellationToken.None);
		result.TraceFlags.ShouldBe(ActivityTraceFlags.Recorded);
	}

	[Fact]
	public async Task Inject_ThrowOnNullCarrier()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => _propagator.InjectAsync(default, null!, CancellationToken.None));
	}

	[Fact]
	public async Task Extract_ThrowOnNullCarrier()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => _propagator.ExtractAsync(null!, CancellationToken.None));
	}
}
