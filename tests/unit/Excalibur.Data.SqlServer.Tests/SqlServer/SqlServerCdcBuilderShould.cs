// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Data.SqlServer.Cdc;

using Excalibur.Data.SqlServer;
namespace Excalibur.Data.Tests.SqlServer.Cdc.Builders;

/// <summary>
/// Unit tests for <see cref="ISqlServerCdcBuilder"/> fluent API.
/// </summary>
/// <remarks>
/// These tests validate the ADR-098 Microsoft-style fluent builder pattern implementation
/// for the SQL Server CDC provider.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerCdcBuilderShould : UnitTestBase
{
	private const string TestConnectionString = "Server=localhost;Database=TestDb;Integrated Security=true;TrustServerCertificate=true;";

	[Fact]
	public void UseSqlServer_ThrowsOnNullBuilder()
	{
		// Arrange
		ICdcBuilder builder = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			builder.UseSqlServer(TestConnectionString));
	}

	[Fact]
	public void UseSqlServer_ThrowsOnNullConnectionString()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddCdcProcessor(builder =>
			{
				_ = builder.UseSqlServer((string)null!);
			}));
	}

	[Fact]
	public void UseSqlServer_ThrowsOnEmptyConnectionString()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			services.AddCdcProcessor(builder =>
			{
				_ = builder.UseSqlServer("");
			}));
	}

	[Fact]
	public void UseSqlServer_ThrowsOnWhitespaceConnectionString()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			services.AddCdcProcessor(builder =>
			{
				_ = builder.UseSqlServer("   ");
			}));
	}

	[Fact]
	public void UseSqlServer_ReturnsBuilderForChaining()
	{
		// Arrange
		var services = new ServiceCollection();
		ICdcBuilder? capturedResult = null;

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			capturedResult = builder.UseSqlServer(TestConnectionString);
		});

		// Assert
		_ = capturedResult.ShouldNotBeNull();
	}

	[Fact]
	public void UseSqlServer_RegistersSqlServerCdcOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.UseSqlServer(TestConnectionString);
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetService<IOptions<SqlServerCdcOptions>>();
		_ = options.ShouldNotBeNull();
		_ = options.Value.ShouldNotBeNull();
		options.Value.ConnectionString.ShouldBe(TestConnectionString);
	}

	[Fact]
	public void UseSqlServer_RegistersICdcStateStore()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.UseSqlServer(TestConnectionString);
		});

		// Assert - service descriptor exists
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(ICdcStateStore) &&
			sd.Lifetime == ServiceLifetime.Singleton);
	}

	[Fact]
	public void UseSqlServer_RegistersICdcRepository()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.UseSqlServer(TestConnectionString);
		});

		// Assert - service descriptor exists
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(ICdcRepository) &&
			sd.Lifetime == ServiceLifetime.Singleton);
	}

	[Fact]
	public void UseSqlServer_RegistersICdcProcessor()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.UseSqlServer(TestConnectionString);
		});

		// Assert - service descriptor exists
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(ICdcProcessor) &&
			sd.Lifetime == ServiceLifetime.Singleton);
	}

	[Fact]
	public void UseSqlServer_ConfiguresSchemaName()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.UseSqlServer(TestConnectionString, sql =>
			{
				_ = sql.SchemaName("audit");
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<SqlServerCdcOptions>>();
		options.Value.SchemaName.ShouldBe("audit");
	}

	[Fact]
	public void UseSqlServer_ConfiguresStateTableName()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.UseSqlServer(TestConnectionString, sql =>
			{
				_ = sql.StateTableName("CdcState");
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<SqlServerCdcOptions>>();
		options.Value.StateTableName.ShouldBe("CdcState");
	}

	[Fact]
	public void UseSqlServer_ConfiguresPollingInterval()
	{
		// Arrange
		var services = new ServiceCollection();
		var expectedInterval = TimeSpan.FromSeconds(10);

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.UseSqlServer(TestConnectionString, sql =>
			{
				_ = sql.PollingInterval(expectedInterval);
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<SqlServerCdcOptions>>();
		options.Value.PollingInterval.ShouldBe(expectedInterval);
	}

	[Fact]
	public void UseSqlServer_ConfiguresBatchSize()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.UseSqlServer(TestConnectionString, sql =>
			{
				_ = sql.BatchSize(500);
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<SqlServerCdcOptions>>();
		options.Value.BatchSize.ShouldBe(500);
	}

	[Fact]
	public void UseSqlServer_ConfiguresCommandTimeout()
	{
		// Arrange
		var services = new ServiceCollection();
		var expectedTimeout = TimeSpan.FromSeconds(60);

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.UseSqlServer(TestConnectionString, sql =>
			{
				_ = sql.CommandTimeout(expectedTimeout);
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<SqlServerCdcOptions>>();
		options.Value.CommandTimeout.ShouldBe(expectedTimeout);
	}

	[Fact]
	public void UseSqlServer_SupportsFluentChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.UseSqlServer(TestConnectionString, sql =>
			{
				_ = sql.SchemaName("cdc")
				   .StateTableName("ProcessingState")
				   .PollingInterval(TimeSpan.FromSeconds(5))
				   .BatchSize(100)
				   .CommandTimeout(TimeSpan.FromSeconds(30));
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<SqlServerCdcOptions>>();
		options.Value.SchemaName.ShouldBe("cdc");
		options.Value.StateTableName.ShouldBe("ProcessingState");
		options.Value.PollingInterval.ShouldBe(TimeSpan.FromSeconds(5));
		options.Value.BatchSize.ShouldBe(100);
		options.Value.CommandTimeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void UseSqlServer_CombinesWithCoreBuilderMethods()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder
				.UseSqlServer(TestConnectionString, sql =>
				{
					_ = sql.SchemaName("cdc")
					   .BatchSize(200);
				})
				.TrackTable("dbo.Orders", table =>
				{
					_ = table.MapInsert<OrderCreatedEvent>()
						 .MapUpdate<OrderUpdatedEvent>()
						 .MapDelete<OrderDeletedEvent>();
				})
				.WithRecovery(r => r.MaxAttempts(5).AttemptDelay(TimeSpan.FromSeconds(30)))
				.EnableBackgroundProcessing();
		});
		var provider = services.BuildServiceProvider();

		// Assert - SQL Server options
		var sqlOptions = provider.GetRequiredService<IOptions<SqlServerCdcOptions>>();
		sqlOptions.Value.SchemaName.ShouldBe("cdc");
		sqlOptions.Value.BatchSize.ShouldBe(200);

		// Assert - Core CDC options
		var cdcOptions = provider.GetRequiredService<IOptions<CdcOptions>>();
		cdcOptions.Value.TrackedTables.Count.ShouldBe(1);
		cdcOptions.Value.MaxRecoveryAttempts.ShouldBe(5);
		cdcOptions.Value.RecoveryAttemptDelay.ShouldBe(TimeSpan.FromSeconds(30));
		cdcOptions.Value.EnableBackgroundProcessing.ShouldBeTrue();
	}

	[Fact]
	public void UseSqlServer_WorksWithoutConfigure()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.UseSqlServer(TestConnectionString);
		});
		var provider = services.BuildServiceProvider();

		// Assert - defaults are applied
		var options = provider.GetRequiredService<IOptions<SqlServerCdcOptions>>();
		_ = options.Value.ShouldNotBeNull();
		options.Value.ConnectionString.ShouldBe(TestConnectionString);
	}

	// Test event types
	private sealed class OrderCreatedEvent { }
	private sealed class OrderUpdatedEvent { }
	private sealed class OrderDeletedEvent { }
}
