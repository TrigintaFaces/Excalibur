// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Outbox;

using Microsoft.Extensions.DependencyInjection;

using Excalibur.Data.Postgres;
namespace Excalibur.Data.Tests.Postgres.Builders;

/// <summary>
/// Unit tests for <see cref="IPostgresOutboxBuilder"/> argument validation.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Database", "Postgres")]
public sealed class PostgresOutboxBuilderValidationShould : UnitTestBase
{
	private const string TestConnectionString = "Host=localhost;Database=TestDb;Username=test;Password=test;";

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
				_ = builder.UsePostgres(TestConnectionString, postgres =>
				{
					_ = postgres.SchemaName(invalidValue);
				});
			}));
	}

	[Theory]
	[InlineData("public")]
	[InlineData("messaging")]
	[InlineData("outbox")]
	public void SchemaName_AcceptsValidValues(string validValue)
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.UsePostgres(TestConnectionString, postgres =>
			{
				_ = postgres.SchemaName(validValue);
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
				_ = builder.UsePostgres(TestConnectionString, postgres =>
				{
					_ = postgres.TableName(invalidValue);
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
				_ = builder.UsePostgres(TestConnectionString, postgres =>
				{
					_ = postgres.DeadLetterTableName(invalidValue);
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
				_ = builder.UsePostgres(TestConnectionString, postgres =>
				{
					_ = postgres.CommandTimeout(TimeSpan.Zero);
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
				_ = builder.UsePostgres(TestConnectionString, postgres =>
				{
					_ = postgres.CommandTimeout(TimeSpan.FromSeconds(-1));
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
			_ = builder.UsePostgres(TestConnectionString, postgres =>
			{
				_ = postgres.CommandTimeout(TimeSpan.FromSeconds(seconds));
			});
		});

		// Assert - no exception thrown
	}

	[Fact]
	public void ReservationTimeout_ThrowsOnZero()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			services.AddExcaliburOutbox(builder =>
			{
				_ = builder.UsePostgres(TestConnectionString, postgres =>
				{
					_ = postgres.ReservationTimeout(TimeSpan.Zero);
				});
			}));
	}

	[Fact]
	public void ReservationTimeout_ThrowsOnNegative()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			services.AddExcaliburOutbox(builder =>
			{
				_ = builder.UsePostgres(TestConnectionString, postgres =>
				{
					_ = postgres.ReservationTimeout(TimeSpan.FromMinutes(-1));
				});
			}));
	}

	[Theory]
	[InlineData(1)]
	[InlineData(5)]
	[InlineData(30)]
	public void ReservationTimeout_AcceptsValidMinutes(int minutes)
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.UsePostgres(TestConnectionString, postgres =>
			{
				_ = postgres.ReservationTimeout(TimeSpan.FromMinutes(minutes));
			});
		});

		// Assert - no exception thrown
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	[InlineData(-100)]
	public void MaxAttempts_ThrowsOnInvalidValue(int invalidValue)
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			services.AddExcaliburOutbox(builder =>
			{
				_ = builder.UsePostgres(TestConnectionString, postgres =>
				{
					_ = postgres.MaxAttempts(invalidValue);
				});
			}));
	}

	[Theory]
	[InlineData(1)]
	[InlineData(5)]
	[InlineData(10)]
	public void MaxAttempts_AcceptsValidValues(int validValue)
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddExcaliburOutbox(builder =>
		{
			_ = builder.UsePostgres(TestConnectionString, postgres =>
			{
				_ = postgres.MaxAttempts(validValue);
			});
		});

		// Assert - no exception thrown
	}
}
