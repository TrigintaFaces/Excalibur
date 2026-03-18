// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Cdc.SqlServer;

using Microsoft.Data.SqlClient;

namespace Excalibur.Data.Tests.SqlServer.Cdc.Builders;

/// <summary>
/// Unit tests for the new <see cref="ISqlServerCdcBuilder"/> fluent methods
/// added in the DX improvements sprint (DatabaseName, DatabaseConnectionIdentifier,
/// StateConnectionIdentifier, CaptureInstances, StopOnMissingTableHandler).
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
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddCdcProcessor(builder =>
			builder.UseSqlServer(TestConnectionString, sql =>
				sql.DatabaseName("MyDatabase")));

		// Assert - DatabaseName flows through IDatabaseConfig, not IOptions<SqlServerCdcOptions>
		var provider = services.BuildServiceProvider();
		var dbConfig = provider.GetRequiredService<IDatabaseConfig>();
		dbConfig.DatabaseName.ShouldBe("MyDatabase");
	}

	[Fact]
	public void DatabaseName_RegistersIDatabaseConfigAutomatically()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddCdcProcessor(builder =>
			builder.UseSqlServer(TestConnectionString, sql =>
				sql.DatabaseName("OrdersDb")));

		// Assert - IDatabaseConfig should be auto-registered
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IDatabaseConfig) &&
			sd.Lifetime == ServiceLifetime.Singleton);
	}

	[Fact]
	public void DatabaseName_SetsDefaultConnectionIdentifiers()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddCdcProcessor(builder =>
			builder.UseSqlServer(TestConnectionString, sql =>
				sql.DatabaseName("OrdersDb")));

		// Assert - defaults derived from database name
		var provider = services.BuildServiceProvider();
		var dbConfig = provider.GetRequiredService<IDatabaseConfig>();
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
				builder.UseSqlServer(TestConnectionString, sql =>
					sql.DatabaseName(invalidValue!))));
	}

	// --- DatabaseConnectionIdentifier ---

	[Fact]
	public void DatabaseConnectionIdentifier_SetsOptionValue()
	{
		var services = new ServiceCollection();

		services.AddCdcProcessor(builder =>
			builder.UseSqlServer(TestConnectionString, sql =>
				sql.DatabaseName("Db").DatabaseConnectionIdentifier("my-db-conn")));

		var provider = services.BuildServiceProvider();
		var dbConfig = provider.GetRequiredService<IDatabaseConfig>();
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
				builder.UseSqlServer(TestConnectionString, sql =>
					sql.DatabaseConnectionIdentifier(invalidValue!))));
	}

	// --- StateConnectionIdentifier ---

	[Fact]
	public void StateConnectionIdentifier_SetsOptionValue()
	{
		var services = new ServiceCollection();

		services.AddCdcProcessor(builder =>
			builder.UseSqlServer(TestConnectionString, sql =>
				sql.DatabaseName("Db").StateConnectionIdentifier("my-state-conn")));

		var provider = services.BuildServiceProvider();
		var dbConfig = provider.GetRequiredService<IDatabaseConfig>();
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
				builder.UseSqlServer(TestConnectionString, sql =>
					sql.StateConnectionIdentifier(invalidValue!))));
	}

	// --- CaptureInstances ---

	[Fact]
	public void CaptureInstances_SetsOptionValue()
	{
		var services = new ServiceCollection();

		services.AddCdcProcessor(builder =>
			builder.UseSqlServer(TestConnectionString, sql =>
				sql.DatabaseName("Db").CaptureInstances("dbo_Orders", "dbo_Customers")));

		var provider = services.BuildServiceProvider();
		var dbConfig = provider.GetRequiredService<IDatabaseConfig>();
		dbConfig.CaptureInstances.ShouldBe(OrderCustomerCaptureInstances);
	}

	[Fact]
	public void CaptureInstances_ThrowsOnNull()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentNullException>(() =>
			services.AddCdcProcessor(builder =>
				builder.UseSqlServer(TestConnectionString, sql =>
					sql.CaptureInstances(null!))));
	}

	[Fact]
	public void CaptureInstances_DefaultsToEmptyWhenNotConfigured()
	{
		var services = new ServiceCollection();

		services.AddCdcProcessor(builder =>
			builder.UseSqlServer(TestConnectionString, sql =>
				sql.DatabaseName("Db")));

		var provider = services.BuildServiceProvider();
		var dbConfig = provider.GetRequiredService<IDatabaseConfig>();
		dbConfig.CaptureInstances.ShouldBeEmpty();
	}

	// --- StopOnMissingTableHandler ---

	[Fact]
	public void StopOnMissingTableHandler_DefaultsToTrue()
	{
		var services = new ServiceCollection();

		services.AddCdcProcessor(builder =>
			builder.UseSqlServer(TestConnectionString, sql =>
				sql.DatabaseName("Db")));

		var provider = services.BuildServiceProvider();
		var dbConfig = provider.GetRequiredService<IDatabaseConfig>();
		dbConfig.StopOnMissingTableHandler.ShouldBeTrue();
	}

	[Fact]
	public void StopOnMissingTableHandler_CanBeSetToFalse()
	{
		var services = new ServiceCollection();

		services.AddCdcProcessor(builder =>
			builder.UseSqlServer(TestConnectionString, sql =>
				sql.DatabaseName("Db").StopOnMissingTableHandler(false)));

		var provider = services.BuildServiceProvider();
		var dbConfig = provider.GetRequiredService<IDatabaseConfig>();
		dbConfig.StopOnMissingTableHandler.ShouldBeFalse();
	}

	// --- Fluent chaining ---

	[Fact]
	public void FluentChain_ConfiguresAllNewOptions()
	{
		var services = new ServiceCollection();

		services.AddCdcProcessor(builder =>
			builder.UseSqlServer(TestConnectionString, sql =>
				sql.SchemaName("audit")
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

		// Verify SQL Server options
		var sqlOptions = provider.GetRequiredService<IOptions<SqlServerCdcOptions>>();
		sqlOptions.Value.SchemaName.ShouldBe("audit");
		sqlOptions.Value.StateTableName.ShouldBe("AuditState");
		sqlOptions.Value.BatchSize.ShouldBe(200);
		sqlOptions.Value.PollingInterval.ShouldBe(TimeSpan.FromSeconds(10));
		sqlOptions.Value.CommandTimeout.ShouldBe(TimeSpan.FromSeconds(60));

		// Verify IDatabaseConfig
		var dbConfig = provider.GetRequiredService<IDatabaseConfig>();
		dbConfig.DatabaseName.ShouldBe("AuditDb");
		dbConfig.DatabaseConnectionIdentifier.ShouldBe("audit-conn");
		dbConfig.StateConnectionIdentifier.ShouldBe("audit-state-conn");
		dbConfig.CaptureInstances.ShouldBe(AuditCaptureInstances);
		dbConfig.StopOnMissingTableHandler.ShouldBeFalse();
	}

	// --- Without DatabaseName, IDatabaseConfig should NOT be registered ---

	[Fact]
	public void NoDatabaseName_DoesNotRegisterIDatabaseConfig()
	{
		var services = new ServiceCollection();

		services.AddCdcProcessor(builder =>
			builder.UseSqlServer(TestConnectionString, sql =>
				sql.SchemaName("cdc").BatchSize(50)));

		// Assert - no IDatabaseConfig registered (HasDatabaseConfig is false)
		services.ShouldNotContain(sd =>
			sd.ServiceType == typeof(IDatabaseConfig));
	}

	// --- Connection factory overload also supports new methods ---

	[Fact]
	public void ConnectionFactoryOverload_SupportsNewFluentMethods()
	{
		var services = new ServiceCollection();

		services.AddCdcProcessor(builder =>
			builder.UseSqlServer(
				_ => () => new SqlConnection(TestConnectionString),
				sql => sql
					.DatabaseName("FactoryDb")
					.DatabaseConnectionIdentifier("factory-conn")
					.StateConnectionIdentifier("factory-state")
					.CaptureInstances("dbo_Items")
					.StopOnMissingTableHandler(false)));

		var provider = services.BuildServiceProvider();
		var dbConfig = provider.GetRequiredService<IDatabaseConfig>();
		dbConfig.DatabaseName.ShouldBe("FactoryDb");
		dbConfig.DatabaseConnectionIdentifier.ShouldBe("factory-conn");
		dbConfig.StateConnectionIdentifier.ShouldBe("factory-state");
		dbConfig.CaptureInstances.ShouldBe(new[] { "dbo_Items" });
		dbConfig.StopOnMissingTableHandler.ShouldBeFalse();
	}
}
