// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

using Excalibur.Dispatch.Observability.Propagation;

namespace Excalibur.Dispatch.Observability.Tests.Propagation;

/// <summary>
/// Unit tests for <see cref="B3TracingContextPropagator"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Propagation")]
public sealed class B3TracingContextPropagatorShould
{
	private readonly B3TracingContextPropagator _propagator = new();

	[Fact]
	public void HaveCorrectFormatName()
	{
		_propagator.FormatName.ShouldBe("b3");
	}

	[Fact]
	public async Task InjectBothHeaderFormats()
	{
		// Arrange
		var traceId = ActivityTraceId.CreateRandom();
		var spanId = ActivitySpanId.CreateRandom();
		var context = new ActivityContext(traceId, spanId, ActivityTraceFlags.Recorded);
		var carrier = new Dictionary<string, string>();

		// Act
		await _propagator.InjectAsync(context, carrier, CancellationToken.None);

		// Assert — single-header
		carrier.ShouldContainKey("b3");
		carrier["b3"].ShouldContain(traceId.ToString());
		carrier["b3"].ShouldContain(spanId.ToString());
		carrier["b3"].ShouldEndWith("-1"); // Recorded flag

		// Assert — multi-header
		carrier.ShouldContainKey("X-B3-TraceId");
		carrier.ShouldContainKey("X-B3-SpanId");
		carrier.ShouldContainKey("X-B3-Sampled");
		carrier["X-B3-TraceId"].ShouldBe(traceId.ToString());
		carrier["X-B3-SpanId"].ShouldBe(spanId.ToString());
		carrier["X-B3-Sampled"].ShouldBe("1");
	}

	[Fact]
	public async Task InjectNotRecordedFlag()
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
		carrier["X-B3-Sampled"].ShouldBe("0");
		carrier["b3"].ShouldEndWith("-0");
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
	public async Task ExtractFromSingleHeader()
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
		result.ShouldNotBe(default(ActivityContext));
		result.TraceId.ShouldBe(traceId);
		result.SpanId.ShouldBe(spanId);
		result.TraceFlags.ShouldBe(ActivityTraceFlags.Recorded);
	}

	[Fact]
	public async Task ExtractFromMultiHeader()
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
		result.ShouldNotBe(default(ActivityContext));
		result.TraceId.ShouldBe(traceId);
		result.SpanId.ShouldBe(spanId);
	}

	[Fact]
	public async Task ReturnDefault_WhenNoHeaders()
	{
		// Arrange
		var carrier = new Dictionary<string, string>();

		// Act
		var result = await _propagator.ExtractAsync(carrier, CancellationToken.None);

		// Assert
		result.ShouldBe(default(ActivityContext));
	}

	[Fact]
	public async Task ReturnDefault_WhenDenyHeader()
	{
		// Arrange
		var carrier = new Dictionary<string, string> { ["b3"] = "0" };

		// Act
		var result = await _propagator.ExtractAsync(carrier, CancellationToken.None);

		// Assert
		result.ShouldBe(default(ActivityContext));
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
