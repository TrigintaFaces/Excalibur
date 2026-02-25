// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor — field is set in InitializeAsync()

using Dapper;

using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

using Tests.Shared.Fixtures;

namespace Excalibur.Dispatch.Integration.Tests.Observability.Projections;

/// <summary>
/// Integration tests for <see cref="SqlServerProjectionStore{TProjection}"/>.
/// Tests all IProjectionStore operations including CRUD, filtering, pagination, and sorting.
/// </summary>
[Collection("SqlServer Projection Store Tests")]
public sealed class SqlServerProjectionStoreShould : IClassFixture<SqlServerFixture>, IAsyncLifetime
{
	private const string TableName = "TestOrderProjection";
	private readonly SqlServerFixture _fixture;
	private readonly ILogger<SqlServerProjectionStore<TestOrderProjection>> _logger;
	private SqlServerProjectionStore<TestOrderProjection> _store;

	public SqlServerProjectionStoreShould(SqlServerFixture fixture)
	{
		_fixture = fixture;
		_logger = new LoggerFactory().CreateLogger<SqlServerProjectionStore<TestOrderProjection>>();
	}

	public async Task InitializeAsync()
	{
		// Create the projection table
		await using var connection = new SqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync();

		_ = await connection.ExecuteAsync($"""
			IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = '{TableName}')
			BEGIN
				CREATE TABLE [{TableName}] (
					Id NVARCHAR(450) NOT NULL PRIMARY KEY,
					Data NVARCHAR(MAX) NOT NULL,
					CreatedAt DATETIMEOFFSET NOT NULL,
					UpdatedAt DATETIMEOFFSET NOT NULL
				)
			END
			""");

		_store = new SqlServerProjectionStore<TestOrderProjection>(
			_fixture.ConnectionString,
			_logger,
			TableName);
	}

	public async Task DisposeAsync()
	{
		// Clean up test data
		await using var connection = new SqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync();
		_ = await connection.ExecuteAsync($"DELETE FROM [{TableName}]");
	}

	#region CRUD Tests (5)

