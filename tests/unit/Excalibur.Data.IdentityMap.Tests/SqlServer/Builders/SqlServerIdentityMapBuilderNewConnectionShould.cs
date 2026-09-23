// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.IdentityMap.Builders;
using Excalibur.Data.IdentityMap.SqlServer;
using Excalibur.Data.IdentityMap.SqlServer.Builders;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

namespace Excalibur.Data.IdentityMap.Tests.SqlServer.Builders;

/// <summary>
/// Tests for the connection overloads on <see cref="ISqlServerIdentityMapBuilder"/>
/// (ConnectionFactory, ConnectionStringName).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerIdentityMapBuilderNewConnectionShould
{
	private const string TestConnectionString =
		"Server=localhost;Database=TestDb;Integrated Security=true;TrustServerCertificate=true;";

	private sealed class TestIdentityMapBuilder : IIdentityMapBuilder
	{
		public IServiceCollection Services { get; } = new ServiceCollection();
	}

	// --- ConnectionFactory ---

	[Fact]
	public void ConnectionFactory_RegisterFactory()
	{
		var builder = new TestIdentityMapBuilder();
		builder.UseSqlServer(sql =>
			sql.ConnectionFactory(_ => () => new SqlConnection(TestConnectionString)));

		var provider = builder.Services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<SqlServerIdentityMapOptions>>();
		options.Value.ConnectionString.ShouldBeNull();
	}

	[Fact]
	public void ConnectionFactory_ThrowOnNull()
	{
		var builder = new TestIdentityMapBuilder();
		Should.Throw<ArgumentNullException>(() =>
			builder.UseSqlServer(sql => sql.ConnectionFactory(null!)));
	}

	// --- ConnectionStringName ---

	[Fact]
	public void ConnectionStringName_RegisterName()
	{
		var builder = new TestIdentityMapBuilder();
		builder.UseSqlServer(sql => sql.ConnectionStringName("IdentityMapDb"));

		var provider = builder.Services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<SqlServerIdentityMapOptions>>();
		options.Value.ConnectionString.ShouldBeNull();
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ConnectionStringName_ThrowOnInvalidValue(string? invalidValue)
	{
		var builder = new TestIdentityMapBuilder();
		Should.Throw<ArgumentException>(() =>
			builder.UseSqlServer(sql => sql.ConnectionStringName(invalidValue!)));
	}

	// --- Last-wins ---

	[Fact]
	public void ConnectionFactory_ClearConnectionString()
	{
		var builder = new TestIdentityMapBuilder();
		builder.UseSqlServer(sql =>
		{
			sql.ConnectionString(TestConnectionString);
			sql.ConnectionFactory(_ => () => new SqlConnection());
		});

		var provider = builder.Services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<SqlServerIdentityMapOptions>>();
		options.Value.ConnectionString.ShouldBeNull();
	}

	[Fact]
	public void ConnectionString_ClearFactory()
	{
		var builder = new TestIdentityMapBuilder();
		builder.UseSqlServer(sql =>
		{
			sql.ConnectionFactory(_ => () => new SqlConnection());
			sql.ConnectionString(TestConnectionString);
		});

		var provider = builder.Services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<SqlServerIdentityMapOptions>>();
		options.Value.ConnectionString.ShouldBe(TestConnectionString);
	}
}
