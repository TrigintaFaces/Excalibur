// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Observability.Propagation;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Middleware.Tests.Observability;

/// <summary>
/// Unit tests for <see cref="DispatchBuilderObservabilityExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DispatchBuilderObservabilityExtensionsShould : UnitTestBase
{
	private static IDispatchBuilder CreateBuilder()
	{
		var services = new ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);
		return builder;
	}

	#region AddObservability Tests

	[Fact]
	public void AddObservability_ThrowsArgumentNullException_WhenBuilderIsNull()
	{
		IDispatchBuilder builder = null!;

		Should.Throw<ArgumentNullException>(() => builder.AddObservability());
	}

	[Fact]
	public void AddObservability_ReturnsBuilder()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		var result = builder.AddObservability();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region UseTracing Tests

	[Fact]
	public void UseTracing_ThrowsArgumentNullException_WhenBuilderIsNull()
	{
		IDispatchBuilder builder = null!;

		Should.Throw<ArgumentNullException>(() => builder.UseTracing());
	}

	#endregion

	#region UseMetrics Tests

	[Fact]
	public void UseMetrics_ThrowsArgumentNullException_WhenBuilderIsNull()
	{
		IDispatchBuilder builder = null!;

		Should.Throw<ArgumentNullException>(() => builder.UseMetrics());
	}

	#endregion

	#region UseOpenTelemetry Tests

	[Fact]
	public void UseOpenTelemetry_ThrowsArgumentNullException_WhenBuilderIsNull()
	{
		IDispatchBuilder builder = null!;

		Should.Throw<ArgumentNullException>(() => builder.UseOpenTelemetry());
	}

	#endregion

	#region UseW3CTraceContext Tests

	[Fact]
	public void UseW3CTraceContext_ThrowsArgumentNullException_WhenBuilderIsNull()
	{
		IDispatchBuilder builder = null!;

		Should.Throw<ArgumentNullException>(() => builder.UseW3CTraceContext());
	}

	#endregion

	#region UseTraceSampling Tests

	[Fact]
	public void UseTraceSampling_ThrowsArgumentNullException_WhenBuilderIsNull()
	{
		IDispatchBuilder builder = null!;

		Should.Throw<ArgumentNullException>(() => builder.UseTraceSampling());
	}

	#endregion

	#region AddW3CTracingPropagator Tests

	[Fact]
	public void AddW3CTracingPropagator_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		IServiceCollection services = null!;

		Should.Throw<ArgumentNullException>(() => services.AddW3CTracingPropagator());
	}

	[Fact]
	public async Task AddW3CTracingPropagator_RegistersPropagator()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddW3CTracingPropagator();

		// Assert
		await using var provider = services.BuildServiceProvider();
		var propagator = provider.GetService<ITracingContextPropagator>();
		propagator.ShouldNotBeNull();
		propagator.ShouldBeOfType<W3CTracingContextPropagator>();
	}

	#endregion

	#region AddB3TracingPropagator Tests

	[Fact]
	public void AddB3TracingPropagator_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		IServiceCollection services = null!;

		Should.Throw<ArgumentNullException>(() => services.AddB3TracingPropagator());
	}

	[Fact]
	public async Task AddB3TracingPropagator_RegistersPropagator()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddB3TracingPropagator();

		// Assert
		await using var provider = services.BuildServiceProvider();
		var propagator = provider.GetService<ITracingContextPropagator>();
		propagator.ShouldNotBeNull();
		propagator.ShouldBeOfType<B3TracingContextPropagator>();
	}

	#endregion
}
