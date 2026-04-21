// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.LeaderElection.DependencyInjection;
using Excalibur.LeaderElection.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

namespace Excalibur.LeaderElection.Tests.SqlServer.Builders;

/// <summary>
/// Unit tests for <see cref="ISqlServerLeaderElectionBuilder"/> — connection overloads,
/// LockResource, last-wins semantics, and ValidateOnStart.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerLeaderElectionBuilderShould
{
	private const string TestConnectionString =
		"Server=localhost;Database=TestDb;Integrated Security=true;TrustServerCertificate=true;";

	private sealed class TestLeaderElectionBuilder : ILeaderElectionBuilder
	{
		public IServiceCollection Services { get; } = new ServiceCollection();
	}

	// --- Connection overloads ---

	[Fact]
	public void ConnectionString_SetConnectionStringOnOptions()
	{
		var builder = new TestLeaderElectionBuilder();
		builder.UseSqlServer(sql =>
			sql.ConnectionString(TestConnectionString).LockResource("test"));

		var provider = builder.Services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<SqlServerLeaderElectionOptions>>();
		options.Value.ConnectionString.ShouldBe(TestConnectionString);
	}

	[Fact]
	public void ConnectionFactory_RegisterFactory()
	{
		var builder = new TestLeaderElectionBuilder();
		builder.UseSqlServer(sql =>
			sql.ConnectionFactory(_ => () => new SqlConnection(TestConnectionString))
			   .LockResource("test"));

		var provider = builder.Services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<SqlServerLeaderElectionOptions>>();
		options.Value.ConnectionString.ShouldBeNull();
	}

	[Fact]
	public void ConnectionStringName_RegisterName()
	{
		var builder = new TestLeaderElectionBuilder();
		builder.UseSqlServer(sql =>
			sql.ConnectionStringName("LeaderDb").LockResource("test"));

		var provider = builder.Services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<SqlServerLeaderElectionOptions>>();
		options.Value.ConnectionString.ShouldBeNull();
	}

	[Fact]
	public void BindConfiguration_RegisterPath()
	{
		var builder = new TestLeaderElectionBuilder();
		builder.UseSqlServer(sql =>
			sql.BindConfiguration("LeaderElection:SqlServer").LockResource("test"));

		builder.Services.ShouldNotBeEmpty();
	}

	// --- LockResource ---

	[Fact]
	public void LockResource_SetLockResourceOnOptions()
	{
		var builder = new TestLeaderElectionBuilder();
		builder.UseSqlServer(sql =>
			sql.ConnectionString(TestConnectionString).LockResource("MyApp.Leader"));

		var provider = builder.Services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<SqlServerLeaderElectionOptions>>();
		options.Value.LockResource.ShouldBe("MyApp.Leader");
	}

	// --- Last-wins ---

	[Fact]
	public void ConnectionFactory_ClearConnectionString()
	{
		var builder = new TestLeaderElectionBuilder();
		builder.UseSqlServer(sql =>
		{
			sql.ConnectionString(TestConnectionString);
			sql.ConnectionFactory(_ => () => new SqlConnection());
			sql.LockResource("test");
		});

		var provider = builder.Services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<SqlServerLeaderElectionOptions>>();
		options.Value.ConnectionString.ShouldBeNull();
	}

	[Fact]
	public void ConnectionString_ClearFactory()
	{
		var builder = new TestLeaderElectionBuilder();
		builder.UseSqlServer(sql =>
		{
			sql.ConnectionFactory(_ => () => new SqlConnection());
			sql.ConnectionString(TestConnectionString);
			sql.LockResource("test");
		});

		var provider = builder.Services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<SqlServerLeaderElectionOptions>>();
		options.Value.ConnectionString.ShouldBe(TestConnectionString);
	}

	// --- Argument guards ---

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ConnectionString_ThrowOnInvalidValue(string? invalidValue)
	{
		var builder = new TestLeaderElectionBuilder();
		Should.Throw<ArgumentException>(() =>
			builder.UseSqlServer(sql => sql.ConnectionString(invalidValue!)));
	}

	[Fact]
	public void ConnectionFactory_ThrowOnNull()
	{
		var builder = new TestLeaderElectionBuilder();
		Should.Throw<ArgumentNullException>(() =>
			builder.UseSqlServer(sql => sql.ConnectionFactory(null!)));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ConnectionStringName_ThrowOnInvalidValue(string? invalidValue)
	{
		var builder = new TestLeaderElectionBuilder();
		Should.Throw<ArgumentException>(() =>
			builder.UseSqlServer(sql => sql.ConnectionStringName(invalidValue!)));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void BindConfiguration_ThrowOnInvalidValue(string? invalidValue)
	{
		var builder = new TestLeaderElectionBuilder();
		Should.Throw<ArgumentException>(() =>
			builder.UseSqlServer(sql => sql.BindConfiguration(invalidValue!)));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void LockResource_ThrowOnInvalidValue(string? invalidValue)
	{
		var builder = new TestLeaderElectionBuilder();
		Should.Throw<ArgumentException>(() =>
			builder.UseSqlServer(sql =>
				sql.ConnectionString(TestConnectionString).LockResource(invalidValue!)));
	}

	// --- Entry point ---

	[Fact]
	public void UseSqlServer_ReturnBuilderForChaining()
	{
		var builder = new TestLeaderElectionBuilder();
		var result = builder.UseSqlServer(sql =>
			sql.ConnectionString(TestConnectionString).LockResource("test"));

		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void UseSqlServer_ThrowOnNullBuilder()
	{
		Should.Throw<ArgumentNullException>(() =>
			((ILeaderElectionBuilder)null!).UseSqlServer(sql =>
				sql.ConnectionString(TestConnectionString)));
	}

	[Fact]
	public void UseSqlServer_ThrowOnNullConfigure()
	{
		var builder = new TestLeaderElectionBuilder();
		Should.Throw<ArgumentNullException>(() =>
			builder.UseSqlServer((Action<ISqlServerLeaderElectionBuilder>)null!));
	}
}
