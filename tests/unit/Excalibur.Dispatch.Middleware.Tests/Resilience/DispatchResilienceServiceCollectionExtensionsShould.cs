// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience.Polly;
using Excalibur.Dispatch.Resilience.Polly;
using Excalibur.Dispatch.Resilience.Polly;

using Microsoft.Extensions.DependencyInjection;

using Polly;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Unit tests for <see cref="DispatchResilienceServiceCollectionExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DispatchResilienceServiceCollectionExtensionsShould : UnitTestBase
{
	#region UseDispatchResilience Tests

	[Fact]
	public void UseDispatchResilience_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		IServiceCollection services = null!;

		Should.Throw<ArgumentNullException>(() =>
			services.UseDispatchResilience("test", _ => { }));
	}

	[Fact]
	public void UseDispatchResilience_ThrowsArgumentException_WhenPipelineNameIsNullOrWhiteSpace()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentNullException>(() =>
			services.UseDispatchResilience(null!, _ => { }));
	}

	[Fact]
	public void UseDispatchResilience_ThrowsArgumentNullException_WhenConfigurePipelineIsNull()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentNullException>(() =>
			services.UseDispatchResilience("test", null!));
	}

	[Fact]
	public void UseDispatchResilience_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.UseDispatchResilience("test-pipeline", builder =>
			builder.AddTimeout(TimeSpan.FromSeconds(10)));

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public async Task UseDispatchResilience_RegistersKeyedAdapter()
	{
		// Arrange
		var services = new ServiceCollection();
		services.UseDispatchResilience("my-pipeline", builder =>
			builder.AddTimeout(TimeSpan.FromSeconds(30)));

		// Act
		await using var provider = services.BuildServiceProvider();
		var adapter = provider.GetKeyedService<DispatchResilienceAdapter>("my-pipeline");

		// Assert
		adapter.ShouldNotBeNull();
	}

	[Fact]
	public async Task UseDispatchResilience_RegistersDefaultNonKeyedAdapter()
	{
		// Arrange
		var services = new ServiceCollection();
		services.UseDispatchResilience("my-pipeline", builder =>
			builder.AddTimeout(TimeSpan.FromSeconds(30)));

		// Act
		await using var provider = services.BuildServiceProvider();
		var adapter = provider.GetService<DispatchResilienceAdapter>();

		// Assert
		adapter.ShouldNotBeNull();
	}

	#endregion

	#region AddHedgingPolicy Tests

	[Fact]
	public void AddHedgingPolicy_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		IServiceCollection services = null!;

		Should.Throw<ArgumentNullException>(() =>
			services.AddHedgingPolicy());
	}

	[Fact]
	public void AddHedgingPolicy_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddHedgingPolicy();

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public async Task AddHedgingPolicy_RegistersHedgingPolicy()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddHedgingPolicy(opts => opts.MaxHedgedAttempts = 3);

		// Act
		await using var provider = services.BuildServiceProvider();
		var policy = provider.GetService<HedgingPolicy>();

		// Assert
		policy.ShouldNotBeNull();
	}

	#endregion

	#region AddResilienceTelemetry Tests

	[Fact]
	public void AddResilienceTelemetry_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		IServiceCollection services = null!;

		Should.Throw<ArgumentNullException>(() =>
			services.AddResilienceTelemetry("test"));
	}

	[Fact]
	public void AddResilienceTelemetry_ThrowsArgumentException_WhenNameIsNullOrWhiteSpace()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentNullException>(() =>
			services.AddResilienceTelemetry(null!));
	}

	[Fact]
	public void AddResilienceTelemetry_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddResilienceTelemetry("telemetry-pipeline");

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public async Task AddResilienceTelemetry_RegistersKeyedTelemetryPipeline()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddResilienceTelemetry("my-telemetry");

		// Act
		await using var provider = services.BuildServiceProvider();
		var pipeline = provider.GetKeyedService<TelemetryResiliencePipeline>("my-telemetry");

		// Assert
		pipeline.ShouldNotBeNull();
	}

	#endregion
}
