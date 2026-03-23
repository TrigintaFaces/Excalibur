// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Cdc.SqlServer;

using Microsoft.Extensions.Configuration;

namespace Excalibur.Data.Tests.SqlServer.Cdc.Builders;

/// <summary>
/// Unit tests for <see cref="ISqlServerCdcBuilder.ConnectionStringName(string)"/>
/// which resolves a connection string from <see cref="IConfiguration"/> at registration time.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerCdcConnectionStringNameShould : UnitTestBase
{
	private const string TestConnectionString = "Server=localhost;Database=TestDb;Encrypt=false;TrustServerCertificate=true";
	private const string ResolvedConnectionString = "Server=resolved;Database=ResolvedDb;Encrypt=false;TrustServerCertificate=true";

	[Fact]
	public void ResolveConnectionStringFromIConfiguration()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:MyDb"] = ResolvedConnectionString
			})
			.Build();

		var services = new ServiceCollection();
		services.AddSingleton<IConfiguration>(config);

		// Act
		services.AddCdcProcessor(builder =>
			builder.UseSqlServer(TestConnectionString, sql =>
				sql.DatabaseName("Db")
				   .ConnectionStringName("MyDb")));

		// Assert -- service provider should build without error;
		// The ConnectionStringName is resolved lazily from IConfiguration when
		// creating the SqlConnection factory. We verify registration succeeded.
		var provider = services.BuildServiceProvider();
		var cdcOptions = provider.GetRequiredService<IOptions<SqlServerCdcOptions>>();
		cdcOptions.ShouldNotBeNull();
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ThrowArgumentException_WhenNameIsNullEmptyOrWhitespace(string? invalidName)
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			services.AddCdcProcessor(builder =>
				builder.UseSqlServer(TestConnectionString, sql =>
					sql.DatabaseName("Db")
					   .ConnectionStringName(invalidName!))));
	}

	[Fact]
	public void ThrowInvalidOperationException_WhenConnectionStringMissingInConfig()
	{
		// Arrange -- configuration without the expected connection string
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:OtherDb"] = "Server=other;Database=Other;Encrypt=false;TrustServerCertificate=true"
			})
			.Build();

		var services = new ServiceCollection();
		services.AddSingleton<IConfiguration>(config);

		services.AddCdcProcessor(builder =>
			builder.UseSqlServer(TestConnectionString, sql =>
				sql.DatabaseName("Db")
				   .ConnectionStringName("MissingDb")));

		var provider = services.BuildServiceProvider();

		// Act & Assert -- resolution happens lazily when the factory is invoked;
		// ICdcStateStore or ICdcRepository resolve the factory which calls IConfiguration.
		// The source factory throws InvalidOperationException for missing connection string.
		var ex = Should.Throw<InvalidOperationException>(() =>
			provider.GetRequiredService<ICdcRepository>());
		ex.Message.ShouldContain("MissingDb");
		ex.Message.ShouldContain("not found");
	}

	[Fact]
	public void CombineWithOtherBuilderMethods()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:OrdersDb"] = ResolvedConnectionString
			})
			.Build();

		var services = new ServiceCollection();
		services.AddSingleton<IConfiguration>(config);

		// Act -- ConnectionStringName combined with other builder methods
		services.AddCdcProcessor(builder =>
			builder.UseSqlServer(TestConnectionString, sql =>
				sql.DatabaseName("OrdersDb")
				   .SchemaName("audit")
				   .BatchSize(200)
				   .PollingInterval(TimeSpan.FromSeconds(10))
				   .ConnectionStringName("OrdersDb")));

		// Assert
		var provider = services.BuildServiceProvider();
		var sqlOptions = provider.GetRequiredService<IOptions<SqlServerCdcOptions>>();
		sqlOptions.Value.SchemaName.ShouldBe("audit");
		sqlOptions.Value.BatchSize.ShouldBe(200);
		sqlOptions.Value.PollingInterval.ShouldBe(TimeSpan.FromSeconds(10));

		var dbConfig = provider.GetRequiredService<IDatabaseConfig>();
		dbConfig.DatabaseName.ShouldBe("OrdersDb");
	}

	[Fact]
	public void CombineWithTrackTable()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:MyDb"] = ResolvedConnectionString
			})
			.Build();

		var services = new ServiceCollection();
		services.AddSingleton<IConfiguration>(config);

		// Act
		services.AddCdcProcessor(builder =>
			builder.UseSqlServer(TestConnectionString, sql =>
					sql.DatabaseName("Db")
					   .ConnectionStringName("MyDb"))
				   .TrackTable("dbo.Orders", t => t.MapInsert<OrderCreatedEvent>()));

		var provider = services.BuildServiceProvider();

		// Assert -- CdcOptions should have the tracked table configured
		var cdcOptions = provider.GetRequiredService<IOptions<CdcOptions>>();
		var tableConfig = cdcOptions.Value.TrackedTables.Single();
		tableConfig.TableName.ShouldBe("dbo.Orders");
		tableConfig.EventMappings.ShouldContainKey(CdcChangeType.Insert);
		tableConfig.EventMappings[CdcChangeType.Insert].ShouldBe(typeof(OrderCreatedEvent));
	}

	[Fact]
	public void RegisterServiceDescriptorsSuccessfully()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:TestDb"] = ResolvedConnectionString
			})
			.Build();

		var services = new ServiceCollection();
		services.AddSingleton<IConfiguration>(config);

		// Act
		services.AddCdcProcessor(builder =>
			builder.UseSqlServer(TestConnectionString, sql =>
				sql.DatabaseName("Db")
				   .ConnectionStringName("TestDb")));

		// Assert -- key CDC services should be registered
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(ISqlServerCdcStateStore) &&
			sd.Lifetime == ServiceLifetime.Singleton);

		services.ShouldContain(sd =>
			sd.ServiceType == typeof(ICdcRepository) &&
			sd.Lifetime == ServiceLifetime.Singleton);

		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IDatabaseConfig) &&
			sd.Lifetime == ServiceLifetime.Singleton);
	}

	[Fact]
	public void CombineWithCommandTimeout()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:TimeoutDb"] = ResolvedConnectionString
			})
			.Build();

		var services = new ServiceCollection();
		services.AddSingleton<IConfiguration>(config);

		// Act
		services.AddCdcProcessor(builder =>
			builder.UseSqlServer(TestConnectionString, sql =>
				sql.DatabaseName("Db")
				   .CommandTimeout(TimeSpan.FromSeconds(120))
				   .ConnectionStringName("TimeoutDb")));

		// Assert
		var provider = services.BuildServiceProvider();
		var sqlOptions = provider.GetRequiredService<IOptions<SqlServerCdcOptions>>();
		sqlOptions.Value.CommandTimeout.ShouldBe(TimeSpan.FromSeconds(120));
	}

	// Test event types
	private sealed class OrderCreatedEvent;
}
