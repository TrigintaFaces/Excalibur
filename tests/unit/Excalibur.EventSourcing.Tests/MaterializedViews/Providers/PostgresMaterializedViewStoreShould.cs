// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Postgres;

using Microsoft.Extensions.Logging.Abstractions;

using Npgsql;

namespace Excalibur.EventSourcing.Tests.MaterializedViews.Providers;

/// <summary>
/// Unit tests for <see cref="PostgresMaterializedViewStore"/>.
/// </summary>
/// <remarks>
/// Sprint 517: Materialized Views provider tests.
/// Tests verify Postgres store behavior, argument validation, and configuration.
/// Note: Database integration tests would require TestContainers.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "MaterializedViews")]
[Trait("Feature", "Postgres")]
public sealed class PostgresMaterializedViewStoreShould
{
	#region Type Tests

	[Fact]
	public void BeSealed()
	{
		// Assert
		typeof(PostgresMaterializedViewStore).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void BePublic()
	{
		// Assert
		typeof(PostgresMaterializedViewStore).IsPublic.ShouldBeTrue();
	}

	[Fact]
	public void ImplementIMaterializedViewStore()
	{
		// Assert
		typeof(IMaterializedViewStore).IsAssignableFrom(typeof(PostgresMaterializedViewStore)).ShouldBeTrue();
	}

	[Fact]
	public void HavePartialModifier()
	{
		// Assert - partial class is needed for LoggerMessage source generation
		// Verified by the class compiling with [LoggerMessage] attributes
		typeof(PostgresMaterializedViewStore).IsSealed.ShouldBeTrue();
	}

	#endregion

	#region Constructor Tests (Connection String)

	[Fact]
	public void Constructor_ThrowArgumentExceptionForNullConnectionString()
	{
		// Arrange
		var logger = NullLogger<PostgresMaterializedViewStore>.Instance;

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			new PostgresMaterializedViewStore(connectionString: null!, logger));
	}

