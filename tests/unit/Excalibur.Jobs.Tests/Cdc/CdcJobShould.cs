// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc.SqlServer;
using Excalibur.Jobs.Cdc;
using Excalibur.Jobs.Core;

using FakeItEasy;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Quartz;

namespace Excalibur.Jobs.Tests.Cdc;

/// <summary>
/// Unit tests for <see cref="CdcJob"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Jobs")]
public sealed class CdcJobShould
{
	private readonly IDataChangeEventProcessorFactory _fakeFactory;
	private readonly Func<string, SqlConnection> _fakeConnectionFactory;
	private readonly IOptions<CdcJobOptions> _fakeCdcConfigOptions;
	private readonly JobHeartbeatTracker _heartbeatTracker;

	public CdcJobShould()
	{
		_fakeFactory = A.Fake<IDataChangeEventProcessorFactory>();
		_fakeConnectionFactory = A.Fake<Func<string, SqlConnection>>();
		_fakeCdcConfigOptions = Microsoft.Extensions.Options.Options.Create(new CdcJobOptions { DatabaseConfigs = [] });
		_heartbeatTracker = new JobHeartbeatTracker();
	}

	// --- Constructor (Func<string, SqlConnection>) null guards ---

	[Fact]
	public void ThrowWhenFactoryIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new CdcJob(null!, _fakeConnectionFactory, _fakeCdcConfigOptions, _heartbeatTracker, NullLogger<CdcJob>.Instance));
	}

	[Fact]
	public void ThrowWhenConnectionFactoryIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new CdcJob(_fakeFactory, (Func<string, SqlConnection>)null!, _fakeCdcConfigOptions, _heartbeatTracker, NullLogger<CdcJob>.Instance));
	}

	[Fact]
	public void ThrowWhenCdcConfigOptionsIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new CdcJob(_fakeFactory, _fakeConnectionFactory, null!, _heartbeatTracker, NullLogger<CdcJob>.Instance));
	}

	[Fact]
	public void ThrowWhenHeartbeatTrackerIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new CdcJob(_fakeFactory, _fakeConnectionFactory, _fakeCdcConfigOptions, null!, NullLogger<CdcJob>.Instance));
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new CdcJob(_fakeFactory, _fakeConnectionFactory, _fakeCdcConfigOptions, _heartbeatTracker, null!));
	}

	// --- Constructor (IConfiguration) null guards ---

	[Fact]
	public void ThrowWhenFactoryIsNullForConfigurationConstructor()
	{
		// Arrange
		var config = new ConfigurationBuilder().Build();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new CdcJob(null!, config, _fakeCdcConfigOptions, _heartbeatTracker, NullLogger<CdcJob>.Instance));
	}

	[Fact]
	public void ThrowWhenConfigurationIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new CdcJob(_fakeFactory, (IConfiguration)null!, _fakeCdcConfigOptions, _heartbeatTracker, NullLogger<CdcJob>.Instance));
	}

	// --- DI activation (Quartz / ActivatorUtilities) ---

	private ServiceProvider BuildActivationProvider(bool registerConnectionFactory)
	{
		var services = new ServiceCollection();
		_ = services.AddSingleton(_fakeFactory);
		_ = services.AddSingleton(_fakeCdcConfigOptions);
		_ = services.AddSingleton(_heartbeatTracker);
		_ = services.AddSingleton<ILogger<CdcJob>>(NullLogger<CdcJob>.Instance);
		_ = services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

		if (registerConnectionFactory)
		{
			// When a Func<string, SqlConnection> is also registered, BOTH public constructors
			// become equally satisfiable from DI. Without [ActivatorUtilitiesConstructor] this
			// is exactly what made Quartz throw "Multiple constructors accepting all given
			// argument types".
			_ = services.AddSingleton(_fakeConnectionFactory);
		}

		return services.BuildServiceProvider();
	}

	[Fact]
	public void BeActivatableViaActivatorUtilitiesWithoutConnectionFactory()
	{
		// Arrange — mirrors a job worker that did not register Func<string, SqlConnection>.
		using var provider = BuildActivationProvider(registerConnectionFactory: false);

		// Act — this is the path Quartz's MicrosoftDependencyInjectionJobFactory uses.
		var job = ActivatorUtilities.CreateInstance<CdcJob>(provider);

		// Assert
		_ = job.ShouldNotBeNull();
	}

	[Fact]
	public void BeActivatableViaActivatorUtilitiesWhenBothConstructorsAreSatisfiable()
	{
		// Arrange — Func<string, SqlConnection> registered makes both ctors DI-satisfiable.
		// Regression: previously threw "Multiple constructors accepting all given argument types".
		using var provider = BuildActivationProvider(registerConnectionFactory: true);

		// Act
		var job = ActivatorUtilities.CreateInstance<CdcJob>(provider);

		// Assert — [ActivatorUtilitiesConstructor] on the IConfiguration ctor disambiguates.
		_ = job.ShouldNotBeNull();
	}

	[Fact]
	public void RegisterProcessorFactoryViaAddSqlServerCdcJob()
	{
		// Arrange — only the host-level prerequisites a job worker already has.
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddSingleton(A.Fake<IHostApplicationLifetime>());
		_ = services.AddSingleton(_heartbeatTracker);
		var configuration = new ConfigurationBuilder().Build();
		_ = services.AddSingleton<IConfiguration>(configuration);

		// Act — the single feature-registration entry point.
		_ = services.AddSqlServerCdcJob(configuration);
		using var provider = services.BuildServiceProvider();

		// Assert — the previously-unregistered factory is now resolvable...
		_ = provider.GetService<IDataChangeEventProcessorFactory>().ShouldNotBeNull();

		// ...and CdcJob activates through the exact path Quartz uses.
		var job = ActivatorUtilities.CreateInstance<CdcJob>(provider);
		_ = job.ShouldNotBeNull();
	}

	// --- Execute null guard ---

	[Fact]
	public async Task ExecuteThrowWhenContextIsNull()
	{
		// Arrange
		var sut = new CdcJob(_fakeFactory, _fakeConnectionFactory, _fakeCdcConfigOptions, _heartbeatTracker, NullLogger<CdcJob>.Instance);

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(() =>
			sut.Execute(null!));
	}

	// --- JobConfigSectionName ---

	[Fact]
	public void HaveCorrectJobConfigSectionName()
	{
		// Assert
		CdcJob.JobConfigSectionName.ShouldBe("Jobs:CdcJob");
	}

	// --- ConfigureJob / ConfigureHealthChecks null guards ---
	// Note: IServiceCollectionQuartzConfigurator cannot be faked by FakeItEasy (Castle.DynamicProxy fails).
	// We test the first null guard (configurator) which triggers before the interface is used.

	[Fact]
	public void ConfigureJobThrowWhenConfiguratorIsNull()
	{
		// Arrange
		var config = new ConfigurationBuilder().Build();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			CdcJob.ConfigureJob(null!, config));
	}

	[Fact]
	public void ConfigureHealthChecksThrowWhenHealthChecksIsNull()
	{
		// Arrange
		var config = new ConfigurationBuilder().Build();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			CdcJob.ConfigureHealthChecks(null!, config));
	}

	[Fact]
	public void ConfigureHealthChecksThrowWhenConfigurationIsNull()
	{
		// Arrange
		var healthChecks = A.Fake<Microsoft.Extensions.DependencyInjection.IHealthChecksBuilder>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			CdcJob.ConfigureHealthChecks(healthChecks, null!));
	}

	// --- ConfigureJob honors the Disabled flag (Excalibur.Dispatch-ku1i3e) ---
	// IServiceCollectionQuartzConfigurator cannot be faked (see note above), so these drive the real
	// Quartz configurator and inspect the resulting QuartzOptions for the registered job detail.
	// Inspecting options (rather than building a scheduler) keeps the test deterministic — it avoids
	// Quartz's process-global SchedulerRepository, which is shared across parallel test classes.

	[Fact]
	public void ConfigureJobDoesNotRegisterJobWhenDisabled()
	{
		// Arrange — Disabled:true must mean the job is never registered with the scheduler.
		var config = BuildJobConfig(disabled: true);

		// Act
		var registered = IsJobRegistered(config);

		// Assert
		registered.ShouldBeFalse();
	}

	[Fact]
	public void ConfigureJobRegistersJobWhenEnabled()
	{
		// Arrange — control case: proves the assertion above tests the Disabled gate, not a wiring slip.
		var config = BuildJobConfig(disabled: false);

		// Act
		var registered = IsJobRegistered(config);

		// Assert
		registered.ShouldBeTrue();
	}

	private static bool IsJobRegistered(IConfiguration config)
	{
		var services = new ServiceCollection();
		_ = services.AddQuartz(q => CdcJob.ConfigureJob(q, config));
		using var provider = services.BuildServiceProvider();

		var quartzOptions = provider.GetRequiredService<IOptions<QuartzOptions>>().Value;
		return quartzOptions.JobDetails.Any(j => j.Key.Equals(new JobKey("CdcJob", "TestGroup")));
	}

	private static IConfiguration BuildJobConfig(bool disabled) =>
		new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
			{
				["Jobs:CdcJob:JobName"] = "CdcJob",
				["Jobs:CdcJob:JobGroup"] = "TestGroup",
				["Jobs:CdcJob:CronSchedule"] = "0/30 * * * * ?",
				["Jobs:CdcJob:Disabled"] = disabled ? "true" : "false",
			})
			.Build();
}
