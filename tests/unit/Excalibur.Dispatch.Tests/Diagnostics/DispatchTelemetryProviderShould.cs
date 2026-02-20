// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Diagnostics;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Tests.Diagnostics;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DispatchTelemetryProviderShould : IDisposable
{
	private readonly DispatchTelemetryProvider _provider;

	public DispatchTelemetryProviderShould()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new DispatchTelemetryOptions());
		_provider = new DispatchTelemetryProvider(options);
	}

	[Fact]
	public void GetActivitySource_ReturnActivitySource()
	{
		// Act
		var source = _provider.GetActivitySource("Test.Component");

		// Assert
		source.ShouldNotBeNull();
		source.Name.ShouldBe("Test.Component");
	}

	[Fact]
	public void GetActivitySource_ReturnSameInstanceForSameName()
	{
		// Act
		var source1 = _provider.GetActivitySource("Test.Component");
		var source2 = _provider.GetActivitySource("Test.Component");

		// Assert
		source1.ShouldBeSameAs(source2);
	}

	[Fact]
	public void GetActivitySource_ReturnDifferentInstancesForDifferentNames()
	{
		// Act
		var source1 = _provider.GetActivitySource("Component.A");
		var source2 = _provider.GetActivitySource("Component.B");

		// Assert
		source1.ShouldNotBeSameAs(source2);
	}

	[Fact]
	public void GetActivitySource_ThrowOnNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => _provider.GetActivitySource(null!));
	}

	[Fact]
	public void GetActivitySource_WhenTracingDisabled_ReturnNoOpSource()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(
			new DispatchTelemetryOptions { EnableTracing = false });
		using var provider = new DispatchTelemetryProvider(options);

		// Act
		var source = provider.GetActivitySource("Test.Component");

		// Assert
		source.ShouldNotBeNull();
		source.Name.ShouldContain("NoOp");
	}

	[Fact]
	public void GetMeter_ReturnMeter()
	{
		// Act
		var meter = _provider.GetMeter("Test.Component");

		// Assert
		meter.ShouldNotBeNull();
		meter.Name.ShouldBe("Test.Component");
	}

	[Fact]
	public void GetMeter_ReturnSameInstanceForSameName()
	{
		// Act
		var meter1 = _provider.GetMeter("Test.Component");
		var meter2 = _provider.GetMeter("Test.Component");

		// Assert
		meter1.ShouldBeSameAs(meter2);
	}

	[Fact]
	public void GetMeter_ThrowOnNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => _provider.GetMeter(null!));
	}

	[Fact]
	public void GetMeter_WhenMetricsDisabled_ReturnMeter()
	{
		// Arrange - even when disabled, we still return a meter (it just won't collect)
		var options = Microsoft.Extensions.Options.Options.Create(
			new DispatchTelemetryOptions { EnableMetrics = false });
		using var provider = new DispatchTelemetryProvider(options);

		// Act
		var meter = provider.GetMeter("Test.Component");

		// Assert
		meter.ShouldNotBeNull();
	}

	[Fact]
	public void GetOptions_ReturnConfiguredOptions()
	{
		// Act
		var options = _provider.GetOptions();

		// Assert
		options.ShouldNotBeNull();
		options.ServiceName.ShouldBe("Excalibur.Dispatch");
	}

	[Fact]
	public void ThrowOnNullOptions()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => new DispatchTelemetryProvider(null!));
	}

	[Fact]
	public void ThrowAfterDispose_GetActivitySource()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new DispatchTelemetryOptions());
		var provider = new DispatchTelemetryProvider(options);
		provider.Dispose();

		// Act & Assert
		Should.Throw<ObjectDisposedException>(() => provider.GetActivitySource("Test"));
	}

	[Fact]
	public void ThrowAfterDispose_GetMeter()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new DispatchTelemetryOptions());
		var provider = new DispatchTelemetryProvider(options);
		provider.Dispose();

		// Act & Assert
		Should.Throw<ObjectDisposedException>(() => provider.GetMeter("Test"));
	}

	[Fact]
	public void ThrowAfterDispose_GetOptions()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new DispatchTelemetryOptions());
		var provider = new DispatchTelemetryProvider(options);
		provider.Dispose();

		// Act & Assert
		Should.Throw<ObjectDisposedException>(() => provider.GetOptions());
	}

	[Fact]
	public void DisposeIdempotently()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new DispatchTelemetryOptions());
		var provider = new DispatchTelemetryProvider(options);

		// Act & Assert - should not throw
		provider.Dispose();
		provider.Dispose();
	}

	[Fact]
	public void AcceptMeterFactory()
	{
		// Arrange
		var meterFactory = A.Fake<System.Diagnostics.Metrics.IMeterFactory>();
		var options = Microsoft.Extensions.Options.Options.Create(new DispatchTelemetryOptions());

		// Act & Assert - should not throw
		using var provider = new DispatchTelemetryProvider(options, meterFactory);
		provider.ShouldNotBeNull();
	}

	public void Dispose()
	{
		_provider.Dispose();
	}
}
