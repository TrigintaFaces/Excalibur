// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.EventSourcing.Postgres;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Npgsql;

namespace Excalibur.EventSourcing.Tests.Postgres;

/// <summary>
/// Unit tests for the <see cref="PostgresMigrator"/> class.
/// </summary>
/// <remarks>
/// Tests focus on constructor validation, dual-constructor pattern consistency,
/// and interface implementation. Database-dependent behavior is tested via
/// integration tests with TestContainers separately.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Migrations")]
[Trait("Feature", "Postgres")]
public sealed class PostgresMigratorShould
{
	private readonly ILogger<PostgresMigrator> _logger = NullLoggerFactory.Instance.CreateLogger<PostgresMigrator>();
	private readonly Assembly _migrationAssembly = Assembly.GetExecutingAssembly();
	private const string MigrationNamespace = "TestMigrations";
	private const string ConnectionString = "Host=localhost;Database=TestDb;Username=postgres;Password=secret";

	#region Simple Constructor Tests (Connection String)

	[Fact]
	public void SimpleConstructor_WithNullConnectionString_ThrowsArgumentException()
	{
		// The simple constructor calls NpgsqlDataSource.Create(connectionString)
		// which validates the connection string immediately and throws ArgumentException
		// ("Host can't be null") rather than ArgumentNullException.
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => new PostgresMigrator(
			connectionString: null!,
			_migrationAssembly,
			MigrationNamespace,
			_logger));
	}

	[Fact]
	public void SimpleConstructor_WithNullMigrationAssembly_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new PostgresMigrator(
			ConnectionString,
			migrationAssembly: null!,
			MigrationNamespace,
			_logger));
	}

	[Fact]
	public void SimpleConstructor_WithNullMigrationNamespace_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new PostgresMigrator(
			ConnectionString,
			_migrationAssembly,
			migrationNamespace: null!,
			_logger));
	}

	[Fact]
	public void SimpleConstructor_WithNullLogger_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new PostgresMigrator(
			ConnectionString,
			_migrationAssembly,
			MigrationNamespace,
			logger: null!));
	}

	[Fact]
	public void SimpleConstructor_WithValidParameters_CreatesInstance()
	{
		// Act
		var migrator = new PostgresMigrator(
			ConnectionString,
			_migrationAssembly,
			MigrationNamespace,
			_logger);

		// Assert
		_ = migrator.ShouldNotBeNull();
	}

	#endregion Simple Constructor Tests (Connection String)

	#region Advanced Constructor Tests (NpgsqlDataSource)

	[Fact]
	public void AdvancedConstructor_WithNullDataSource_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new PostgresMigrator(
			dataSource: null!,
			_migrationAssembly,
			MigrationNamespace,
			_logger));
	}

	[Fact]
	public void AdvancedConstructor_WithNullMigrationAssembly_ThrowsArgumentNullException()
	{
		// Arrange
		var dataSource = NpgsqlDataSource.Create(ConnectionString);

		try
		{
			// Act & Assert
			_ = Should.Throw<ArgumentNullException>(() => new PostgresMigrator(
				dataSource,
				migrationAssembly: null!,
				MigrationNamespace,
				_logger));
		}
		finally
		{
			dataSource.Dispose();
		}
	}

	[Fact]
	public void AdvancedConstructor_WithNullMigrationNamespace_ThrowsArgumentNullException()
	{
		// Arrange
		var dataSource = NpgsqlDataSource.Create(ConnectionString);

		try
		{
			// Act & Assert
			_ = Should.Throw<ArgumentNullException>(() => new PostgresMigrator(
				dataSource,
				_migrationAssembly,
				migrationNamespace: null!,
				_logger));
		}
		finally
		{
			dataSource.Dispose();
		}
	}

	[Fact]
	public void AdvancedConstructor_WithNullLogger_ThrowsArgumentNullException()
	{
		// Arrange
		var dataSource = NpgsqlDataSource.Create(ConnectionString);

		try
		{
			// Act & Assert
			_ = Should.Throw<ArgumentNullException>(() => new PostgresMigrator(
				dataSource,
				_migrationAssembly,
				MigrationNamespace,
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
		var dataSource = NpgsqlDataSource.Create(ConnectionString);

		try
		{
			// Act
			var migrator = new PostgresMigrator(
				dataSource,
				_migrationAssembly,
				MigrationNamespace,
				_logger);

			// Assert
			_ = migrator.ShouldNotBeNull();
		}
		finally
		{
			dataSource.Dispose();
		}
	}

	[Fact]
	public void AdvancedConstructor_WithCustomDataSourceBuilder_CreatesInstance()
	{
		// Arrange
		var builder = new NpgsqlDataSourceBuilder(ConnectionString);
		var dataSource = builder.Build();

		try
		{
			// Act
			var migrator = new PostgresMigrator(
				dataSource,
				_migrationAssembly,
				MigrationNamespace,
				_logger);

			// Assert
			_ = migrator.ShouldNotBeNull();
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
		var dataSource = NpgsqlDataSource.Create(ConnectionString);

		try
		{
			// Act
			var simpleInstance = new PostgresMigrator(
				ConnectionString,
				_migrationAssembly,
				MigrationNamespace,
				_logger);

			var advancedInstance = new PostgresMigrator(
				dataSource,
				_migrationAssembly,
				MigrationNamespace,
				_logger);

			// Assert - Both should be valid instances
			_ = simpleInstance.ShouldNotBeNull();
			_ = advancedInstance.ShouldNotBeNull();
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

		// Act - Creating instance should not throw
		var migrator = new PostgresMigrator(
			"Host=localhost;Port=5432;Database=MigrationTest;Username=test;Password=test",
			_migrationAssembly,
			MigrationNamespace,
			_logger);

		// Assert
		_ = migrator.ShouldNotBeNull();
	}

	#endregion Dual Constructor Pattern Consistency Tests

	#region Interface Implementation Tests

	[Fact]
	public void ImplementIMigrator()
	{
		// Arrange
		var migrator = new PostgresMigrator(
			ConnectionString,
			_migrationAssembly,
			MigrationNamespace,
			_logger);

		// Assert
		_ = migrator.ShouldBeAssignableTo<IMigrator>();
	}

	#endregion Interface Implementation Tests

	#region Type Tests

	[Fact]
	public void BeSealed()
	{
		// Assert
		typeof(PostgresMigrator).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void BePublic()
	{
		// Assert
		typeof(PostgresMigrator).IsPublic.ShouldBeTrue();
	}

	#endregion Type Tests

	#region RollbackAsync Validation Tests

	[Fact]
	public async Task RollbackAsync_WithNullTargetMigrationId_ThrowsArgumentException()
	{
		// Arrange
		var migrator = new PostgresMigrator(
			ConnectionString,
			_migrationAssembly,
			MigrationNamespace,
			_logger);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(
			() => migrator.RollbackAsync(null!, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task RollbackAsync_WithEmptyTargetMigrationId_ThrowsArgumentException()
	{
		// Arrange
		var migrator = new PostgresMigrator(
			ConnectionString,
			_migrationAssembly,
			MigrationNamespace,
			_logger);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(
			() => migrator.RollbackAsync(string.Empty, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task RollbackAsync_WithWhitespaceTargetMigrationId_ThrowsArgumentException()
	{
		// Arrange
		var migrator = new PostgresMigrator(
			ConnectionString,
			_migrationAssembly,
			MigrationNamespace,
			_logger);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(
			() => migrator.RollbackAsync("   ", CancellationToken.None)).ConfigureAwait(false);
	}

	#endregion RollbackAsync Validation Tests

	#region Connection String Format Tests

	[Fact]
	public void Constructor_WithStandardConnectionString_CreatesInstance()
	{
		// Arrange
		var connectionString = "Host=localhost;Port=5432;Database=events;Username=user;Password=pass";

		// Act
		var migrator = new PostgresMigrator(connectionString, _migrationAssembly, MigrationNamespace, _logger);

		// Assert
		_ = migrator.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_WithPoolingOptions_CreatesInstance()
	{
		// Arrange
		var connectionString = "Host=localhost;Database=events;Pooling=true;Minimum Pool Size=1;Maximum Pool Size=100";

		// Act
		var migrator = new PostgresMigrator(connectionString, _migrationAssembly, MigrationNamespace, _logger);

		// Assert
		_ = migrator.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_WithSslOptions_CreatesInstance()
	{
		// Arrange
		var connectionString = "Host=localhost;Database=events;SSL Mode=Prefer;Trust Server Certificate=true";

		// Act
		var migrator = new PostgresMigrator(connectionString, _migrationAssembly, MigrationNamespace, _logger);

		// Assert
		_ = migrator.ShouldNotBeNull();
	}

	#endregion Connection String Format Tests
}
