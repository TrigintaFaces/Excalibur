// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

using Excalibur.Data.Postgres.Outbox;
using Excalibur.Outbox;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Excalibur.Data.Postgres;
namespace Excalibur.Data.Tests.Postgres.Builders;

/// <summary>
/// Unit tests for <see cref="IPostgresOutboxBuilder"/> fluent API.
/// </summary>
/// <remarks>
/// These tests validate the ADR-098 Microsoft-style fluent builder pattern implementation
/// for the Postgres outbox provider.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
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
			builder.UsePostgres(TestConnectionString));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void UsePostgres_ThrowsOnInvalidConnectionString(string? connectionString)
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			services.AddExcaliburOutbox(builder =>
			{
				_ = builder.UsePostgres(connectionString);
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
			capturedResult = builder.UsePostgres(TestConnectionString);
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
			_ = builder.UsePostgres(TestConnectionString);
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
			_ = builder.UsePostgres(TestConnectionString);
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
			_ = builder.UsePostgres(TestConnectionString, postgres =>
			{
				_ = postgres.SchemaName("messaging");
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
			_ = builder.UsePostgres(TestConnectionString, postgres =>
			{
				_ = postgres.TableName("outbox_messages");
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
			_ = builder.UsePostgres(TestConnectionString, postgres =>
			{
				_ = postgres.DeadLetterTableName("dead_letter_queue");
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
			_ = builder.UsePostgres(TestConnectionString, postgres =>
			{
				_ = postgres.ReservationTimeout(expectedTimeout);
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
			_ = builder.UsePostgres(TestConnectionString, postgres =>
			{
				_ = postgres.MaxAttempts(10);
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
			_ = builder.UsePostgres(TestConnectionString, postgres =>
			{
				_ = postgres.SchemaName("messaging")
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
		options.Value.BatchProcessingTimeout.ShouldBe(TimeSpan.FromSeconds(45));
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
				.UsePostgres(TestConnectionString, postgres =>
				{
					_ = postgres.SchemaName("outbox");
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
		outboxOptions.EnableAutomaticCleanup.ShouldBeTrue();
		outboxOptions.MessageRetentionPeriod.ShouldBe(TimeSpan.FromDays(14));
		outboxOptions.EnableBackgroundProcessing.ShouldBeTrue();
	}
}
