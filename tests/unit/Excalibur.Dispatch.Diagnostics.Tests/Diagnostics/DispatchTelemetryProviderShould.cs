// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Diagnostics;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Tests.Diagnostics;

public sealed class DispatchTelemetryProviderShould : IDisposable
{
	private readonly DispatchTelemetryProvider _sut;

	public DispatchTelemetryProviderShould()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new DispatchTelemetryOptions());
		_sut = new DispatchTelemetryProvider(options);
	}

	[Fact]
	public void ThrowOnNullOptions()
	{
		Should.Throw<ArgumentNullException>(() => new DispatchTelemetryProvider(null!));
	}

	[Fact]
	public void ReturnActivitySourceForComponent()
	{
		var source = _sut.GetActivitySource("TestComponent");

		source.ShouldNotBeNull();
		source.Name.ShouldBe("TestComponent");
	}

	[Fact]
	public void ReturnSameActivitySourceForSameComponent()
	{
		var source1 = _sut.GetActivitySource("TestComponent");
		var source2 = _sut.GetActivitySource("TestComponent");

		source1.ShouldBeSameAs(source2);
	}

	[Fact]
	public void ReturnDifferentActivitySourcesForDifferentComponents()
	{
		var source1 = _sut.GetActivitySource("Component1");
		var source2 = _sut.GetActivitySource("Component2");

		source1.ShouldNotBeSameAs(source2);
		source1.Name.ShouldBe("Component1");
		source2.Name.ShouldBe("Component2");
	}

	[Fact]
	public void ReturnNoOpActivitySourceWhenTracingDisabled()
	{
		var options = Microsoft.Extensions.Options.Options.Create(
			new DispatchTelemetryOptions { EnableTracing = false });
		using var provider = new DispatchTelemetryProvider(options);

		var source = provider.GetActivitySource("TestComponent");

		source.ShouldNotBeNull();
		source.Name.ShouldBe("TestComponent.NoOp");
	}

	[Fact]
	public void CacheNoOpActivitySourceWhenTracingDisabled()
	{
		var options = Microsoft.Extensions.Options.Options.Create(
			new DispatchTelemetryOptions { EnableTracing = false });
		using var provider = new DispatchTelemetryProvider(options);

		var source1 = provider.GetActivitySource("TestComponent");
		var source2 = provider.GetActivitySource("TestComponent");

		source1.ShouldBeSameAs(source2);
	}

	[Fact]
	public void ReturnMeterForComponent()
	{
		var meter = _sut.GetMeter("TestComponent");

		meter.ShouldNotBeNull();
		meter.Name.ShouldBe("TestComponent");
	}

	[Fact]
	public void ReturnSameMeterForSameComponent()
	{
		var meter1 = _sut.GetMeter("TestComponent");
		var meter2 = _sut.GetMeter("TestComponent");

		meter1.ShouldBeSameAs(meter2);
	}

	[Fact]
	public void ReturnDifferentMetersForDifferentComponents()
	{
		var meter1 = _sut.GetMeter("Component1");
		var meter2 = _sut.GetMeter("Component2");

		meter1.ShouldNotBeSameAs(meter2);
		meter1.Name.ShouldBe("Component1");
		meter2.Name.ShouldBe("Component2");
	}

	[Fact]
	public void CacheNoOpMeterWhenMetricsDisabled()
	{
		var options = Microsoft.Extensions.Options.Options.Create(
			new DispatchTelemetryOptions { EnableMetrics = false });
		using var provider = new DispatchTelemetryProvider(options);

		var meter1 = provider.GetMeter("TestComponent");
		var meter2 = provider.GetMeter("TestComponent");

		meter1.ShouldBeSameAs(meter2);
	}

	[Fact]
	public void ReturnConfiguredOptions()
	{
		var options = _sut.GetOptions();

		options.ShouldNotBeNull();
		options.ServiceName.ShouldBe("Excalibur.Dispatch");
	}

	[Fact]
	public void SetVersionFromOptions()
	{
		var options = Microsoft.Extensions.Options.Options.Create(
			new DispatchTelemetryOptions { ServiceVersion = "2.0.0" });
		using var provider = new DispatchTelemetryProvider(options);

		var source = provider.GetActivitySource("TestComponent");

		source.Version.ShouldBe("2.0.0");
	}

	[Fact]
	public void ThrowOnNullComponentNameForActivitySource()
	{
		Should.Throw<ArgumentNullException>(() => _sut.GetActivitySource(null!));
	}

	[Fact]
	public void ThrowOnNullComponentNameForMeter()
	{
		Should.Throw<ArgumentNullException>(() => _sut.GetMeter(null!));
	}

	[Fact]
	public void ThrowAfterDispose()
	{
		_sut.Dispose();

		Should.Throw<ObjectDisposedException>(() => _sut.GetActivitySource("Test"));
		Should.Throw<ObjectDisposedException>(() => _sut.GetMeter("Test"));
		Should.Throw<ObjectDisposedException>(() => _sut.GetOptions());
	}

	[Fact]
	public void NotThrowOnDoubleDispose()
	{
		_sut.Dispose();

		Should.NotThrow(() => _sut.Dispose());
	}

	public void Dispose() => _sut.Dispose();
}
