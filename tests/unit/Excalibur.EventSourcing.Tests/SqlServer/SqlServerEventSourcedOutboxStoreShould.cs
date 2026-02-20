// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Outbox;
using Excalibur.EventSourcing.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Excalibur.EventSourcing.Tests.SqlServer;

/// <summary>
/// Unit tests for the <see cref="SqlServerEventSourcedOutboxStore"/> class.
/// </summary>
/// <remarks>
/// Sprint 511 (S511.2): SQL Server data provider unit tests.
/// Tests focus on constructor validation and dual-constructor pattern consistency.
/// No database dependencies - pure unit tests with mocks.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "SqlServer")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerEventSourcedOutboxStoreShould : UnitTestBase
{
	private readonly ILogger<SqlServerEventSourcedOutboxStore> _logger = NullLoggerFactory.CreateLogger<SqlServerEventSourcedOutboxStore>();

	#region Simple Constructor Tests (Connection String)

	[Fact]
	public void SimpleConstructor_WithNullConnectionString_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new SqlServerEventSourcedOutboxStore(
			connectionString: null!,
			_logger));
	}

	[Fact]
	public void SimpleConstructor_WithNullLogger_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new SqlServerEventSourcedOutboxStore(
			connectionString: "Server=localhost;Database=TestDb",
			logger: null!));
	}

	[Fact]
	public void SimpleConstructor_WithValidParameters_CreatesInstance()
	{
		// Act
		var store = new SqlServerEventSourcedOutboxStore(
			connectionString: "Server=localhost;Database=TestDb",
			_logger);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	[Fact]
	public void SimpleConstructor_WithEmptyConnectionString_AcceptsForLazyValidation()
	{
		// Note: Empty connection strings are accepted at construction time.
		// Validation happens when the connection factory is invoked.
		// This is consistent with lazy connection factory pattern.

		// Act - This doesn't throw during construction
		var store = new SqlServerEventSourcedOutboxStore(
			connectionString: string.Empty,
			_logger);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	#endregion Simple Constructor Tests (Connection String)

	#region Advanced Constructor Tests (Connection Factory)

	[Fact]
	public void AdvancedConstructor_WithNullConnectionFactory_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new SqlServerEventSourcedOutboxStore(
			connectionFactory: null!,
			_logger));
	}

	[Fact]
	public void AdvancedConstructor_WithNullLogger_ThrowsArgumentNullException()
	{
		// Arrange
		Func<SqlConnection> factory = () => new SqlConnection("Server=localhost");

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new SqlServerEventSourcedOutboxStore(
			factory,
			logger: null!));
	}

	[Fact]
	public void AdvancedConstructor_WithValidParameters_CreatesInstance()
	{
		// Arrange
		Func<SqlConnection> factory = () => new SqlConnection("Server=localhost;Database=TestDb");

		// Act
		var store = new SqlServerEventSourcedOutboxStore(
			factory,
			_logger);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	[Fact]
	public void AdvancedConstructor_FactoryNotCalledDuringConstruction()
	{
		// Arrange
		var factoryCalled = false;
		Func<SqlConnection> factory = () =>
		{
			factoryCalled = true;
			return new SqlConnection("Server=localhost;Database=TestDb");
		};

		// Act
		var store = new SqlServerEventSourcedOutboxStore(
			factory,
			_logger);

		// Assert - Factory should NOT be invoked during construction
		_ = store.ShouldNotBeNull();
		factoryCalled.ShouldBeFalse();
	}

	#endregion Advanced Constructor Tests (Connection Factory)

	#region Dual Constructor Pattern Consistency Tests

	[Fact]
	public void BothConstructors_CreateEquivalentInstances()
	{
		// Arrange
		var connectionString = "Server=localhost;Database=TestDb";

		// Act
		var simpleStore = new SqlServerEventSourcedOutboxStore(
			connectionString,
			_logger);

		var advancedStore = new SqlServerEventSourcedOutboxStore(
			() => new SqlConnection(connectionString),
			_logger);

		// Assert - Both should be valid instances
		_ = simpleStore.ShouldNotBeNull();
		_ = advancedStore.ShouldNotBeNull();
	}

	[Fact]
	public void SimpleConstructor_ChainsToAdvancedConstructor()
	{
		// This test verifies the constructor chaining pattern works correctly
		// by ensuring the simple constructor produces a working instance

		// Arrange
		var connectionString = "Server=(localdb)\\mssqllocaldb;Database=TestDb;Trusted_Connection=true";

		// Act - Creating instance should not throw
		var store = new SqlServerEventSourcedOutboxStore(
			connectionString,
			_logger);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	#endregion Dual Constructor Pattern Consistency Tests

	#region Connection String Format Tests

	[Fact]
	public void Constructor_WithSqlServerConnectionString_CreatesInstance()
	{
		// Arrange - SQL Server connection string format
		var connectionString = "Server=localhost;Database=outbox;User Id=sa;Password=Pass123!";

		// Act
		var store = new SqlServerEventSourcedOutboxStore(connectionString, _logger);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_WithIntegratedSecurity_CreatesInstance()
	{
		// Arrange - Integrated security connection string
		var connectionString = "Server=localhost;Database=outbox;Integrated Security=true";

		// Act
		var store = new SqlServerEventSourcedOutboxStore(connectionString, _logger);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_WithTrustedConnection_CreatesInstance()
	{
		// Arrange - Trusted connection string
		var connectionString = "Server=localhost;Database=outbox;Trusted_Connection=true";

		// Act
		var store = new SqlServerEventSourcedOutboxStore(connectionString, _logger);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_WithPoolingOptions_CreatesInstance()
	{
		// Arrange - Connection string with pooling options
		var connectionString = "Server=localhost;Database=outbox;Pooling=true;Min Pool Size=1;Max Pool Size=100";

		// Act
		var store = new SqlServerEventSourcedOutboxStore(connectionString, _logger);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_WithEncryptedConnection_CreatesInstance()
	{
		// Arrange - Connection string with encryption options
		var connectionString = "Server=localhost;Database=outbox;Encrypt=true;TrustServerCertificate=true";

		// Act
		var store = new SqlServerEventSourcedOutboxStore(connectionString, _logger);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_WithLocalDbInstance_CreatesInstance()
	{
		// Arrange - LocalDB connection string
		var connectionString = "Server=(localdb)\\MSSQLLocalDB;Database=outbox;Integrated Security=true";

		// Act
		var store = new SqlServerEventSourcedOutboxStore(connectionString, _logger);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	#endregion Connection String Format Tests

	#region Interface Implementation Tests

	[Fact]
	public void Store_ImplementsIEventSourcedOutboxStore()
	{
		// Arrange
		var store = new SqlServerEventSourcedOutboxStore("Server=localhost;Database=TestDb", _logger);

		// Assert
		_ = store.ShouldBeAssignableTo<IEventSourcedOutboxStore>();
	}

	#endregion Interface Implementation Tests
}
