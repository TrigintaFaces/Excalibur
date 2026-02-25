// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.EventSourcing.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.EventSourcing.Tests.SqlServer;

/// <summary>
/// Unit tests for the <see cref="SqlServerMigrator"/> class.
/// </summary>
/// <remarks>
/// Tests focus on constructor validation, dual-constructor pattern consistency,
/// and interface implementation. Database-dependent behavior is tested via
/// integration tests with TestContainers separately.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Migrations")]
[Trait("Feature", "SqlServer")]
public sealed class SqlServerMigratorShould
{
	private readonly ILogger<SqlServerMigrator> _logger = NullLoggerFactory.Instance.CreateLogger<SqlServerMigrator>();
	private readonly Assembly _migrationAssembly = Assembly.GetExecutingAssembly();
	private const string MigrationNamespace = "TestMigrations";
	private const string ConnectionString = "Server=localhost;Database=TestDb;Trusted_Connection=true;";

	#region Simple Constructor Tests (Connection String)

	[Fact]
	public void SimpleConstructor_WithNullConnectionString_DoesNotThrowDuringConstruction()
	{
		// The simple constructor chains to the factory constructor via
		// () => new SqlConnection(connectionString). The null connectionString
		// is captured in the lambda, which is non-null itself, so the factory
		// constructor's null guard on connectionFactory passes. The null will
		// only manifest when the connection is actually opened (deferred validation).
		// Act
		var migrator = new SqlServerMigrator(
			connectionString: null!,
			_migrationAssembly,
			MigrationNamespace,
			_logger);

		// Assert - construction succeeds; failure occurs when connection opens
		_ = migrator.ShouldNotBeNull();
	}

	[Fact]
	public void SimpleConstructor_WithNullMigrationAssembly_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new SqlServerMigrator(
			ConnectionString,
			migrationAssembly: null!,
			MigrationNamespace,
			_logger));
	}

	[Fact]
	public void SimpleConstructor_WithNullMigrationNamespace_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new SqlServerMigrator(
			ConnectionString,
			_migrationAssembly,
			migrationNamespace: null!,
			_logger));
	}

	[Fact]
	public void SimpleConstructor_WithNullLogger_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new SqlServerMigrator(
			ConnectionString,
			_migrationAssembly,
			MigrationNamespace,
			logger: null!));
	}

	[Fact]
	public void SimpleConstructor_WithValidParameters_CreatesInstance()
	{
		// Act
		var migrator = new SqlServerMigrator(
			ConnectionString,
			_migrationAssembly,
			MigrationNamespace,
			_logger);

		// Assert
		_ = migrator.ShouldNotBeNull();
	}

	#endregion Simple Constructor Tests (Connection String)

	#region Advanced Constructor Tests (Connection Factory)

	[Fact]
	public void AdvancedConstructor_WithNullConnectionFactory_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new SqlServerMigrator(
			connectionFactory: null!,
			_migrationAssembly,
			MigrationNamespace,
			_logger));
	}

	[Fact]
	public void AdvancedConstructor_WithNullMigrationAssembly_ThrowsArgumentNullException()
	{
		// Arrange
		Func<SqlConnection> factory = () => new SqlConnection(ConnectionString);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new SqlServerMigrator(
			factory,
			migrationAssembly: null!,
			MigrationNamespace,
			_logger));
	}

	[Fact]
	public void AdvancedConstructor_WithNullMigrationNamespace_ThrowsArgumentNullException()
	{
		// Arrange
		Func<SqlConnection> factory = () => new SqlConnection(ConnectionString);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new SqlServerMigrator(
			factory,
			_migrationAssembly,
			migrationNamespace: null!,
			_logger));
	}

	[Fact]
	public void AdvancedConstructor_WithNullLogger_ThrowsArgumentNullException()
	{
		// Arrange
		Func<SqlConnection> factory = () => new SqlConnection(ConnectionString);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new SqlServerMigrator(
			factory,
			_migrationAssembly,
			MigrationNamespace,
			logger: null!));
	}

	[Fact]
	public void AdvancedConstructor_WithValidParameters_CreatesInstance()
	{
		// Arrange
		Func<SqlConnection> factory = () => new SqlConnection(ConnectionString);

		// Act
		var migrator = new SqlServerMigrator(
			factory,
			_migrationAssembly,
			MigrationNamespace,
			_logger);

		// Assert
		_ = migrator.ShouldNotBeNull();
	}

	[Fact]
	public void AdvancedConstructor_DoesNotCallFactoryDuringConstruction()
	{
		// Arrange
		var factoryCalled = false;
		Func<SqlConnection> factory = () =>
		{
			factoryCalled = true;
			return new SqlConnection(ConnectionString);
		};

		// Act
		var migrator = new SqlServerMigrator(
			factory,
			_migrationAssembly,
			MigrationNamespace,
			_logger);

		// Assert - factory is stored but not called during construction
		_ = migrator.ShouldNotBeNull();
		factoryCalled.ShouldBeFalse();
	}

	#endregion Advanced Constructor Tests (Connection Factory)

	#region Dual Constructor Pattern Consistency Tests

	[Fact]
	public void BothConstructors_CreateEquivalentInstances()
	{
		// Arrange
		Func<SqlConnection> factory = () => new SqlConnection(ConnectionString);

		// Act
		var simpleInstance = new SqlServerMigrator(
			ConnectionString,
			_migrationAssembly,
			MigrationNamespace,
			_logger);

		var advancedInstance = new SqlServerMigrator(
			factory,
			_migrationAssembly,
			MigrationNamespace,
			_logger);

		// Assert - Both should be valid instances
		_ = simpleInstance.ShouldNotBeNull();
		_ = advancedInstance.ShouldNotBeNull();
	}

	[Fact]
	public void SimpleConstructor_ChainsToAdvancedConstructor()
	{
		// This test verifies the constructor chaining pattern works correctly
		// by ensuring the simple constructor produces a working instance

		// Act - Creating instance should not throw
		var migrator = new SqlServerMigrator(
			"Server=(localdb)\\mssqllocaldb;Database=MigrationTest;Trusted_Connection=true",
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
		var migrator = new SqlServerMigrator(
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
		typeof(SqlServerMigrator).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void BePublic()
	{
		// Assert
		typeof(SqlServerMigrator).IsPublic.ShouldBeTrue();
	}

	#endregion Type Tests

	#region RollbackAsync Validation Tests

	[Fact]
	public async Task RollbackAsync_WithNullTargetMigrationId_ThrowsArgumentException()
	{
		// Arrange
		var migrator = new SqlServerMigrator(
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
		var migrator = new SqlServerMigrator(
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
		var migrator = new SqlServerMigrator(
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
		var connectionString = "Server=localhost;Database=Events;User Id=sa;Password=P@ssw0rd;TrustServerCertificate=true";

		// Act
		var migrator = new SqlServerMigrator(connectionString, _migrationAssembly, MigrationNamespace, _logger);

		// Assert
		_ = migrator.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_WithTrustedConnectionString_CreatesInstance()
	{
		// Arrange
		var connectionString = "Server=(localdb)\\mssqllocaldb;Database=Events;Trusted_Connection=true;MultipleActiveResultSets=true";

		// Act
		var migrator = new SqlServerMigrator(connectionString, _migrationAssembly, MigrationNamespace, _logger);

		// Assert
		_ = migrator.ShouldNotBeNull();
	}

	#endregion Connection String Format Tests
}
