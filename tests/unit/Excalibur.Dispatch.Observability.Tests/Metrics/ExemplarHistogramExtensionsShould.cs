// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Observability.Metrics;

namespace Excalibur.Dispatch.Observability.Tests.Metrics;

/// <summary>
/// Unit tests for <see cref="ExemplarHistogramExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Metrics")]
public sealed class ExemplarHistogramExtensionsShould : IDisposable
{
	private readonly Meter _meter;
	private readonly Histogram<double> _doubleHistogram;
	private readonly Histogram<long> _longHistogram;

	public ExemplarHistogramExtensionsShould()
	{
		_meter = new Meter("Test.Exemplars");
		_doubleHistogram = _meter.CreateHistogram<double>("test.duration");
		_longHistogram = _meter.CreateHistogram<long>("test.count");
	}

	public void Dispose() => _meter.Dispose();

	[Fact]
	public void HaveCorrectTraceIdTagConstant()
	{
		ExemplarHistogramExtensions.TraceIdTag.ShouldBe("trace_id");
	}

	[Fact]
	public void HaveCorrectSpanIdTagConstant()
	{
		ExemplarHistogramExtensions.SpanIdTag.ShouldBe("span_id");
	}

	[Fact]
	public void RecordDoubleWithExemplar_WithoutActivity()
	{
		// Arrange
		var tags = new TagList { { "operation", "test" } };

		// Act — should not throw even without an active activity
		_doubleHistogram.RecordWithExemplar(42.5, tags);
	}

	[Fact]
	public void RecordDoubleWithExemplar_WithMessageTypeAndSuccess()
	{
		// Act — should not throw
		_doubleHistogram.RecordWithExemplar(100.0, "TestMessage", success: true);
	}

	[Fact]
	public void RecordDoubleWithExemplar_WithActivityContext()
	{
		// Arrange
		var activityContext = new ActivityContext(
			ActivityTraceId.CreateRandom(),
			ActivitySpanId.CreateRandom(),
			ActivityTraceFlags.Recorded);
		var tags = new TagList { { "operation", "test" } };

		// Act — should not throw
		_doubleHistogram.RecordWithExemplar(200.0, activityContext, tags);
	}

	[Fact]
	public void RecordDoubleWithExemplar_WithDefaultActivityContext()
	{
		// Arrange
		var tags = new TagList { { "operation", "test" } };

		// Act — should not throw with default context
		_doubleHistogram.RecordWithExemplar(100.0, default, tags);
	}

	[Fact]
	public void RecordLongWithExemplar_WithoutActivity()
	{
		// Arrange
		var tags = new TagList { { "operation", "test" } };

		// Act — should not throw
		_longHistogram.RecordWithExemplar(42L, tags);
	}

	[Fact]
	public void ThrowOnNullHistogram_DoubleTagList()
	{
		Should.Throw<ArgumentNullException>(() =>
			ExemplarHistogramExtensions.RecordWithExemplar(null!, 1.0, new TagList()));
	}

	[Fact]
	public void ThrowOnNullHistogram_DoubleMessageType()
	{
		Should.Throw<ArgumentNullException>(() =>
			ExemplarHistogramExtensions.RecordWithExemplar(null!, 1.0, "msg", true));
	}

	[Fact]
	public void ThrowOnNullHistogram_LongTagList()
	{
		Should.Throw<ArgumentNullException>(() =>
			ExemplarHistogramExtensions.RecordWithExemplar((Histogram<long>)null!, 1L, new TagList()));
	}
}
