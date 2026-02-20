// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Domain;

using Microsoft.Extensions.Configuration;

namespace Excalibur.Hosting.Tests.Core;

/// <summary>
/// Unit tests for <see cref="HostApplicationBuilderExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Hosting")]
[Collection("EnvironmentVariableTests")]
public sealed class HostApplicationBuilderExtensionsShould : UnitTestBase
{
	#region ConfigureApplicationContext

	[Fact]
	public void ConfigureApplicationContext_ThrowsArgumentNullException_WhenBuilderIsNull()
	{
		// Arrange
		IHostApplicationBuilder? builder = null;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => builder.ConfigureApplicationContext());
	}

	[Fact]
	public void ConfigureApplicationContext_ReturnsBuilder_WhenCalled()
	{
		// Arrange
		var builder = Host.CreateApplicationBuilder();

		// Act
		var result = builder.ConfigureApplicationContext();

		// Assert
		result.ShouldBe(builder);
	}

	[Fact]
	public void ConfigureApplicationContext_RegistersApplicationContextOptions()
	{
		// Arrange
		var builder = Host.CreateApplicationBuilder();

		// Act
		_ = builder.ConfigureApplicationContext();

		// Assert
		builder.Services.ShouldContain(s =>
			s.ServiceType == typeof(IConfigureOptions<ApplicationContextOptions>));
	}

	#endregion

	#region AddApplicationContext

	[Fact]
	public void AddApplicationContext_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection? services = null;
		var config = new ConfigurationBuilder().Build();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => services.AddApplicationContext(config));
	}

	[Fact]
	public void AddApplicationContext_ThrowsArgumentNullException_WhenConfigurationIsNull()
	{
		// Arrange
		var services = new ServiceCollection();
		IConfiguration? config = null;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => services.AddApplicationContext(config));
	}

	[Fact]
	public void AddApplicationContext_ReturnsServiceCollection_WhenCalled()
	{
		// Arrange
		var services = new ServiceCollection();
		var config = new ConfigurationBuilder().Build();

		// Act
		var result = services.AddApplicationContext(config);

		// Assert
		result.ShouldBe(services);
	}

	[Fact]
	public void AddApplicationContext_RegistersOptionsConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();
		var config = new ConfigurationBuilder().Build();

		// Act
		services.AddApplicationContext(config);

		// Assert
		services.ShouldContain(s =>
			s.ServiceType == typeof(IConfigureOptions<ApplicationContextOptions>));
	}

	#endregion

	#region ConfigureExcaliburLogging

	[Fact]
	public void ConfigureExcaliburLogging_ThrowsArgumentNullException_WhenBuilderIsNull()
	{
		// Arrange
		IHostApplicationBuilder? builder = null;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => builder.ConfigureExcaliburLogging());
	}

	[Fact]
	public void ConfigureExcaliburLogging_ReturnsBuilder_WhenCalled()
	{
		// Arrange
		var builder = Host.CreateApplicationBuilder();
		_ = builder.ConfigureApplicationContext();

		// Act
		var result = builder.ConfigureExcaliburLogging();

		// Assert
		result.ShouldBe(builder);
	}

	[Fact]
	public void ConfigureExcaliburLogging_ReturnsBuilder_WhenCalledWithNullSinks()
	{
		// Arrange
		var builder = Host.CreateApplicationBuilder();
		_ = builder.ConfigureApplicationContext();

		// Act
		var result = builder.ConfigureExcaliburLogging(null);

		// Assert
		result.ShouldBe(builder);
	}

	#endregion

	#region ConfigureExcaliburMetrics

	[Fact]
	public void ConfigureExcaliburMetrics_ThrowsArgumentNullException_WhenBuilderIsNull()
	{
		// Arrange
		IHostApplicationBuilder? builder = null;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => builder.ConfigureExcaliburMetrics());
	}

	[Fact]
	public void ConfigureExcaliburMetrics_ReturnsBuilder_WhenCalled()
	{
		// Arrange
		var builder = Host.CreateApplicationBuilder();
		_ = builder.ConfigureApplicationContext();

		// Act
		var result = builder.ConfigureExcaliburMetrics();

		// Assert
		result.ShouldBe(builder);
	}

	[Fact]
	public void ConfigureExcaliburMetrics_InvokesCustomConfiguration()
	{
		// Arrange
		var builder = Host.CreateApplicationBuilder();
		_ = builder.ConfigureApplicationContext();
		var configCalled = false;

		// Act
		var result = builder.ConfigureExcaliburMetrics(_ => configCalled = true);

		// Assert
		result.ShouldBe(builder);
		configCalled.ShouldBeTrue();
	}

	[Fact]
	public void ConfigureExcaliburMetrics_AddsServicesToBuilder()
	{
		// Arrange
		var builder = Host.CreateApplicationBuilder();
		_ = builder.ConfigureApplicationContext();
		var initialCount = builder.Services.Count;

		// Act
		_ = builder.ConfigureExcaliburMetrics();

		// Assert
		builder.Services.Count.ShouldBeGreaterThan(initialCount);
	}

	#endregion

	#region ConfigureExcaliburTracing

	[Fact]
	public void ConfigureExcaliburTracing_ThrowsArgumentNullException_WhenBuilderIsNull()
	{
		// Arrange
		IHostApplicationBuilder? builder = null;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => builder.ConfigureExcaliburTracing());
	}

	[Fact]
	public void ConfigureExcaliburTracing_ReturnsBuilder_WhenCalled()
	{
		// Arrange
		var builder = Host.CreateApplicationBuilder();
		_ = builder.ConfigureApplicationContext();

		// Act
		var result = builder.ConfigureExcaliburTracing();

		// Assert
		result.ShouldBe(builder);
	}

	[Fact]
	public void ConfigureExcaliburTracing_InvokesCustomConfiguration()
	{
		// Arrange
		var builder = Host.CreateApplicationBuilder();
		_ = builder.ConfigureApplicationContext();
		var configCalled = false;

		// Act
		var result = builder.ConfigureExcaliburTracing(_ => configCalled = true);

		// Assert
		result.ShouldBe(builder);
		configCalled.ShouldBeTrue();
	}

	[Fact]
	public void ConfigureExcaliburTracing_AddsServicesToBuilder()
	{
		// Arrange
		var builder = Host.CreateApplicationBuilder();
		_ = builder.ConfigureApplicationContext();
		var initialCount = builder.Services.Count;

		// Act
		_ = builder.ConfigureExcaliburTracing();

		// Assert
		builder.Services.Count.ShouldBeGreaterThan(initialCount);
	}

	#endregion
}
