// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Observability.Metrics;

namespace Excalibur.Dispatch.Middleware.Tests.Observability.Metrics;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class ExemplarHistogramExtensionsShould : IDisposable
{
	private readonly Meter _meter;

	public ExemplarHistogramExtensionsShould()
	{
		_meter = new Meter("Test.ExemplarHistogram", "1.0.0");
	}

	[Fact]
	public void RecordWithExemplarTagList_ThrowsOnNullHistogram()
	{
		Should.Throw<ArgumentNullException>(() =>
			ExemplarHistogramExtensions.RecordWithExemplar(
				(Histogram<double>)null!, 1.0, new TagList()));
	}

	[Fact]
	public void RecordWithExemplarMessageType_ThrowsOnNullHistogram()
	{
		Should.Throw<ArgumentNullException>(() =>
			ExemplarHistogramExtensions.RecordWithExemplar(
				(Histogram<double>)null!, 1.0, "TestMsg", true));
	}

	[Fact]
	public void RecordWithExemplarActivityContext_ThrowsOnNullHistogram()
	{
		Should.Throw<ArgumentNullException>(() =>
			ExemplarHistogramExtensions.RecordWithExemplar(
				(Histogram<double>)null!, 1.0, default, new TagList()));
	}

	[Fact]
	public void RecordWithExemplarLong_ThrowsOnNullHistogram()
	{
		Should.Throw<ArgumentNullException>(() =>
			ExemplarHistogramExtensions.RecordWithExemplar(
				(Histogram<long>)null!, 1L, new TagList()));
	}

	[Fact]
	public void RecordWithExemplarRecordsValue()
	{
		// Arrange
		var histogram = _meter.CreateHistogram<double>("test.duration");
		var recorded = false;

		using var meterListener = new MeterListener();
		meterListener.InstrumentPublished = (instrument, listener) =>
		{
			if (instrument.Name == "test.duration")
			{
				listener.EnableMeasurementEvents(instrument);
			}
		};

		meterListener.SetMeasurementEventCallback<double>((_, value, _, _) =>
		{
			if (value > 0)
			{
				recorded = true;
			}
		});

		meterListener.Start();

		// Act
		histogram.RecordWithExemplar(42.5, new TagList());

		// Assert
		recorded.ShouldBeTrue();
	}

	[Fact]
	public void RecordWithExemplarRecordsMessageTypeOverload()
	{
		// Arrange
		var histogram = _meter.CreateHistogram<double>("test.msgtype");
		var recorded = false;

		using var meterListener = new MeterListener();
		meterListener.InstrumentPublished = (instrument, listener) =>
		{
			if (instrument.Name == "test.msgtype")
			{
				listener.EnableMeasurementEvents(instrument);
			}
		};

		meterListener.SetMeasurementEventCallback<double>((_, _, _, _) =>
		{
			recorded = true;
		});

		meterListener.Start();

		// Act
		histogram.RecordWithExemplar(10.0, "OrderCreated", true);

		// Assert
		recorded.ShouldBeTrue();
	}

	[Fact]
	public void RecordWithExemplarRecordsLongValue()
	{
		// Arrange
		var histogram = _meter.CreateHistogram<long>("test.count");
		var recorded = false;

		using var meterListener = new MeterListener();
		meterListener.InstrumentPublished = (instrument, listener) =>
		{
			if (instrument.Name == "test.count")
			{
				listener.EnableMeasurementEvents(instrument);
			}
		};

		meterListener.SetMeasurementEventCallback<long>((_, _, _, _) =>
		{
			recorded = true;
		});

		meterListener.Start();

		// Act
		histogram.RecordWithExemplar(100L, new TagList());

		// Assert
		recorded.ShouldBeTrue();
	}

	[Fact]
	public void VerifyConstantValues()
	{
		ExemplarHistogramExtensions.TraceIdTag.ShouldBe("trace_id");
		ExemplarHistogramExtensions.SpanIdTag.ShouldBe("span_id");
	}

	public void Dispose()
	{
		_meter.Dispose();
	}
}
