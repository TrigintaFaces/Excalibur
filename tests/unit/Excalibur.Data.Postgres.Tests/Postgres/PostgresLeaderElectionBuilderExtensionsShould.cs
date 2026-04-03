// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.LeaderElection.Postgres;
using Excalibur.Dispatch.LeaderElection;
using Excalibur.Dispatch.LeaderElection.DependencyInjection;

namespace Excalibur.Data.Tests.Postgres;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class PostgresLeaderElectionBuilderExtensionsShould
{
	[Fact]
	public void UsePostgres_ThrowWhenBuilderIsNull()
	{
		ILeaderElectionBuilder builder = null!;
		Should.Throw<ArgumentNullException>(() =>
			builder.UsePostgres(o => o.ConnectionString = "Host=localhost;"));
	}

	[Fact]
	public void UsePostgres_ThrowWhenConfigureOptionsIsNull()
	{
		var services = new ServiceCollection();
		var builder = A.Fake<ILeaderElectionBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		Should.Throw<ArgumentNullException>(() =>
			builder.UsePostgres((Action<PostgresLeaderElectionOptions>)null!));
	}

	[Fact]
	public void UsePostgres_ReturnSameBuilderForChaining()
	{
		var services = new ServiceCollection();
		var builder = A.Fake<ILeaderElectionBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		var result = builder.UsePostgres(o => o.ConnectionString = "Host=localhost;");
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void UsePostgres_RegisterPostgresLeaderElectionOptions()
	{
		var services = new ServiceCollection();
		var builder = A.Fake<ILeaderElectionBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		builder.UsePostgres(o => o.ConnectionString = "Host=localhost;Database=test;");

		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IConfigureOptions<PostgresLeaderElectionOptions>));
	}

	[Fact]
	public void UsePostgresFactory_ThrowWhenBuilderIsNull()
	{
		ILeaderElectionBuilder builder = null!;
		Should.Throw<ArgumentNullException>(() =>
			builder.UsePostgresFactory(o => o.ConnectionString = "Host=localhost;"));
	}

	[Fact]
	public void UsePostgresFactory_ThrowWhenConfigureOptionsIsNull()
	{
		var services = new ServiceCollection();
		var builder = A.Fake<ILeaderElectionBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		Should.Throw<ArgumentNullException>(() =>
			builder.UsePostgresFactory((Action<PostgresLeaderElectionOptions>)null!));
	}

	[Fact]
	public void UsePostgresFactory_ReturnSameBuilderForChaining()
	{
		var services = new ServiceCollection();
		var builder = A.Fake<ILeaderElectionBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		var result = builder.UsePostgresFactory(o => o.ConnectionString = "Host=localhost;");
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void UsePostgresFactory_RegisterILeaderElectionFactory()
	{
		var services = new ServiceCollection();
		var builder = A.Fake<ILeaderElectionBuilder>();
		A.CallTo(() => builder.Services).Returns(services);

		builder.UsePostgresFactory(o => o.ConnectionString = "Host=localhost;Database=test;");

		services.ShouldContain(sd =>
			sd.ServiceType == typeof(ILeaderElectionFactory));
	}
}
