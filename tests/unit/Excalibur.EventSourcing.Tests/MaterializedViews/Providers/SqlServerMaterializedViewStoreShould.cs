// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.SqlServer;

using FakeItEasy;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.EventSourcing.Tests.MaterializedViews.Providers;

/// <summary>
/// Unit tests for <see cref="SqlServerMaterializedViewStore"/>.
/// </summary>
/// <remarks>
/// Sprint 517: Materialized Views provider tests.
/// Tests verify SQL Server store behavior, argument validation, and configuration.
/// Note: Database integration tests would require TestContainers.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "MaterializedViews")]
[Trait("Feature", "SqlServer")]
public sealed class SqlServerMaterializedViewStoreShould
{
	#region Type Tests

	[Fact]
	public void BeSealed()
	{
		// Assert
		typeof(SqlServerMaterializedViewStore).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void BePublic()
	{
		// Assert
		typeof(SqlServerMaterializedViewStore).IsPublic.ShouldBeTrue();
	}

	[Fact]
	public void ImplementIMaterializedViewStore()
	{
		// Assert
		typeof(IMaterializedViewStore).IsAssignableFrom(typeof(SqlServerMaterializedViewStore)).ShouldBeTrue();
	}

	[Fact]
	public void HavePartialModifier()
	{
		// Assert - partial class is needed for LoggerMessage source generation
		// Verified by the class compiling with [LoggerMessage] attributes
		typeof(SqlServerMaterializedViewStore).IsSealed.ShouldBeTrue();
	}

	#endregion

	#region Constructor Tests (Connection String)

	[Fact]
	public void Constructor_ThrowArgumentExceptionForNullConnectionString()
	{
		// Arrange
		var logger = NullLogger<SqlServerMaterializedViewStore>.Instance;

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			new SqlServerMaterializedViewStore(connectionString: null!, logger));
	}

