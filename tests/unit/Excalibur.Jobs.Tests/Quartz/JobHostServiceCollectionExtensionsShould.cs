// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Jobs.Core;
using Excalibur.Jobs.Quartz;

using Microsoft.Extensions.DependencyInjection;

using Quartz;

using ExcaliburJobConfigurator = Excalibur.Jobs.Quartz.IJobConfigurator;

namespace Excalibur.Jobs.Tests.Quartz;

/// <summary>
/// Unit tests for <see cref="JobHostServiceCollectionExtensions" />.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Jobs")]
public sealed class JobHostServiceCollectionExtensionsShould
{
	#region AddExcaliburJobHost (assemblies only)

	[Fact]
	public void AddExcaliburJobHost_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection? services = null;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddExcaliburJobHost());
	}

	[Fact]
	public void AddExcaliburJobHost_ReturnsServiceCollection_WhenCalledWithEmptyAssemblies()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddExcaliburJobHost();

		// Assert
		result.ShouldBe(services);
	}

	[Fact]
	public void AddExcaliburJobHost_ReturnsServiceCollection_WhenCalledWithAssemblies()
	{
		// Arrange
		var services = new ServiceCollection();
		var assemblies = new[] { Assembly.GetExecutingAssembly() };

		// Act
		var result = services.AddExcaliburJobHost(assemblies);

		// Assert
		result.ShouldBe(services);
	}

	[Fact]
	public void AddExcaliburJobHost_AcceptsMultipleAssemblies()
	{
		// Arrange
		var services = new ServiceCollection();
		var assemblies = new[]
		{
			Assembly.GetExecutingAssembly(),
			typeof(JobHostServiceCollectionExtensions).Assembly
		};

		// Act
		var result = services.AddExcaliburJobHost(assemblies);

		// Assert
		result.ShouldBe(services);
	}

	#endregion

	#region AddExcaliburJobHost (with Quartz config)

	[Fact]
	public void AddExcaliburJobHost_WithQuartzConfig_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection? services = null;
		Action<IServiceCollectionQuartzConfigurator> config = _ => { };

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddExcaliburJobHost(config));
	}

	[Fact]
	public void AddExcaliburJobHost_WithQuartzConfig_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();
		var configCalled = false;

		// Act
		var result = services.AddExcaliburJobHost(
			configureQuartz: _ => configCalled = true);

		// Assert
		result.ShouldBe(services);
		configCalled.ShouldBeTrue();
	}

	[Fact]
	public void AddExcaliburJobHost_WithNullQuartzConfig_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddExcaliburJobHost(configureQuartz: null);

		// Assert
		result.ShouldBe(services);
	}

	[Fact]
	public void AddExcaliburJobHost_WithQuartzConfigAndAssemblies_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();
		var assemblies = new[] { Assembly.GetExecutingAssembly() };
		var configCalled = false;

		// Act
		var result = services.AddExcaliburJobHost(
			configureQuartz: _ => configCalled = true,
			assemblies: assemblies);

		// Assert
		result.ShouldBe(services);
		configCalled.ShouldBeTrue();
	}

	#endregion

	#region AddExcaliburJobHost (with Job config)

	[Fact]
	public void AddExcaliburJobHost_WithJobConfig_ThrowsArgumentNullException_WhenConfigureJobsIsNull()
	{
		// Arrange
		var services = new ServiceCollection();
		Action<ExcaliburJobConfigurator>? config = null;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddExcaliburJobHost(config));
	}

	[Fact]
	public void AddExcaliburJobHost_WithJobConfig_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();
		var configCalled = false;
		Action<ExcaliburJobConfigurator> config = _ => configCalled = true;

		// Act
		var result = services.AddExcaliburJobHost(config);

		// Assert
		result.ShouldBe(services);
		configCalled.ShouldBeTrue();
	}

	[Fact]
	public void AddExcaliburJobHost_WithJobConfigAndAssemblies_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();
		var assemblies = new[] { Assembly.GetExecutingAssembly() };
		var configCalled = false;
		Action<ExcaliburJobConfigurator> config = _ => configCalled = true;

		// Act
		var result = services.AddExcaliburJobHost(config, assemblies);

		// Assert
		result.ShouldBe(services);
		configCalled.ShouldBeTrue();
	}

	#endregion

	#region AddExcaliburJobHost (with both configs)

	[Fact]
	public void AddExcaliburJobHost_WithBothConfigs_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection? services = null;
		Action<IServiceCollectionQuartzConfigurator> quartzConfig = _ => { };
		Action<ExcaliburJobConfigurator> jobConfig = _ => { };

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddExcaliburJobHost(quartzConfig, jobConfig));
	}

	[Fact]
	public void AddExcaliburJobHost_WithBothConfigs_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();
		var quartzConfigCalled = false;
		var jobConfigCalled = false;

		// Act
		var result = services.AddExcaliburJobHost(
			configureQuartz: _ => quartzConfigCalled = true,
			configureJobs: _ => jobConfigCalled = true);

		// Assert
		result.ShouldBe(services);
		quartzConfigCalled.ShouldBeTrue();
		jobConfigCalled.ShouldBeTrue();
	}

	[Fact]
	public void AddExcaliburJobHost_WithNullBothConfigs_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddExcaliburJobHost(
			configureQuartz: null,
			configureJobs: null);

		// Assert
		result.ShouldBe(services);
	}

	[Fact]
	public void AddExcaliburJobHost_WithBothConfigsAndAssemblies_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();
		var assemblies = new[] { Assembly.GetExecutingAssembly() };
		var quartzConfigCalled = false;
		var jobConfigCalled = false;

		// Act
		var result = services.AddExcaliburJobHost(
			configureQuartz: _ => quartzConfigCalled = true,
			configureJobs: _ => jobConfigCalled = true,
			assemblies: assemblies);

		// Assert
		result.ShouldBe(services);
		quartzConfigCalled.ShouldBeTrue();
		jobConfigCalled.ShouldBeTrue();
	}

	#endregion

	#region Service Registration Verification

	[Fact]
	public void AddExcaliburJobHost_RegistersQuartzServices()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburJobHost();

		// Assert
		services.ShouldContain(s => s.ServiceType == typeof(ISchedulerFactory));
	}

	[Fact]
	public void AddExcaliburJobHost_RegistersQuartzHostedService()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburJobHost();

		// Assert
		services.ShouldContain(s => s.ServiceType.Name.Contains("HostedService"));
	}

	[Fact]
	public void AddExcaliburJobHost_RegistersJobAdapters()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburJobHost();

		// Assert
		services.ShouldContain(s => s.ImplementationType == typeof(QuartzJobAdapter));
		var hasGenericAdapter = services.Any(s => s.ImplementationType != null && s.ImplementationType.Name.Contains("QuartzGenericJobAdapter"));
		hasGenericAdapter.ShouldBeTrue();
	}

	[Fact]
	public void AddExcaliburJobHost_RegistersHeartbeatTracker()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburJobHost();

		// Assert - Per-job health checks are registered via each job's ConfigureHealthChecks method.
		// The central registration provides the JobHeartbeatTracker singleton that health checks depend on.
		services.ShouldContain(s => s.ServiceType == typeof(JobHeartbeatTracker));
	}

	#endregion
}
