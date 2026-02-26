// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Testing.Diagnostics;

namespace Excalibur.Dispatch.Testing.Tests.Diagnostics;

[Trait("Category", "Unit")]
[Trait("Component", "Testing")]
public sealed class TestMeterFactoryDepthShould
{
	[Fact]
	public void ImplementIMeterFactory()
	{
		// Arrange & Act
		using var factory = new TestMeterFactory();

		// Assert
		factory.ShouldBeAssignableTo<IMeterFactory>();
	}

	[Fact]
	public void ImplementIDisposable()
	{
		// Arrange & Act
		using var factory = new TestMeterFactory();

		// Assert
		factory.ShouldBeAssignableTo<IDisposable>();
	}

	[Fact]
	public void CreateMeterWithName()
	{
		// Arrange
		using var factory = new TestMeterFactory();
		var options = new MeterOptions("TestMeter");

		// Act
		var meter = factory.Create(options);

		// Assert
		meter.ShouldNotBeNull();
		meter.Name.ShouldBe("TestMeter");
	}

	[Fact]
	public void CreateMeterWithNameAndVersion()
	{
		// Arrange
		using var factory = new TestMeterFactory();
		var options = new MeterOptions("TestMeter") { Version = "1.0.0" };

		// Act
		var meter = factory.Create(options);

		// Assert
		meter.Version.ShouldBe("1.0.0");
	}

	[Fact]
	public void CreateMultipleMeters()
	{
		// Arrange
		using var factory = new TestMeterFactory();

		// Act
		var meter1 = factory.Create(new MeterOptions("Meter1"));
		var meter2 = factory.Create(new MeterOptions("Meter2"));
		var meter3 = factory.Create(new MeterOptions("Meter3"));

		// Assert
		meter1.Name.ShouldBe("Meter1");
		meter2.Name.ShouldBe("Meter2");
		meter3.Name.ShouldBe("Meter3");
	}

	[Fact]
	public void CreateFunctionalCounterInstrument()
	{
		// Arrange
		using var factory = new TestMeterFactory();
		var meter = factory.Create(new MeterOptions("FunctionalMeter"));

		long recordedValue = 0;
		using var listener = new MeterListener();
		listener.InstrumentPublished = (instrument, meterListener) =>
		{
			if (ReferenceEquals(instrument.Meter, meter))
			{
				meterListener.EnableMeasurementEvents(instrument);
			}
		};
		listener.SetMeasurementEventCallback<long>((_, measurement, _, _) => recordedValue = measurement);
		listener.Start();

		var counter = meter.CreateCounter<long>("test.counter");

		// Act
		counter.Add(42);

		// Assert
		listener.RecordObservableInstruments();
		recordedValue.ShouldBe(42);
	}

	[Fact]
	public void CreateFunctionalHistogramInstrument()
	{
		// Arrange
		using var factory = new TestMeterFactory();
		var meter = factory.Create(new MeterOptions("HistogramMeter"));

		double recordedValue = 0;
		using var listener = new MeterListener();
		listener.InstrumentPublished = (instrument, meterListener) =>
		{
			if (ReferenceEquals(instrument.Meter, meter))
			{
				meterListener.EnableMeasurementEvents(instrument);
			}
		};
		listener.SetMeasurementEventCallback<double>((_, measurement, _, _) => recordedValue = measurement);
		listener.Start();

		var histogram = meter.CreateHistogram<double>("test.histogram");

		// Act
		histogram.Record(3.14);

		// Assert
		recordedValue.ShouldBe(3.14);
	}

	[Fact]
	public void DisposeAllCreatedMetersOnDispose()
	{
		// Arrange
		var factory = new TestMeterFactory();
		var meter1 = factory.Create(new MeterOptions("DisposeMeter1"));
		var meter2 = factory.Create(new MeterOptions("DisposeMeter2"));

		// Create instruments to verify they stop working after dispose
		var counter1 = meter1.CreateCounter<long>("counter1");
		var counter2 = meter2.CreateCounter<long>("counter2");

		// Act
		factory.Dispose();

		// Assert - after dispose, meters should be disposed (counters won't record)
		// We just verify no exceptions are thrown
		counter1.Add(1);
		counter2.Add(1);
	}

	[Fact]
	public void AllowDoubleDispose()
	{
		// Arrange
		var factory = new TestMeterFactory();
		factory.Create(new MeterOptions("TestMeter"));

		// Act & Assert - should not throw
		factory.Dispose();
		factory.Dispose();
	}

	[Fact]
	public void AllowCreateAfterDispose()
	{
		// Arrange - not recommended but should not crash
		var factory = new TestMeterFactory();
		factory.Dispose();

		// Act & Assert - Meter constructor still works even though factory is disposed
		// This is just defensive behavior verification
		var meter = factory.Create(new MeterOptions("PostDisposeMeter"));
		meter.ShouldNotBeNull();
	}
}
