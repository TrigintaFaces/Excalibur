// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Data.Postgres.Cdc;

using Excalibur.Data.Postgres;
namespace Excalibur.Data.Tests.Postgres.Cdc.Builders;

/// <summary>
/// Unit tests for <see cref="IPostgresCdcBuilder"/> fluent API.
/// </summary>
/// <remarks>
/// These tests validate the ADR-098 Microsoft-style fluent builder pattern implementation
/// for the Postgres CDC provider.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Database", "Postgres")]
public sealed class PostgresCdcBuilderShould : UnitTestBase
{
	private const string TestConnectionString = "Host=localhost;Database=TestDb;Username=test;Password=test;";

	[Fact]
	public void UsePostgres_ThrowsOnNullBuilder()
	{
		// Arrange
		ICdcBuilder builder = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			builder.UsePostgres(TestConnectionString));
	}

	[Fact]
	public void UsePostgres_ThrowsOnNullConnectionString()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddCdcProcessor(builder =>
			{
				_ = builder.UsePostgres((string)null!);
			}));
	}

	[Fact]
	public void UsePostgres_ThrowsOnEmptyConnectionString()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			services.AddCdcProcessor(builder =>
			{
				_ = builder.UsePostgres("");
			}));
	}

	[Fact]
	public void UsePostgres_ThrowsOnWhitespaceConnectionString()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			services.AddCdcProcessor(builder =>
			{
				_ = builder.UsePostgres("   ");
			}));
	}

	[Fact]
	public void UsePostgres_ReturnsBuilderForChaining()
	{
		// Arrange
		var services = new ServiceCollection();
		ICdcBuilder? capturedResult = null;

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			capturedResult = builder.UsePostgres(TestConnectionString);
		});

		// Assert
		_ = capturedResult.ShouldNotBeNull();
	}

	[Fact]
	public void UsePostgres_RegistersPostgresCdcOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.UsePostgres(TestConnectionString);
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetService<IOptions<PostgresCdcOptions>>();
		_ = options.ShouldNotBeNull();
		_ = options.Value.ShouldNotBeNull();
		options.Value.ConnectionString.ShouldBe(TestConnectionString);
	}

	[Fact]
	public void UsePostgres_RegistersIPostgresCdcStateStore()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.UsePostgres(TestConnectionString);
		});

		// Assert - service descriptor exists
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IPostgresCdcStateStore) &&
			sd.Lifetime == ServiceLifetime.Singleton);
	}

	[Fact]
	public void UsePostgres_RegistersIPostgresCdcProcessor()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.UsePostgres(TestConnectionString);
		});

		// Assert - service descriptor exists
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IPostgresCdcProcessor) &&
			sd.Lifetime == ServiceLifetime.Singleton);
	}

	[Fact]
	public void UsePostgres_ConfiguresSchemaName()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.UsePostgres(TestConnectionString, pg =>
			{
				_ = pg.SchemaName("audit");
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<PostgresCdcStateStoreOptions>>();
		options.Value.SchemaName.ShouldBe("audit");
	}

	[Fact]
	public void UsePostgres_ConfiguresStateTableName()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.UsePostgres(TestConnectionString, pg =>
			{
				_ = pg.StateTableName("cdc_positions");
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<PostgresCdcStateStoreOptions>>();
		options.Value.TableName.ShouldBe("cdc_positions");
	}

	[Fact]
	public void UsePostgres_ConfiguresReplicationSlotName()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.UsePostgres(TestConnectionString, pg =>
			{
				_ = pg.ReplicationSlotName("my_slot");
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<PostgresCdcOptions>>();
		options.Value.ReplicationSlotName.ShouldBe("my_slot");
	}

	[Fact]
	public void UsePostgres_ConfiguresPublicationName()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.UsePostgres(TestConnectionString, pg =>
			{
				_ = pg.PublicationName("my_publication");
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<PostgresCdcOptions>>();
		options.Value.PublicationName.ShouldBe("my_publication");
	}

	[Fact]
	public void UsePostgres_ConfiguresPollingInterval()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		var expectedInterval = TimeSpan.FromSeconds(2);

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.UsePostgres(TestConnectionString, pg =>
			{
				_ = pg.PollingInterval(expectedInterval);
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<PostgresCdcOptions>>();
		options.Value.PollingInterval.ShouldBe(expectedInterval);
	}

	[Fact]
	public void UsePostgres_ConfiguresBatchSize()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.UsePostgres(TestConnectionString, pg =>
			{
				_ = pg.BatchSize(500);
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<PostgresCdcOptions>>();
		options.Value.BatchSize.ShouldBe(500);
	}

	[Fact]
	public void UsePostgres_ConfiguresTimeout()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		var expectedTimeout = TimeSpan.FromSeconds(60);

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.UsePostgres(TestConnectionString, pg =>
			{
				_ = pg.Timeout(expectedTimeout);
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<PostgresCdcOptions>>();
		options.Value.Timeout.ShouldBe(expectedTimeout);
	}

	[Fact]
	public void UsePostgres_ConfiguresProcessorId()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		const string processorId = "worker-1";

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.UsePostgres(TestConnectionString, pg =>
			{
				_ = pg.ProcessorId(processorId);
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<PostgresCdcOptions>>();
		options.Value.ProcessorId.ShouldBe(processorId);
	}

	[Fact]
	public void UsePostgres_ConfiguresUseBinaryProtocol()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.UsePostgres(TestConnectionString, pg =>
			{
				_ = pg.UseBinaryProtocol(true);
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<PostgresCdcOptions>>();
		options.Value.UseBinaryProtocol.ShouldBeTrue();
	}

	[Fact]
	public void UsePostgres_ConfiguresAutoCreateSlot()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.UsePostgres(TestConnectionString, pg =>
			{
				_ = pg.AutoCreateSlot(false);
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<PostgresCdcOptions>>();
		options.Value.AutoCreateSlot.ShouldBeFalse();
	}

	[Fact]
	public void UsePostgres_SupportsFluentChaining()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.UsePostgres(TestConnectionString, pg =>
			{
				_ = pg.SchemaName("excalibur")
				  .StateTableName("cdc_state")
				  .ReplicationSlotName("my_slot")
				  .PublicationName("my_pub")
				  .PollingInterval(TimeSpan.FromSeconds(1))
				  .BatchSize(1000)
				  .Timeout(TimeSpan.FromSeconds(30))
				  .ProcessorId("worker-1")
				  .UseBinaryProtocol(true)
				  .AutoCreateSlot(true);
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var pgOptions = provider.GetRequiredService<IOptions<PostgresCdcOptions>>();
		pgOptions.Value.ReplicationSlotName.ShouldBe("my_slot");
		pgOptions.Value.PublicationName.ShouldBe("my_pub");
		pgOptions.Value.PollingInterval.ShouldBe(TimeSpan.FromSeconds(1));
		pgOptions.Value.BatchSize.ShouldBe(1000);
		pgOptions.Value.Timeout.ShouldBe(TimeSpan.FromSeconds(30));
		pgOptions.Value.ProcessorId.ShouldBe("worker-1");
		pgOptions.Value.UseBinaryProtocol.ShouldBeTrue();
		pgOptions.Value.AutoCreateSlot.ShouldBeTrue();

		var stateOptions = provider.GetRequiredService<IOptions<PostgresCdcStateStoreOptions>>();
		stateOptions.Value.SchemaName.ShouldBe("excalibur");
		stateOptions.Value.TableName.ShouldBe("cdc_state");
	}

	[Fact]
	public void UsePostgres_CombinesWithCoreBuilderMethods()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder
				.UsePostgres(TestConnectionString, pg =>
				{
					_ = pg.SchemaName("excalibur")
					  .BatchSize(500);
				})
				.TrackTable("public.orders", table =>
				{
					_ = table.MapInsert<OrderCreatedEvent>()
						 .MapUpdate<OrderUpdatedEvent>()
						 .MapDelete<OrderDeletedEvent>();
				})
				.WithRecovery(r => r.MaxAttempts(3).AttemptDelay(TimeSpan.FromSeconds(10)))
				.EnableBackgroundProcessing();
		});
		var provider = services.BuildServiceProvider();

		// Assert - Postgres options
		var pgOptions = provider.GetRequiredService<IOptions<PostgresCdcOptions>>();
		pgOptions.Value.BatchSize.ShouldBe(500);

		// Assert - Core CDC options
		var cdcOptions = provider.GetRequiredService<IOptions<CdcOptions>>();
		cdcOptions.Value.TrackedTables.Count.ShouldBe(1);
		cdcOptions.Value.MaxRecoveryAttempts.ShouldBe(3);
		cdcOptions.Value.RecoveryAttemptDelay.ShouldBe(TimeSpan.FromSeconds(10));
		cdcOptions.Value.EnableBackgroundProcessing.ShouldBeTrue();
	}

	[Fact]
	public void UsePostgres_WorksWithoutConfigure()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.UsePostgres(TestConnectionString);
		});
		var provider = services.BuildServiceProvider();

		// Assert - defaults are applied
		var options = provider.GetRequiredService<IOptions<PostgresCdcOptions>>();
		_ = options.Value.ShouldNotBeNull();
		options.Value.ConnectionString.ShouldBe(TestConnectionString);
	}

	// Test event types
	private sealed class OrderCreatedEvent { }
	private sealed class OrderUpdatedEvent { }
	private sealed class OrderDeletedEvent { }
}
