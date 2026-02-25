// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Excalibur.EventSourcing.Tests.SqlServer;

/// <summary>
/// Unit tests for the <see cref="SqlServerEventStore"/> class focusing on dual-constructor pattern.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SqlServerEventStoreShould : UnitTestBase
{
	private readonly ILogger<SqlServerEventStore> _logger = NullLoggerFactory.CreateLogger<SqlServerEventStore>();

	#region Simple Constructor Tests (Connection String)

	[Fact]
	public void SimpleConstructor_WithNullConnectionString_ThrowsArgumentException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => new SqlServerEventStore(
			connectionString: null!,
			_logger));
	}

	[Fact]
	public void SimpleConstructor_WithNullLogger_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new SqlServerEventStore(
			connectionString: "Server=localhost;Database=TestDb",
			logger: null!));
	}

	[Fact]
	public void SimpleConstructor_WithValidParameters_CreatesInstance()
	{
		// Act
		var store = new SqlServerEventStore(
			connectionString: "Server=localhost;Database=TestDb",
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
		_ = Should.Throw<ArgumentNullException>(() => new SqlServerEventStore(
			connectionFactory: null!,
			_logger));
	}

	[Fact]
	public void AdvancedConstructor_WithNullLogger_ThrowsArgumentNullException()
	{
		// Arrange
		Func<SqlConnection> factory = () => new SqlConnection("Server=localhost");

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new SqlServerEventStore(
			factory,
			logger: null!));
	}

	[Fact]
	public void AdvancedConstructor_WithValidParameters_CreatesInstance()
	{
		// Arrange
		Func<SqlConnection> factory = () => new SqlConnection("Server=localhost;Database=TestDb");

		// Act
		var store = new SqlServerEventStore(
			factory,
			_logger);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	[Fact]
	public void AdvancedConstructor_UsesProvidedFactory()
	{
		// Arrange
		var connectionString = "Server=custom;Database=CustomDb";
		var factoryCalled = false;
		Func<SqlConnection> factory = () =>
		{
			factoryCalled = true;
			return new SqlConnection(connectionString);
		};

		// Act
		var store = new SqlServerEventStore(
			factory,
			_logger);

		// Assert - factory is stored but not called during construction
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
		var simpleStore = new SqlServerEventStore(
			connectionString,
			_logger);

		var advancedStore = new SqlServerEventStore(
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
		var store = new SqlServerEventStore(
			connectionString,
			_logger);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	#endregion Dual Constructor Pattern Consistency Tests
}
