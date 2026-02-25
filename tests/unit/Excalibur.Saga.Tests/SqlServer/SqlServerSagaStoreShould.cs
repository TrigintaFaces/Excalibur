// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Serialization;

using Excalibur.Saga.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Excalibur.Saga.Tests.SqlServer;

/// <summary>
/// Unit tests for the <see cref="SqlServerSagaStore"/> class focusing on dual-constructor pattern.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SqlServerSagaStoreShould : UnitTestBase
{
	private readonly ILogger<SqlServerSagaStore> _logger = NullLoggerFactory.CreateLogger<SqlServerSagaStore>();
	private readonly IJsonSerializer _serializer = A.Fake<IJsonSerializer>();

	#region Simple Constructor Tests (Connection String)

	[Fact]
	public void SimpleConstructor_WithNullConnectionString_ThrowsArgumentException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => new SqlServerSagaStore(
			connectionString: null!,
			_logger,
			_serializer));
	}

	[Fact]
	public void SimpleConstructor_WithEmptyConnectionString_ThrowsArgumentException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => new SqlServerSagaStore(
			connectionString: string.Empty,
			_logger,
			_serializer));
	}

	[Fact]
	public void SimpleConstructor_WithWhitespaceConnectionString_ThrowsArgumentException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => new SqlServerSagaStore(
			connectionString: "   ",
			_logger,
			_serializer));
	}

	[Fact]
	public void SimpleConstructor_WithNullLogger_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new SqlServerSagaStore(
			connectionString: "Server=localhost;Database=TestDb",
			logger: null!,
			_serializer));
	}

	[Fact]
	public void SimpleConstructor_WithNullSerializer_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new SqlServerSagaStore(
			connectionString: "Server=localhost;Database=TestDb",
			_logger,
			serializer: null!));
	}

	[Fact]
	public void SimpleConstructor_WithValidParameters_CreatesInstance()
	{
		// Act
		var store = new SqlServerSagaStore(
			connectionString: "Server=localhost;Database=TestDb",
			_logger,
			_serializer);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	#endregion Simple Constructor Tests (Connection String)

	#region Advanced Constructor Tests (Connection Factory)

	[Fact]
	public void AdvancedConstructor_WithNullConnectionFactory_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new SqlServerSagaStore(
			connectionFactory: null!,
			_logger,
			_serializer));
	}

	[Fact]
	public void AdvancedConstructor_WithNullLogger_ThrowsArgumentNullException()
	{
		// Arrange
		Func<SqlConnection> factory = () => new SqlConnection("Server=localhost");

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new SqlServerSagaStore(
			factory,
			logger: null!,
			_serializer));
	}

	[Fact]
	public void AdvancedConstructor_WithNullSerializer_ThrowsArgumentNullException()
	{
		// Arrange
		Func<SqlConnection> factory = () => new SqlConnection("Server=localhost");

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new SqlServerSagaStore(
			factory,
			_logger,
			serializer: null!));
	}

	[Fact]
	public void AdvancedConstructor_WithValidParameters_CreatesInstance()
	{
		// Arrange
		Func<SqlConnection> factory = () => new SqlConnection("Server=localhost;Database=TestDb");

		// Act
		var store = new SqlServerSagaStore(
			factory,
			_logger,
			_serializer);

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
		var store = new SqlServerSagaStore(
			factory,
			_logger,
			_serializer);

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
		var simpleStore = new SqlServerSagaStore(
			connectionString,
			_logger,
			_serializer);

		var advancedStore = new SqlServerSagaStore(
			() => new SqlConnection(connectionString),
			_logger,
			_serializer);

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
		var store = new SqlServerSagaStore(
			connectionString,
			_logger,
			_serializer);

		// Assert
		_ = store.ShouldNotBeNull();
	}

	#endregion Dual Constructor Pattern Consistency Tests

	#region ISagaStore Interface Implementation Tests

	[Fact]
	public async Task SaveAsync_WithNullSagaState_ThrowsArgumentNullException()
	{
		// Arrange
		var store = new SqlServerSagaStore(
			connectionString: "Server=localhost;Database=TestDb",
			_logger,
			_serializer);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			() => store.SaveAsync<TestSagaState>(null!, CancellationToken.None));
	}

	#endregion ISagaStore Interface Implementation Tests

	/// <summary>
	/// Test saga state for unit testing.
	/// </summary>
	private sealed class TestSagaState : Dispatch.Abstractions.Messaging.SagaState
	{
		public string Data { get; set; } = string.Empty;
	}
}
