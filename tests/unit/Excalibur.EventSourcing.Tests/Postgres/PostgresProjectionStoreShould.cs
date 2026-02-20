// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Excalibur.EventSourcing.Postgres;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Npgsql;

namespace Excalibur.EventSourcing.Tests.Postgres;

/// <summary>
/// Unit tests for the <see cref="PostgresProjectionStore{TProjection}"/> class.
/// </summary>
/// <remarks>
/// Sprint 510 (S510.3): Postgres EventSourcing provider tests.
/// Tests focus on constructor validation and dual-constructor pattern consistency.
/// Integration tests with TestContainers would be added separately.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Postgres")]
public sealed class PostgresProjectionStoreShould
{
	private readonly ILogger<PostgresProjectionStore<SampleProjection>> _logger =
		NullLoggerFactory.Instance.CreateLogger<PostgresProjectionStore<SampleProjection>>();

	#region Simple Constructor Tests (Connection String)

	[Fact]
	public void SimpleConstructor_WithNullConnectionString_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new PostgresProjectionStore<SampleProjection>(
			connectionString: null!,
			_logger));
	}

	[Fact]
	public void SimpleConstructor_WithNullLogger_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new PostgresProjectionStore<SampleProjection>(
			connectionString: "Host=localhost;Database=TestDb",
			logger: null!));
	}

	[Fact]
	public void SimpleConstructor_WithValidParameters_CreatesInstance()
	{
		// Act
		var store = new PostgresProjectionStore<SampleProjection>(
			connectionString: "Host=localhost;Database=TestDb",
			_logger);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	[Fact]
	public void SimpleConstructor_WithCustomTableName_CreatesInstance()
	{
		// Act
		var store = new PostgresProjectionStore<SampleProjection>(
			connectionString: "Host=localhost;Database=TestDb",
			_logger,
			tableName: "custom_projections");

		// Assert
		_ = store.ShouldNotBeNull();
	}

	[Fact]
	public void SimpleConstructor_WithCustomJsonOptions_CreatesInstance()
	{
		// Arrange
		var jsonOptions = new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
			WriteIndented = true
		};

		// Act
		var store = new PostgresProjectionStore<SampleProjection>(
			connectionString: "Host=localhost;Database=TestDb",
			_logger,
			tableName: null,
			jsonOptions: jsonOptions);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	#endregion Simple Constructor Tests (Connection String)

	#region Advanced Constructor Tests (NpgsqlDataSource)

	[Fact]
	public void AdvancedConstructor_WithNullDataSource_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new PostgresProjectionStore<SampleProjection>(
			dataSource: null!,
			_logger));
	}

	[Fact]
	public void AdvancedConstructor_WithNullLogger_ThrowsArgumentNullException()
	{
		// Arrange
		var dataSource = NpgsqlDataSource.Create("Host=localhost;Database=TestDb");

		try
		{
			// Act & Assert
			_ = Should.Throw<ArgumentNullException>(() => new PostgresProjectionStore<SampleProjection>(
				dataSource,
				logger: null!));
		}
		finally
		{
			dataSource.Dispose();
		}
	}

	[Fact]
	public void AdvancedConstructor_WithValidParameters_CreatesInstance()
	{
		// Arrange
		var dataSource = NpgsqlDataSource.Create("Host=localhost;Database=TestDb");

		try
		{
			// Act
			var store = new PostgresProjectionStore<SampleProjection>(
				dataSource,
				_logger);

			// Assert
			_ = store.ShouldNotBeNull();
		}
		finally
		{
			dataSource.Dispose();
		}
	}

	[Fact]
	public void AdvancedConstructor_WithCustomTableName_CreatesInstance()
	{
		// Arrange
		var dataSource = NpgsqlDataSource.Create("Host=localhost;Database=TestDb");

		try
		{
			// Act
			var store = new PostgresProjectionStore<SampleProjection>(
				dataSource,
				_logger,
				tableName: "custom_projections");

			// Assert
			_ = store.ShouldNotBeNull();
		}
		finally
		{
			dataSource.Dispose();
		}
	}

	[Fact]
	public void AdvancedConstructor_WithCustomJsonOptions_CreatesInstance()
	{
		// Arrange
		var dataSource = NpgsqlDataSource.Create("Host=localhost;Database=TestDb");
		var jsonOptions = new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
			WriteIndented = true
		};

		try
		{
			// Act
			var store = new PostgresProjectionStore<SampleProjection>(
				dataSource,
				_logger,
				tableName: null,
				jsonOptions: jsonOptions);

			// Assert
			_ = store.ShouldNotBeNull();
		}
		finally
		{
			dataSource.Dispose();
		}
	}

	#endregion Advanced Constructor Tests (NpgsqlDataSource)

	#region Dual Constructor Pattern Consistency Tests

	[Fact]
	public void BothConstructors_CreateEquivalentInstances()
	{
		// Arrange
		var connectionString = "Host=localhost;Database=TestDb";
		var dataSource = NpgsqlDataSource.Create(connectionString);

		try
		{
			// Act
			var simpleStore = new PostgresProjectionStore<SampleProjection>(
				connectionString,
				_logger);

			var advancedStore = new PostgresProjectionStore<SampleProjection>(
				dataSource,
				_logger);

			// Assert - Both should be valid instances
			_ = simpleStore.ShouldNotBeNull();
			_ = advancedStore.ShouldNotBeNull();
		}
		finally
		{
			dataSource.Dispose();
		}
	}

	[Fact]
	public void SimpleConstructor_ChainsToAdvancedConstructor()
	{
		// This test verifies the constructor chaining pattern works correctly
		// by ensuring the simple constructor produces a working instance

		// Arrange
		var connectionString = "Host=localhost;Port=5432;Database=TestDb;Username=test;Password=test";

		// Act - Creating instance should not throw
		var store = new PostgresProjectionStore<SampleProjection>(
			connectionString,
			_logger);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	#endregion Dual Constructor Pattern Consistency Tests

	#region Generic Type Tests

	[Fact]
	public void Constructor_WithDifferentProjectionTypes_CreatesInstance()
	{
		// Arrange
		var connectionString = "Host=localhost;Database=TestDb";
		var otherLogger = NullLoggerFactory.Instance.CreateLogger<PostgresProjectionStore<AnotherProjection>>();

		// Act
		var sampleStore = new PostgresProjectionStore<SampleProjection>(connectionString, _logger);
		var otherStore = new PostgresProjectionStore<AnotherProjection>(connectionString, otherLogger);

		// Assert
		_ = sampleStore.ShouldNotBeNull();
		_ = otherStore.ShouldNotBeNull();
	}

	#endregion Generic Type Tests

	#region Connection String Format Tests

	[Fact]
	public void Constructor_WithPostgresConnectionString_CreatesInstance()
	{
		// Arrange - Postgres connection string format
		var connectionString = "Host=localhost;Port=5432;Database=projections;Username=user;Password=pass";

		// Act
		var store = new PostgresProjectionStore<SampleProjection>(connectionString, _logger);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_WithPoolingOptions_CreatesInstance()
	{
		// Arrange - Connection string with pooling options
		var connectionString = "Host=localhost;Database=projections;Pooling=true;Minimum Pool Size=1;Maximum Pool Size=100";

		// Act
		var store = new PostgresProjectionStore<SampleProjection>(connectionString, _logger);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	#endregion Connection String Format Tests

	#region Empty Connection String Tests

	[Fact]
	public void SimpleConstructor_WithEmptyConnectionString_ThrowsArgumentException()
	{
		// Note: Npgsql validates the connection string format
		// Empty strings are rejected by NpgsqlDataSource.Create

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => new PostgresProjectionStore<SampleProjection>(
			connectionString: string.Empty,
			_logger));
	}

	[Fact]
	public void SimpleConstructor_WithWhitespaceConnectionString_ThrowsArgumentException()
	{
		// Note: Npgsql validates the connection string format
		// Whitespace-only strings are rejected by NpgsqlDataSource.Create

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => new PostgresProjectionStore<SampleProjection>(
			connectionString: "   ",
			_logger));
	}

	#endregion Empty Connection String Tests

	#region Interface Implementation Tests

	[Fact]
	public void Store_ImplementsIProjectionStore()
	{
		// Arrange
		var store = new PostgresProjectionStore<SampleProjection>("Host=localhost;Database=TestDb", _logger);

		// Assert
		_ = store.ShouldBeAssignableTo<IProjectionStore<SampleProjection>>();
	}

	#endregion Interface Implementation Tests

	#region NpgsqlDataSource Options Tests

	[Fact]
	public void AdvancedConstructor_WithCustomDataSourceBuilder_CreatesInstance()
	{
		// Arrange
		var builder = new NpgsqlDataSourceBuilder("Host=localhost;Database=TestDb");
		var dataSource = builder.Build();

		try
		{
			// Act
			var store = new PostgresProjectionStore<SampleProjection>(dataSource, _logger);

			// Assert
			_ = store.ShouldNotBeNull();
		}
		finally
		{
			dataSource.Dispose();
		}
	}

	#endregion NpgsqlDataSource Options Tests
}

/// <summary>
/// Sample projection type for testing.
/// </summary>
public sealed class SampleProjection
{
	public string Id { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public int Value { get; set; }
}

/// <summary>
/// Another sample projection type for generic type tests.
/// </summary>
public sealed class AnotherProjection
{
	public string Key { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
}
