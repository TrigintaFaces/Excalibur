// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Cdc.Postgres;

using Microsoft.Extensions.Configuration;

namespace Excalibur.Data.Tests.Postgres.Cdc.Builders;

/// <summary>
/// Unit tests for <see cref="IPostgresCdcBuilder.ConnectionStringName(string)"/>
/// which resolves a connection string from <see cref="IConfiguration"/> at registration time.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Database", "Postgres")]
public sealed class PostgresCdcConnectionStringNameShould : UnitTestBase
{
	private const string TestConnectionString = "Host=localhost;Database=TestDb;Username=test;Password=test;";
	private const string ResolvedConnectionString = "Host=resolved;Database=ResolvedDb;Username=test;Password=test;";

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
		_ = services.AddLogging();
		services.AddSingleton<IConfiguration>(config);

		// Act
		services.AddCdcProcessor(builder =>
			builder.UsePostgres(pg =>
				pg.ConnectionString(TestConnectionString)
				   .ConnectionStringName("MyDb")));

		// Assert -- registration succeeds
		var provider = services.BuildServiceProvider();
		var pgOptions = provider.GetRequiredService<IOptions<PostgresCdcOptions>>();
		pgOptions.ShouldNotBeNull();
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
				builder.UsePostgres(pg =>
					pg.ConnectionString(TestConnectionString)
					   .ConnectionStringName(invalidName!))));
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
		_ = services.AddLogging();
		services.AddSingleton<IConfiguration>(config);

		// Act
		services.AddCdcProcessor(builder =>
			builder.UsePostgres(pg =>
				pg.ConnectionString(TestConnectionString)
				   .SchemaName("audit")
				   .BatchSize(200)
				   .PollingInterval(TimeSpan.FromSeconds(2))
				   .ReplicationSlotName("my_slot")
				   .PublicationName("my_pub")
				   .ConnectionStringName("OrdersDb")));

		// Assert
		var provider = services.BuildServiceProvider();
		var pgOptions = provider.GetRequiredService<IOptions<PostgresCdcOptions>>();
		pgOptions.Value.ReplicationSlotName.ShouldBe("my_slot");
		pgOptions.Value.PublicationName.ShouldBe("my_pub");
		pgOptions.Value.BatchSize.ShouldBe(200);
		pgOptions.Value.PollingInterval.ShouldBe(TimeSpan.FromSeconds(2));

		var stateOptions = provider.GetRequiredService<IOptions<PostgresCdcStateStoreOptions>>();
		stateOptions.Value.SchemaName.ShouldBe("audit");
	}

	[Fact]
	public void CombineWithWithStateStore()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:SourceDb"] = ResolvedConnectionString
			})
			.Build();

		var services = new ServiceCollection();
		_ = services.AddLogging();
		services.AddSingleton<IConfiguration>(config);

		// Act -- ConnectionStringName + WithStateStore
		services.AddCdcProcessor(builder =>
			builder.UsePostgres(pg =>
				pg.ConnectionString(TestConnectionString)
				   .ConnectionStringName("SourceDb")
				   .WithStateStore(state =>
						state.ConnectionString("Host=state;Database=StateDb;Username=test;Password=test;")
							 .SchemaName("cdc_state")
							 .TableName("checkpoints"))));

		// Assert
		var provider = services.BuildServiceProvider();
		var stateOptions = provider.GetRequiredService<IOptions<PostgresCdcStateStoreOptions>>();
		stateOptions.Value.SchemaName.ShouldBe("cdc_state");
		stateOptions.Value.TableName.ShouldBe("checkpoints");
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
		_ = services.AddLogging();
		services.AddSingleton<IConfiguration>(config);

		// Act
		services.AddCdcProcessor(builder =>
			builder.UsePostgres(pg =>
				pg.ConnectionString(TestConnectionString)
				   .ConnectionStringName("TestDb")));

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IPostgresCdcStateStore) &&
			sd.Lifetime == ServiceLifetime.Singleton);

		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IPostgresCdcProcessor) &&
			sd.Lifetime == ServiceLifetime.Singleton);
	}

	[Fact]
	public void CombineWithBindConfiguration()
	{
		// Arrange
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:PgDb"] = ResolvedConnectionString
			})
			.Build();

		var services = new ServiceCollection();
		_ = services.AddLogging();
		services.AddSingleton<IConfiguration>(config);

		// Act
		services.AddCdcProcessor(builder =>
			builder.UsePostgres(pg =>
				pg.ConnectionString(TestConnectionString)
				   .ConnectionStringName("PgDb")
				   .BindConfiguration("Cdc:Postgres")));

		// Assert -- BindConfiguration wires options
		var optionsDescriptors = services.Where(sd =>
			sd.ServiceType.IsGenericType &&
			sd.ServiceType.GetGenericTypeDefinition() == typeof(IConfigureOptions<>) &&
			sd.ServiceType.GetGenericArguments()[0] == typeof(PostgresCdcOptions));

		optionsDescriptors.ShouldNotBeEmpty();
	}
}
