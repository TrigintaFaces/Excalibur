// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Metrics;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Middleware.Tests.Observability.Metrics;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Platform")]
public sealed class ObservabilityMetricsServiceCollectionExtensionsShould
{
	[Fact]
	public void AddDispatchMetricsInstrumentationRegistersServices()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDispatchMetricsInstrumentation();

		// Assert
		var provider = services.BuildServiceProvider();
		provider.GetService<IDispatchMetrics>().ShouldNotBeNull();
		provider.GetService<DispatchMetrics>().ShouldNotBeNull();
	}

	[Fact]
	public void AddDispatchMetricsInstrumentationThrowsOnNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			((IServiceCollection)null!).AddDispatchMetricsInstrumentation());
	}

	[Fact]
	public void AddCircuitBreakerMetricsRegistersServices()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddCircuitBreakerMetrics();

		// Assert
		var provider = services.BuildServiceProvider();
		provider.GetService<ICircuitBreakerMetrics>().ShouldNotBeNull();
	}

	[Fact]
	public void AddCircuitBreakerMetricsThrowsOnNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			((IServiceCollection)null!).AddCircuitBreakerMetrics());
	}

	[Fact]
	public void AddDeadLetterQueueMetricsRegistersServices()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDeadLetterQueueMetrics();

		// Assert
		var provider = services.BuildServiceProvider();
		provider.GetService<IDeadLetterQueueMetrics>().ShouldNotBeNull();
	}

	[Fact]
	public void AddDeadLetterQueueMetricsThrowsOnNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			((IServiceCollection)null!).AddDeadLetterQueueMetrics());
	}

	[Fact]
	public void AddAllDispatchMetricsRegistersAllServices()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddAllDispatchMetrics();

		// Assert
		var provider = services.BuildServiceProvider();
		provider.GetService<IDispatchMetrics>().ShouldNotBeNull();
		provider.GetService<ICircuitBreakerMetrics>().ShouldNotBeNull();
		provider.GetService<IDeadLetterQueueMetrics>().ShouldNotBeNull();
	}

	[Fact]
	public void AddAllDispatchMetricsThrowsOnNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			((IServiceCollection)null!).AddAllDispatchMetrics());
	}

	[Fact]
	public void AddDispatchMetricsWithConfigureRegistersAndConfigures()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDispatchMetricsInstrumentation(options =>
		{
			options.EnableMetrics = false;
		});

		// Assert
		var provider = services.BuildServiceProvider();
		provider.GetService<IDispatchMetrics>().ShouldNotBeNull();
		var opts = provider.GetRequiredService<IOptions<ObservabilityOptions>>().Value;
		opts.EnableMetrics.ShouldBeFalse();
	}

	[Fact]
	public void AddDispatchMetricsWithConfigureThrowsOnNullServices()
	{
		Should.Throw<ArgumentNullException>(() =>
			((IServiceCollection)null!).AddDispatchMetricsInstrumentation(_ => { }));
	}

	[Fact]
	public void AddDispatchMetricsWithConfigureThrowsOnNullConfigure()
	{
		var services = new ServiceCollection();
		Should.Throw<ArgumentNullException>(() =>
			services.AddDispatchMetricsInstrumentation((Action<ObservabilityOptions>)null!));
	}
}
