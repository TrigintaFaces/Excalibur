// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Outbox;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Outbox.Tests.SqlServer;

/// <summary>
/// Unit tests for <see cref="ISqlServerOutboxBuilder"/> implementation.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SqlServerOutboxBuilderShould : UnitTestBase
{
	private const string TestConnectionString = "Server=localhost;Database=Test;Integrated Security=True";

	#region SchemaName Tests

	[Theory]
	[InlineData("dbo")]
	[InlineData("messaging")]
	[InlineData("outbox")]
	[InlineData("Outbox_Schema")]
	public void SchemaName_AcceptsValidValues(string schema)
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(outbox =>
		{
			_ = outbox.UseSqlServer(TestConnectionString, sql => sql.SchemaName(schema));
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<SqlServerOutboxOptions>>().Value;
		options.SchemaName.ShouldBe(schema);
	}

	[Fact]
	public void SchemaName_ThrowsOnNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			services.AddExcaliburOutbox(outbox =>
			{
				_ = outbox.UseSqlServer(TestConnectionString, sql => sql.SchemaName(null!));
			}));
	}

	[Fact]
	public void SchemaName_ThrowsOnEmpty()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			services.AddExcaliburOutbox(outbox =>
			{
				_ = outbox.UseSqlServer(TestConnectionString, sql => sql.SchemaName(""));
			}));
	}

	[Fact]
	public void SchemaName_ThrowsOnWhitespace()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			services.AddExcaliburOutbox(outbox =>
			{
				_ = outbox.UseSqlServer(TestConnectionString, sql => sql.SchemaName("   "));
			}));
	}

	#endregion

	#region TableName Tests

	[Theory]
	[InlineData("OutboxMessages")]
	[InlineData("Messages")]
	[InlineData("Outbox_Messages")]
	public void TableName_AcceptsValidValues(string tableName)
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(outbox =>
		{
			_ = outbox.UseSqlServer(TestConnectionString, sql => sql.TableName(tableName));
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<SqlServerOutboxOptions>>().Value;
		options.OutboxTableName.ShouldBe(tableName);
	}

	[Fact]
	public void TableName_ThrowsOnNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			services.AddExcaliburOutbox(outbox =>
			{
				_ = outbox.UseSqlServer(TestConnectionString, sql => sql.TableName(null!));
			}));
	}

	[Fact]
	public void TableName_ThrowsOnEmpty()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			services.AddExcaliburOutbox(outbox =>
			{
				_ = outbox.UseSqlServer(TestConnectionString, sql => sql.TableName(""));
			}));
	}

	#endregion

	#region TransportsTableName Tests

	[Theory]
	[InlineData("OutboxMessageTransports")]
	[InlineData("Transports")]
	[InlineData("DeliveryRecords")]
	public void TransportsTableName_AcceptsValidValues(string tableName)
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(outbox =>
		{
			_ = outbox.UseSqlServer(TestConnectionString, sql => sql.TransportsTableName(tableName));
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<SqlServerOutboxOptions>>().Value;
		options.TransportsTableName.ShouldBe(tableName);
	}

	[Fact]
	public void TransportsTableName_ThrowsOnNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			services.AddExcaliburOutbox(outbox =>
			{
				_ = outbox.UseSqlServer(TestConnectionString, sql => sql.TransportsTableName(null!));
			}));
	}

	#endregion

	#region DeadLetterTableName Tests

	[Theory]
	[InlineData("OutboxDeadLetters")]
	[InlineData("DeadLetters")]
	[InlineData("FailedMessages")]
	public void DeadLetterTableName_AcceptsValidValues(string tableName)
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(outbox =>
		{
			_ = outbox.UseSqlServer(TestConnectionString, sql => sql.DeadLetterTableName(tableName));
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<SqlServerOutboxOptions>>().Value;
		options.DeadLetterTableName.ShouldBe(tableName);
	}

	[Fact]
	public void DeadLetterTableName_ThrowsOnNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			services.AddExcaliburOutbox(outbox =>
			{
				_ = outbox.UseSqlServer(TestConnectionString, sql => sql.DeadLetterTableName(null!));
			}));
	}

	#endregion

	#region CommandTimeout Tests

	[Theory]
	[InlineData(1)]
	[InlineData(30)]
	[InlineData(120)]
	[InlineData(600)]
	public void CommandTimeout_AcceptsValidSeconds(int seconds)
	{
		// Arrange
		var services = new ServiceCollection();
		var timeout = TimeSpan.FromSeconds(seconds);

		// Act
		_ = services.AddExcaliburOutbox(outbox =>
		{
			_ = outbox.UseSqlServer(TestConnectionString, sql => sql.CommandTimeout(timeout));
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<SqlServerOutboxOptions>>().Value;
		options.CommandTimeoutSeconds.ShouldBe(seconds);
	}

	[Fact]
	public void CommandTimeout_ThrowsOnZero()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			services.AddExcaliburOutbox(outbox =>
			{
				_ = outbox.UseSqlServer(TestConnectionString, sql => sql.CommandTimeout(TimeSpan.Zero));
			}));
	}

	[Fact]
	public void CommandTimeout_ThrowsOnNegative()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			services.AddExcaliburOutbox(outbox =>
			{
				_ = outbox.UseSqlServer(TestConnectionString, sql => sql.CommandTimeout(TimeSpan.FromSeconds(-1)));
			}));
	}

	#endregion

	#region UseRowLocking Tests

	[Fact]
	public void UseRowLocking_EnabledByDefault()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(outbox =>
		{
			_ = outbox.UseSqlServer(TestConnectionString);
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<SqlServerOutboxOptions>>().Value;
		options.UseRowLocking.ShouldBeTrue();
	}

	[Fact]
	public void UseRowLocking_CanBeDisabled()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(outbox =>
		{
			_ = outbox.UseSqlServer(TestConnectionString, sql => sql.UseRowLocking(false));
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<SqlServerOutboxOptions>>().Value;
		options.UseRowLocking.ShouldBeFalse();
	}

	[Fact]
	public void UseRowLocking_CanBeExplicitlyEnabled()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(outbox =>
		{
			_ = outbox.UseSqlServer(TestConnectionString, sql => sql.UseRowLocking(true));
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<SqlServerOutboxOptions>>().Value;
		options.UseRowLocking.ShouldBeTrue();
	}

	[Fact]
	public void UseRowLocking_DefaultsToTrue_WhenCalledWithNoArgument()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(outbox =>
		{
			_ = outbox.UseSqlServer(TestConnectionString, sql => sql.UseRowLocking());
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<SqlServerOutboxOptions>>().Value;
		options.UseRowLocking.ShouldBeTrue();
	}

	#endregion

	#region DefaultBatchSize Tests

	[Theory]
	[InlineData(1)]
	[InlineData(100)]
	[InlineData(500)]
	[InlineData(10000)]
	public void DefaultBatchSize_AcceptsValidValues(int size)
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(outbox =>
		{
			_ = outbox.UseSqlServer(TestConnectionString, sql => sql.DefaultBatchSize(size));
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<SqlServerOutboxOptions>>().Value;
		options.DefaultBatchSize.ShouldBe(size);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	[InlineData(-100)]
	public void DefaultBatchSize_ThrowsOnInvalidValues(int size)
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			services.AddExcaliburOutbox(outbox =>
			{
				_ = outbox.UseSqlServer(TestConnectionString, sql => sql.DefaultBatchSize(size));
			}));
	}

	#endregion

	#region Fluent Chaining Tests

	[Fact]
	public void AllMethods_ReturnBuilder_ForChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(outbox =>
		{
			_ = outbox.UseSqlServer(TestConnectionString, sql => sql
				.SchemaName("messaging")
				.TableName("Messages")
				.TransportsTableName("Deliveries")
				.DeadLetterTableName("FailedMessages")
				.CommandTimeout(TimeSpan.FromSeconds(60))
				.UseRowLocking(true)
				.DefaultBatchSize(200));
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<SqlServerOutboxOptions>>().Value;
		options.SchemaName.ShouldBe("messaging");
		options.OutboxTableName.ShouldBe("Messages");
		options.TransportsTableName.ShouldBe("Deliveries");
		options.DeadLetterTableName.ShouldBe("FailedMessages");
		options.CommandTimeoutSeconds.ShouldBe(60);
		options.UseRowLocking.ShouldBeTrue();
		options.DefaultBatchSize.ShouldBe(200);
	}

	#endregion

	#region Connection String Tests

	[Fact]
	public void UseSqlServer_SetsConnectionString()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(outbox =>
		{
			_ = outbox.UseSqlServer(TestConnectionString);
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<SqlServerOutboxOptions>>().Value;
		options.ConnectionString.ShouldBe(TestConnectionString);
	}

	#endregion
}
