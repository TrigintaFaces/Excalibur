// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

using Excalibur.Outbox;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Outbox.Tests.SqlServer.Builders;

/// <summary>
/// Unit tests for <see cref="ISqlServerOutboxBuilder"/> fluent API.
/// </summary>
/// <remarks>
/// These tests validate the ADR-098 Microsoft-style fluent builder pattern implementation
/// for the SQL Server outbox provider.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerOutboxBuilderShould : UnitTestBase
{
	private const string TestConnectionString = "Server=localhost;Database=TestDb;Trusted_Connection=True;";

	[Fact]
	public void UseSqlServer_ThrowsOnNullBuilder()
	{
		// Arrange
		IOutboxBuilder builder = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			builder.UseSqlServer(TestConnectionString));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void UseSqlServer_ThrowsOnInvalidConnectionString(string? connectionString)
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			services.AddExcaliburOutbox(builder =>
			{
				_ = builder.UseSqlServer(connectionString);
			}));
	}

	[Fact]
	public void UseSqlServer_ReturnsBuilderForChaining()
	{
		// Arrange
		var services = new ServiceCollection();
		IOutboxBuilder? capturedResult = null;

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			capturedResult = builder.UseSqlServer(TestConnectionString);
		});

		// Assert
		_ = capturedResult.ShouldNotBeNull();
	}

	[Fact]
	public void UseSqlServer_RegistersSqlServerOutboxOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.UseSqlServer(TestConnectionString);
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetService<IOptions<SqlServerOutboxOptions>>();
		_ = options.ShouldNotBeNull();
		options.Value.ConnectionString.ShouldBe(TestConnectionString);
	}

	[Fact]
	public void UseSqlServer_RegistersIOutboxStore()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.UseSqlServer(TestConnectionString);
		});

		// Assert - service descriptor exists
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IOutboxStore) &&
			sd.Lifetime == ServiceLifetime.Singleton);
	}

	[Fact]
	public void UseSqlServer_RegistersIMultiTransportOutboxStore()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.UseSqlServer(TestConnectionString);
		});

		// Assert - service descriptor exists
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IMultiTransportOutboxStore) &&
			sd.Lifetime == ServiceLifetime.Singleton);
	}

	[Fact]
	public void UseSqlServer_ConfiguresSchemaName()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.UseSqlServer(TestConnectionString, sql =>
			{
				_ = sql.SchemaName("Messaging");
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<SqlServerOutboxOptions>>();
		options.Value.SchemaName.ShouldBe("Messaging");
	}

	[Fact]
	public void UseSqlServer_ConfiguresTableName()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.UseSqlServer(TestConnectionString, sql =>
			{
				_ = sql.TableName("CustomOutbox");
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<SqlServerOutboxOptions>>();
		options.Value.OutboxTableName.ShouldBe("CustomOutbox");
	}

	[Fact]
	public void UseSqlServer_ConfiguresTransportsTableName()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.UseSqlServer(TestConnectionString, sql =>
			{
				_ = sql.TransportsTableName("CustomTransports");
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<SqlServerOutboxOptions>>();
		options.Value.TransportsTableName.ShouldBe("CustomTransports");
	}

	[Fact]
	public void UseSqlServer_ConfiguresDeadLetterTableName()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.UseSqlServer(TestConnectionString, sql =>
			{
				_ = sql.DeadLetterTableName("CustomDeadLetters");
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<SqlServerOutboxOptions>>();
		options.Value.DeadLetterTableName.ShouldBe("CustomDeadLetters");
	}

	[Fact]
	public void UseSqlServer_ConfiguresCommandTimeout()
	{
		// Arrange
		var services = new ServiceCollection();
		var expectedTimeout = TimeSpan.FromSeconds(60);

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.UseSqlServer(TestConnectionString, sql =>
			{
				_ = sql.CommandTimeout(expectedTimeout);
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<SqlServerOutboxOptions>>();
		options.Value.CommandTimeoutSeconds.ShouldBe(60);
	}

	[Fact]
	public void UseSqlServer_ConfiguresUseRowLocking()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.UseSqlServer(TestConnectionString, sql =>
			{
				_ = sql.UseRowLocking(false);
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<SqlServerOutboxOptions>>();
		options.Value.UseRowLocking.ShouldBeFalse();
	}

	[Fact]
	public void UseSqlServer_ConfiguresDefaultBatchSize()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.UseSqlServer(TestConnectionString, sql =>
			{
				_ = sql.DefaultBatchSize(500);
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<SqlServerOutboxOptions>>();
		options.Value.DefaultBatchSize.ShouldBe(500);
	}

	[Fact]
	public void UseSqlServer_SupportsFluentChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.UseSqlServer(TestConnectionString, sql =>
			{
				_ = sql.SchemaName("Outbox")
				   .TableName("Messages")
				   .TransportsTableName("MessageTransports")
				   .DeadLetterTableName("DeadLetters")
				   .CommandTimeout(TimeSpan.FromSeconds(45))
				   .UseRowLocking(true)
				   .DefaultBatchSize(200);
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<SqlServerOutboxOptions>>();
		options.Value.SchemaName.ShouldBe("Outbox");
		options.Value.OutboxTableName.ShouldBe("Messages");
		options.Value.TransportsTableName.ShouldBe("MessageTransports");
		options.Value.DeadLetterTableName.ShouldBe("DeadLetters");
		options.Value.CommandTimeoutSeconds.ShouldBe(45);
		options.Value.UseRowLocking.ShouldBeTrue();
		options.Value.DefaultBatchSize.ShouldBe(200);
	}

	[Fact]
	public void UseSqlServer_CombinesWithCoreBuilderMethods()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder
				.UseSqlServer(TestConnectionString, sql =>
				{
					_ = sql.SchemaName("Messaging");
				})
				.WithProcessing(p => p.BatchSize(150).PollingInterval(TimeSpan.FromSeconds(10)))
				.WithCleanup(c => c.EnableAutoCleanup(true).RetentionPeriod(TimeSpan.FromDays(14)))
				.EnableBackgroundProcessing();
		});
		var provider = services.BuildServiceProvider();

		// Assert - SQL Server options
		var sqlOptions = provider.GetRequiredService<IOptions<SqlServerOutboxOptions>>();
		sqlOptions.Value.SchemaName.ShouldBe("Messaging");

		// Assert - Core outbox options
		var outboxOptions = provider.GetRequiredService<OutboxOptions>();
		outboxOptions.BatchSize.ShouldBe(150);
		outboxOptions.PollingInterval.ShouldBe(TimeSpan.FromSeconds(10));
		outboxOptions.EnableAutomaticCleanup.ShouldBeTrue();
		outboxOptions.MessageRetentionPeriod.ShouldBe(TimeSpan.FromDays(14));
		outboxOptions.EnableBackgroundProcessing.ShouldBeTrue();
	}
}
