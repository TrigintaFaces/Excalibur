// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Outbox;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Outbox.Tests.SqlServer.Builders;

/// <summary>
/// Unit tests for <see cref="ISqlServerOutboxBuilder"/> argument validation.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerOutboxBuilderValidationShould : UnitTestBase
{
	private const string TestConnectionString = "Server=localhost;Database=TestDb;Trusted_Connection=True;";

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void SchemaName_ThrowsOnInvalidValue(string? invalidValue)
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			services.AddExcaliburOutbox(builder =>
			{
				_ = builder.UseSqlServer(TestConnectionString, sql =>
				{
					_ = sql.SchemaName(invalidValue);
				});
			}));
	}

	[Theory]
	[InlineData("dbo")]
	[InlineData("Messaging")]
	[InlineData("outbox")]
	public void SchemaName_AcceptsValidValues(string validValue)
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.UseSqlServer(TestConnectionString, sql =>
			{
				_ = sql.SchemaName(validValue);
			});
		});

		// Assert - no exception thrown
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void TableName_ThrowsOnInvalidValue(string? invalidValue)
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			services.AddExcaliburOutbox(builder =>
			{
				_ = builder.UseSqlServer(TestConnectionString, sql =>
				{
					_ = sql.TableName(invalidValue);
				});
			}));
	}

	[Theory]
	[InlineData("OutboxMessages")]
	[InlineData("Messages")]
	[InlineData("outbox_messages")]
	public void TableName_AcceptsValidValues(string validValue)
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.UseSqlServer(TestConnectionString, sql =>
			{
				_ = sql.TableName(validValue);
			});
		});

		// Assert - no exception thrown
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void TransportsTableName_ThrowsOnInvalidValue(string? invalidValue)
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			services.AddExcaliburOutbox(builder =>
			{
				_ = builder.UseSqlServer(TestConnectionString, sql =>
				{
					_ = sql.TransportsTableName(invalidValue);
				});
			}));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void DeadLetterTableName_ThrowsOnInvalidValue(string? invalidValue)
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			services.AddExcaliburOutbox(builder =>
			{
				_ = builder.UseSqlServer(TestConnectionString, sql =>
				{
					_ = sql.DeadLetterTableName(invalidValue);
				});
			}));
	}

	[Fact]
	public void CommandTimeout_ThrowsOnZero()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			services.AddExcaliburOutbox(builder =>
			{
				_ = builder.UseSqlServer(TestConnectionString, sql =>
				{
					_ = sql.CommandTimeout(TimeSpan.Zero);
				});
			}));
	}

	[Fact]
	public void CommandTimeout_ThrowsOnNegative()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			services.AddExcaliburOutbox(builder =>
			{
				_ = builder.UseSqlServer(TestConnectionString, sql =>
				{
					_ = sql.CommandTimeout(TimeSpan.FromSeconds(-1));
				});
			}));
	}

	[Theory]
	[InlineData(1)]
	[InlineData(30)]
	[InlineData(120)]
	public void CommandTimeout_AcceptsValidValues(int seconds)
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.UseSqlServer(TestConnectionString, sql =>
			{
				_ = sql.CommandTimeout(TimeSpan.FromSeconds(seconds));
			});
		});

		// Assert - no exception thrown
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	[InlineData(-100)]
	public void DefaultBatchSize_ThrowsOnInvalidValue(int invalidValue)
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			services.AddExcaliburOutbox(builder =>
			{
				_ = builder.UseSqlServer(TestConnectionString, sql =>
				{
					_ = sql.DefaultBatchSize(invalidValue);
				});
			}));
	}

	[Theory]
	[InlineData(1)]
	[InlineData(100)]
	[InlineData(1000)]
	public void DefaultBatchSize_AcceptsValidValues(int validValue)
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.UseSqlServer(TestConnectionString, sql =>
			{
				_ = sql.DefaultBatchSize(validValue);
			});
		});

		// Assert - no exception thrown
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void UseRowLocking_AcceptsBooleanValues(bool enable)
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.UseSqlServer(TestConnectionString, sql =>
			{
				_ = sql.UseRowLocking(enable);
			});
		});

		// Assert - no exception thrown
	}
}
