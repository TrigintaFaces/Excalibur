// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Jobs.Core;

using Microsoft.Extensions.DependencyInjection;

using Quartz;

namespace Excalibur.Hosting.Tests.Jobs;

/// <summary>
/// Unit tests for <see cref="HostingJobsServiceCollectionExtensions" />.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Hosting.Jobs")]
public sealed class HostingJobsServiceCollectionExtensionsShould : UnitTestBase
{
	#region AddExcaliburJobHost (assemblies only)

	[Fact]
	public void AddExcaliburJobHost_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		// Arrange
		IServiceCollection? services = null;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			HostingJobsServiceCollectionExtensions.AddExcaliburJobHost(services));
	}

	[Fact]
	public void AddExcaliburJobHost_ReturnsServiceCollection_WhenCalledWithEmptyAssemblies()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = HostingJobsServiceCollectionExtensions.AddExcaliburJobHost(services);

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
		var result = HostingJobsServiceCollectionExtensions.AddExcaliburJobHost(services, assemblies);

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
			typeof(HostingJobsServiceCollectionExtensions).Assembly
		};

		// Act
		var result = HostingJobsServiceCollectionExtensions.AddExcaliburJobHost(services, assemblies);

		// Assert
		result.ShouldBe(services);
	}

	#endregion

	#region AddExcaliburJobHost (with Quartz config)

	[Fact]
	public void AddExcaliburJobHost_WithQuartzConfig_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();
		var configCalled = false;

		// Act
		var result = HostingJobsServiceCollectionExtensions.AddExcaliburJobHost(
			services,
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
		var result = HostingJobsServiceCollectionExtensions.AddExcaliburJobHost(
			services,
			configureQuartz: null);

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
		var result = HostingJobsServiceCollectionExtensions.AddExcaliburJobHost(
			services,
			configureQuartz: _ => configCalled = true,
			assemblies: assemblies);

		// Assert
		result.ShouldBe(services);
		configCalled.ShouldBeTrue();
	}

	#endregion

	#region Service Registration Verification

	[Fact]
	public void AddExcaliburJobHost_RegistersQuartzServices()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = HostingJobsServiceCollectionExtensions.AddExcaliburJobHost(services);

		// Assert
		services.ShouldContain(s => s.ServiceType == typeof(ISchedulerFactory));
	}

	[Fact]
	public void AddExcaliburJobHost_RegistersHeartbeatTracker()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = HostingJobsServiceCollectionExtensions.AddExcaliburJobHost(services);

		// Assert - Per-job health checks are registered via each job's ConfigureHealthChecks method.
		// The central registration provides the JobHeartbeatTracker singleton that health checks depend on.
		services.ShouldContain(s => s.ServiceType == typeof(JobHeartbeatTracker));
	}

	#endregion
}
