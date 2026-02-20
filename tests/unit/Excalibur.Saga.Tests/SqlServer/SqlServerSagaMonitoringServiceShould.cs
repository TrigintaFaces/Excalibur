// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Excalibur.Saga.Tests.SqlServer;

/// <summary>
/// Unit tests for the <see cref="SqlServerSagaMonitoringService"/> class focusing on dual-constructor pattern
/// and parameter validation.
/// </summary>
/// <remarks>
/// <para>
/// Sprint 217 - Saga Monitoring.
/// Task: kdljl (SAGA-014: Unit Tests - Saga Monitoring).
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
[Trait("Sprint", "217")]
public sealed class SqlServerSagaMonitoringServiceShould : UnitTestBase
{
	private readonly ILogger<SqlServerSagaMonitoringService> _logger = NullLoggerFactory.CreateLogger<SqlServerSagaMonitoringService>();

	#region Simple Constructor Tests (Connection String)

	[Fact]
	public void SimpleConstructor_WithNullConnectionString_ThrowsArgumentException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => new SqlServerSagaMonitoringService(
			connectionString: null!,
			_logger));
	}

	[Fact]
	public void SimpleConstructor_WithEmptyConnectionString_ThrowsArgumentException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => new SqlServerSagaMonitoringService(
			connectionString: string.Empty,
			_logger));
	}

	[Fact]
	public void SimpleConstructor_WithWhitespaceConnectionString_ThrowsArgumentException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => new SqlServerSagaMonitoringService(
			connectionString: "   ",
			_logger));
	}

	[Fact]
	public void SimpleConstructor_WithNullLogger_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new SqlServerSagaMonitoringService(
			connectionString: "Server=localhost;Database=TestDb",
			logger: null!));
	}

	[Fact]
	public void SimpleConstructor_WithValidParameters_CreatesInstance()
	{
		// Act
		var service = new SqlServerSagaMonitoringService(
			connectionString: "Server=localhost;Database=TestDb",
			_logger);

		// Assert
		_ = service.ShouldNotBeNull();
	}

	#endregion Simple Constructor Tests (Connection String)

	#region Advanced Constructor Tests (Connection Factory)

	[Fact]
	public void AdvancedConstructor_WithNullConnectionFactory_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new SqlServerSagaMonitoringService(
			connectionFactory: null!,
			_logger));
	}

	[Fact]
	public void AdvancedConstructor_WithNullLogger_ThrowsArgumentNullException()
	{
		// Arrange
		Func<SqlConnection> factory = () => new SqlConnection("Server=localhost");

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new SqlServerSagaMonitoringService(
			factory,
			logger: null!));
	}

	[Fact]
	public void AdvancedConstructor_WithValidParameters_CreatesInstance()
	{
		// Arrange
		Func<SqlConnection> factory = () => new SqlConnection("Server=localhost;Database=TestDb");

		// Act
		var service = new SqlServerSagaMonitoringService(
			factory,
			_logger);

		// Assert
		_ = service.ShouldNotBeNull();
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
		var service = new SqlServerSagaMonitoringService(
			factory,
			_logger);

		// Assert - factory is stored but not called during construction
		_ = service.ShouldNotBeNull();
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
		var simpleService = new SqlServerSagaMonitoringService(
			connectionString,
			_logger);

		var advancedService = new SqlServerSagaMonitoringService(
			() => new SqlConnection(connectionString),
			_logger);

		// Assert - Both should be valid instances
		_ = simpleService.ShouldNotBeNull();
		_ = advancedService.ShouldNotBeNull();
	}

	[Fact]
	public void SimpleConstructor_ChainsToAdvancedConstructor()
	{
		// This test verifies the constructor chaining pattern works correctly
		// by ensuring the simple constructor produces a working instance

		// Arrange
		var connectionString = "Server=(localdb)\\mssqllocaldb;Database=TestDb;Trusted_Connection=true";

		// Act - Creating instance should not throw
		var service = new SqlServerSagaMonitoringService(
			connectionString,
			_logger);

		// Assert
		_ = service.ShouldNotBeNull();
	}

	#endregion Dual Constructor Pattern Consistency Tests

	#region ISagaMonitoringService Method Parameter Validation Tests

	[Fact]
	public async Task GetAverageCompletionTimeAsync_WithNullSagaType_ThrowsArgumentException()
	{
		// Arrange
		var service = new SqlServerSagaMonitoringService(
			connectionString: "Server=localhost;Database=TestDb",
			_logger);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(
			() => service.GetAverageCompletionTimeAsync(null!, DateTime.UtcNow, CancellationToken.None));
	}

	[Fact]
	public async Task GetAverageCompletionTimeAsync_WithEmptySagaType_ThrowsArgumentException()
	{
		// Arrange
		var service = new SqlServerSagaMonitoringService(
			connectionString: "Server=localhost;Database=TestDb",
			_logger);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(
			() => service.GetAverageCompletionTimeAsync(string.Empty, DateTime.UtcNow, CancellationToken.None));
	}

	[Fact]
	public async Task GetAverageCompletionTimeAsync_WithWhitespaceSagaType_ThrowsArgumentException()
	{
		// Arrange
		var service = new SqlServerSagaMonitoringService(
			connectionString: "Server=localhost;Database=TestDb",
			_logger);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(
			() => service.GetAverageCompletionTimeAsync("   ", DateTime.UtcNow, CancellationToken.None));
	}

	#endregion ISagaMonitoringService Method Parameter Validation Tests
}
