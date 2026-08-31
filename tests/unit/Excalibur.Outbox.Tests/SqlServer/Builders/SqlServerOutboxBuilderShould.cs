// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

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
			builder.UseSqlServer(sql => sql.ConnectionString(TestConnectionString)));
	}

	[Fact]
	public void UseSqlServer_ThrowsOnNullConfigure()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddExcaliburOutbox(builder =>
			{
				_ = builder.UseSqlServer((Action<ISqlServerOutboxBuilder>)null!);
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
			capturedResult = builder.UseSqlServer(sql => sql.ConnectionString(TestConnectionString));
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
			_ = builder.UseSqlServer(sql => sql.ConnectionString(TestConnectionString));
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
			_ = builder.UseSqlServer(sql => sql.ConnectionString(TestConnectionString));
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
			_ = builder.UseSqlServer(sql => sql.ConnectionString(TestConnectionString));
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
			_ = builder.UseSqlServer(sql =>
			{
				sql.ConnectionString(TestConnectionString)
				   .SchemaName("Messaging");
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<SqlServerOutboxOptions>>();
		options.Value.Tables.SchemaName.ShouldBe("Messaging");
	}

	[Fact]
	public void UseSqlServer_ConfiguresTableName()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.UseSqlServer(sql =>
			{
				sql.ConnectionString(TestConnectionString)
				   .TableName("CustomOutbox");
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<SqlServerOutboxOptions>>();
		options.Value.Tables.OutboxTableName.ShouldBe("CustomOutbox");
	}

	[Fact]
	public void UseSqlServer_ConfiguresTransportsTableName()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.UseSqlServer(sql =>
			{
				sql.ConnectionString(TestConnectionString)
				   .TransportsTableName("CustomTransports");
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<SqlServerOutboxOptions>>();
		options.Value.Tables.TransportsTableName.ShouldBe("CustomTransports");
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
			_ = builder.UseSqlServer(sql =>
			{
				sql.ConnectionString(TestConnectionString)
				   .CommandTimeout(expectedTimeout);
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<SqlServerOutboxOptions>>();
		options.Value.Processing.CommandTimeoutSeconds.ShouldBe(60);
	}

	[Fact]
	public void UseSqlServer_ConfiguresUseRowLocking()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.UseSqlServer(sql =>
			{
				sql.ConnectionString(TestConnectionString)
				   .UseRowLocking(false);
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<SqlServerOutboxOptions>>();
		options.Value.Processing.UseRowLocking.ShouldBeFalse();
	}

	[Fact]
	public void UseSqlServer_ConfiguresDefaultBatchSize()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.UseSqlServer(sql =>
			{
				sql.ConnectionString(TestConnectionString)
				   .DefaultBatchSize(500);
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<SqlServerOutboxOptions>>();
		options.Value.Processing.DefaultBatchSize.ShouldBe(500);
	}

	[Fact]
	public void UseSqlServer_SupportsFluentChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.UseSqlServer(sql =>
			{
				_ = sql.ConnectionString(TestConnectionString)
				   .SchemaName("Outbox")
				   .TableName("Messages")
				   .TransportsTableName("MessageTransports")
				   .CommandTimeout(TimeSpan.FromSeconds(45))
				   .UseRowLocking(true)
				   .DefaultBatchSize(200);
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<SqlServerOutboxOptions>>();
		options.Value.ConnectionString.ShouldBe(TestConnectionString);
		options.Value.Tables.SchemaName.ShouldBe("Outbox");
		options.Value.Tables.OutboxTableName.ShouldBe("Messages");
		options.Value.Tables.TransportsTableName.ShouldBe("MessageTransports");
		options.Value.Processing.CommandTimeoutSeconds.ShouldBe(45);
		options.Value.Processing.UseRowLocking.ShouldBeTrue();
		options.Value.Processing.DefaultBatchSize.ShouldBe(200);
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
				.UseSqlServer(sql =>
				{
					sql.ConnectionString(TestConnectionString)
					   .SchemaName("Messaging");
				})
				.WithProcessing(p => p.BatchSize(150).PollingInterval(TimeSpan.FromSeconds(10)))
				.EnableBackgroundProcessing();
		});
		var provider = services.BuildServiceProvider();

		// Assert - SQL Server options
		var sqlOptions = provider.GetRequiredService<IOptions<SqlServerOutboxOptions>>();
		sqlOptions.Value.Tables.SchemaName.ShouldBe("Messaging");

		// Assert - Core outbox options
		var outboxOptions = provider.GetRequiredService<OutboxOptions>();
		outboxOptions.BatchSize.ShouldBe(150);
		outboxOptions.PollingInterval.ShouldBe(TimeSpan.FromSeconds(10));
		outboxOptions.EnableBackgroundProcessing.ShouldBeTrue();
	}
}
