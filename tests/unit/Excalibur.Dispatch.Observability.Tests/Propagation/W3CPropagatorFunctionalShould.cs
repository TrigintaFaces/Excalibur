// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.Dispatch.Observability.Propagation;

namespace Excalibur.Dispatch.Observability.Tests.Propagation;

/// <summary>
/// Functional tests for <see cref="W3CTracingContextPropagator"/> verifying inject/extract round-trip.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Propagation")]
public sealed class W3CPropagatorFunctionalShould
{
	private readonly W3CTracingContextPropagator _propagator = new();

	[Fact]
	public void HaveCorrectFormatName()
	{
		_propagator.FormatName.ShouldBe("w3c");
	}

	[Fact]
	public async Task InjectAndExtract_RoundTrip()
	{
		var traceId = ActivityTraceId.CreateRandom();
		var spanId = ActivitySpanId.CreateRandom();
		var context = new ActivityContext(traceId, spanId, ActivityTraceFlags.Recorded);
		var carrier = new Dictionary<string, string>();

		await _propagator.InjectAsync(context, carrier, CancellationToken.None);

		carrier.ShouldContainKey("traceparent");
		var traceparent = carrier["traceparent"];
		traceparent.ShouldStartWith("00-");
		traceparent.ShouldContain(traceId.ToString());
		traceparent.ShouldContain(spanId.ToString());
		traceparent.ShouldEndWith("-01"); // Recorded flag

		var extracted = await _propagator.ExtractAsync(carrier, CancellationToken.None);

		extracted.TraceId.ShouldBe(traceId);
		extracted.SpanId.ShouldBe(spanId);
		extracted.TraceFlags.ShouldBe(ActivityTraceFlags.Recorded);
	}

	[Fact]
	public async Task Inject_WithTraceState_IncludesTracestateHeader()
	{
		var traceId = ActivityTraceId.CreateRandom();
		var spanId = ActivitySpanId.CreateRandom();
		var context = new ActivityContext(traceId, spanId, ActivityTraceFlags.Recorded, "congo=lZWRzIHRoNhcm5hbWVkT2");
		var carrier = new Dictionary<string, string>();

		await _propagator.InjectAsync(context, carrier, CancellationToken.None);

		carrier.ShouldContainKey("tracestate");
		carrier["tracestate"].ShouldBe("congo=lZWRzIHRoNhcm5hbWVkT2");
	}

	[Fact]
	public async Task Inject_WithoutTraceState_OmitsTracestateHeader()
	{
		var traceId = ActivityTraceId.CreateRandom();
		var spanId = ActivitySpanId.CreateRandom();
		var context = new ActivityContext(traceId, spanId, ActivityTraceFlags.None);
		var carrier = new Dictionary<string, string>();

		await _propagator.InjectAsync(context, carrier, CancellationToken.None);

		carrier.ShouldNotContainKey("tracestate");
	}

	[Fact]
	public async Task Inject_NotSampled_SetsZeroFlag()
	{
		var traceId = ActivityTraceId.CreateRandom();
		var spanId = ActivitySpanId.CreateRandom();
		var context = new ActivityContext(traceId, spanId, ActivityTraceFlags.None);
		var carrier = new Dictionary<string, string>();

		await _propagator.InjectAsync(context, carrier, CancellationToken.None);

		carrier["traceparent"].ShouldEndWith("-00");
	}

	[Fact]
	public async Task Inject_DefaultContext_DoesNothing()
	{
		var carrier = new Dictionary<string, string>();

		await _propagator.InjectAsync(default, carrier, CancellationToken.None);

		carrier.ShouldBeEmpty();
	}

	[Fact]
	public async Task Extract_ValidTraceparent_ParsesCorrectly()
	{
		var carrier = new Dictionary<string, string>
		{
			["traceparent"] = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
		};

		var result = await _propagator.ExtractAsync(carrier, CancellationToken.None);

		result.TraceId.ToString().ShouldBe("4bf92f3577b34da6a3ce929d0e0e4736");
		result.SpanId.ToString().ShouldBe("00f067aa0ba902b7");
		result.TraceFlags.ShouldBe(ActivityTraceFlags.Recorded);
	}

	[Fact]
	public async Task Extract_WithTracestate_PreservesIt()
	{
		var carrier = new Dictionary<string, string>
		{
			["traceparent"] = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
			["tracestate"] = "rojo=00f067aa0ba902b7",
		};

		var result = await _propagator.ExtractAsync(carrier, CancellationToken.None);

		result.TraceState.ShouldBe("rojo=00f067aa0ba902b7");
	}

	[Fact]
	public async Task Extract_MissingTraceparent_ReturnsDefault()
	{
		var carrier = new Dictionary<string, string>();

		var result = await _propagator.ExtractAsync(carrier, CancellationToken.None);

		result.ShouldBe(default);
	}

	[Fact]
	public async Task Extract_EmptyTraceparent_ReturnsDefault()
	{
		var carrier = new Dictionary<string, string> { ["traceparent"] = "" };

		var result = await _propagator.ExtractAsync(carrier, CancellationToken.None);

		result.ShouldBe(default);
	}

	[Fact]
	public async Task Extract_TooFewParts_ReturnsDefault()
	{
		var carrier = new Dictionary<string, string> { ["traceparent"] = "00-abc-def" };

		var result = await _propagator.ExtractAsync(carrier, CancellationToken.None);

		result.ShouldBe(default);
	}

	[Fact]
	public async Task Extract_WrongTraceIdLength_ReturnsDefault()
	{
		var carrier = new Dictionary<string, string> { ["traceparent"] = "00-short-00f067aa0ba902b7-01" };

		var result = await _propagator.ExtractAsync(carrier, CancellationToken.None);

		result.ShouldBe(default);
	}

	[Fact]
	public async Task Extract_WrongSpanIdLength_ReturnsDefault()
	{
		var carrier = new Dictionary<string, string> { ["traceparent"] = "00-4bf92f3577b34da6a3ce929d0e0e4736-short-01" };

		var result = await _propagator.ExtractAsync(carrier, CancellationToken.None);

		result.ShouldBe(default);
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
