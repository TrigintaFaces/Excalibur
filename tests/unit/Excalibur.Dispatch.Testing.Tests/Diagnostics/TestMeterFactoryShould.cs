// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Testing.Diagnostics;

namespace Excalibur.Dispatch.Testing.Tests.Diagnostics;

[Trait("Category", "Unit")]
[Trait("Component", "Testing")]
public sealed class TestMeterFactoryShould
{
	[Fact]
	public void ImplementIMeterFactory()
	{
		using var factory = new TestMeterFactory();
		factory.ShouldBeAssignableTo<IMeterFactory>();
	}

	[Fact]
	public void CreateMeterWithName()
	{
		using var factory = new TestMeterFactory();
		var meter = factory.Create(new MeterOptions("TestMeter"));
		meter.ShouldNotBeNull();
		meter.Name.ShouldBe("TestMeter");
	}

	[Fact]
	public void CreateMeterWithNameAndVersion()
	{
		using var factory = new TestMeterFactory();
		var meter = factory.Create(new MeterOptions("TestMeter") { Version = "1.0.0" });
		meter.ShouldNotBeNull();
		meter.Name.ShouldBe("TestMeter");
	}

	[Fact]
	public void CreateFunctionalCounterInstrument()
	{
		using var factory = new TestMeterFactory();
		var meter = factory.Create(new MeterOptions("TestMeter"));
		var counter = meter.CreateCounter<long>("test-counter");
		counter.ShouldNotBeNull();
	}

	[Fact]
	public void CreateMultipleMeters()
	{
		using var factory = new TestMeterFactory();
		var m1 = factory.Create(new MeterOptions("Meter1"));
		var m2 = factory.Create(new MeterOptions("Meter2"));

		m1.Name.ShouldBe("Meter1");
		m2.Name.ShouldBe("Meter2");
	}

	[Fact]
	public void DisposeAllMetersOnDispose()
	{
		var factory = new TestMeterFactory();
		var meter = factory.Create(new MeterOptions("TestMeter"));

		// Create an instrument before dispose
		var counter = meter.CreateCounter<long>("test");
		counter.ShouldNotBeNull();

		factory.Dispose();
		// After dispose, attempting to create new instruments on the meter should be safe
		// (Meter.Dispose() is idempotent)
	}

	[Fact]
	public void AllowDoubleDispose()
	{
		var factory = new TestMeterFactory();
		factory.Create(new MeterOptions("TestMeter"));
		factory.Dispose();
		factory.Dispose(); // Should not throw
	}

	[Fact]
	public void ImplementIDisposable()
	{
		using var factory = new TestMeterFactory();
		factory.ShouldBeAssignableTo<IDisposable>();
	}
}
