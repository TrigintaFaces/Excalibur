// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Cdc.SqlServer;

using Microsoft.Data.SqlClient;

namespace Excalibur.Data.Tests.SqlServer.Cdc.Builders;

/// <summary>
/// Unit tests for the new <see cref="ISqlServerCdcBuilder"/> fluent methods
/// (DatabaseName, DatabaseConnectionIdentifier, StateConnectionIdentifier,
/// CaptureInstances, StopOnMissingTableHandler).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerCdcBuilderNewMethodsShould : UnitTestBase
{
	private const string TestConnectionString = "Server=localhost;Database=TestDb;Encrypt=false;TrustServerCertificate=true";

	private static readonly string[] AuditCaptureInstances = ["dbo_AuditLog", "dbo_Events"];
	private static readonly string[] OrderCustomerCaptureInstances = ["dbo_Orders", "dbo_Customers"];

	// --- DatabaseName ---

	[Fact]
	public void DatabaseName_FlowsThroughIDatabaseConfig()
	{
		var services = new ServiceCollection();

		services.AddCdcProcessor(builder =>
			builder.UseSqlServer(sql =>
				sql.ConnectionString(TestConnectionString)
				   .DatabaseName("MyDatabase")));

		var provider = services.BuildServiceProvider();
		var dbConfig = provider.GetRequiredService<IDatabaseOptions>();
		dbConfig.DatabaseName.ShouldBe("MyDatabase");
	}

	[Fact]
	public void DatabaseName_RegistersIDatabaseConfigAutomatically()
	{
		var services = new ServiceCollection();

		services.AddCdcProcessor(builder =>
			builder.UseSqlServer(sql =>
				sql.ConnectionString(TestConnectionString)
				   .DatabaseName("OrdersDb")));

		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IDatabaseOptions) &&
			sd.Lifetime == ServiceLifetime.Singleton);
	}

	[Fact]
	public void DatabaseName_SetsDefaultConnectionIdentifiers()
	{
		var services = new ServiceCollection();

		services.AddCdcProcessor(builder =>
			builder.UseSqlServer(sql =>
				sql.ConnectionString(TestConnectionString)
				   .DatabaseName("OrdersDb")));

		var provider = services.BuildServiceProvider();
		var dbConfig = provider.GetRequiredService<IDatabaseOptions>();
		dbConfig.DatabaseName.ShouldBe("OrdersDb");
		dbConfig.DatabaseConnectionIdentifier.ShouldBe("cdc-OrdersDb");
		dbConfig.StateConnectionIdentifier.ShouldBe("state-OrdersDb");
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void DatabaseName_ThrowsOnInvalidValue(string? invalidValue)
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentException>(() =>
			services.AddCdcProcessor(builder =>
				builder.UseSqlServer(sql =>
					sql.ConnectionString(TestConnectionString)
					   .DatabaseName(invalidValue!))));
	}

	// --- DatabaseConnectionIdentifier ---

	[Fact]
	public void DatabaseConnectionIdentifier_SetsOptionValue()
	{
		var services = new ServiceCollection();

		services.AddCdcProcessor(builder =>
			builder.UseSqlServer(sql =>
				sql.ConnectionString(TestConnectionString)
				   .DatabaseName("Db")
				   .DatabaseConnectionIdentifier("my-db-conn")));

		var provider = services.BuildServiceProvider();
		var dbConfig = provider.GetRequiredService<IDatabaseOptions>();
		dbConfig.DatabaseConnectionIdentifier.ShouldBe("my-db-conn");
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void DatabaseConnectionIdentifier_ThrowsOnInvalidValue(string? invalidValue)
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentException>(() =>
			services.AddCdcProcessor(builder =>
				builder.UseSqlServer(sql =>
					sql.ConnectionString(TestConnectionString)
					   .DatabaseConnectionIdentifier(invalidValue!))));
	}

	// --- StateConnectionIdentifier ---

	[Fact]
	public void StateConnectionIdentifier_SetsOptionValue()
	{
		var services = new ServiceCollection();

		services.AddCdcProcessor(builder =>
			builder.UseSqlServer(sql =>
				sql.ConnectionString(TestConnectionString)
				   .DatabaseName("Db")
				   .StateConnectionIdentifier("my-state-conn")));

		var provider = services.BuildServiceProvider();
		var dbConfig = provider.GetRequiredService<IDatabaseOptions>();
		dbConfig.StateConnectionIdentifier.ShouldBe("my-state-conn");
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void StateConnectionIdentifier_ThrowsOnInvalidValue(string? invalidValue)
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentException>(() =>
			services.AddCdcProcessor(builder =>
				builder.UseSqlServer(sql =>
					sql.ConnectionString(TestConnectionString)
					   .StateConnectionIdentifier(invalidValue!))));
	}

	// --- CaptureInstances ---

	[Fact]
	public void CaptureInstances_SetsOptionValue()
	{
		var services = new ServiceCollection();

		services.AddCdcProcessor(builder =>
			builder.UseSqlServer(sql =>
				sql.ConnectionString(TestConnectionString)
				   .DatabaseName("Db")
				   .CaptureInstances("dbo_Orders", "dbo_Customers")));

		var provider = services.BuildServiceProvider();
		var dbConfig = provider.GetRequiredService<IDatabaseOptions>();
		dbConfig.CaptureInstances.ShouldBe(OrderCustomerCaptureInstances);
	}

	[Fact]
	public void CaptureInstances_ThrowsOnNull()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentNullException>(() =>
			services.AddCdcProcessor(builder =>
				builder.UseSqlServer(sql =>
					sql.ConnectionString(TestConnectionString)
					   .CaptureInstances(null!))));
	}

	[Fact]
	public void CaptureInstances_DefaultsToEmptyWhenNotConfigured()
	{
		var services = new ServiceCollection();

		services.AddCdcProcessor(builder =>
			builder.UseSqlServer(sql =>
				sql.ConnectionString(TestConnectionString)
				   .DatabaseName("Db")));

		var provider = services.BuildServiceProvider();
		var dbConfig = provider.GetRequiredService<IDatabaseOptions>();
		dbConfig.CaptureInstances.ShouldBeEmpty();
	}

	// --- StopOnMissingTableHandler ---

	[Fact]
	public void StopOnMissingTableHandler_DefaultsToTrue()
	{
		var services = new ServiceCollection();

		services.AddCdcProcessor(builder =>
			builder.UseSqlServer(sql =>
				sql.ConnectionString(TestConnectionString)
				   .DatabaseName("Db")));

		var provider = services.BuildServiceProvider();
		var dbConfig = provider.GetRequiredService<IDatabaseOptions>();
		dbConfig.StopOnMissingTableHandler.ShouldBeTrue();
	}

	[Fact]
	public void StopOnMissingTableHandler_CanBeSetToFalse()
	{
		var services = new ServiceCollection();

		services.AddCdcProcessor(builder =>
			builder.UseSqlServer(sql =>
				sql.ConnectionString(TestConnectionString)
				   .DatabaseName("Db")
				   .StopOnMissingTableHandler(false)));

		var provider = services.BuildServiceProvider();
		var dbConfig = provider.GetRequiredService<IDatabaseOptions>();
		dbConfig.StopOnMissingTableHandler.ShouldBeFalse();
	}

	// --- Fluent chaining ---

	[Fact]
	public void FluentChain_ConfiguresAllNewOptions()
	{
		var services = new ServiceCollection();

		services.AddCdcProcessor(builder =>
			builder.UseSqlServer(sql =>
				sql.ConnectionString(TestConnectionString)
				   .SchemaName("audit")
				   .StateTableName("AuditState")
				   .BatchSize(200)
				   .PollingInterval(TimeSpan.FromSeconds(10))
				   .CommandTimeout(TimeSpan.FromSeconds(60))
				   .DatabaseName("AuditDb")
				   .DatabaseConnectionIdentifier("audit-conn")
				   .StateConnectionIdentifier("audit-state-conn")
				   .CaptureInstances("dbo_AuditLog", "dbo_Events")
				   .StopOnMissingTableHandler(false)));

		var provider = services.BuildServiceProvider();

		var sqlOptions = provider.GetRequiredService<IOptions<SqlServerCdcOptions>>();
		sqlOptions.Value.SchemaName.ShouldBe("audit");
		sqlOptions.Value.StateTableName.ShouldBe("AuditState");
		sqlOptions.Value.BatchSize.ShouldBe(200);
		sqlOptions.Value.PollingInterval.ShouldBe(TimeSpan.FromSeconds(10));
		sqlOptions.Value.CommandTimeout.ShouldBe(TimeSpan.FromSeconds(60));

		var dbConfig = provider.GetRequiredService<IDatabaseOptions>();
		dbConfig.DatabaseName.ShouldBe("AuditDb");
		dbConfig.DatabaseConnectionIdentifier.ShouldBe("audit-conn");
		dbConfig.StateConnectionIdentifier.ShouldBe("audit-state-conn");
		dbConfig.CaptureInstances.ShouldBe(AuditCaptureInstances);
		dbConfig.StopOnMissingTableHandler.ShouldBeFalse();
	}

	// --- Without DatabaseName, IDatabaseOptions should NOT be registered ---

	[Fact]
	public void NoDatabaseName_DoesNotRegisterIDatabaseConfig()
	{
		var services = new ServiceCollection();

		services.AddCdcProcessor(builder =>
			builder.UseSqlServer(sql =>
				sql.ConnectionString(TestConnectionString)
				   .SchemaName("cdc")
				   .BatchSize(50)));

		services.ShouldNotContain(sd =>
			sd.ServiceType == typeof(IDatabaseOptions));
	}

	// --- Connection factory overload also supports new methods ---

	[Fact]
	public void ConnectionFactoryOverload_SupportsNewFluentMethods()
	{
		var services = new ServiceCollection();

		services.AddCdcProcessor(builder =>
			builder.UseSqlServer(sql =>
				sql.ConnectionFactory(_ => () => new SqlConnection(TestConnectionString))
				   .DatabaseName("FactoryDb")
				   .DatabaseConnectionIdentifier("factory-conn")
				   .StateConnectionIdentifier("factory-state")
				   .CaptureInstances("dbo_Items")
				   .StopOnMissingTableHandler(false)));

		var provider = services.BuildServiceProvider();
		var dbConfig = provider.GetRequiredService<IDatabaseOptions>();
		dbConfig.DatabaseName.ShouldBe("FactoryDb");
		dbConfig.DatabaseConnectionIdentifier.ShouldBe("factory-conn");
		dbConfig.StateConnectionIdentifier.ShouldBe("factory-state");
		dbConfig.CaptureInstances.ShouldBe(["dbo_Items"]);
		dbConfig.StopOnMissingTableHandler.ShouldBeFalse();
	}
}
