// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.Dispatch.Observability.Propagation;

namespace Excalibur.Dispatch.Observability.Tests.Propagation;

/// <summary>
/// Unit tests for <see cref="W3CTracingContextPropagator"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Propagation")]
public sealed class W3CTracingContextPropagatorShould
{
	private readonly W3CTracingContextPropagator _propagator = new();

	[Fact]
	public void HaveCorrectFormatName()
	{
		_propagator.FormatName.ShouldBe("w3c");
	}

	[Fact]
	public async Task InjectTraceparentHeader()
	{
		// Arrange
		var traceId = ActivityTraceId.CreateRandom();
		var spanId = ActivitySpanId.CreateRandom();
		var context = new ActivityContext(traceId, spanId, ActivityTraceFlags.Recorded);
		var carrier = new Dictionary<string, string>();

		// Act
		await _propagator.InjectAsync(context, carrier, CancellationToken.None);

		// Assert
		carrier.ShouldContainKey("traceparent");
		var traceparent = carrier["traceparent"];
		traceparent.ShouldStartWith("00-");
		traceparent.ShouldContain(traceId.ToString());
		traceparent.ShouldContain(spanId.ToString());
		traceparent.ShouldEndWith("-01"); // Recorded
	}

	[Fact]
	public async Task InjectTracestateHeader_WhenPresent()
	{
		// Arrange
		var context = new ActivityContext(
			ActivityTraceId.CreateRandom(),
			ActivitySpanId.CreateRandom(),
			ActivityTraceFlags.None,
			"congo=t61rcWkgMzE");
		var carrier = new Dictionary<string, string>();

		// Act
		await _propagator.InjectAsync(context, carrier, CancellationToken.None);

		// Assert
		carrier.ShouldContainKey("tracestate");
		carrier["tracestate"].ShouldBe("congo=t61rcWkgMzE");
	}

	[Fact]
	public async Task NotInjectTracestate_WhenEmpty()
	{
		// Arrange
		var context = new ActivityContext(
			ActivityTraceId.CreateRandom(),
			ActivitySpanId.CreateRandom(),
			ActivityTraceFlags.None);
		var carrier = new Dictionary<string, string>();

		// Act
		await _propagator.InjectAsync(context, carrier, CancellationToken.None);

		// Assert
		carrier.ShouldNotContainKey("tracestate");
	}

	[Fact]
	public async Task NotInject_WhenDefaultContext()
	{
		// Arrange
		var carrier = new Dictionary<string, string>();

		// Act
		await _propagator.InjectAsync(default, carrier, CancellationToken.None);

		// Assert
		carrier.ShouldBeEmpty();
	}

	[Fact]
	public async Task ExtractValidTraceparent()
	{
		// Arrange
		var traceId = ActivityTraceId.CreateRandom();
		var spanId = ActivitySpanId.CreateRandom();
		var carrier = new Dictionary<string, string>
		{
			["traceparent"] = $"00-{traceId}-{spanId}-01",
		};

		// Act
		var result = await _propagator.ExtractAsync(carrier, CancellationToken.None);

		// Assert
		result.ShouldNotBe(default(ActivityContext));
		result.TraceId.ShouldBe(traceId);
		result.SpanId.ShouldBe(spanId);
		result.TraceFlags.ShouldBe(ActivityTraceFlags.Recorded);
	}

	[Fact]
	public async Task ExtractTracestate()
	{
		// Arrange
		var traceId = ActivityTraceId.CreateRandom();
		var spanId = ActivitySpanId.CreateRandom();
		var carrier = new Dictionary<string, string>
		{
			["traceparent"] = $"00-{traceId}-{spanId}-01",
			["tracestate"] = "congo=t61rcWkgMzE",
		};

		// Act
		var result = await _propagator.ExtractAsync(carrier, CancellationToken.None);

		// Assert
		result.TraceState.ShouldBe("congo=t61rcWkgMzE");
	}

	[Fact]
	public async Task ReturnDefault_WhenNoTraceparent()
	{
		// Arrange
		var carrier = new Dictionary<string, string>();

		// Act
		var result = await _propagator.ExtractAsync(carrier, CancellationToken.None);

		// Assert
		result.ShouldBe(default(ActivityContext));
	}

	[Fact]
	public async Task ReturnDefault_WhenInvalidFormat()
	{
		// Arrange
		var carrier = new Dictionary<string, string>
		{
			["traceparent"] = "invalid-format",
		};

		// Act
		var result = await _propagator.ExtractAsync(carrier, CancellationToken.None);

		// Assert
		result.ShouldBe(default(ActivityContext));
	}

	[Fact]
	public async Task ReturnDefault_WhenTooFewParts()
	{
		// Arrange
		var carrier = new Dictionary<string, string>
		{
			["traceparent"] = "00-abc",
		};

		// Act
		var result = await _propagator.ExtractAsync(carrier, CancellationToken.None);

		// Assert
		result.ShouldBe(default(ActivityContext));
	}

	[Fact]
	public async Task RoundTrip_InjectAndExtract()
	{
		// Arrange
		var traceId = ActivityTraceId.CreateRandom();
		var spanId = ActivitySpanId.CreateRandom();
		var original = new ActivityContext(traceId, spanId, ActivityTraceFlags.Recorded);
		var carrier = new Dictionary<string, string>();

		// Act — inject then extract
		await _propagator.InjectAsync(original, carrier, CancellationToken.None);
		var extracted = await _propagator.ExtractAsync(carrier, CancellationToken.None);

		// Assert
		extracted.TraceId.ShouldBe(original.TraceId);
		extracted.SpanId.ShouldBe(original.SpanId);
		extracted.TraceFlags.ShouldBe(original.TraceFlags);
	}

	[Fact]
	public async Task ThrowOnNullCarrier_Inject()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			async () => await _propagator.InjectAsync(default, null!, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowOnNullCarrier_Extract()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			async () => await _propagator.ExtractAsync(null!, CancellationToken.None));
	}

	[Fact]
	public void ImplementITracingContextPropagator()
	{
		_propagator.ShouldBeAssignableTo<ITracingContextPropagator>();
	}
}
