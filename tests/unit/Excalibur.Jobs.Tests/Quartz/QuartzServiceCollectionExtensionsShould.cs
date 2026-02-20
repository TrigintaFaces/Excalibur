// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access
#pragma warning disable IL3050 // Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling

using Excalibur.Jobs.Abstractions;
using Excalibur.Jobs.Core;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.Jobs.Tests.Quartz;

/// <summary>
/// Unit tests for <see cref="QuartzServiceCollectionExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Jobs")]
public sealed class QuartzServiceCollectionExtensionsShould
{
	// --- AddQuartzWithJobs ---

	[Fact]
	public void AddQuartzWithJobsThrowWhenServicesIsNull()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddQuartzWithJobs(null));
	}

	[Fact]
	public void AddQuartzWithJobsReturnServiceCollectionForChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddQuartzWithJobs(null);

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddQuartzWithJobsRegisterQuartzHostedService()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddQuartzWithJobs(null);

		// Assert — IHostedService should be registered (QuartzHostedService)
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IHostedService));
	}

	[Fact]
	public void AddQuartzWithJobsAcceptConfigureAction()
	{
		// Arrange
		var services = new ServiceCollection();
		var invoked = false;

		// Act
		services.AddQuartzWithJobs(_ => invoked = true);

		// Assert
		invoked.ShouldBeTrue();
	}

	// --- AddJobWatcher ---

	[Fact]
	public void AddJobWatcherThrowWhenServicesIsNull()
	{
		// Arrange
		IServiceCollection services = null!;
		var config = new ConfigurationBuilder().Build();
		var section = config.GetSection("Jobs:Test");

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddJobWatcher<TestConfigurableJob, TestJobConfig>(section));
	}

	[Fact]
	public void AddJobWatcherThrowWhenConfigurationSectionIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddJobWatcher<TestConfigurableJob, TestJobConfig>(null!));
	}

	[Fact]
	public void AddJobWatcherRegisterHostedService()
	{
		// Arrange
		var services = new ServiceCollection();
		var config = new ConfigurationBuilder().Build();
		var section = config.GetSection("Jobs:Test");

		// Act
		services.AddJobWatcher<TestConfigurableJob, TestJobConfig>(section);

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IHostedService) &&
			sd.Lifetime == ServiceLifetime.Singleton);
	}

	// --- Test helpers ---

	private sealed class TestJobConfig : JobConfig;

	private sealed class TestConfigurableJob : IConfigurableJob<TestJobConfig>;
}
