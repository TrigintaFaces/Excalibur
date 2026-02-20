// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Data.Postgres.Cdc;

using Excalibur.Data.Postgres;
namespace Excalibur.Data.Tests.Postgres.Cdc.Builders;

/// <summary>
/// Unit tests for <see cref="IPostgresCdcBuilder"/> argument validation.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Database", "Postgres")]
public sealed class PostgresCdcBuilderValidationShould : UnitTestBase
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
			services.AddCdcProcessor(builder =>
			{
				_ = builder.UsePostgres(TestConnectionString, pg =>
				{
					_ = pg.SchemaName(invalidValue);
				});
			}));
	}

	[Theory]
	[InlineData("public")]
	[InlineData("cdc")]
	[InlineData("audit")]
	public void SchemaName_AcceptsValidValues(string validValue)
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.UsePostgres(TestConnectionString, pg =>
			{
				_ = pg.SchemaName(validValue);
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<PostgresCdcStateStoreOptions>>();
		options.Value.SchemaName.ShouldBe(validValue);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void StateTableName_ThrowsOnInvalidValue(string? invalidValue)
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			services.AddCdcProcessor(builder =>
			{
				_ = builder.UsePostgres(TestConnectionString, pg =>
				{
					_ = pg.StateTableName(invalidValue);
				});
			}));
	}

	[Theory]
	[InlineData("cdc_state")]
	[InlineData("processing_state")]
	[InlineData("cdc_positions")]
	public void StateTableName_AcceptsValidValues(string validValue)
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.UsePostgres(TestConnectionString, pg =>
			{
				_ = pg.StateTableName(validValue);
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<PostgresCdcStateStoreOptions>>();
		options.Value.TableName.ShouldBe(validValue);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ReplicationSlotName_ThrowsOnInvalidValue(string? invalidValue)
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			services.AddCdcProcessor(builder =>
			{
				_ = builder.UsePostgres(TestConnectionString, pg =>
				{
					_ = pg.ReplicationSlotName(invalidValue);
				});
			}));
	}

	[Theory]
	[InlineData("my_slot")]
	[InlineData("excalibur_cdc_slot")]
	[InlineData("replication_001")]
	public void ReplicationSlotName_AcceptsValidValues(string validValue)
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.UsePostgres(TestConnectionString, pg =>
			{
				_ = pg.ReplicationSlotName(validValue);
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<PostgresCdcOptions>>();
		options.Value.ReplicationSlotName.ShouldBe(validValue);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void PublicationName_ThrowsOnInvalidValue(string? invalidValue)
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			services.AddCdcProcessor(builder =>
			{
				_ = builder.UsePostgres(TestConnectionString, pg =>
				{
					_ = pg.PublicationName(invalidValue);
				});
			}));
	}

	[Theory]
	[InlineData("my_publication")]
	[InlineData("excalibur_pub")]
	[InlineData("cdc_publication")]
	public void PublicationName_AcceptsValidValues(string validValue)
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.UsePostgres(TestConnectionString, pg =>
			{
				_ = pg.PublicationName(validValue);
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<PostgresCdcOptions>>();
		options.Value.PublicationName.ShouldBe(validValue);
	}

	[Fact]
	public void PollingInterval_ThrowsOnZero()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			services.AddCdcProcessor(builder =>
			{
				_ = builder.UsePostgres(TestConnectionString, pg =>
				{
					_ = pg.PollingInterval(TimeSpan.Zero);
				});
			}));
	}

	[Fact]
	public void PollingInterval_ThrowsOnNegative()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			services.AddCdcProcessor(builder =>
			{
				_ = builder.UsePostgres(TestConnectionString, pg =>
				{
					_ = pg.PollingInterval(TimeSpan.FromSeconds(-1));
				});
			}));
	}

	[Theory]
	[InlineData(1)]
	[InlineData(5)]
	[InlineData(30)]
	[InlineData(60)]
	public void PollingInterval_AcceptsValidSeconds(int seconds)
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		var expectedInterval = TimeSpan.FromSeconds(seconds);

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

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	[InlineData(-100)]
	public void BatchSize_ThrowsOnInvalidValue(int invalidValue)
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			services.AddCdcProcessor(builder =>
			{
				_ = builder.UsePostgres(TestConnectionString, pg =>
				{
					_ = pg.BatchSize(invalidValue);
				});
			}));
	}

	[Theory]
	[InlineData(1)]
	[InlineData(100)]
	[InlineData(500)]
	[InlineData(10000)]
	public void BatchSize_AcceptsValidValues(int validValue)
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.UsePostgres(TestConnectionString, pg =>
			{
				_ = pg.BatchSize(validValue);
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<PostgresCdcOptions>>();
		options.Value.BatchSize.ShouldBe(validValue);
	}

	[Fact]
	public void Timeout_ThrowsOnZero()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			services.AddCdcProcessor(builder =>
			{
				_ = builder.UsePostgres(TestConnectionString, pg =>
				{
					_ = pg.Timeout(TimeSpan.Zero);
				});
			}));
	}

	[Fact]
	public void Timeout_ThrowsOnNegative()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			services.AddCdcProcessor(builder =>
			{
				_ = builder.UsePostgres(TestConnectionString, pg =>
				{
					_ = pg.Timeout(TimeSpan.FromSeconds(-1));
				});
			}));
	}

	[Theory]
	[InlineData(1)]
	[InlineData(30)]
	[InlineData(60)]
	[InlineData(300)]
	public void Timeout_AcceptsValidSeconds(int seconds)
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		var expectedTimeout = TimeSpan.FromSeconds(seconds);

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

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ProcessorId_ThrowsOnInvalidValue(string? invalidValue)
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			services.AddCdcProcessor(builder =>
			{
				_ = builder.UsePostgres(TestConnectionString, pg =>
				{
					_ = pg.ProcessorId(invalidValue);
				});
			}));
	}

	[Theory]
	[InlineData("worker-1")]
	[InlineData("processor-node-001")]
	[InlineData("cdc-instance")]
	public void ProcessorId_AcceptsValidValues(string validValue)
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.UsePostgres(TestConnectionString, pg =>
			{
				_ = pg.ProcessorId(validValue);
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<PostgresCdcOptions>>();
		options.Value.ProcessorId.ShouldBe(validValue);
	}
}
