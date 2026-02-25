// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Observability.Metrics;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Excalibur.Dispatch.Observability.Tests;

/// <summary>
/// Unit tests for the UseOpenTelemetry() convenience method in
/// <see cref="DispatchBuilderObservabilityExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "OpenTelemetry")]
public sealed class UseOpenTelemetryShould
{
	#region Null argument tests

	[Fact]
	public void ThrowOnNullBuilder()
	{
		// Arrange
		IDispatchBuilder builder = null!;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => builder.UseOpenTelemetry());
	}

	#endregion

	#region Fluent chaining tests

	[Fact]
	public void ReturnBuilderForFluentChaining()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateMockDispatchBuilder(services);

		// Act
		var result = builder.UseOpenTelemetry();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void SupportChainedCalls()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateMockDispatchBuilder(services);

		// Act & Assert - Should not throw when chaining
		var result = builder
			.UseOpenTelemetry()
			.UseOpenTelemetry(); // Calling twice should be safe

		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region Service registration tests

	[Fact]
	public void RegisterTracingMiddleware()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateMockDispatchBuilder(services);

		// Act
		builder.UseOpenTelemetry();

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(TracingMiddleware));
	}

	[Fact]
	public void RegisterMetricsMiddleware()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateMockDispatchBuilder(services);

		// Act
		builder.UseOpenTelemetry();

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(MetricsMiddleware));
	}

	[Fact]
	public void RegisterDispatchMetrics()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateMockDispatchBuilder(services);

		// Act
		builder.UseOpenTelemetry();

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(IDispatchMetrics));
	}

	[Fact]
	public void RegisterTracingMiddlewareAsSingleton()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateMockDispatchBuilder(services);

		// Act
		builder.UseOpenTelemetry();

		// Assert
		var tracingDescriptor = services.FirstOrDefault(sd => sd.ServiceType == typeof(TracingMiddleware));
		tracingDescriptor.ShouldNotBeNull();
		tracingDescriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
	}

	[Fact]
	public void RegisterMetricsMiddlewareAsSingleton()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateMockDispatchBuilder(services);

		// Act
		builder.UseOpenTelemetry();

		// Assert
		var metricsDescriptor = services.FirstOrDefault(sd => sd.ServiceType == typeof(MetricsMiddleware));
		metricsDescriptor.ShouldNotBeNull();
		metricsDescriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
	}

	[Fact]
	public void RegisterDispatchMetricsAsSingleton()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateMockDispatchBuilder(services);

		// Act
		builder.UseOpenTelemetry();

		// Assert
		var metricsDescriptor = services.FirstOrDefault(sd => sd.ServiceType == typeof(IDispatchMetrics));
		metricsDescriptor.ShouldNotBeNull();
		metricsDescriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
	}

	#endregion

	#region TryAdd behavior tests

	[Fact]
	public void NotDuplicateTracingMiddlewareOnMultipleCalls()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateMockDispatchBuilder(services);

		// Act
		builder.UseOpenTelemetry();
		builder.UseOpenTelemetry();
		builder.UseOpenTelemetry();

		// Assert - Should only have one registration
		var tracingCount = services.Count(sd => sd.ServiceType == typeof(TracingMiddleware));
		tracingCount.ShouldBe(1);
	}

	[Fact]
	public void NotDuplicateMetricsMiddlewareOnMultipleCalls()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateMockDispatchBuilder(services);

		// Act
		builder.UseOpenTelemetry();
		builder.UseOpenTelemetry();
		builder.UseOpenTelemetry();

		// Assert - Should only have one registration
		var metricsCount = services.Count(sd => sd.ServiceType == typeof(MetricsMiddleware));
		metricsCount.ShouldBe(1);
	}

	[Fact]
	public void NotDuplicateDispatchMetricsOnMultipleCalls()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateMockDispatchBuilder(services);

		// Act
		builder.UseOpenTelemetry();
		builder.UseOpenTelemetry();
		builder.UseOpenTelemetry();

		// Assert - Should only have one registration
		var dispatchMetricsCount = services.Count(sd => sd.ServiceType == typeof(IDispatchMetrics));
		dispatchMetricsCount.ShouldBe(1);
	}

	#endregion

	#region Equivalence with individual calls tests

	[Fact]
	public void BeEquivalentToCallingUseTracingAndUseMetricsSeparately()
	{
		// Arrange
		var servicesWithConvenience = new ServiceCollection();
		var builderWithConvenience = CreateMockDispatchBuilder(servicesWithConvenience);

		var servicesWithSeparate = new ServiceCollection();
		var builderWithSeparate = CreateMockDispatchBuilder(servicesWithSeparate);

		// Act
		builderWithConvenience.UseOpenTelemetry();
		builderWithSeparate.UseTracing().UseMetrics();

		// Assert - Both should register the same service types
		var convenienceServiceTypes = servicesWithConvenience
			.Select(sd => sd.ServiceType)
			.OrderBy(t => t.FullName)
			.ToList();

		var separateServiceTypes = servicesWithSeparate
			.Select(sd => sd.ServiceType)
			.OrderBy(t => t.FullName)
			.ToList();

		convenienceServiceTypes.ShouldBe(separateServiceTypes);
	}

	#endregion

	#region Middleware pipeline registration tests

	[Fact]
	public void CallUseMiddlewareForTracingMiddleware()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);
		A.CallTo(() => builder.UseMiddleware<TracingMiddleware>()).Returns(builder);
		A.CallTo(() => builder.UseMiddleware<MetricsMiddleware>()).Returns(builder);

		// Act
		builder.UseOpenTelemetry();

		// Assert
		A.CallTo(() => builder.UseMiddleware<TracingMiddleware>())
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void CallUseMiddlewareForMetricsMiddleware()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);
		A.CallTo(() => builder.UseMiddleware<TracingMiddleware>()).Returns(builder);
		A.CallTo(() => builder.UseMiddleware<MetricsMiddleware>()).Returns(builder);

		// Act
		builder.UseOpenTelemetry();

		// Assert
		A.CallTo(() => builder.UseMiddleware<MetricsMiddleware>())
			.MustHaveHappenedOnceExactly();
	}

	#endregion

	#region Integration scenario tests

	[Fact]
	public void WorkWithTypicalDispatchConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateMockDispatchBuilder(services);

		// Act - Simulates a typical Dispatch configuration
		builder.UseOpenTelemetry();

		// Assert - All expected services are registered
		services.ShouldContain(sd => sd.ServiceType == typeof(TracingMiddleware));
		services.ShouldContain(sd => sd.ServiceType == typeof(MetricsMiddleware));
		services.ShouldContain(sd => sd.ServiceType == typeof(IDispatchMetrics));
	}

	#endregion

	#region Helper methods

	private static IDispatchBuilder CreateMockDispatchBuilder(IServiceCollection services)
	{
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		// Setup UseMiddleware to add middleware to services and return the builder
		A.CallTo(() => builder.UseMiddleware<TracingMiddleware>())
			.Invokes(() =>
			{
				services.TryAddSingleton<TracingMiddleware>();
			})
			.Returns(builder);

		A.CallTo(() => builder.UseMiddleware<MetricsMiddleware>())
			.Invokes(() =>
			{
				services.TryAddSingleton<IDispatchMetrics, DispatchMetrics>();
				services.TryAddSingleton<MetricsMiddleware>();
			})
			.Returns(builder);

		return builder;
	}

	#endregion
}
