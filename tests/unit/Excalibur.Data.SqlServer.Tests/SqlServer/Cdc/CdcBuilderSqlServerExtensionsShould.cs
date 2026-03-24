// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Cdc.Processing;
using Excalibur.Cdc.SqlServer;

using Microsoft.Data.SqlClient;

namespace Excalibur.Data.Tests.SqlServer.Cdc;

/// <summary>
/// Unit tests for <see cref="CdcBuilderSqlServerExtensions"/>.
/// Tests the connection factory overload and DI registration.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data.SqlServer")]
[Trait("Feature", "CDC")]
public sealed class CdcBuilderSqlServerExtensionsShould : UnitTestBase
{
	private const string TestConnectionString = "Server=localhost;Database=TestDb;Encrypt=false;TrustServerCertificate=true";

	[Fact]
	public void UseSqlServer_ThrowsOnNullBuilder()
	{
		ICdcBuilder builder = null!;

		Should.Throw<ArgumentNullException>(() =>
			builder.UseSqlServer(sql =>
				sql.ConnectionFactory(_ => () => new SqlConnection(TestConnectionString))));
	}

	[Fact]
	public void UseSqlServer_ThrowsOnNullConfigure()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ServiceCollection().AddCdcProcessor(builder =>
				builder.UseSqlServer((Action<ISqlServerCdcBuilder>)null!)));
	}

	[Fact]
	public void UseSqlServer_ConnectionFactory_ThrowsOnMissingSchemaName()
	{
		Should.Throw<ArgumentException>(() =>
			new ServiceCollection().AddCdcProcessor(builder =>
				builder.UseSqlServer(sql =>
					sql.ConnectionFactory(_ => () => new SqlConnection(TestConnectionString))
					   .SchemaName("")
					   .StateTableName("State"))));
	}

	[Fact]
	public void UseSqlServer_ConnectionFactory_ThrowsOnMissingStateTableName()
	{
		Should.Throw<ArgumentException>(() =>
			new ServiceCollection().AddCdcProcessor(builder =>
				builder.UseSqlServer(sql =>
					sql.ConnectionFactory(_ => () => new SqlConnection(TestConnectionString))
					   .SchemaName("cdc")
					   .StateTableName(""))));
	}

	[Fact]
	public void UseSqlServer_ConnectionFactory_RegistersRequiredServices()
	{
		var services = new ServiceCollection();

		services.AddCdcProcessor(builder =>
			builder.UseSqlServer(sql =>
				sql.ConnectionFactory(_ => () => new SqlConnection(TestConnectionString))
				   .SchemaName("cdc")
				   .StateTableName("State")));

		services.ShouldContain(sd => sd.ServiceType == typeof(ISqlServerCdcStateStore));
		services.ShouldContain(sd => sd.ServiceType == typeof(ICdcRepository));
		services.ShouldContain(sd => sd.ServiceType == typeof(ICdcRepositoryLsnMapping));
		services.ShouldContain(sd => sd.ServiceType == typeof(ICdcProcessor));
		services.ShouldContain(sd => sd.ServiceType == typeof(IDataChangeEventProcessor));
		services.ShouldContain(sd => sd.ServiceType == typeof(ICdcBackgroundProcessor));
	}

	[Fact]
	public void UseSqlServer_ConnectionFactory_RegistersSqlServerCdcOptions()
	{
		var services = new ServiceCollection();

		services.AddCdcProcessor(builder =>
			builder.UseSqlServer(sql =>
				sql.ConnectionFactory(_ => () => new SqlConnection(TestConnectionString))
				   .SchemaName("audit")
				   .StateTableName("CdcTracking")
				   .BatchSize(500)));

		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<SqlServerCdcOptions>>();

		options.Value.SchemaName.ShouldBe("audit");
		options.Value.StateTableName.ShouldBe("CdcTracking");
		options.Value.BatchSize.ShouldBe(500);
	}

	[Fact]
	public void UseSqlServer_ConnectionFactory_RegistersStateStoreOptions()
	{
		var services = new ServiceCollection();

		services.AddCdcProcessor(builder =>
			builder.UseSqlServer(sql =>
				sql.ConnectionFactory(_ => () => new SqlConnection(TestConnectionString))
				   .SchemaName("cdc")
				   .StateTableName("ProcessingState")));

		var provider = services.BuildServiceProvider();
		var stateStoreOptions = provider.GetRequiredService<IOptions<SqlServerCdcStateStoreOptions>>();

		stateStoreOptions.Value.SchemaName.ShouldBe("cdc");
		stateStoreOptions.Value.TableName.ShouldBe("ProcessingState");
	}

	[Fact]
	public void UseSqlServer_ConnectionFactory_WorksWithoutAdditionalConfig()
	{
		var services = new ServiceCollection();

		// Should use defaults (cdc schema, CdcProcessingState table)
		services.AddCdcProcessor(builder =>
			builder.UseSqlServer(sql =>
				sql.ConnectionFactory(_ => () => new SqlConnection(TestConnectionString))));

		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<SqlServerCdcOptions>>();
		options.Value.ShouldNotBeNull();
	}

	[Fact]
	public void UseSqlServer_ConnectionString_RegistersBackgroundProcessorAdapter()
	{
		var services = new ServiceCollection();

		services.AddCdcProcessor(builder =>
			builder.UseSqlServer(sql =>
				sql.ConnectionString(TestConnectionString)));

		services.ShouldContain(sd =>
			sd.ServiceType == typeof(ICdcBackgroundProcessor) &&
			sd.Lifetime == ServiceLifetime.Singleton);
	}

	[Fact]
	public void UseSqlServer_ConnectionFactory_ReturnsBuilderForChaining()
	{
		ICdcBuilder? capturedResult = null;

		new ServiceCollection().AddCdcProcessor(builder =>
		{
			capturedResult = builder.UseSqlServer(sql =>
				sql.ConnectionFactory(_ => () => new SqlConnection(TestConnectionString))
				   .SchemaName("cdc")
				   .StateTableName("State"));
		});

		capturedResult.ShouldNotBeNull();
	}
}
