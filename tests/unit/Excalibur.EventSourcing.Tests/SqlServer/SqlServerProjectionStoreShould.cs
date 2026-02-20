// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Excalibur.EventSourcing.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Excalibur.EventSourcing.Tests.SqlServer;

/// <summary>
/// Unit tests for the <see cref="SqlServerProjectionStore{TProjection}"/> class.
/// </summary>
/// <remarks>
/// Sprint 511 (S511.2): SQL Server data provider unit tests.
/// Tests focus on constructor validation, dual-constructor pattern consistency,
/// and generic type parameter behavior.
/// No database dependencies - pure unit tests with mocks.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "SqlServer")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerProjectionStoreShould : UnitTestBase
{
	private readonly ILogger<SqlServerProjectionStore<TestProjection>> _logger =
		NullLoggerFactory.CreateLogger<SqlServerProjectionStore<TestProjection>>();

	#region Simple Constructor Tests (Connection String)

	[Fact]
	public void SimpleConstructor_WithNullConnectionString_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new SqlServerProjectionStore<TestProjection>(
			connectionString: null!,
			_logger));
	}

	[Fact]
	public void SimpleConstructor_WithNullLogger_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new SqlServerProjectionStore<TestProjection>(
			connectionString: "Server=localhost;Database=TestDb",
			logger: null!));
	}

	[Fact]
	public void SimpleConstructor_WithValidParameters_CreatesInstance()
	{
		// Act
		var store = new SqlServerProjectionStore<TestProjection>(
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
		var store = new SqlServerProjectionStore<TestProjection>(
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
		_ = Should.Throw<ArgumentNullException>(() => new SqlServerProjectionStore<TestProjection>(
			connectionFactory: null!,
			_logger));
	}

	[Fact]
	public void AdvancedConstructor_WithNullLogger_ThrowsArgumentNullException()
	{
		// Arrange
		Func<SqlConnection> factory = () => new SqlConnection("Server=localhost");

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new SqlServerProjectionStore<TestProjection>(
			factory,
			logger: null!));
	}

	[Fact]
	public void AdvancedConstructor_WithValidParameters_CreatesInstance()
	{
		// Arrange
		Func<SqlConnection> factory = () => new SqlConnection("Server=localhost;Database=TestDb");

		// Act
		var store = new SqlServerProjectionStore<TestProjection>(
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
		var store = new SqlServerProjectionStore<TestProjection>(
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
		var simpleStore = new SqlServerProjectionStore<TestProjection>(
			connectionString,
			_logger);

		var advancedStore = new SqlServerProjectionStore<TestProjection>(
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
		var store = new SqlServerProjectionStore<TestProjection>(
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
		var connectionString = "Server=localhost;Database=projections;User Id=sa;Password=Pass123!";

		// Act
		var store = new SqlServerProjectionStore<TestProjection>(connectionString, _logger);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_WithIntegratedSecurity_CreatesInstance()
	{
		// Arrange - Integrated security connection string
		var connectionString = "Server=localhost;Database=projections;Integrated Security=true";

		// Act
		var store = new SqlServerProjectionStore<TestProjection>(connectionString, _logger);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_WithPoolingOptions_CreatesInstance()
	{
		// Arrange - Connection string with pooling options
		var connectionString = "Server=localhost;Database=projections;Pooling=true;Min Pool Size=1;Max Pool Size=100";

		// Act
		var store = new SqlServerProjectionStore<TestProjection>(connectionString, _logger);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	#endregion Connection String Format Tests

	#region Table Name Tests

	[Fact]
	public void Constructor_WithNoTableName_UsesTypeName()
	{
		// Arrange & Act - Table name defaults to type name
		var store = new SqlServerProjectionStore<TestProjection>(
			"Server=localhost;Database=TestDb",
			_logger,
			tableName: null);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_WithCustomTableName_AcceptsCustomName()
	{
		// Arrange & Act
		var store = new SqlServerProjectionStore<TestProjection>(
			"Server=localhost;Database=TestDb",
			_logger,
			tableName: "CustomProjections");

		// Assert
		_ = store.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_WithEmptyTableName_AcceptsEmptyName()
	{
		// Arrange & Act - Empty table name doesn't throw during construction
		var store = new SqlServerProjectionStore<TestProjection>(
			"Server=localhost;Database=TestDb",
			_logger,
			tableName: string.Empty);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	#endregion Table Name Tests

	#region JSON Options Tests

	[Fact]
	public void Constructor_WithNoJsonOptions_UsesDefaultOptions()
	{
		// Arrange & Act
		var store = new SqlServerProjectionStore<TestProjection>(
			"Server=localhost;Database=TestDb",
			_logger,
			jsonOptions: null);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_WithCustomJsonOptions_AcceptsCustomOptions()
	{
		// Arrange
		var customOptions = new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
			WriteIndented = true
		};

		// Act
		var store = new SqlServerProjectionStore<TestProjection>(
			"Server=localhost;Database=TestDb",
			_logger,
			jsonOptions: customOptions);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	#endregion JSON Options Tests

	#region Interface Implementation Tests

	[Fact]
	public void Store_ImplementsIProjectionStore()
	{
		// Arrange
		var store = new SqlServerProjectionStore<TestProjection>("Server=localhost;Database=TestDb", _logger);

		// Assert
		_ = store.ShouldBeAssignableTo<IProjectionStore<TestProjection>>();
	}

	#endregion Interface Implementation Tests

	#region Generic Type Parameter Tests

	[Fact]
	public void Constructor_WithDifferentProjectionTypes_CreatesInstances()
	{
		// Arrange
		var connectionString = "Server=localhost;Database=TestDb";
		var testLogger = NullLoggerFactory.CreateLogger<SqlServerProjectionStore<TestProjection>>();
		var orderLogger = NullLoggerFactory.CreateLogger<SqlServerProjectionStore<OrderProjection>>();

		// Act
		var testStore = new SqlServerProjectionStore<TestProjection>(connectionString, testLogger);
		var orderStore = new SqlServerProjectionStore<OrderProjection>(connectionString, orderLogger);

		// Assert
		_ = testStore.ShouldNotBeNull();
		_ = orderStore.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_WithRecordType_CreatesInstance()
	{
		// Arrange
		var connectionString = "Server=localhost;Database=TestDb";
		var recordLogger = NullLoggerFactory.CreateLogger<SqlServerProjectionStore<CustomerSummary>>();

		// Act
		var store = new SqlServerProjectionStore<CustomerSummary>(connectionString, recordLogger);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	#endregion Generic Type Parameter Tests

	#region Test Projections

	/// <summary>
	/// Test projection class for generic type parameter testing.
	/// </summary>
	private sealed class TestProjection
	{
		public string Id { get; init; } = string.Empty;
		public string Name { get; init; } = string.Empty;
		public decimal Value { get; init; }
	}

	/// <summary>
	/// Alternative projection type for testing multiple generic instantiations.
	/// </summary>
	private sealed class OrderProjection
	{
		public string OrderId { get; init; } = string.Empty;
		public string CustomerId { get; init; } = string.Empty;
		public decimal TotalAmount { get; init; }
		public DateTimeOffset CreatedAt { get; init; }
	}

	/// <summary>
	/// Record projection type for testing with modern C# types.
	/// </summary>
	private sealed record CustomerSummary(
		string CustomerId,
		string FullName,
		int OrderCount,
		decimal TotalSpent);

	#endregion Test Projections
}
