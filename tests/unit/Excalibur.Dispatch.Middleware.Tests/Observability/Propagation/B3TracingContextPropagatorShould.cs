// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.Dispatch.Observability.Propagation;

namespace Excalibur.Dispatch.Middleware.Tests.Observability.Propagation;

/// <summary>
/// Unit tests for <see cref="B3TracingContextPropagator"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class B3TracingContextPropagatorShould : UnitTestBase
{
	private readonly B3TracingContextPropagator _propagator = new();

	[Fact]
	public void FormatName_ReturnsB3()
	{
		_propagator.FormatName.ShouldBe("b3");
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
	public async Task InjectAsync_SetsSingleHeaderAndMultiHeaders()
	{
		// Arrange
		var traceId = ActivityTraceId.CreateRandom();
		var spanId = ActivitySpanId.CreateRandom();
		var context = new ActivityContext(traceId, spanId, ActivityTraceFlags.Recorded);
		var carrier = new Dictionary<string, string>();

		// Act
		await _propagator.InjectAsync(context, carrier, CancellationToken.None);

		// Assert
		carrier.ShouldContainKey("b3");
		carrier.ShouldContainKey("X-B3-TraceId");
		carrier.ShouldContainKey("X-B3-SpanId");
		carrier.ShouldContainKey("X-B3-Sampled");

		// Single header format: {TraceId}-{SpanId}-{Sampled}
		var b3Single = carrier["b3"];
		b3Single.ShouldContain(traceId.ToString());
		b3Single.ShouldContain(spanId.ToString());
		b3Single.ShouldContain("-1"); // Recorded

		// Multi-header values
		carrier["X-B3-TraceId"].ShouldBe(traceId.ToString());
		carrier["X-B3-SpanId"].ShouldBe(spanId.ToString());
		carrier["X-B3-Sampled"].ShouldBe("1");
	}

	[Fact]
	public async Task InjectAsync_SetsSampledToZero_WhenNotRecorded()
	{
		// Arrange
		var traceId = ActivityTraceId.CreateRandom();
		var spanId = ActivitySpanId.CreateRandom();
		var context = new ActivityContext(traceId, spanId, ActivityTraceFlags.None);
		var carrier = new Dictionary<string, string>();

		// Act
		await _propagator.InjectAsync(context, carrier, CancellationToken.None);

		// Assert
		carrier["X-B3-Sampled"].ShouldBe("0");
	}

	[Fact]
	public async Task ExtractAsync_ThrowsArgumentNullException_WhenCarrierIsNull()
	{
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await _propagator.ExtractAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task ExtractAsync_ReturnsDefault_WhenNoHeaders()
	{
		// Arrange
		var carrier = new Dictionary<string, string>();

		// Act
		var result = await _propagator.ExtractAsync(carrier, CancellationToken.None);

		// Assert
		result.ShouldBe(default);
	}

	[Fact]
	public async Task ExtractAsync_ParsesSingleHeader()
	{
		// Arrange
		var traceId = ActivityTraceId.CreateRandom();
		var spanId = ActivitySpanId.CreateRandom();
		var carrier = new Dictionary<string, string>
		{
			["b3"] = $"{traceId}-{spanId}-1",
		};

		// Act
		var result = await _propagator.ExtractAsync(carrier, CancellationToken.None);

		// Assert
		result.TraceId.ShouldBe(traceId);
		result.SpanId.ShouldBe(spanId);
		result.TraceFlags.ShouldBe(ActivityTraceFlags.Recorded);
	}

	[Fact]
	public async Task ExtractAsync_ParsesMultiHeaders()
	{
		// Arrange
		var traceId = ActivityTraceId.CreateRandom();
		var spanId = ActivitySpanId.CreateRandom();
		var carrier = new Dictionary<string, string>
		{
			["X-B3-TraceId"] = traceId.ToString(),
			["X-B3-SpanId"] = spanId.ToString(),
			["X-B3-Sampled"] = "1",
		};

		// Act
		var result = await _propagator.ExtractAsync(carrier, CancellationToken.None);

		// Assert
		result.TraceId.ShouldBe(traceId);
		result.SpanId.ShouldBe(spanId);
		result.TraceFlags.ShouldBe(ActivityTraceFlags.Recorded);
	}

	[Fact]
	public async Task ExtractAsync_ReturnsDefault_WhenSingleHeaderIsDenyOnly()
	{
		// Arrange
		var carrier = new Dictionary<string, string> { ["b3"] = "0" };

		// Act
		var result = await _propagator.ExtractAsync(carrier, CancellationToken.None);

		// Assert
		result.ShouldBe(default);
	}

	[Fact]
	public async Task ExtractAsync_PrefersSingleHeader_OverMultiHeader()
	{
		// Arrange
		var singleTraceId = ActivityTraceId.CreateRandom();
		var singleSpanId = ActivitySpanId.CreateRandom();
		var multiTraceId = ActivityTraceId.CreateRandom();
		var multiSpanId = ActivitySpanId.CreateRandom();

		var carrier = new Dictionary<string, string>
		{
			["b3"] = $"{singleTraceId}-{singleSpanId}-1",
			["X-B3-TraceId"] = multiTraceId.ToString(),
			["X-B3-SpanId"] = multiSpanId.ToString(),
			["X-B3-Sampled"] = "0",
		};

		// Act
		var result = await _propagator.ExtractAsync(carrier, CancellationToken.None);

		// Assert - Single header takes precedence
		result.TraceId.ShouldBe(singleTraceId);
		result.SpanId.ShouldBe(singleSpanId);
	}

	[Fact]
	public async Task RoundTrip_InjectThenExtract_ProducesEquivalentContext()
	{
		// Arrange
		var traceId = ActivityTraceId.CreateRandom();
		var spanId = ActivitySpanId.CreateRandom();
		var originalContext = new ActivityContext(traceId, spanId, ActivityTraceFlags.Recorded);
		var carrier = new Dictionary<string, string>();

		// Act
		await _propagator.InjectAsync(originalContext, carrier, CancellationToken.None);
		var extractedContext = await _propagator.ExtractAsync(carrier, CancellationToken.None);

		// Assert
		extractedContext.TraceId.ShouldBe(originalContext.TraceId);
		extractedContext.SpanId.ShouldBe(originalContext.SpanId);
		extractedContext.TraceFlags.ShouldBe(originalContext.TraceFlags);
	}
}