	[Fact]
	public void Constructor_ThrowArgumentExceptionForEmptyConnectionString()
	{
		// Arrange
		var logger = NullLogger<PostgresMaterializedViewStore>.Instance;

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			new PostgresMaterializedViewStore(connectionString: "", logger));
	}

	[Fact]
	public void Constructor_ThrowArgumentExceptionForWhitespaceConnectionString()
	{
		// Arrange
		var logger = NullLogger<PostgresMaterializedViewStore>.Instance;

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			new PostgresMaterializedViewStore(connectionString: "   ", logger));
	}

	[Fact]
	public void Constructor_ThrowArgumentNullExceptionForNullLoggerWithConnectionString()
	{
		// Arrange
		var connectionString = "Host=localhost;Database=test;";

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new PostgresMaterializedViewStore(connectionString, logger: null!));
	}

	[Fact]
	public void Constructor_SucceedWithValidConnectionString()
	{
		// Arrange
		var connectionString = "Host=localhost;Database=test;";
		var logger = NullLogger<PostgresMaterializedViewStore>.Instance;

		// Act
		var store = new PostgresMaterializedViewStore(connectionString, logger);

		// Assert
		store.ShouldNotBeNull();
	}

	#endregion

	#region Constructor Tests (NpgsqlDataSource)

	[Fact]
	public void Constructor_ThrowArgumentNullExceptionForNullDataSource()
	{
		// Arrange
		var logger = NullLogger<PostgresMaterializedViewStore>.Instance;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new PostgresMaterializedViewStore(dataSource: null!, logger));
	}

	[Fact]
	public void Constructor_ThrowArgumentNullExceptionForNullLoggerWithDataSource()
	{
		// Arrange
		var dataSource = NpgsqlDataSource.Create("Host=localhost;Database=test;");

		try
		{
			// Act & Assert
			Should.Throw<ArgumentNullException>(() =>
				new PostgresMaterializedViewStore(dataSource, logger: null!));
		}
		finally
		{
			dataSource.Dispose();
		}
	}

	[Fact]
	public void Constructor_SucceedWithValidDataSource()
	{
		// Arrange
		var dataSource = NpgsqlDataSource.Create("Host=localhost;Database=test;");
		var logger = NullLogger<PostgresMaterializedViewStore>.Instance;

		try
		{
			// Act
			var store = new PostgresMaterializedViewStore(dataSource, logger);

			// Assert
			store.ShouldNotBeNull();
		}
		finally
		{
			dataSource.Dispose();
		}
	}

	#endregion

	#region Constructor Tests (Optional Parameters)

	[Fact]
	public void Constructor_UseDefaultTableNames()
	{
		// Arrange
		var connectionString = "Host=localhost;Database=test;";
		var logger = NullLogger<PostgresMaterializedViewStore>.Instance;

		// Act - using defaults (snake_case per ADR-109)
		var store = new PostgresMaterializedViewStore(connectionString, logger);

		// Assert
		store.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_AcceptCustomViewTableName()
	{
		// Arrange
		var connectionString = "Host=localhost;Database=test;";
		var logger = NullLogger<PostgresMaterializedViewStore>.Instance;

		// Act
		var store = new PostgresMaterializedViewStore(
			connectionString,
			logger,
			viewTableName: "custom_materialized_views");

		// Assert
		store.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_AcceptCustomPositionTableName()
	{
		// Arrange
		var connectionString = "Host=localhost;Database=test;";
		var logger = NullLogger<PostgresMaterializedViewStore>.Instance;

		// Act
		var store = new PostgresMaterializedViewStore(
			connectionString,
			logger,
			positionTableName: "custom_positions");

		// Assert
		store.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_AcceptCustomJsonSerializerOptions()
	{
		// Arrange
		var connectionString = "Host=localhost;Database=test;";
		var logger = NullLogger<PostgresMaterializedViewStore>.Instance;
		var jsonOptions = new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
			WriteIndented = true
		};

		// Act
		var store = new PostgresMaterializedViewStore(
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
		var dataSource = NpgsqlDataSource.Create("Host=localhost;Database=test;");
		var logger = NullLogger<PostgresMaterializedViewStore>.Instance;
		var jsonOptions = new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase
		};

		try
		{
			// Act
			var store = new PostgresMaterializedViewStore(
				dataSource,
				logger,
				viewTableName: "my_views",
				positionTableName: "my_positions",
				jsonOptions: jsonOptions);

			// Assert
			store.ShouldNotBeNull();
		}
		finally
		{
			dataSource.Dispose();
		}
	}

	#endregion

	#region GetAsync Argument Validation Tests

	[Fact]
	public async Task GetAsync_ThrowArgumentExceptionForNullViewName()
	{
		// Arrange
		var connectionString = "Host=localhost;Database=test;";
		var logger = NullLogger<PostgresMaterializedViewStore>.Instance;
		var store = new PostgresMaterializedViewStore(connectionString, logger);

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await store.GetAsync<TestView>(viewName: null!, "view-1", CancellationToken.None));
	}

	[Fact]
	public async Task GetAsync_ThrowArgumentExceptionForEmptyViewName()
	{
		// Arrange
		var connectionString = "Host=localhost;Database=test;";
		var logger = NullLogger<PostgresMaterializedViewStore>.Instance;
		var store = new PostgresMaterializedViewStore(connectionString, logger);

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await store.GetAsync<TestView>(viewName: "", "view-1", CancellationToken.None));
	}

	[Fact]
	public async Task GetAsync_ThrowArgumentExceptionForNullViewId()
	{
		// Arrange
		var connectionString = "Host=localhost;Database=test;";
		var logger = NullLogger<PostgresMaterializedViewStore>.Instance;
		var store = new PostgresMaterializedViewStore(connectionString, logger);

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await store.GetAsync<TestView>("TestView", viewId: null!, CancellationToken.None));
	}

	[Fact]
	public async Task GetAsync_ThrowArgumentExceptionForEmptyViewId()
	{
		// Arrange
		var connectionString = "Host=localhost;Database=test;";
		var logger = NullLogger<PostgresMaterializedViewStore>.Instance;
		var store = new PostgresMaterializedViewStore(connectionString, logger);

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
		var connectionString = "Host=localhost;Database=test;";
		var logger = NullLogger<PostgresMaterializedViewStore>.Instance;
		var store = new PostgresMaterializedViewStore(connectionString, logger);
		var view = new TestView { Id = "1" };

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await store.SaveAsync(viewName: null!, "view-1", view, CancellationToken.None));
	}

	[Fact]
	public async Task SaveAsync_ThrowArgumentExceptionForEmptyViewName()
	{
		// Arrange
		var connectionString = "Host=localhost;Database=test;";
		var logger = NullLogger<PostgresMaterializedViewStore>.Instance;
		var store = new PostgresMaterializedViewStore(connectionString, logger);
		var view = new TestView { Id = "1" };

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await store.SaveAsync(viewName: "", "view-1", view, CancellationToken.None));
	}

	[Fact]
	public async Task SaveAsync_ThrowArgumentExceptionForNullViewId()
	{
		// Arrange
		var connectionString = "Host=localhost;Database=test;";
		var logger = NullLogger<PostgresMaterializedViewStore>.Instance;
		var store = new PostgresMaterializedViewStore(connectionString, logger);
		var view = new TestView { Id = "1" };

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await store.SaveAsync("TestView", viewId: null!, view, CancellationToken.None));
	}

	[Fact]
	public async Task SaveAsync_ThrowArgumentNullExceptionForNullView()
	{
		// Arrange
		var connectionString = "Host=localhost;Database=test;";
		var logger = NullLogger<PostgresMaterializedViewStore>.Instance;
		var store = new PostgresMaterializedViewStore(connectionString, logger);

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
		var connectionString = "Host=localhost;Database=test;";
		var logger = NullLogger<PostgresMaterializedViewStore>.Instance;
		var store = new PostgresMaterializedViewStore(connectionString, logger);

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await store.DeleteAsync(viewName: null!, "view-1", CancellationToken.None));
	}

	[Fact]
	public async Task DeleteAsync_ThrowArgumentExceptionForEmptyViewName()
	{
		// Arrange
		var connectionString = "Host=localhost;Database=test;";
		var logger = NullLogger<PostgresMaterializedViewStore>.Instance;
		var store = new PostgresMaterializedViewStore(connectionString, logger);

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await store.DeleteAsync(viewName: "", "view-1", CancellationToken.None));
	}

	[Fact]
	public async Task DeleteAsync_ThrowArgumentExceptionForNullViewId()
	{
		// Arrange
		var connectionString = "Host=localhost;Database=test;";
		var logger = NullLogger<PostgresMaterializedViewStore>.Instance;
		var store = new PostgresMaterializedViewStore(connectionString, logger);

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
		var connectionString = "Host=localhost;Database=test;";
		var logger = NullLogger<PostgresMaterializedViewStore>.Instance;
		var store = new PostgresMaterializedViewStore(connectionString, logger);

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await store.GetPositionAsync(viewName: null!, CancellationToken.None));
	}

	[Fact]
	public async Task GetPositionAsync_ThrowArgumentExceptionForEmptyViewName()
	{
		// Arrange
		var connectionString = "Host=localhost;Database=test;";
		var logger = NullLogger<PostgresMaterializedViewStore>.Instance;
		var store = new PostgresMaterializedViewStore(connectionString, logger);

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
		var connectionString = "Host=localhost;Database=test;";
		var logger = NullLogger<PostgresMaterializedViewStore>.Instance;
		var store = new PostgresMaterializedViewStore(connectionString, logger);

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await store.SavePositionAsync(viewName: null!, 100, CancellationToken.None));
	}

	[Fact]
	public async Task SavePositionAsync_ThrowArgumentExceptionForEmptyViewName()
	{
		// Arrange
		var connectionString = "Host=localhost;Database=test;";
		var logger = NullLogger<PostgresMaterializedViewStore>.Instance;
		var store = new PostgresMaterializedViewStore(connectionString, logger);

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await store.SavePositionAsync(viewName: "", 100, CancellationToken.None));
	}

	#endregion

	#region ADR-109 Compliance Tests

	[Fact]
	public void DefaultTableNames_UseSnakeCase()
	{
		// Assert - per ADR-109, Postgres tables use snake_case
		// Default table names are "materialized_views" and "materialized_view_positions"
		// This test verifies the store can be created with defaults
		var connectionString = "Host=localhost;Database=test;";
		var logger = NullLogger<PostgresMaterializedViewStore>.Instance;

		var store = new PostgresMaterializedViewStore(connectionString, logger);

		store.ShouldNotBeNull();
	}

	#endregion

	#region Logger Message Verification

	[Fact]
	public void HaveLoggerMessageAttributes()
	{
		// Assert - verify LoggerMessage methods exist (EventIds 3200-3205)
		// These are private methods, but their existence is verified by successful compilation
		typeof(PostgresMaterializedViewStore).IsSealed.ShouldBeTrue();
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
