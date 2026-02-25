// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Observability.Metrics;
using Excalibur.Dispatch.Observability.Propagation;
using Excalibur.Dispatch.Observability.Sampling;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Observability.Tests;

/// <summary>
/// Unit tests for <see cref="DispatchBuilderObservabilityExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "DependencyInjection")]
public sealed class DispatchBuilderObservabilityExtensionsShould
{
	[Fact]
	public void AddObservability_ThrowOnNullBuilder()
	{
		IDispatchBuilder builder = null!;
		Should.Throw<ArgumentNullException>(() => builder.AddObservability());
	}

	[Fact]
	public void UseTracing_ThrowOnNullBuilder()
	{
		IDispatchBuilder builder = null!;
		Should.Throw<ArgumentNullException>(() => builder.UseTracing());
	}

	[Fact]
	public void UseMetrics_ThrowOnNullBuilder()
	{
		IDispatchBuilder builder = null!;
		Should.Throw<ArgumentNullException>(() => builder.UseMetrics());
	}

	[Fact]
	public void UseOpenTelemetry_ThrowOnNullBuilder()
	{
		IDispatchBuilder builder = null!;
		Should.Throw<ArgumentNullException>(() => builder.UseOpenTelemetry());
	}

	[Fact]
	public void UseW3CTraceContext_ThrowOnNullBuilder()
	{
		IDispatchBuilder builder = null!;
		Should.Throw<ArgumentNullException>(() => builder.UseW3CTraceContext());
	}

	[Fact]
	public void UseTraceSampling_ThrowOnNullBuilder()
	{
		IDispatchBuilder builder = null!;
		Should.Throw<ArgumentNullException>(() => builder.UseTraceSampling());
	}

	[Fact]
	public void UseTracing_RegistersTracingMiddleware()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateFakeBuilder(services);

		// Act
		builder.UseTracing();

		// Assert — TracingMiddleware is registered
		services.ShouldContain(sd => sd.ServiceType == typeof(TracingMiddleware));
	}

	[Fact]
	public void UseMetrics_RegistersMetricsMiddleware()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateFakeBuilder(services);

		// Act
		builder.UseMetrics();

		// Assert — MetricsMiddleware and IDispatchMetrics are registered
		services.ShouldContain(sd => sd.ServiceType == typeof(MetricsMiddleware));
		services.ShouldContain(sd => sd.ServiceType == typeof(IDispatchMetrics));
	}

	[Fact]
	public void UseTraceSampling_RegistersSamplerServices()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateFakeBuilder(services);

		// Act
		builder.UseTraceSampling();

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(ITraceSampler));
		services.ShouldContain(sd => sd.ServiceType == typeof(TraceSamplerMiddleware));
	}

	[Fact]
	public void UseTraceSampling_AcceptsConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateFakeBuilder(services);

		// Act
		builder.UseTraceSampling(opts =>
		{
			opts.Strategy = SamplingStrategy.RatioBased;
			opts.SamplingRatio = 0.5;
		});

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(ITraceSampler));
	}

	[Fact]
	public void AddW3CTracingPropagator_ThrowOnNullServices()
	{
		IServiceCollection services = null!;
		Should.Throw<ArgumentNullException>(() => services.AddW3CTracingPropagator());
	}

	[Fact]
	public void AddW3CTracingPropagator_RegistersW3CPropagator()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddW3CTracingPropagator();

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(ITracingContextPropagator) &&
			sd.ImplementationType == typeof(W3CTracingContextPropagator));
	}

	[Fact]
	public void AddB3TracingPropagator_ThrowOnNullServices()
	{
		IServiceCollection services = null!;
		Should.Throw<ArgumentNullException>(() => services.AddB3TracingPropagator());
	}

	[Fact]
	public void AddB3TracingPropagator_RegistersB3Propagator()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddB3TracingPropagator();

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(ITracingContextPropagator) &&
			sd.ImplementationType == typeof(B3TracingContextPropagator));
	}

	private static IDispatchBuilder CreateFakeBuilder(IServiceCollection services)
	{
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);
		A.CallTo(() => builder.UseMiddleware<TracingMiddleware>()).Returns(builder);
		A.CallTo(() => builder.UseMiddleware<MetricsMiddleware>()).Returns(builder);
		A.CallTo(() => builder.UseMiddleware<TraceSamplerMiddleware>()).Returns(builder);
		return builder;
	}
}
