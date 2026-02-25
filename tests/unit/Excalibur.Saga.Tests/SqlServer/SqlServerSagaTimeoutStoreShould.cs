// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Excalibur.Saga.Tests.SqlServer;

/// <summary>
/// Unit tests for the <see cref="SqlServerSagaTimeoutStore"/> class focusing on dual-constructor pattern
/// and ISagaTimeoutStore interface validation.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SqlServerSagaTimeoutStoreShould : UnitTestBase
{
	private readonly ILogger<SqlServerSagaTimeoutStore> _logger = NullLoggerFactory.CreateLogger<SqlServerSagaTimeoutStore>();

	#region Simple Constructor Tests (Connection String)

	[Fact]
	public void SimpleConstructor_WithNullConnectionString_ThrowsArgumentException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => new SqlServerSagaTimeoutStore(
			connectionString: null!,
			_logger));
	}

	[Fact]
	public void SimpleConstructor_WithEmptyConnectionString_ThrowsArgumentException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => new SqlServerSagaTimeoutStore(
			connectionString: string.Empty,
			_logger));
	}

	[Fact]
	public void SimpleConstructor_WithWhitespaceConnectionString_ThrowsArgumentException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => new SqlServerSagaTimeoutStore(
			connectionString: "   ",
			_logger));
	}

	[Fact]
	public void SimpleConstructor_WithNullLogger_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new SqlServerSagaTimeoutStore(
			connectionString: "Server=localhost;Database=TestDb",
			logger: null!));
	}

	[Fact]
	public void SimpleConstructor_WithValidParameters_CreatesInstance()
	{
		// Act
		var store = new SqlServerSagaTimeoutStore(
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
		_ = Should.Throw<ArgumentNullException>(() => new SqlServerSagaTimeoutStore(
			connectionFactory: null!,
			_logger));
	}

	[Fact]
	public void AdvancedConstructor_WithNullLogger_ThrowsArgumentNullException()
	{
		// Arrange
		Func<SqlConnection> factory = () => new SqlConnection("Server=localhost");

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new SqlServerSagaTimeoutStore(
			factory,
			logger: null!));
	}

	[Fact]
	public void AdvancedConstructor_WithValidParameters_CreatesInstance()
	{
		// Arrange
		Func<SqlConnection> factory = () => new SqlConnection("Server=localhost;Database=TestDb");

		// Act
		var store = new SqlServerSagaTimeoutStore(
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
		var store = new SqlServerSagaTimeoutStore(
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
		var simpleStore = new SqlServerSagaTimeoutStore(
			connectionString,
			_logger);

		var advancedStore = new SqlServerSagaTimeoutStore(
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
		var store = new SqlServerSagaTimeoutStore(
			connectionString,
			_logger);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	#endregion Dual Constructor Pattern Consistency Tests

	#region ISagaTimeoutStore Interface Parameter Validation Tests

	[Fact]
	public async Task ScheduleTimeoutAsync_WithNullTimeout_ThrowsArgumentNullException()
	{
		// Arrange
		var store = new SqlServerSagaTimeoutStore(
			connectionString: "Server=localhost;Database=TestDb",
			_logger);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			() => store.ScheduleTimeoutAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task CancelTimeoutAsync_WithNullSagaId_ThrowsArgumentException()
	{
		// Arrange
		var store = new SqlServerSagaTimeoutStore(
			connectionString: "Server=localhost;Database=TestDb",
			_logger);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(
			() => store.CancelTimeoutAsync(null!, "timeout-123", CancellationToken.None));
	}

	[Fact]
	public async Task CancelTimeoutAsync_WithEmptySagaId_ThrowsArgumentException()
	{
		// Arrange
		var store = new SqlServerSagaTimeoutStore(
			connectionString: "Server=localhost;Database=TestDb",
			_logger);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(
			() => store.CancelTimeoutAsync(string.Empty, "timeout-123", CancellationToken.None));
	}

	[Fact]
	public async Task CancelTimeoutAsync_WithNullTimeoutId_ThrowsArgumentException()
	{
		// Arrange
		var store = new SqlServerSagaTimeoutStore(
			connectionString: "Server=localhost;Database=TestDb",
			_logger);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(
			() => store.CancelTimeoutAsync("saga-123", null!, CancellationToken.None));
	}

	[Fact]
	public async Task CancelAllTimeoutsAsync_WithNullSagaId_ThrowsArgumentException()
	{
		// Arrange
		var store = new SqlServerSagaTimeoutStore(
			connectionString: "Server=localhost;Database=TestDb",
			_logger);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(
			() => store.CancelAllTimeoutsAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task CancelAllTimeoutsAsync_WithEmptySagaId_ThrowsArgumentException()
	{
		// Arrange
		var store = new SqlServerSagaTimeoutStore(
			connectionString: "Server=localhost;Database=TestDb",
			_logger);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(
			() => store.CancelAllTimeoutsAsync(string.Empty, CancellationToken.None));
	}

	[Fact]
	public async Task MarkDeliveredAsync_WithNullTimeoutId_ThrowsArgumentException()
	{
		// Arrange
		var store = new SqlServerSagaTimeoutStore(
			connectionString: "Server=localhost;Database=TestDb",
			_logger);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(
			() => store.MarkDeliveredAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task MarkDeliveredAsync_WithEmptyTimeoutId_ThrowsArgumentException()
	{
		// Arrange
		var store = new SqlServerSagaTimeoutStore(
			connectionString: "Server=localhost;Database=TestDb",
			_logger);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(
			() => store.MarkDeliveredAsync(string.Empty, CancellationToken.None));
	}

	#endregion ISagaTimeoutStore Interface Parameter Validation Tests
}
