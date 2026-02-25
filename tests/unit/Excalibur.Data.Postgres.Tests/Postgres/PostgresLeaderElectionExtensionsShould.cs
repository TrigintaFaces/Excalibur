// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Postgres.LeaderElection;
using Excalibur.Dispatch.LeaderElection;
using Excalibur.LeaderElection.Diagnostics;

namespace Excalibur.Data.Tests.Postgres;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class PostgresLeaderElectionExtensionsShould
{
	[Fact]
	public void ThrowWhenServicesIsNull_ForConfigureOverload()
	{
		IServiceCollection services = null!;

		Should.Throw<ArgumentNullException>(
			() => services.AddPostgresLeaderElection(
				opts => opts.ConnectionString = "Host=localhost;"));
	}

	[Fact]
	public void ThrowWhenConfigureOptionsIsNull()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentNullException>(
			() => services.AddPostgresLeaderElection(
				(Action<PostgresLeaderElectionOptions>)null!));
	}

	[Fact]
	public void RegisterLeaderElectionServices()
	{
		var services = new ServiceCollection();

		services.AddPostgresLeaderElection(opts =>
		{
			opts.ConnectionString = "Host=localhost;Database=test;";
		});

		services.ShouldContain(sd => sd.ServiceType == typeof(PostgresLeaderElection));
		services.ShouldContain(sd => sd.ServiceType == typeof(ILeaderElection));
	}

	[Fact]
	public void ReturnSameServiceCollectionForChaining()
	{
		var services = new ServiceCollection();

		var result = services.AddPostgresLeaderElection(opts =>
		{
			opts.ConnectionString = "Host=localhost;Database=test;";
		});

		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void ThrowWhenServicesIsNull_ForTwoActionOverload()
	{
		IServiceCollection services = null!;

		Should.Throw<ArgumentNullException>(
			() => services.AddPostgresLeaderElection(
				opts => opts.ConnectionString = "Host=localhost;",
				_ => { }));
	}

	[Fact]
	public void ThrowWhenConfigureElectionIsNull()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentNullException>(
			() => services.AddPostgresLeaderElection(
				opts => opts.ConnectionString = "Host=localhost;",
				(Action<LeaderElectionOptions>)null!));
	}

	[Fact]
	public void RegisterLeaderElectionServicesWithBothOptions()
	{
		var services = new ServiceCollection();

		services.AddPostgresLeaderElection(
			opts => opts.ConnectionString = "Host=localhost;Database=test;",
			election => election.LeaseDuration = TimeSpan.FromSeconds(30));

		services.ShouldContain(sd => sd.ServiceType == typeof(PostgresLeaderElection));
		services.ShouldContain(sd => sd.ServiceType == typeof(ILeaderElection));
	}

	[Fact]
	public async Task ResolveLeaderElectionWithoutMeterFactory_UsesFallbackMeter()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddPostgresLeaderElection(
			opts => opts.ConnectionString = "Host=localhost;Database=test;",
			election =>
			{
				election.InstanceId = "instance-a";
				election.LeaseDuration = TimeSpan.FromSeconds(3);
			});

		await using var provider = services.BuildServiceProvider();
		var election = provider.GetRequiredService<ILeaderElection>();
		var options = provider.GetRequiredService<IOptions<LeaderElectionOptions>>().Value;

		election.ShouldNotBeNull();
		options.InstanceId.ShouldBe("instance-a");
		options.LeaseDuration.ShouldBe(TimeSpan.FromSeconds(3));
	}

	[Fact]
	public async Task ResolveLeaderElectionWithMeterFactory_CallsFactory()
	{
		var meterFactory = A.Fake<System.Diagnostics.Metrics.IMeterFactory>();
		A.CallTo(() => meterFactory.Create(
				A<System.Diagnostics.Metrics.MeterOptions>.That.Matches(
					options => options.Name == LeaderElectionTelemetryConstants.MeterName)))
			.ReturnsLazily(call =>
			{
				var options = call.GetArgument<System.Diagnostics.Metrics.MeterOptions>(0);
				return new System.Diagnostics.Metrics.Meter(options.Name, options.Version);
			});

		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton(meterFactory);
		services.AddPostgresLeaderElection(opts => opts.ConnectionString = "Host=localhost;Database=test;");

		await using var provider = services.BuildServiceProvider();
		var election = provider.GetRequiredService<ILeaderElection>();

		election.ShouldNotBeNull();
		A.CallTo(() => meterFactory.Create(A<System.Diagnostics.Metrics.MeterOptions>._))
			.MustHaveHappened();
	}
}
