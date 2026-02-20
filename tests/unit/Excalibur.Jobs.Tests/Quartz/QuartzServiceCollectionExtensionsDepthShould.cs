// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Jobs.Abstractions;
using Excalibur.Jobs.Core;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.Jobs.Tests.Quartz;

/// <summary>
/// Depth tests for <see cref="QuartzServiceCollectionExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Jobs")]
public sealed class QuartzServiceCollectionExtensionsDepthShould
{
	[Fact]
	public void AddQuartzWithJobs_ThrowsOnNullServices()
	{
		IServiceCollection? services = null;

		Should.Throw<ArgumentNullException>(() =>
			services!.AddQuartzWithJobs(null));
	}

	[Fact]
	public void AddQuartzWithJobs_RegistersQuartzHostedService()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddQuartzWithJobs(null);

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(IHostedService));
	}

	[Fact]
	public void AddQuartzWithJobs_ReturnsServices()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddQuartzWithJobs(null);

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddJobWatcher_ThrowsOnNullServices()
	{
		IServiceCollection? services = null;
		var config = new ConfigurationBuilder().Build();

		Should.Throw<ArgumentNullException>(() =>
			services!.AddJobWatcher<QuartzExtTestWatcherJob, QuartzExtTestWatcherConfig>(config.GetSection("test")));
	}

	[Fact]
	public void AddJobWatcher_ThrowsOnNullConfigurationSection()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentNullException>(() =>
			services.AddJobWatcher<QuartzExtTestWatcherJob, QuartzExtTestWatcherConfig>(null!));
	}

	[Fact]
	public void AddJobWatcher_RegistersHostedService()
	{
		// Arrange
		var services = new ServiceCollection();
		var config = new ConfigurationBuilder().Build();

		// Act
		services.AddJobWatcher<QuartzExtTestWatcherJob, QuartzExtTestWatcherConfig>(config.GetSection("Jobs:Test"));

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(IHostedService));
	}
}

internal sealed class QuartzExtTestWatcherConfig : JobConfig
{
}

internal sealed class QuartzExtTestWatcherJob : IConfigurableJob<QuartzExtTestWatcherConfig>
{
	public Task ExecuteAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
