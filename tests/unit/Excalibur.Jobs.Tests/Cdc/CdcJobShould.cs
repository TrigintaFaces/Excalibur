// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.SqlServer.Cdc;
using Excalibur.Jobs.Cdc;
using Excalibur.Jobs.Core;

using FakeItEasy;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

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
	private readonly IOptions<CdcJobConfig> _fakeCdcConfigOptions;
	private readonly JobHeartbeatTracker _heartbeatTracker;

	public CdcJobShould()
	{
		_fakeFactory = A.Fake<IDataChangeEventProcessorFactory>();
		_fakeConnectionFactory = A.Fake<Func<string, SqlConnection>>();
		_fakeCdcConfigOptions = Microsoft.Extensions.Options.Options.Create(new CdcJobConfig { DatabaseConfigs = [] });
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
}
