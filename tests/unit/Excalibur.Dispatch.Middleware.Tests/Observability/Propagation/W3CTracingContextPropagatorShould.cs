// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.Dispatch.Observability.Propagation;

namespace Excalibur.Dispatch.Middleware.Tests.Observability.Propagation;

/// <summary>
/// Unit tests for <see cref="W3CTracingContextPropagator"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class W3CTracingContextPropagatorShould : UnitTestBase
{
	private readonly W3CTracingContextPropagator _propagator = new();

	[Fact]
	public void FormatName_ReturnsW3C()
	{
		_propagator.FormatName.ShouldBe("w3c");
	}

	[Fact]
	public async Task InjectAsync_ThrowsArgumentNullException_WhenCarrierIsNull()
	{
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await _propagator.InjectAsync(default, null!, CancellationToken.None));
	}

	[Fact]
	public async Task InjectAsync_DoesNothing_WhenContextIsDefault()
	{
		// Arrange
		var carrier = new Dictionary<string, string>();

		// Act
		await _propagator.InjectAsync(default, carrier, CancellationToken.None);

		// Assert
		carrier.ShouldBeEmpty();
	}

	[Fact]
	public async Task InjectAsync_SetsTraceparentHeader()
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
		traceparent.ShouldEndWith("-01"); // Recorded flag
	}

	[Fact]
	public async Task InjectAsync_SetsTracestateHeader_WhenPresent()
	{
		// Arrange
		var traceId = ActivityTraceId.CreateRandom();
		var spanId = ActivitySpanId.CreateRandom();
		var context = new ActivityContext(traceId, spanId, ActivityTraceFlags.None, "vendor1=value1");
		var carrier = new Dictionary<string, string>();

		// Act
		await _propagator.InjectAsync(context, carrier, CancellationToken.None);

		// Assert
		carrier.ShouldContainKey("tracestate");
		carrier["tracestate"].ShouldBe("vendor1=value1");
	}

	[Fact]
	public async Task InjectAsync_DoesNotSetTracestate_WhenEmpty()
	{
		// Arrange
		var traceId = ActivityTraceId.CreateRandom();
		var spanId = ActivitySpanId.CreateRandom();
		var context = new ActivityContext(traceId, spanId, ActivityTraceFlags.None);
		var carrier = new Dictionary<string, string>();

		// Act
		await _propagator.InjectAsync(context, carrier, CancellationToken.None);

		// Assert
		carrier.ShouldContainKey("traceparent");
		carrier.ShouldNotContainKey("tracestate");
	}

	[Fact]
	public async Task ExtractAsync_ThrowsArgumentNullException_WhenCarrierIsNull()
	{
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await _propagator.ExtractAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task ExtractAsync_ReturnsDefault_WhenTraceparentIsMissing()
	{
		// Arrange
		var carrier = new Dictionary<string, string>();

		// Act
		var result = await _propagator.ExtractAsync(carrier, CancellationToken.None);

		// Assert
		result.ShouldBe(default);
	}

	[Fact]
	public async Task ExtractAsync_ReturnsDefault_WhenTraceparentIsEmpty()
	{
		// Arrange
		var carrier = new Dictionary<string, string> { ["traceparent"] = "" };

		// Act
		var result = await _propagator.ExtractAsync(carrier, CancellationToken.None);

		// Assert
		result.ShouldBe(default);
	}

	[Fact]
	public async Task ExtractAsync_ReturnsDefault_WhenTraceparentHasTooFewParts()
	{
		// Arrange
		var carrier = new Dictionary<string, string> { ["traceparent"] = "00-abc" };

		// Act
		var result = await _propagator.ExtractAsync(carrier, CancellationToken.None);

		// Assert
		result.ShouldBe(default);
	}

	[Fact]
	public async Task ExtractAsync_ParsesValidTraceparent()
	{
		// Arrange
		var traceId = ActivityTraceId.CreateRandom();
		var spanId = ActivitySpanId.CreateRandom();
		var traceparent = $"00-{traceId}-{spanId}-01";
		var carrier = new Dictionary<string, string> { ["traceparent"] = traceparent };

		// Act
		var result = await _propagator.ExtractAsync(carrier, CancellationToken.None);

		// Assert
		result.TraceId.ShouldBe(traceId);
		result.SpanId.ShouldBe(spanId);
		result.TraceFlags.ShouldBe(ActivityTraceFlags.Recorded);
	}

	[Fact]
	public async Task ExtractAsync_IncludesTracestate_WhenPresent()
	{
		// Arrange
		var traceId = ActivityTraceId.CreateRandom();
		var spanId = ActivitySpanId.CreateRandom();
		var traceparent = $"00-{traceId}-{spanId}-00";
		var carrier = new Dictionary<string, string>
		{
			["traceparent"] = traceparent,
			["tracestate"] = "vendor1=value1",
		};

		// Act
		var result = await _propagator.ExtractAsync(carrier, CancellationToken.None);

		// Assert
		result.TraceState.ShouldBe("vendor1=value1");
	}

	[Fact]
	public async Task ExtractAsync_ReturnsDefault_WhenTraceIdHasInvalidLength()
	{
		// Arrange - trace-id must be 32 hex chars
		var carrier = new Dictionary<string, string>
		{
			["traceparent"] = "00-short-0123456789abcdef-01",
		};

		// Act
		var result = await _propagator.ExtractAsync(carrier, CancellationToken.None);

		// Assert
		result.ShouldBe(default);
	}

	[Fact]
	public async Task RoundTrip_InjectThenExtract_ProducesEquivalentContext()
	{
		// Arrange
		var traceId = ActivityTraceId.CreateRandom();
		var spanId = ActivitySpanId.CreateRandom();
		var originalContext = new ActivityContext(traceId, spanId, ActivityTraceFlags.Recorded, "test=value");
		var carrier = new Dictionary<string, string>();

		// Act
		await _propagator.InjectAsync(originalContext, carrier, CancellationToken.None);
		var extractedContext = await _propagator.ExtractAsync(carrier, CancellationToken.None);

		// Assert
		extractedContext.TraceId.ShouldBe(originalContext.TraceId);
		extractedContext.SpanId.ShouldBe(originalContext.SpanId);
		extractedContext.TraceFlags.ShouldBe(originalContext.TraceFlags);
		extractedContext.TraceState.ShouldBe(originalContext.TraceState);
	}
}
