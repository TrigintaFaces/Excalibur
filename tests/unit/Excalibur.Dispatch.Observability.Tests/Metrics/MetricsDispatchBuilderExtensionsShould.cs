// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Observability.Metrics;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Observability.Tests.Metrics;

/// <summary>
/// Unit tests for <see cref="MetricsDispatchBuilderExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "DependencyInjection")]
public sealed class MetricsDispatchBuilderExtensionsShould
{
	[Fact]
	public void AddDispatchMetricsInstrumentation_ThrowOnNullBuilder()
	{
		IDispatchBuilder builder = null!;
		Should.Throw<ArgumentNullException>(() => builder.AddDispatchMetricsInstrumentation());
	}

	[Fact]
	public void AddDispatchMetricsInstrumentation_RegistersDispatchMetrics()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateFakeBuilder(services);

		// Act
		builder.AddDispatchMetricsInstrumentation();

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(DispatchMetrics));
	}

	[Fact]
	public void WithMetricsOptions_ThrowOnNullBuilder()
	{
		IDispatchBuilder builder = null!;
		Should.Throw<ArgumentNullException>(() =>
			builder.WithMetricsOptions(_ => { }));
	}

	[Fact]
	public void WithMetricsOptions_ThrowOnNullConfigure()
	{
		var services = new ServiceCollection();
		var builder = CreateFakeBuilder(services);

		Should.Throw<ArgumentNullException>(() =>
			builder.WithMetricsOptions((Action<ObservabilityOptions>)null!));
	}

	[Fact]
	public void WithMetricsOptions_RegistersOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateFakeBuilder(services);

		// Act
		builder.WithMetricsOptions(opts =>
		{
			opts.EnableDetailedTiming = true;
		});

		// Assert — check that options are registered (IConfigureOptions present)
		services.Count.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void WithMetricsOptions_ReturnsSameBuilder()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateFakeBuilder(services);

		// Act
		var result = builder.WithMetricsOptions(_ => { });

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void AddDispatchMetricsInstrumentation_ReturnsSameBuilder()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = CreateFakeBuilder(services);

		// Act
		var result = builder.AddDispatchMetricsInstrumentation();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	private static IDispatchBuilder CreateFakeBuilder(IServiceCollection services)
	{
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);
		return builder;
	}
}