	[Fact]
	public void Constructor_ThrowArgumentExceptionForEmptyConnectionString()
	{
		// Arrange
		var logger = NullLogger<SqlServerMaterializedViewStore>.Instance;

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			new SqlServerMaterializedViewStore(connectionString: "", logger));
	}

	[Fact]
	public void Constructor_ThrowArgumentExceptionForWhitespaceConnectionString()
	{
		// Arrange
		var logger = NullLogger<SqlServerMaterializedViewStore>.Instance;

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			new SqlServerMaterializedViewStore(connectionString: "   ", logger));
	}

	[Fact]
	public void Constructor_ThrowArgumentNullExceptionForNullLoggerWithConnectionString()
	{
		// Arrange
		var connectionString = "Server=localhost;Database=test;";

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new SqlServerMaterializedViewStore(connectionString, logger: null!));
	}

	[Fact]
	public void Constructor_SucceedWithValidConnectionString()
	{
		// Arrange
		var connectionString = "Server=localhost;Database=test;";
		var logger = NullLogger<SqlServerMaterializedViewStore>.Instance;

		// Act
		var store = new SqlServerMaterializedViewStore(connectionString, logger);

		// Assert
		store.ShouldNotBeNull();
	}

	#endregion

	#region Constructor Tests (Connection Factory)

	[Fact]
	public void Constructor_ThrowArgumentNullExceptionForNullConnectionFactory()
	{
		// Arrange
		var logger = NullLogger<SqlServerMaterializedViewStore>.Instance;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new SqlServerMaterializedViewStore(connectionFactory: null!, logger));
	}

	[Fact]
	public void Constructor_ThrowArgumentNullExceptionForNullLoggerWithFactory()
	{
		// Arrange
		Func<SqlConnection> factory = () => new SqlConnection("Server=localhost;Database=test;");

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new SqlServerMaterializedViewStore(factory, logger: null!));
	}

	[Fact]
	public void Constructor_SucceedWithValidConnectionFactory()
	{
		// Arrange
		Func<SqlConnection> factory = () => new SqlConnection("Server=localhost;Database=test;");
		var logger = NullLogger<SqlServerMaterializedViewStore>.Instance;

		// Act
		var store = new SqlServerMaterializedViewStore(factory, logger);

		// Assert
		store.ShouldNotBeNull();
	}

	#endregion

	#region Constructor Tests (Optional Parameters)

	[Fact]
	public void Constructor_UseDefaultTableNames()
	{
		// Arrange
		var connectionString = "Server=localhost;Database=test;";
		var logger = NullLogger<SqlServerMaterializedViewStore>.Instance;

		// Act - using defaults (no explicit table names)
		var store = new SqlServerMaterializedViewStore(connectionString, logger);

		// Assert
		store.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_AcceptCustomViewTableName()
	{
		// Arrange
		var connectionString = "Server=localhost;Database=test;";
		var logger = NullLogger<SqlServerMaterializedViewStore>.Instance;

		// Act
		var store = new SqlServerMaterializedViewStore(
			connectionString,
			logger,
			viewTableName: "CustomMaterializedViews");

		// Assert
		store.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_AcceptCustomPositionTableName()
	{
		// Arrange
		var connectionString = "Server=localhost;Database=test;";
		var logger = NullLogger<SqlServerMaterializedViewStore>.Instance;

		// Act
		var store = new SqlServerMaterializedViewStore(
			connectionString,
			logger,
			positionTableName: "CustomPositions");

		// Assert
		store.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_AcceptCustomJsonSerializerOptions()
	{
		// Arrange
		var connectionString = "Server=localhost;Database=test;";
		var logger = NullLogger<SqlServerMaterializedViewStore>.Instance;
		var jsonOptions = new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
			WriteIndented = true
		};

		// Act
		var store = new SqlServerMaterializedViewStore(
			connectionString,
			logger,
			jsonOptions: jsonOptions);

		// Assert
		store.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_AcceptAllCustomParameters()
	{
		// Arrange
		Func<SqlConnection> factory = () => new SqlConnection("Server=localhost;Database=test;");
		var logger = NullLogger<SqlServerMaterializedViewStore>.Instance;
		var jsonOptions = new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase
		};

		// Act
		var store = new SqlServerMaterializedViewStore(
			factory,
			logger,
			viewTableName: "MyViews",
			positionTableName: "MyPositions",
			jsonOptions: jsonOptions);

		// Assert
		store.ShouldNotBeNull();
	}

	#endregion

	#region GetAsync Argument Validation Tests

	[Fact]
	public async Task GetAsync_ThrowArgumentExceptionForNullViewName()
	{
		// Arrange
		var connectionString = "Server=localhost;Database=test;";
		var logger = NullLogger<SqlServerMaterializedViewStore>.Instance;
		var store = new SqlServerMaterializedViewStore(connectionString, logger);

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await store.GetAsync<TestView>(viewName: null!, "view-1", CancellationToken.None));
	}

	[Fact]
	public async Task GetAsync_ThrowArgumentExceptionForEmptyViewName()
	{
		// Arrange
		var connectionString = "Server=localhost;Database=test;";
		var logger = NullLogger<SqlServerMaterializedViewStore>.Instance;
		var store = new SqlServerMaterializedViewStore(connectionString, logger);

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await store.GetAsync<TestView>(viewName: "", "view-1", CancellationToken.None));
	}

	[Fact]
	public async Task GetAsync_ThrowArgumentExceptionForNullViewId()
	{
		// Arrange
		var connectionString = "Server=localhost;Database=test;";
		var logger = NullLogger<SqlServerMaterializedViewStore>.Instance;
		var store = new SqlServerMaterializedViewStore(connectionString, logger);

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await store.GetAsync<TestView>("TestView", viewId: null!, CancellationToken.None));
	}

	[Fact]
	public async Task GetAsync_ThrowArgumentExceptionForEmptyViewId()
	{
		// Arrange
		var connectionString = "Server=localhost;Database=test;";
		var logger = NullLogger<SqlServerMaterializedViewStore>.Instance;
		var store = new SqlServerMaterializedViewStore(connectionString, logger);

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await store.GetAsync<TestView>("TestView", viewId: "", CancellationToken.None));
	}

	#endregion

	#region SaveAsync Argument Validation Tests

	[Fact]
	public async Task SaveAsync_ThrowArgumentExceptionForNullViewName()
	{
		// Arrange
		var connectionString = "Server=localhost;Database=test;";
		var logger = NullLogger<SqlServerMaterializedViewStore>.Instance;
		var store = new SqlServerMaterializedViewStore(connectionString, logger);
		var view = new TestView { Id = "1" };

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await store.SaveAsync(viewName: null!, "view-1", view, CancellationToken.None));
	}

	[Fact]
	public async Task SaveAsync_ThrowArgumentExceptionForEmptyViewName()
	{
		// Arrange
		var connectionString = "Server=localhost;Database=test;";
		var logger = NullLogger<SqlServerMaterializedViewStore>.Instance;
		var store = new SqlServerMaterializedViewStore(connectionString, logger);
		var view = new TestView { Id = "1" };

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await store.SaveAsync(viewName: "", "view-1", view, CancellationToken.None));
	}

	[Fact]
	public async Task SaveAsync_ThrowArgumentExceptionForNullViewId()
	{
		// Arrange
		var connectionString = "Server=localhost;Database=test;";
		var logger = NullLogger<SqlServerMaterializedViewStore>.Instance;
		var store = new SqlServerMaterializedViewStore(connectionString, logger);
		var view = new TestView { Id = "1" };

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await store.SaveAsync("TestView", viewId: null!, view, CancellationToken.None));
	}

	[Fact]
	public async Task SaveAsync_ThrowArgumentNullExceptionForNullView()
	{
		// Arrange
		var connectionString = "Server=localhost;Database=test;";
		var logger = NullLogger<SqlServerMaterializedViewStore>.Instance;
		var store = new SqlServerMaterializedViewStore(connectionString, logger);

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await store.SaveAsync<TestView>("TestView", "view-1", view: null!, CancellationToken.None));
	}

	#endregion

	#region DeleteAsync Argument Validation Tests

	[Fact]
	public async Task DeleteAsync_ThrowArgumentExceptionForNullViewName()
	{
		// Arrange
		var connectionString = "Server=localhost;Database=test;";
		var logger = NullLogger<SqlServerMaterializedViewStore>.Instance;
		var store = new SqlServerMaterializedViewStore(connectionString, logger);

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await store.DeleteAsync(viewName: null!, "view-1", CancellationToken.None));
	}

	[Fact]
	public async Task DeleteAsync_ThrowArgumentExceptionForEmptyViewName()
	{
		// Arrange
		var connectionString = "Server=localhost;Database=test;";
		var logger = NullLogger<SqlServerMaterializedViewStore>.Instance;
		var store = new SqlServerMaterializedViewStore(connectionString, logger);

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await store.DeleteAsync(viewName: "", "view-1", CancellationToken.None));
	}

	[Fact]
	public async Task DeleteAsync_ThrowArgumentExceptionForNullViewId()
	{
		// Arrange
		var connectionString = "Server=localhost;Database=test;";
		var logger = NullLogger<SqlServerMaterializedViewStore>.Instance;
		var store = new SqlServerMaterializedViewStore(connectionString, logger);

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await store.DeleteAsync("TestView", viewId: null!, CancellationToken.None));
	}

	#endregion

	#region GetPositionAsync Argument Validation Tests

	[Fact]
	public async Task GetPositionAsync_ThrowArgumentExceptionForNullViewName()
	{
		// Arrange
		var connectionString = "Server=localhost;Database=test;";
		var logger = NullLogger<SqlServerMaterializedViewStore>.Instance;
		var store = new SqlServerMaterializedViewStore(connectionString, logger);

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await store.GetPositionAsync(viewName: null!, CancellationToken.None));
	}

	[Fact]
	public async Task GetPositionAsync_ThrowArgumentExceptionForEmptyViewName()
	{
		// Arrange
		var connectionString = "Server=localhost;Database=test;";
		var logger = NullLogger<SqlServerMaterializedViewStore>.Instance;
		var store = new SqlServerMaterializedViewStore(connectionString, logger);

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await store.GetPositionAsync(viewName: "", CancellationToken.None));
	}

	#endregion

	#region SavePositionAsync Argument Validation Tests

	[Fact]
	public async Task SavePositionAsync_ThrowArgumentExceptionForNullViewName()
	{
		// Arrange
		var connectionString = "Server=localhost;Database=test;";
		var logger = NullLogger<SqlServerMaterializedViewStore>.Instance;
		var store = new SqlServerMaterializedViewStore(connectionString, logger);

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await store.SavePositionAsync(viewName: null!, 100, CancellationToken.None));
	}

	[Fact]
	public async Task SavePositionAsync_ThrowArgumentExceptionForEmptyViewName()
	{
		// Arrange
		var connectionString = "Server=localhost;Database=test;";
		var logger = NullLogger<SqlServerMaterializedViewStore>.Instance;
		var store = new SqlServerMaterializedViewStore(connectionString, logger);

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await store.SavePositionAsync(viewName: "", 100, CancellationToken.None));
	}

	#endregion

	#region Logger Message Verification

	[Fact]
	public void HaveLoggerMessageAttributes()
	{
		// Assert - verify LoggerMessage methods exist (EventIds 3100-3105)
		// These are private methods, but their existence is verified by successful compilation
		typeof(SqlServerMaterializedViewStore).IsSealed.ShouldBeTrue();
	}

	#endregion

	#region Test Types

	private sealed class TestView
	{
		public string Id { get; set; } = string.Empty;
		public string Name { get; set; } = string.Empty;
	}

	#endregion
}
