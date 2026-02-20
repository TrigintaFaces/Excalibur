// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.Metrics;
using Excalibur.Domain.Metrics;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Tests.Domain.Metrics;

/// <summary>
/// Unit tests for <see cref="MetricsServiceCollectionExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Domain")]
public sealed class MetricsServiceCollectionExtensionsShould
{
	#region AddExcaliburMetrics Tests

	[Fact]
	public void AddExcaliburMetrics_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddExcaliburMetrics());
	}

	[Fact]
	public void AddExcaliburMetrics_RegistersIMeterFactory()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburMetrics();

		// Assert
		var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IMeterFactory));
		descriptor.ShouldNotBeNull();
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
	}

	[Fact]
	public void AddExcaliburMetrics_RegistersIMetrics()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburMetrics();

		// Assert
		var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IMetrics));
		descriptor.ShouldNotBeNull();
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
	}

	[Fact]
	public void AddExcaliburMetrics_RegistersOpenTelemetryMetrics_AsIMetricsImplementation()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddExcaliburMetrics();

		// Assert
		var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IMetrics));
		descriptor.ShouldNotBeNull();
		descriptor.ImplementationType.ShouldBe(typeof(OpenTelemetryMetrics));
	}

	[Fact]
	public void AddExcaliburMetrics_ReturnsSameServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddExcaliburMetrics();

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddExcaliburMetrics_IMeterFactory_IsResolvable()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddExcaliburMetrics();

		// Act
		using var provider = services.BuildServiceProvider();
		var meterFactory = provider.GetService<IMeterFactory>();

		// Assert
		meterFactory.ShouldNotBeNull();
	}

	[Fact]
	public void AddExcaliburMetrics_IMetrics_IsResolvable()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddExcaliburMetrics();

		// Act
		using var provider = services.BuildServiceProvider();
		var metrics = provider.GetService<IMetrics>();

		// Assert
		metrics.ShouldNotBeNull();
	}

	[Fact]
	public void AddExcaliburMetrics_DoesNotReplaceExistingIMeterFactory()
	{
		// Arrange
		var services = new ServiceCollection();
		var existingFactory = A.Fake<IMeterFactory>();
		services.AddSingleton(existingFactory);

		// Act
		services.AddExcaliburMetrics();

		// Assert
		using var provider = services.BuildServiceProvider();
		var resolvedFactory = provider.GetService<IMeterFactory>();
		resolvedFactory.ShouldBeSameAs(existingFactory);
	}

	[Fact]
	public void AddExcaliburMetrics_DoesNotReplaceExistingIMetrics()
	{
		// Arrange
		var services = new ServiceCollection();
		var existingMetrics = A.Fake<IMetrics>();
		services.AddSingleton(existingMetrics);
		services.AddSingleton(A.Fake<IMeterFactory>()); // Required for OpenTelemetryMetrics if it gets registered

		// Act
		services.AddExcaliburMetrics();

		// Assert
		using var provider = services.BuildServiceProvider();
		var resolvedMetrics = provider.GetService<IMetrics>();
		resolvedMetrics.ShouldBeSameAs(existingMetrics);
	}

	[Fact]
	public void AddExcaliburMetrics_MeterFactoryCanCreateMeters()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddExcaliburMetrics();
		using var provider = services.BuildServiceProvider();
		var meterFactory = provider.GetRequiredService<IMeterFactory>();

		// Act
		var meter = meterFactory.Create(new MeterOptions("TestMeter"));

		// Assert
		meter.ShouldNotBeNull();
		meter.Name.ShouldBe("TestMeter");
	}

	[Fact]
	public void AddExcaliburMetrics_MeterFactoryReturnsSameMeterForSameName()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddExcaliburMetrics();
		using var provider = services.BuildServiceProvider();
		var meterFactory = provider.GetRequiredService<IMeterFactory>();

		// Act
		var meter1 = meterFactory.Create(new MeterOptions("TestMeter"));
		var meter2 = meterFactory.Create(new MeterOptions("TestMeter"));

		// Assert
		meter1.ShouldBeSameAs(meter2);
	}

	[Fact]
	public void AddExcaliburMetrics_MeterFactoryCreatesDifferentMetersForDifferentNames()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddExcaliburMetrics();
		using var provider = services.BuildServiceProvider();
		var meterFactory = provider.GetRequiredService<IMeterFactory>();

		// Act
		var meter1 = meterFactory.Create(new MeterOptions("Meter1"));
		var meter2 = meterFactory.Create(new MeterOptions("Meter2"));

		// Assert
		meter1.ShouldNotBeSameAs(meter2);
	}

	[Fact]
	public void AddExcaliburMetrics_MetricsCanRecordCounter()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddExcaliburMetrics();
		using var provider = services.BuildServiceProvider();
		var metrics = provider.GetRequiredService<IMetrics>();

		// Act & Assert - Should not throw
		Should.NotThrow(() => metrics.RecordCounter("test_counter", 1));
	}

	[Fact]
	public void AddExcaliburMetrics_MetricsCanRecordHistogram()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddExcaliburMetrics();
		using var provider = services.BuildServiceProvider();
		var metrics = provider.GetRequiredService<IMetrics>();

		// Act & Assert - Should not throw
		Should.NotThrow(() => metrics.RecordHistogram("test_histogram", 123.45));
	}

	[Fact]
	public void AddExcaliburMetrics_CalledMultipleTimes_DoesNotThrow()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.NotThrow(() =>
		{
			services.AddExcaliburMetrics();
			services.AddExcaliburMetrics();
		});
	}

	[Fact]
	public void AddExcaliburMetrics_IMetricsIsSingleton()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddExcaliburMetrics();
		using var provider = services.BuildServiceProvider();

		// Act
		var metrics1 = provider.GetRequiredService<IMetrics>();
		var metrics2 = provider.GetRequiredService<IMetrics>();

		// Assert
		metrics1.ShouldBeSameAs(metrics2);
	}

	#endregion AddExcaliburMetrics Tests
}