	[Fact]
	public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
	{
		// Arrange
		var nonExistentId = Guid.NewGuid().ToString();

		// Act
		var result = await _store.GetByIdAsync(nonExistentId, CancellationToken.None);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task UpsertAsync_CreatesNewProjection()
	{
		// Arrange
		var id = Guid.NewGuid().ToString();
		var projection = TestOrderProjection.Create(
			id,
			"customer-1",
			"Active",
			100.50m,
			5,
			DateTimeOffset.UtcNow,
			["important"],
			"Widget");

		// Act
		await _store.UpsertAsync(id, projection, CancellationToken.None);
		var result = await _store.GetByIdAsync(id, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Id.ShouldBe(id);
		result.CustomerId.ShouldBe("customer-1");
		result.Status.ShouldBe("Active");
		result.Amount.ShouldBe(100.50m);
		result.Quantity.ShouldBe(5);
		result.ProductName.ShouldBe("Widget");
	}

	[Fact]
	public async Task UpsertAsync_UpdatesExistingProjection()
	{
		// Arrange
		var id = Guid.NewGuid().ToString();
		var original = TestOrderProjection.Create(id, "customer-1", "Pending", 50m);
		await _store.UpsertAsync(id, original, CancellationToken.None);

		var updated = TestOrderProjection.Create(id, "customer-1", "Shipped", 75m);

		// Act
		await _store.UpsertAsync(id, updated, CancellationToken.None);
		var result = await _store.GetByIdAsync(id, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Status.ShouldBe("Shipped");
		result.Amount.ShouldBe(75m);
	}

	[Fact]
	public async Task DeleteAsync_RemovesProjection()
	{
		// Arrange
		var id = Guid.NewGuid().ToString();
		var projection = TestOrderProjection.Create(id, "customer-1");
		await _store.UpsertAsync(id, projection, CancellationToken.None);

		// Verify it exists
		var beforeDelete = await _store.GetByIdAsync(id, CancellationToken.None);
		_ = beforeDelete.ShouldNotBeNull();

		// Act
		await _store.DeleteAsync(id, CancellationToken.None);
		var result = await _store.GetByIdAsync(id, CancellationToken.None);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task DeleteAsync_Succeeds_WhenNotExists()
	{
		// Arrange
		var nonExistentId = Guid.NewGuid().ToString();

		// Act & Assert - Should not throw
		await Should.NotThrowAsync(async () =>
			await _store.DeleteAsync(nonExistentId, CancellationToken.None));
	}

	#endregion CRUD Tests (5)

	#region Equality Filter Tests (2)

	[Fact]
	public async Task QueryAsync_FiltersBy_Equality()
	{
		// Arrange
		var id1 = Guid.NewGuid().ToString();
		var id2 = Guid.NewGuid().ToString();
		var id3 = Guid.NewGuid().ToString();

		await _store.UpsertAsync(id1, TestOrderProjection.Create(id1, "c1", "Active"), CancellationToken.None);
		await _store.UpsertAsync(id2, TestOrderProjection.Create(id2, "c2", "Pending"), CancellationToken.None);
		await _store.UpsertAsync(id3, TestOrderProjection.Create(id3, "c3", "Active"), CancellationToken.None);

		var filters = new Dictionary<string, object> { ["Status"] = "Active" };

		// Act
		var results = await _store.QueryAsync(filters, null, CancellationToken.None);

		// Assert
		results.Count.ShouldBe(2);
		results.ShouldAllBe(p => p.Status == "Active");
	}

	[Fact]
	public async Task QueryAsync_FiltersBy_NotEquals()
	{
		// Arrange
		var id1 = Guid.NewGuid().ToString();
		var id2 = Guid.NewGuid().ToString();
		var id3 = Guid.NewGuid().ToString();

		await _store.UpsertAsync(id1, TestOrderProjection.Create(id1, "c1", "Active"), CancellationToken.None);
		await _store.UpsertAsync(id2, TestOrderProjection.Create(id2, "c2", "Deleted"), CancellationToken.None);
		await _store.UpsertAsync(id3, TestOrderProjection.Create(id3, "c3", "Pending"), CancellationToken.None);

		var filters = new Dictionary<string, object> { ["Status:neq"] = "Deleted" };

		// Act
		var results = await _store.QueryAsync(filters, null, CancellationToken.None);

		// Assert
		results.Count.ShouldBe(2);
		results.ShouldAllBe(p => p.Status != "Deleted");
	}

	#endregion Equality Filter Tests (2)

	#region Comparison Filter Tests (4)

	[Fact]
	public async Task QueryAsync_FiltersBy_GreaterThan()
	{
		// Arrange
		var id1 = Guid.NewGuid().ToString();
		var id2 = Guid.NewGuid().ToString();
		var id3 = Guid.NewGuid().ToString();

		await _store.UpsertAsync(id1, TestOrderProjection.Create(id1, "c1", amount: 50m), CancellationToken.None);
		await _store.UpsertAsync(id2, TestOrderProjection.Create(id2, "c2", amount: 100m), CancellationToken.None);
		await _store.UpsertAsync(id3, TestOrderProjection.Create(id3, "c3", amount: 150m), CancellationToken.None);

		var filters = new Dictionary<string, object> { ["Amount:gt"] = 75 };

		// Act
		var results = await _store.QueryAsync(filters, null, CancellationToken.None);

		// Assert
		results.Count.ShouldBe(2);
		results.ShouldAllBe(p => p.Amount > 75);
	}

	[Fact]
	public async Task QueryAsync_FiltersBy_GreaterThanOrEqual()
	{
		// Arrange
		var id1 = Guid.NewGuid().ToString();
		var id2 = Guid.NewGuid().ToString();
		var id3 = Guid.NewGuid().ToString();

		await _store.UpsertAsync(id1, TestOrderProjection.Create(id1, "c1", amount: 50m), CancellationToken.None);
		await _store.UpsertAsync(id2, TestOrderProjection.Create(id2, "c2", amount: 100m), CancellationToken.None);
		await _store.UpsertAsync(id3, TestOrderProjection.Create(id3, "c3", amount: 150m), CancellationToken.None);

		var filters = new Dictionary<string, object> { ["Amount:gte"] = 100 };

		// Act
		var results = await _store.QueryAsync(filters, null, CancellationToken.None);

		// Assert
		results.Count.ShouldBe(2);
		results.ShouldAllBe(p => p.Amount >= 100);
	}

	[Fact]
	public async Task QueryAsync_FiltersBy_LessThan()
	{
		// Arrange
		var id1 = Guid.NewGuid().ToString();
		var id2 = Guid.NewGuid().ToString();
		var id3 = Guid.NewGuid().ToString();

		await _store.UpsertAsync(id1, TestOrderProjection.Create(id1, "c1", quantity: 5), CancellationToken.None);
		await _store.UpsertAsync(id2, TestOrderProjection.Create(id2, "c2", quantity: 10), CancellationToken.None);
		await _store.UpsertAsync(id3, TestOrderProjection.Create(id3, "c3", quantity: 15), CancellationToken.None);

		var filters = new Dictionary<string, object> { ["Quantity:lt"] = 12 };

		// Act
		var results = await _store.QueryAsync(filters, null, CancellationToken.None);

		// Assert
		results.Count.ShouldBe(2);
		results.ShouldAllBe(p => p.Quantity < 12);
	}

	[Fact]
	public async Task QueryAsync_FiltersBy_LessThanOrEqual()
	{
		// Arrange
		var id1 = Guid.NewGuid().ToString();
		var id2 = Guid.NewGuid().ToString();
		var id3 = Guid.NewGuid().ToString();

		await _store.UpsertAsync(id1, TestOrderProjection.Create(id1, "c1", quantity: 5), CancellationToken.None);
		await _store.UpsertAsync(id2, TestOrderProjection.Create(id2, "c2", quantity: 10), CancellationToken.None);
		await _store.UpsertAsync(id3, TestOrderProjection.Create(id3, "c3", quantity: 15), CancellationToken.None);

		var filters = new Dictionary<string, object> { ["Quantity:lte"] = 10 };

		// Act
		var results = await _store.QueryAsync(filters, null, CancellationToken.None);

		// Assert
		results.Count.ShouldBe(2);
		results.ShouldAllBe(p => p.Quantity <= 10);
	}

	#endregion Comparison Filter Tests (4)

	#region Collection/String Filter Tests (2)

	[Fact]
	public async Task QueryAsync_FiltersBy_In()
	{
		// Arrange
		var id1 = Guid.NewGuid().ToString();
		var id2 = Guid.NewGuid().ToString();
		var id3 = Guid.NewGuid().ToString();

		await _store.UpsertAsync(id1, TestOrderProjection.Create(id1, "c1", "Active"), CancellationToken.None);
		await _store.UpsertAsync(id2, TestOrderProjection.Create(id2, "c2", "Shipped"), CancellationToken.None);
		await _store.UpsertAsync(id3, TestOrderProjection.Create(id3, "c3", "Pending"), CancellationToken.None);

		var filters = new Dictionary<string, object>
		{
			["Status:in"] = new[] { "Active", "Shipped" }
		};

		// Act
		var results = await _store.QueryAsync(filters, null, CancellationToken.None);

		// Assert
		results.Count.ShouldBe(2);
		results.ShouldAllBe(p => p.Status == "Active" || p.Status == "Shipped");
	}

	[Fact]
	public async Task QueryAsync_FiltersBy_Contains()
	{
		// Arrange
		var id1 = Guid.NewGuid().ToString();
		var id2 = Guid.NewGuid().ToString();
		var id3 = Guid.NewGuid().ToString();

		await _store.UpsertAsync(id1, TestOrderProjection.Create(id1, "c1", productName: "Test Widget"), CancellationToken.None);
		await _store.UpsertAsync(id2, TestOrderProjection.Create(id2, "c2", productName: "Another Gadget"), CancellationToken.None);
		await _store.UpsertAsync(id3, TestOrderProjection.Create(id3, "c3", productName: "Widget Pro"), CancellationToken.None);

		var filters = new Dictionary<string, object> { ["ProductName:contains"] = "Widget" };

		// Act
		var results = await _store.QueryAsync(filters, null, CancellationToken.None);

		// Assert
		results.Count.ShouldBe(2);
		results.ShouldAllBe(p => p.ProductName.Contains("Widget"));
	}

	#endregion Collection/String Filter Tests (2)

	#region Pagination Tests (3)

	[Fact]
	public async Task QueryAsync_Pagination_Skip()
	{
		// Arrange - Create 5 projections
		for (var i = 1; i <= 5; i++)
		{
			var id = $"skip-test-{i:D3}";
			await _store.UpsertAsync(id, TestOrderProjection.Create(id, "c1"), CancellationToken.None);
		}

		var options = new QueryOptions(Skip: 2);

		// Act
		var results = await _store.QueryAsync(null, options, CancellationToken.None);

		// Assert
		results.Count.ShouldBe(3); // Skipped first 2, got remaining 3
	}

	[Fact]
	public async Task QueryAsync_Pagination_Take()
	{
		// Arrange - Create 5 projections
		for (var i = 1; i <= 5; i++)
		{
			var id = $"take-test-{i:D3}";
			await _store.UpsertAsync(id, TestOrderProjection.Create(id, "c1"), CancellationToken.None);
		}

		var options = new QueryOptions(Take: 3);

		// Act
		var results = await _store.QueryAsync(null, options, CancellationToken.None);

		// Assert
		results.Count.ShouldBe(3);
	}

	[Fact]
	public async Task QueryAsync_Pagination_SkipAndTake()
	{
		// Arrange - Create 10 projections
		for (var i = 1; i <= 10; i++)
		{
			var id = $"page-test-{i:D3}";
			await _store.UpsertAsync(id, TestOrderProjection.Create(id, "c1"), CancellationToken.None);
		}

		var options = new QueryOptions(Skip: 3, Take: 4);

		// Act
		var results = await _store.QueryAsync(null, options, CancellationToken.None);

		// Assert
		results.Count.ShouldBe(4);
	}

	#endregion Pagination Tests (3)

	#region Sorting Tests (2)

	[Fact]
	public async Task QueryAsync_OrderBy_Ascending()
	{
		// Arrange
		var id1 = Guid.NewGuid().ToString();
		var id2 = Guid.NewGuid().ToString();
		var id3 = Guid.NewGuid().ToString();

		await _store.UpsertAsync(id1, TestOrderProjection.Create(id1, "c1", amount: 300m), CancellationToken.None);
		await _store.UpsertAsync(id2, TestOrderProjection.Create(id2, "c2", amount: 100m), CancellationToken.None);
		await _store.UpsertAsync(id3, TestOrderProjection.Create(id3, "c3", amount: 200m), CancellationToken.None);

		var filters = new Dictionary<string, object>
		{
			["CustomerId:in"] = new[] { "c1", "c2", "c3" }
		};
		var options = new QueryOptions(OrderBy: "Amount", Descending: false);

		// Act
		var results = await _store.QueryAsync(filters, options, CancellationToken.None);

		// Assert
		results.Count.ShouldBe(3);
		results[0].Amount.ShouldBe(100m);
		results[1].Amount.ShouldBe(200m);
		results[2].Amount.ShouldBe(300m);
	}

	[Fact]
	public async Task QueryAsync_OrderBy_Descending()
	{
		// Arrange
		var id1 = Guid.NewGuid().ToString();
		var id2 = Guid.NewGuid().ToString();
		var id3 = Guid.NewGuid().ToString();

		await _store.UpsertAsync(id1, TestOrderProjection.Create(id1, "c1", amount: 300m), CancellationToken.None);
		await _store.UpsertAsync(id2, TestOrderProjection.Create(id2, "c2", amount: 100m), CancellationToken.None);
		await _store.UpsertAsync(id3, TestOrderProjection.Create(id3, "c3", amount: 200m), CancellationToken.None);

		var filters = new Dictionary<string, object>
		{
			["CustomerId:in"] = new[] { "c1", "c2", "c3" }
		};
		var options = new QueryOptions(OrderBy: "Amount", Descending: true);

		// Act
		var results = await _store.QueryAsync(filters, options, CancellationToken.None);

		// Assert
		results.Count.ShouldBe(3);
		results[0].Amount.ShouldBe(300m);
		results[1].Amount.ShouldBe(200m);
		results[2].Amount.ShouldBe(100m);
	}

	#endregion Sorting Tests (2)

	#region Count Tests (2)

	[Fact]
	public async Task CountAsync_ReturnsTotal_WhenNoFilters()
	{
		// Arrange - Create 5 projections
		for (var i = 1; i <= 5; i++)
		{
			var id = $"count-total-{i:D3}";
			await _store.UpsertAsync(id, TestOrderProjection.Create(id, "c1"), CancellationToken.None);
		}

		// Act
		var count = await _store.CountAsync(null, CancellationToken.None);

		// Assert
		count.ShouldBeGreaterThanOrEqualTo(5);
	}

	[Fact]
	public async Task CountAsync_ReturnsFiltered_WhenFiltersApplied()
	{
		// Arrange
		var id1 = Guid.NewGuid().ToString();
		var id2 = Guid.NewGuid().ToString();
		var id3 = Guid.NewGuid().ToString();

		await _store.UpsertAsync(id1, TestOrderProjection.Create(id1, "c1", "Active"), CancellationToken.None);
		await _store.UpsertAsync(id2, TestOrderProjection.Create(id2, "c2", "Pending"), CancellationToken.None);
		await _store.UpsertAsync(id3, TestOrderProjection.Create(id3, "c3", "Active"), CancellationToken.None);

		var filters = new Dictionary<string, object> { ["Status"] = "Active" };

		// Act
		var count = await _store.CountAsync(filters, CancellationToken.None);

		// Assert
		count.ShouldBe(2);
	}

	#endregion Count Tests (2)

	#region Edge Case Tests (3)

	[Fact]
	public async Task QueryAsync_ReturnsEmpty_WhenNoMatches()
	{
		// Arrange
		var id = Guid.NewGuid().ToString();
		await _store.UpsertAsync(id, TestOrderProjection.Create(id, "c1", "Active"), CancellationToken.None);

		var filters = new Dictionary<string, object> { ["Status"] = "NonExistentStatus" };

		// Act
		var results = await _store.QueryAsync(filters, null, CancellationToken.None);

		// Assert
		results.ShouldBeEmpty();
	}

	[Fact]
	public async Task QueryAsync_HandlesNull_Filters()
	{
		// Arrange
		var id = Guid.NewGuid().ToString();
		await _store.UpsertAsync(id, TestOrderProjection.Create(id, "c1"), CancellationToken.None);

		// Act
		var results = await _store.QueryAsync(null, null, CancellationToken.None);

		// Assert
		results.ShouldNotBeEmpty();
	}

	[Fact]
	public async Task QueryAsync_HandlesNull_Options()
	{
		// Arrange
		var id = Guid.NewGuid().ToString();
		await _store.UpsertAsync(id, TestOrderProjection.Create(id, "c1", "TestStatus"), CancellationToken.None);

		var filters = new Dictionary<string, object> { ["Status"] = "TestStatus" };

		// Act
		var results = await _store.QueryAsync(filters, null, CancellationToken.None);

		// Assert
		results.Count.ShouldBe(1);
	}

	#endregion Edge Case Tests (3)
}

/// <summary>
/// Collection definition for SQL Server projection store tests.
/// Ensures tests run sequentially to avoid database conflicts.
/// </summary>
[CollectionDefinition("SqlServer Projection Store Tests")]
public sealed class SqlServerProjectionStoreTestCollection : ICollectionFixture<SqlServerFixture>;
