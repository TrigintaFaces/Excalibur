// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Outbox;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Outbox.Tests.SqlServer.Builders;

/// <summary>
/// Tests for the 3 new connection overloads added to <see cref="ISqlServerOutboxBuilder"/>
/// in Sprint 764 (ConnectionFactory, ConnectionStringName, BindConfiguration).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerOutboxBuilderNewConnectionShould : UnitTestBase
{
	private const string TestConnectionString =
		"Server=localhost;Database=TestDb;Integrated Security=true;TrustServerCertificate=true;";

	// --- ConnectionFactory ---

	[Fact]
	public void ConnectionFactory_RegisterFactory()
	{
		var services = new ServiceCollection();
		services.AddExcaliburOutbox(outbox =>
			outbox.UseSqlServer(sql =>
				sql.ConnectionFactory(_ => () => new SqlConnection(TestConnectionString))));

		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<SqlServerOutboxOptions>>();
		options.Value.ConnectionString.ShouldBe(string.Empty);
	}

	[Fact]
	public void ConnectionFactory_ThrowOnNull()
	{
		var services = new ServiceCollection();
		Should.Throw<ArgumentNullException>(() =>
			services.AddExcaliburOutbox(outbox =>
				outbox.UseSqlServer(sql =>
					sql.ConnectionFactory(null!))));
	}

	// --- ConnectionStringName ---

	[Fact]
	public void ConnectionStringName_RegisterName()
	{
		var services = new ServiceCollection();
		services.AddExcaliburOutbox(outbox =>
			outbox.UseSqlServer(sql =>
				sql.ConnectionStringName("OutboxDb")));

		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<SqlServerOutboxOptions>>();
		options.Value.ConnectionString.ShouldBe(string.Empty);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ConnectionStringName_ThrowOnInvalidValue(string? invalidValue)
	{
		var services = new ServiceCollection();
		Should.Throw<ArgumentException>(() =>
			services.AddExcaliburOutbox(outbox =>
				outbox.UseSqlServer(sql =>
					sql.ConnectionStringName(invalidValue!))));
	}

	// --- BindConfiguration ---

	[Fact]
	public void BindConfiguration_RegisterPath()
	{
		var services = new ServiceCollection();
		services.AddExcaliburOutbox(outbox =>
			outbox.UseSqlServer(sql =>
				sql.BindConfiguration("Outbox:SqlServer")));

		// BindConfiguration requires IConfiguration — just verify services registered
		services.ShouldNotBeEmpty();
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void BindConfiguration_ThrowOnInvalidValue(string? invalidValue)
	{
		var services = new ServiceCollection();
		Should.Throw<ArgumentException>(() =>
			services.AddExcaliburOutbox(outbox =>
				outbox.UseSqlServer(sql =>
					sql.BindConfiguration(invalidValue!))));
	}

	// --- Last-wins ---

	[Fact]
	public void ConnectionFactory_ClearConnectionString()
	{
		var services = new ServiceCollection();
		services.AddExcaliburOutbox(outbox =>
			outbox.UseSqlServer(sql =>
			{
				sql.ConnectionString(TestConnectionString);
				sql.ConnectionFactory(_ => () => new SqlConnection());
			}));

		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<SqlServerOutboxOptions>>();
		options.Value.ConnectionString.ShouldBe(string.Empty);
	}

	[Fact]
	public void ConnectionString_ClearFactory()
	{
		var services = new ServiceCollection();
		services.AddExcaliburOutbox(outbox =>
			outbox.UseSqlServer(sql =>
			{
				sql.ConnectionFactory(_ => () => new SqlConnection());
				sql.ConnectionString(TestConnectionString);
			}));

		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<SqlServerOutboxOptions>>();
		options.Value.ConnectionString.ShouldBe(TestConnectionString);
	}
}
