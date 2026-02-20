// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Diagnostics;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Tests.Diagnostics;

/// <summary>
/// Depth tests for <see cref="DispatchTelemetryServiceCollectionExtensions"/>.
/// Covers all overloads: default, with Action, with IConfiguration,
/// production, development, throughput profiles, and null guards.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DispatchTelemetryServiceCollectionExtensionsShould
{
	[Fact]
	public void AddDispatchTelemetry_Default_RegistersProvider()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDispatchTelemetry();

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IDispatchTelemetryProvider));
	}

	[Fact]
	public void AddDispatchTelemetry_Default_ReturnsSameCollection()
	{
		var services = new ServiceCollection();
		var result = services.AddDispatchTelemetry();
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddDispatchTelemetry_WithAction_ThrowsWhenServicesIsNull()
	{
		IServiceCollection services = null!;

		Should.Throw<ArgumentNullException>(() =>
			services.AddDispatchTelemetry(opts => { }));
	}

	[Fact]
	public void AddDispatchTelemetry_WithAction_ThrowsWhenConfigureIsNull()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentNullException>(() =>
			services.AddDispatchTelemetry((Action<DispatchTelemetryOptions>)null!));
	}

	[Fact]
	public void AddDispatchTelemetry_WithAction_ConfiguresOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDispatchTelemetry(opts =>
		{
			opts.EnableTracing = false;
		});
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<DispatchTelemetryOptions>>().Value;

		// Assert
		options.EnableTracing.ShouldBeFalse();
	}

	[Fact]
	public void AddDispatchTelemetry_WithConfiguration_ThrowsWhenServicesIsNull()
	{
		IServiceCollection services = null!;
		var config = new ConfigurationBuilder().Build();

		Should.Throw<ArgumentNullException>(() =>
			services.AddDispatchTelemetry(config));
	}

	[Fact]
	public void AddDispatchTelemetry_WithConfiguration_ThrowsWhenConfigIsNull()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentNullException>(() =>
			services.AddDispatchTelemetry((IConfiguration)null!));
	}

	[Fact]
	public void AddDispatchTelemetry_WithConfiguration_RegistersProvider()
	{
		// Arrange
		var services = new ServiceCollection();
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["DispatchTelemetry:EnableTracing"] = "true",
			})
			.Build();

		// Act
		services.AddDispatchTelemetry(config);

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IDispatchTelemetryProvider));
	}

	[Fact]
	public void AddDispatchTelemetryForProduction_RegistersProvider()
	{
		var services = new ServiceCollection();
		var result = services.AddDispatchTelemetryForProduction();

		result.ShouldBeSameAs(services);
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IDispatchTelemetryProvider));
	}

	[Fact]
	public void AddDispatchTelemetryForDevelopment_RegistersProvider()
	{
		var services = new ServiceCollection();
		var result = services.AddDispatchTelemetryForDevelopment();

		result.ShouldBeSameAs(services);
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IDispatchTelemetryProvider));
	}

	[Fact]
	public void AddDispatchTelemetryForThroughput_RegistersProvider()
	{
		var services = new ServiceCollection();
		var result = services.AddDispatchTelemetryForThroughput();

		result.ShouldBeSameAs(services);
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IDispatchTelemetryProvider));
	}

	[Fact]
	public void AddDispatchTelemetry_RegistersValidator()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDispatchTelemetry();

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IValidateOptions<DispatchTelemetryOptions>));
	}
}
