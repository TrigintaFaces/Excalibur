// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

using Excalibur.Outbox.Postgres;
using Excalibur.Outbox;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.Tests.Postgres.Builders;

/// <summary>
/// Unit tests for <see cref="IPostgresOutboxBuilder"/> fluent API.
/// </summary>
/// <remarks>
/// These tests validate the Microsoft-style fluent builder pattern implementation
/// for the Postgres outbox provider, where connection is configured via the builder.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Database", "Postgres")]
public sealed class PostgresOutboxBuilderShould : UnitTestBase
{
	private const string TestConnectionString = "Host=localhost;Database=TestDb;Username=test;Password=test;";

	[Fact]
	public void UsePostgres_ThrowsOnNullBuilder()
	{
		// Arrange
		IOutboxBuilder builder = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			builder.UsePostgres(pg => pg.ConnectionString(TestConnectionString)));
	}

	[Fact]
	public void UsePostgres_ThrowsOnNullConfigure()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddExcaliburOutbox(builder =>
			{
				_ = builder.UsePostgres(null!);
			}));
	}

	[Fact]
	public void UsePostgres_ThrowsWhenNoConnectionConfigured()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() =>
			services.AddExcaliburOutbox(builder =>
			{
				_ = builder.UsePostgres(pg =>
				{
					_ = pg.SchemaName("messaging");
				});
			}));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ConnectionString_ThrowsOnInvalidValue(string? connectionString)
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			services.AddExcaliburOutbox(builder =>
			{
				_ = builder.UsePostgres(pg => pg.ConnectionString(connectionString));
			}));
	}

	[Fact]
	public void ConnectionFactory_ThrowsOnNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddExcaliburOutbox(builder =>
			{
				_ = builder.UsePostgres(pg => pg.ConnectionFactory(null!));
			}));
	}

	[Fact]
	public void UsePostgres_ReturnsBuilderForChaining()
	{
		// Arrange
		var services = new ServiceCollection();
		IOutboxBuilder? capturedResult = null;

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			capturedResult = builder.UsePostgres(pg => pg.ConnectionString(TestConnectionString));
		});

		// Assert
		_ = capturedResult.ShouldNotBeNull();
	}

	[Fact]
	public void UsePostgres_RegistersPostgresOutboxOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.UsePostgres(pg => pg.ConnectionString(TestConnectionString));
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetService<IOptions<PostgresOutboxStoreOptions>>();
		_ = options.ShouldNotBeNull();
		_ = options.Value.ShouldNotBeNull();
	}

	[Fact]
	public void UsePostgres_RegistersIOutboxStore()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.UsePostgres(pg => pg.ConnectionString(TestConnectionString));
		});

		// Assert - service descriptor exists
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IOutboxStore) &&
			sd.Lifetime == ServiceLifetime.Singleton);
	}

	[Fact]
	public void UsePostgres_ConfiguresSchemaName()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.UsePostgres(pg =>
			{
				_ = pg.ConnectionString(TestConnectionString)
					.SchemaName("messaging");
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<PostgresOutboxStoreOptions>>();
		options.Value.SchemaName.ShouldBe("messaging");
	}

	[Fact]
	public void UsePostgres_ConfiguresTableName()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.UsePostgres(pg =>
			{
				_ = pg.ConnectionString(TestConnectionString)
					.TableName("outbox_messages");
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<PostgresOutboxStoreOptions>>();
		options.Value.OutboxTableName.ShouldBe("outbox_messages");
	}

	[Fact]
	public void UsePostgres_ConfiguresDeadLetterTableName()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.UsePostgres(pg =>
			{
				_ = pg.ConnectionString(TestConnectionString)
					.DeadLetterTableName("dead_letter_queue");
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<PostgresOutboxStoreOptions>>();
		options.Value.DeadLetterTableName.ShouldBe("dead_letter_queue");
	}

	[Fact]
	public void UsePostgres_ConfiguresReservationTimeout()
	{
		// Arrange
		var services = new ServiceCollection();
		var expectedTimeout = TimeSpan.FromMinutes(10);

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.UsePostgres(pg =>
			{
				_ = pg.ConnectionString(TestConnectionString)
					.ReservationTimeout(expectedTimeout);
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<PostgresOutboxStoreOptions>>();
		options.Value.ReservationTimeout.ShouldBe((int)expectedTimeout.TotalSeconds);
	}

	[Fact]
	public void UsePostgres_ConfiguresMaxAttempts()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.UsePostgres(pg =>
			{
				_ = pg.ConnectionString(TestConnectionString)
					.MaxAttempts(10);
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<PostgresOutboxStoreOptions>>();
		options.Value.MaxAttempts.ShouldBe(10);
	}

	[Fact]
	public void UsePostgres_SupportsFluentChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.UsePostgres(pg =>
			{
				_ = pg.ConnectionString(TestConnectionString)
						.SchemaName("messaging")
						.TableName("outbox")
						.DeadLetterTableName("dead_letters")
						.CommandTimeout(TimeSpan.FromSeconds(45))
						.ReservationTimeout(TimeSpan.FromMinutes(5))
						.MaxAttempts(3);
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<PostgresOutboxStoreOptions>>();
		options.Value.SchemaName.ShouldBe("messaging");
		options.Value.OutboxTableName.ShouldBe("outbox");
		options.Value.DeadLetterTableName.ShouldBe("dead_letters");
		options.Value.BatchProcessing.BatchProcessingTimeout.ShouldBe(TimeSpan.FromSeconds(45));
		options.Value.ReservationTimeout.ShouldBe((int)TimeSpan.FromMinutes(5).TotalSeconds);
		options.Value.MaxAttempts.ShouldBe(3);
	}

	[Fact]
	public void UsePostgres_CombinesWithCoreBuilderMethods()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder
				.UsePostgres(pg =>
				{
					_ = pg.ConnectionString(TestConnectionString)
						.SchemaName("outbox");
				})
				.WithProcessing(p => p.BatchSize(150).PollingInterval(TimeSpan.FromSeconds(10)))
				.WithCleanup(c => c.EnableAutoCleanup(true).RetentionPeriod(TimeSpan.FromDays(14)))
				.EnableBackgroundProcessing();
		});
		var provider = services.BuildServiceProvider();

		// Assert - Postgres options
		var pgOptions = provider.GetRequiredService<IOptions<PostgresOutboxStoreOptions>>();
		pgOptions.Value.SchemaName.ShouldBe("outbox");

		// Assert - Core outbox options
		var outboxOptions = provider.GetRequiredService<OutboxOptions>();
		outboxOptions.BatchSize.ShouldBe(150);
		outboxOptions.PollingInterval.ShouldBe(TimeSpan.FromSeconds(10));
		outboxOptions.Cleanup.EnableAutomaticCleanup.ShouldBeTrue();
		outboxOptions.Cleanup.MessageRetentionPeriod.ShouldBe(TimeSpan.FromDays(14));
		outboxOptions.EnableBackgroundProcessing.ShouldBeTrue();
	}
}
