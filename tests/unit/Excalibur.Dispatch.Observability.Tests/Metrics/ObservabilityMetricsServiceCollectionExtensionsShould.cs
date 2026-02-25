// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Metrics;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Observability.Tests.Metrics;

/// <summary>
/// Unit tests for <see cref="ObservabilityMetricsServiceCollectionExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "DI")]
public sealed class ObservabilityMetricsServiceCollectionExtensionsShould
{
	[Fact]
	public void RegisterDispatchMetrics()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDispatchMetricsInstrumentation();

		// Assert
		var provider = services.BuildServiceProvider();
		var metrics = provider.GetService<IDispatchMetrics>();
		metrics.ShouldNotBeNull();
		(metrics as IDisposable)?.Dispose();
	}

	[Fact]
	public void RegisterCircuitBreakerMetrics()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddCircuitBreakerMetrics();

		// Assert
		var provider = services.BuildServiceProvider();
		var metrics = provider.GetService<ICircuitBreakerMetrics>();
		metrics.ShouldNotBeNull();
		(metrics as IDisposable)?.Dispose();
	}

	[Fact]
	public void RegisterDeadLetterQueueMetrics()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDeadLetterQueueMetrics();

		// Assert
		var provider = services.BuildServiceProvider();
		var metrics = provider.GetService<IDeadLetterQueueMetrics>();
		metrics.ShouldNotBeNull();
		(metrics as IDisposable)?.Dispose();
	}

	[Fact]
	public void RegisterAllMetrics_ViaAddAllDispatchMetrics()
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
	public void RegisterDispatchMetricsWithConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDispatchMetricsInstrumentation(options =>
		{
			options.EnableDetailedTiming = true;
		});

		// Assert
		var provider = services.BuildServiceProvider();
		provider.GetService<IDispatchMetrics>().ShouldNotBeNull();
	}

	[Fact]
	public void ThrowOnNullServices_ForAddDispatchMetricsInstrumentation()
	{
		Should.Throw<ArgumentNullException>(() =>
			ObservabilityMetricsServiceCollectionExtensions.AddDispatchMetricsInstrumentation(null!));
	}

	[Fact]
	public void ThrowOnNullServices_ForAddCircuitBreakerMetrics()
	{
		Should.Throw<ArgumentNullException>(() =>
			ObservabilityMetricsServiceCollectionExtensions.AddCircuitBreakerMetrics(null!));
	}

	[Fact]
	public void ThrowOnNullServices_ForAddDeadLetterQueueMetrics()
	{
		Should.Throw<ArgumentNullException>(() =>
			ObservabilityMetricsServiceCollectionExtensions.AddDeadLetterQueueMetrics(null!));
	}

	[Fact]
	public void ThrowOnNullServices_ForAddAllDispatchMetrics()
	{
		Should.Throw<ArgumentNullException>(() =>
			ObservabilityMetricsServiceCollectionExtensions.AddAllDispatchMetrics(null!));
	}
}
