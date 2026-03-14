// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Buffers;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Serialization;

namespace Excalibur.Dispatch.Tests.Serialization;

[Trait("Category", "Unit")]
public sealed class Utf8JsonWriterPoolMetricsShould : IDisposable
{
	private readonly Meter _meter = new("Test.Utf8JsonWriterPool");

	public void Dispose()
	{
		_meter.Dispose();
	}

	[Fact]
	public void ExposeSerializationMeterName()
	{
		Utf8JsonWriterPool.MeterName.ShouldBe("Excalibur.Dispatch.Serialization");
	}

	[Fact]
	public void CreateCounterInstruments_WhenMeterProvided()
	{
		var instrumentNames = new List<string>();
		using var listener = new MeterListener();
		listener.InstrumentPublished = (instrument, meterListener) =>
		{
			if (ReferenceEquals(instrument.Meter, _meter))
			{
				instrumentNames.Add(instrument.Name);
				meterListener.EnableMeasurementEvents(instrument);
			}
		};
		listener.Start();

		using var pool = new Utf8JsonWriterPool(meter: _meter);

		instrumentNames.ShouldContain("jsonwriter.pool.rent.threadlocal");
		instrumentNames.ShouldContain("jsonwriter.pool.rent.global");
		instrumentNames.ShouldContain("jsonwriter.pool.return.threadlocal");
		instrumentNames.ShouldContain("jsonwriter.pool.return.global");
	}

	[Fact]
	public void CreateObservableGaugeInstruments_WhenMeterProvided()
	{
		var instrumentNames = new List<string>();
		using var listener = new MeterListener();
		listener.InstrumentPublished = (instrument, meterListener) =>
		{
			if (ReferenceEquals(instrument.Meter, _meter))
			{
				instrumentNames.Add(instrument.Name);
				meterListener.EnableMeasurementEvents(instrument);
			}
		};
		listener.Start();

		using var pool = new Utf8JsonWriterPool(meter: _meter);

		instrumentNames.ShouldContain("jsonwriter.pool.size.current");
		instrumentNames.ShouldContain("jsonwriter.pool.size.max");
		instrumentNames.ShouldContain("jsonwriter.pool.size.peak");
	}

	[Fact]
	public void NotCreateInstruments_WhenMeterIsNull()
	{
		// Should not throw when meter is null
		using var pool = new Utf8JsonWriterPool(meter: null);

		// Pool should still function without telemetry
		var buffer = new ArrayBufferWriter<byte>();
		var writer = pool.Rent(buffer);
		pool.ReturnToPool(writer);
	}

	[Fact]
	public void NotCreateInstruments_WhenTelemetryDisabled()
	{
		var instrumentNames = new List<string>();
		using var listener = new MeterListener();
		listener.InstrumentPublished = (instrument, meterListener) =>
		{
			if (ReferenceEquals(instrument.Meter, _meter))
			{
				instrumentNames.Add(instrument.Name);
			}
		};
		listener.Start();

		using var pool = new Utf8JsonWriterPool(enableTelemetry: false, meter: _meter);

		instrumentNames.ShouldBeEmpty();
	}

	[Fact]
	public void RecordRentalMetrics_WhenMeterProvided()
	{
		long globalRentCount = 0;
		using var listener = new MeterListener();
		listener.InstrumentPublished = (instrument, meterListener) =>
		{
			if (ReferenceEquals(instrument.Meter, _meter) && instrument.Name == "jsonwriter.pool.rent.global")
			{
				meterListener.EnableMeasurementEvents(instrument);
			}
		};
		listener.SetMeasurementEventCallback<long>((instrument, measurement, _, _) =>
		{
			if (instrument.Name == "jsonwriter.pool.rent.global")
			{
				Interlocked.Add(ref globalRentCount, measurement);
			}
		});
		listener.Start();

		// threadLocalCacheSize=0 forces global path
		using var pool = new Utf8JsonWriterPool(threadLocalCacheSize: 0, meter: _meter);
		var buffer = new ArrayBufferWriter<byte>();

		var writer = pool.Rent(buffer);
		pool.ReturnToPool(writer);

		listener.RecordObservableInstruments();

		Interlocked.Read(ref globalRentCount).ShouldBeGreaterThan(0);
	}
}
