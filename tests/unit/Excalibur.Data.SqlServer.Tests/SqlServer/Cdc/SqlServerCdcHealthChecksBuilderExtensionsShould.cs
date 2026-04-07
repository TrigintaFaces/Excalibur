// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc.SqlServer;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Excalibur.Data.Tests.SqlServer.Cdc;

/// <summary>
/// Unit tests for <see cref="SqlServerCdcHealthChecksBuilderExtensions"/>.
/// Uses fully-qualified static calls to disambiguate from the base CdcHealthChecksBuilderExtensions.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait(TraitNames.Feature, TestFeatures.CDC)]
public sealed class SqlServerCdcHealthChecksBuilderExtensionsShould : UnitTestBase
{
	#region AddCdcHealthCheck (Action overload)

	[Fact]
	public void AddCdcHealthCheck_ThrowsOnNullBuilder()
	{
		IHealthChecksBuilder builder = null!;

		Should.Throw<ArgumentNullException>(() =>
			SqlServerCdcHealthChecksBuilderExtensions.AddCdcHealthCheck(builder));
	}

	[Fact]
	public void AddCdcHealthCheck_RegistersHealthCheck_WithDefaults()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = services.AddHealthChecks();

		// Act
		SqlServerCdcHealthChecksBuilderExtensions.AddCdcHealthCheck(builder);
		using var provider = services.BuildServiceProvider();

		// Assert
		var registrations = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations;
		registrations.ShouldContain(r => r.Name == "cdc");
	}

	[Fact]
	public void AddCdcHealthCheck_RegistersHealthCheck_WithCustomName()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = services.AddHealthChecks();

		// Act
		SqlServerCdcHealthChecksBuilderExtensions.AddCdcHealthCheck(builder, name: "custom-cdc");
		using var provider = services.BuildServiceProvider();

		// Assert
		var registrations = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations;
		registrations.ShouldContain(r => r.Name == "custom-cdc");
	}

	[Fact]
	public void AddCdcHealthCheck_RegistersCdcHealthState_AsSingleton()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = services.AddHealthChecks();

		// Act
		SqlServerCdcHealthChecksBuilderExtensions.AddCdcHealthCheck(builder);
		using var provider = services.BuildServiceProvider();

		// Assert
		var state1 = provider.GetRequiredService<CdcHealthState>();
		var state2 = provider.GetRequiredService<CdcHealthState>();
		state1.ShouldBeSameAs(state2);
	}

	[Fact]
	public void AddCdcHealthCheck_WithConfigure_BindsOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = services.AddHealthChecks();

		// Act
		SqlServerCdcHealthChecksBuilderExtensions.AddCdcHealthCheck(builder, configure: opts =>
		{
			opts.DegradedLagThreshold = 5000;
		});
		using var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<CdcHealthCheckOptions>>().Value;
		options.DegradedLagThreshold.ShouldBe(5000);
	}

	[Fact]
	public void AddCdcHealthCheck_WithNullConfigure_DoesNotThrow()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = services.AddHealthChecks();

		// Act & Assert -- null configure is allowed (optional parameter)
		Should.NotThrow(() =>
			SqlServerCdcHealthChecksBuilderExtensions.AddCdcHealthCheck(builder, configure: null));
	}

	[Fact]
	public void AddCdcHealthCheck_WithFailureStatus_SetsRegistrationStatus()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = services.AddHealthChecks();

		// Act
		SqlServerCdcHealthChecksBuilderExtensions.AddCdcHealthCheck(builder, failureStatus: HealthStatus.Degraded);
		using var provider = services.BuildServiceProvider();

		// Assert
		var registrations = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations;
		registrations.ShouldContain(r => r.Name == "cdc" && r.FailureStatus == HealthStatus.Degraded);
	}

	[Fact]
	public void AddCdcHealthCheck_WithTags_SetsRegistrationTags()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = services.AddHealthChecks();

		// Act
		SqlServerCdcHealthChecksBuilderExtensions.AddCdcHealthCheck(builder, tags: ["cdc", "database"]);
		using var provider = services.BuildServiceProvider();

		// Assert
		var registrations = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations;
		var reg = registrations.ShouldHaveSingleItem();
		reg.Tags.ShouldContain("cdc");
		reg.Tags.ShouldContain("database");
	}

	[Fact]
	public void AddCdcHealthCheck_ReturnsSameBuilder_ForChaining()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = services.AddHealthChecks();

		// Act
		var result = SqlServerCdcHealthChecksBuilderExtensions.AddCdcHealthCheck(builder);

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region AddCdcHealthCheck (IConfiguration overload)

	[Fact]
	public void AddCdcHealthCheck_Configuration_ThrowsOnNullBuilder()
	{
		IHealthChecksBuilder builder = null!;
		var config = new ConfigurationBuilder().Build();

		Should.Throw<ArgumentNullException>(() =>
			SqlServerCdcHealthChecksBuilderExtensions.AddCdcHealthCheck(builder, config));
	}

	[Fact]
	public void AddCdcHealthCheck_Configuration_ThrowsOnNullConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = services.AddHealthChecks();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			SqlServerCdcHealthChecksBuilderExtensions.AddCdcHealthCheck(builder, configuration: null!));
	}

	[Fact]
	public void AddCdcHealthCheck_Configuration_RegistersHealthCheck()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = services.AddHealthChecks();
		var config = new ConfigurationBuilder().Build();

		// Act
		SqlServerCdcHealthChecksBuilderExtensions.AddCdcHealthCheck(builder, config);
		using var provider = services.BuildServiceProvider();

		// Assert
		var registrations = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations;
		registrations.ShouldContain(r => r.Name == "cdc");
	}

	[Fact]
	public void AddCdcHealthCheck_Configuration_ReturnsSameBuilder_ForChaining()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = services.AddHealthChecks();
		var config = new ConfigurationBuilder().Build();

		// Act
		var result = SqlServerCdcHealthChecksBuilderExtensions.AddCdcHealthCheck(builder, config);

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion
}
