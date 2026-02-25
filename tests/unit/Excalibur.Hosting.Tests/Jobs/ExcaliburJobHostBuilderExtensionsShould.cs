// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Microsoft.Extensions.Hosting;

using Quartz;

namespace Excalibur.Hosting.Tests.Jobs;

/// <summary>
/// Unit tests for <see cref="ExcaliburJobHostBuilderExtensions" />.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Hosting.Jobs")]
public sealed class ExcaliburJobHostBuilderExtensionsShould : UnitTestBase
{
	#region AddExcaliburJobHost (assemblies only)

	[Fact]
	public void AddExcaliburJobHost_ThrowsArgumentNullException_WhenBuilderIsNull()
	{
		// Arrange
		IHostApplicationBuilder? builder = null;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			builder.AddExcaliburJobHost());
	}

	[Fact]
	public void AddExcaliburJobHost_ReturnsBuilder_WhenCalledWithEmptyAssemblies()
	{
		// Arrange
		var builder = Host.CreateApplicationBuilder();

		// Act
		var result = builder.AddExcaliburJobHost();

		// Assert
		result.ShouldBe(builder);
	}

	[Fact]
	public void AddExcaliburJobHost_ReturnsBuilder_WhenCalledWithAssemblies()
	{
		// Arrange
		var builder = Host.CreateApplicationBuilder();
		var assemblies = new[] { Assembly.GetExecutingAssembly() };

		// Act
		var result = builder.AddExcaliburJobHost(assemblies);

		// Assert
		result.ShouldBe(builder);
	}

	[Fact]
	public void AddExcaliburJobHost_AcceptsMultipleAssemblies()
	{
		// Arrange
		var builder = Host.CreateApplicationBuilder();
		var assemblies = new[]
		{
			Assembly.GetExecutingAssembly(),
			typeof(ExcaliburJobHostBuilderExtensions).Assembly
		};

		// Act
		var result = builder.AddExcaliburJobHost(assemblies);

		// Assert
		result.ShouldBe(builder);
	}

	#endregion

	#region AddExcaliburJobHost (with Quartz config)

	[Fact]
	public void AddExcaliburJobHost_WithQuartzConfig_ReturnsBuilder()
	{
		// Arrange
		var builder = Host.CreateApplicationBuilder();
		var configCalled = false;

		// Act
		var result = builder.AddExcaliburJobHost(
			configureQuartz: _ => configCalled = true);

		// Assert
		result.ShouldBe(builder);
		configCalled.ShouldBeTrue();
	}

	[Fact]
	public void AddExcaliburJobHost_WithNullQuartzConfig_ReturnsBuilder()
	{
		// Arrange
		var builder = Host.CreateApplicationBuilder();

		// Act
		var result = builder.AddExcaliburJobHost(configureQuartz: null);

		// Assert
		result.ShouldBe(builder);
	}

	[Fact]
	public void AddExcaliburJobHost_WithQuartzConfigAndAssemblies_ReturnsBuilder()
	{
		// Arrange
		var builder = Host.CreateApplicationBuilder();
		var assemblies = new[] { Assembly.GetExecutingAssembly() };
		var configCalled = false;

		// Act
		var result = builder.AddExcaliburJobHost(
			configureQuartz: _ => configCalled = true,
			assemblies: assemblies);

		// Assert
		result.ShouldBe(builder);
		configCalled.ShouldBeTrue();
	}

	#endregion

	#region Service Registration Verification

	[Fact]
	public void AddExcaliburJobHost_RegistersQuartzServices()
	{
		// Arrange
		var builder = Host.CreateApplicationBuilder();

		// Act
		_ = builder.AddExcaliburJobHost();

		// Assert
		builder.Services.ShouldContain(s => s.ServiceType == typeof(ISchedulerFactory));
	}

	[Fact]
	public void AddExcaliburJobHost_ConfiguresServicesOnBuilder()
	{
		// Arrange
		var builder = Host.CreateApplicationBuilder();
		var initialCount = builder.Services.Count;

		// Act
		_ = builder.AddExcaliburJobHost();

		// Assert - Services should be added
		builder.Services.Count.ShouldBeGreaterThan(initialCount);
	}

	#endregion
}
