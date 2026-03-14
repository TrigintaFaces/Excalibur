// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.LeaderElection;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Excalibur.Data.Tests.Postgres;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class PostgresLeaderElectionHealthCheckExtensionsShould
{
	[Fact]
	public void AddPostgresLeaderElectionHealthCheck_ThrowWhenBuilderIsNull()
	{
		IHealthChecksBuilder builder = null!;
		Should.Throw<ArgumentNullException>(() =>
			builder.AddPostgresLeaderElectionHealthCheck());
	}

	[Fact]
	public void AddPostgresLeaderElectionHealthCheck_RegisterHealthCheck()
	{
		var services = new ServiceCollection();
		// Register ILeaderElection so the health check factory can resolve it
		services.AddSingleton(A.Fake<ILeaderElection>());
		var builder = services.AddHealthChecks();

		builder.AddPostgresLeaderElectionHealthCheck();

		var sp = services.BuildServiceProvider();
		var healthCheckOptions = sp.GetRequiredService<IOptions<HealthCheckServiceOptions>>();
		healthCheckOptions.Value.Registrations
			.ShouldContain(r => r.Name == "postgres-leader-election");
	}

	[Fact]
	public void AddPostgresLeaderElectionHealthCheck_UseCustomName()
	{
		var services = new ServiceCollection();
		services.AddSingleton(A.Fake<ILeaderElection>());
		var builder = services.AddHealthChecks();

		builder.AddPostgresLeaderElectionHealthCheck(name: "custom-pg-le");

		var sp = services.BuildServiceProvider();
		var healthCheckOptions = sp.GetRequiredService<IOptions<HealthCheckServiceOptions>>();
		healthCheckOptions.Value.Registrations
			.ShouldContain(r => r.Name == "custom-pg-le");
	}

	[Fact]
	public void AddPostgresLeaderElectionHealthCheck_UseCustomFailureStatus()
	{
		var services = new ServiceCollection();
		services.AddSingleton(A.Fake<ILeaderElection>());
		var builder = services.AddHealthChecks();

		builder.AddPostgresLeaderElectionHealthCheck(failureStatus: HealthStatus.Degraded);

		var sp = services.BuildServiceProvider();
		var healthCheckOptions = sp.GetRequiredService<IOptions<HealthCheckServiceOptions>>();
		var registration = healthCheckOptions.Value.Registrations
			.Single(r => r.Name == "postgres-leader-election");
		registration.FailureStatus.ShouldBe(HealthStatus.Degraded);
	}

	[Fact]
	public void AddPostgresLeaderElectionHealthCheck_UseCustomTags()
	{
		var services = new ServiceCollection();
		services.AddSingleton(A.Fake<ILeaderElection>());
		var builder = services.AddHealthChecks();

		string[] customTags = ["custom", "test"];
		builder.AddPostgresLeaderElectionHealthCheck(tags: customTags);

		var sp = services.BuildServiceProvider();
		var healthCheckOptions = sp.GetRequiredService<IOptions<HealthCheckServiceOptions>>();
		var registration = healthCheckOptions.Value.Registrations
			.Single(r => r.Name == "postgres-leader-election");
		registration.Tags.ShouldBe(customTags);
	}

	[Fact]
	public void AddPostgresLeaderElectionHealthCheck_ReturnBuilderForChaining()
	{
		var services = new ServiceCollection();
		services.AddSingleton(A.Fake<ILeaderElection>());
		var builder = services.AddHealthChecks();

		var result = builder.AddPostgresLeaderElectionHealthCheck();
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void AddPostgresLeaderElectionHealthCheck_DefaultTagsIncludeLeaderElectionAndPostgres()
	{
		var services = new ServiceCollection();
		services.AddSingleton(A.Fake<ILeaderElection>());
		var builder = services.AddHealthChecks();

		builder.AddPostgresLeaderElectionHealthCheck();

		var sp = services.BuildServiceProvider();
		var healthCheckOptions = sp.GetRequiredService<IOptions<HealthCheckServiceOptions>>();
		var registration = healthCheckOptions.Value.Registrations
			.Single(r => r.Name == "postgres-leader-election");
		registration.Tags.ShouldContain("leader-election");
		registration.Tags.ShouldContain("postgres");
	}
}
