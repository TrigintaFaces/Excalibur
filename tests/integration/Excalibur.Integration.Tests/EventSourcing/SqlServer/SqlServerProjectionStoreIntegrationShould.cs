// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

using Testcontainers.MsSql;

namespace Excalibur.Integration.Tests.EventSourcing.SqlServer;

/// <summary>
/// Integration tests for <see cref="SqlServerProjectionStore{TProjection}"/> using real SQL Server via TestContainers.
/// Tests projection CRUD operations, querying, filtering, and pagination.
/// </summary>
/// <remarks>
/// Sprint 175 - Provider Testing Epic Phase 1.
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Provider", "SqlServer")]
[Trait("Component", "EventSourcing")]
[SuppressMessage("Design", "CA1506", Justification = "Integration test requires multiple dependencies for proper setup")]
public sealed class SqlServerProjectionStoreIntegrationShould : IAsyncLifetime
{
	private MsSqlContainer? _container;
	private string? _connectionString;
	private bool _dockerAvailable;

	public async Task InitializeAsync()
	{
		try
		{
			_container = new MsSqlBuilder()
				.WithImage("mcr.microsoft.com/mssql/server:2022-latest")
				.Build();

			await _container.StartAsync().ConfigureAwait(false);
			_connectionString = _container.GetConnectionString();
			_dockerAvailable = true;

			await InitializeDatabaseAsync().ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Docker initialization failed: {ex.Message}");
			Console.WriteLine(ex.ToString());
			_dockerAvailable = false;
		}
	}

	public async Task DisposeAsync()
	{
		if (_container != null)
		{
			await _container.DisposeAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Verifies that a projection can be upserted and retrieved by ID.
	/// </summary>
	[Fact]
	public async Task UpsertAndGetProjectionById()
	{
		if (!_dockerAvailable)
		{
			return;
		}

		var store = CreateProjectionStore();
		var id = Guid.NewGuid().ToString();
		var projection = new OrderSummary { OrderId = id, CustomerName = "Alice", TotalAmount = 99.99m, Status = "Active" };

		await store.UpsertAsync(id, projection, CancellationToken.None).ConfigureAwait(true);

		var loaded = await store.GetByIdAsync(id, CancellationToken.None).ConfigureAwait(true);

		_ = loaded.ShouldNotBeNull();
		loaded.OrderId.ShouldBe(id);
		loaded.CustomerName.ShouldBe("Alice");
		loaded.TotalAmount.ShouldBe(99.99m);
		loaded.Status.ShouldBe("Active");
	}

	/// <summary>
	/// Verifies that getting a non-existent projection returns null.
	/// </summary>
	[Fact]
	public async Task ReturnNullForNonExistentProjection()
	{
		if (!_dockerAvailable)
		{
			return;
		}

		var store = CreateProjectionStore();

		var loaded = await store.GetByIdAsync(Guid.NewGuid().ToString(), CancellationToken.None).ConfigureAwait(true);

		loaded.ShouldBeNull();
	}

	/// <summary>
	/// Verifies that upserting an existing projection updates the data.
	/// </summary>
	[Fact]
	public async Task UpdateExistingProjectionOnUpsert()
	{
		if (!_dockerAvailable)
		{
			return;
		}

		var store = CreateProjectionStore();
		var id = Guid.NewGuid().ToString();

		var original = new OrderSummary { OrderId = id, CustomerName = "Alice", TotalAmount = 50.00m, Status = "Pending" };
		await store.UpsertAsync(id, original, CancellationToken.None).ConfigureAwait(true);

		var updated = new OrderSummary { OrderId = id, CustomerName = "Alice", TotalAmount = 75.00m, Status = "Shipped" };
		await store.UpsertAsync(id, updated, CancellationToken.None).ConfigureAwait(true);

		var loaded = await store.GetByIdAsync(id, CancellationToken.None).ConfigureAwait(true);

		_ = loaded.ShouldNotBeNull();
		loaded.TotalAmount.ShouldBe(75.00m);
		loaded.Status.ShouldBe("Shipped");
	}

	/// <summary>
	/// Verifies that deleting a projection removes it from the store.
	/// </summary>
	[Fact]
	public async Task DeleteProjectionById()
	{
		if (!_dockerAvailable)
		{
			return;
		}

		var store = CreateProjectionStore();
		var id = Guid.NewGuid().ToString();

		await store.UpsertAsync(id, new OrderSummary { OrderId = id, CustomerName = "Bob", TotalAmount = 10.00m, Status = "Active" }, CancellationToken.None).ConfigureAwait(true);

		await store.DeleteAsync(id, CancellationToken.None).ConfigureAwait(true);

		var loaded = await store.GetByIdAsync(id, CancellationToken.None).ConfigureAwait(true);
		loaded.ShouldBeNull();
	}

	/// <summary>
	/// Verifies that deleting a non-existent projection does not throw (idempotent).
	/// </summary>
	[Fact]
	public async Task NotThrowWhenDeletingNonExistentProjection()
	{
		if (!_dockerAvailable)
		{
			return;
		}

		var store = CreateProjectionStore();

		// Should not throw
		await store.DeleteAsync(Guid.NewGuid().ToString(), CancellationToken.None).ConfigureAwait(true);
	}

	/// <summary>
	/// Verifies that querying with no filters returns all projections.
	/// </summary>
	[Fact]
	public async Task QueryAllProjectionsWithNoFilter()
	{
		if (!_dockerAvailable)
		{
			return;
		}

		await ClearAllProjectionsAsync().ConfigureAwait(true);

		var store = CreateProjectionStore();

		for (int i = 0; i < 3; i++)
		{
			var id = Guid.NewGuid().ToString();
			await store.UpsertAsync(id, new OrderSummary { OrderId = id, CustomerName = $"Customer{i}", TotalAmount = i * 10m, Status = "Active" }, CancellationToken.None).ConfigureAwait(true);
		}

		var results = await store.QueryAsync(null, null, CancellationToken.None).ConfigureAwait(true);

		results.Count.ShouldBe(3);
	}

	/// <summary>
	/// Verifies that counting projections returns the correct count.
	/// </summary>
	[Fact]
	public async Task CountProjectionsCorrectly()
	{
		if (!_dockerAvailable)
		{
			return;
		}

		await ClearAllProjectionsAsync().ConfigureAwait(true);

		var store = CreateProjectionStore();

		for (int i = 0; i < 5; i++)
		{
			var id = Guid.NewGuid().ToString();
			await store.UpsertAsync(id, new OrderSummary { OrderId = id, CustomerName = $"Customer{i}", TotalAmount = i * 10m, Status = "Active" }, CancellationToken.None).ConfigureAwait(true);
		}

		var count = await store.CountAsync(null, CancellationToken.None).ConfigureAwait(true);

		count.ShouldBe(5);
	}

	/// <summary>
	/// Verifies that pagination with Skip and Take works correctly.
	/// </summary>
	[Fact]
	public async Task SupportPaginationWithSkipAndTake()
	{
		if (!_dockerAvailable)
		{
			return;
		}

		await ClearAllProjectionsAsync().ConfigureAwait(true);

		var store = CreateProjectionStore();

		for (int i = 0; i < 10; i++)
		{
			var id = $"order-{i:D3}";
			await store.UpsertAsync(id, new OrderSummary { OrderId = id, CustomerName = $"Customer{i}", TotalAmount = i * 10m, Status = "Active" }, CancellationToken.None).ConfigureAwait(true);
		}

		var page = await store.QueryAsync(
			null,
			new QueryOptions(Skip: 3, Take: 4),
			CancellationToken.None).ConfigureAwait(true);

		page.Count.ShouldBe(4);
	}

	/// <summary>
	/// Verifies that querying with equality filter returns matching projections.
	/// </summary>
	[Fact]
	public async Task QueryWithEqualityFilter()
	{
		if (!_dockerAvailable)
		{
			return;
		}

		await ClearAllProjectionsAsync().ConfigureAwait(true);

		var store = CreateProjectionStore();

		var id1 = Guid.NewGuid().ToString();
		var id2 = Guid.NewGuid().ToString();
		var id3 = Guid.NewGuid().ToString();

		await store.UpsertAsync(id1, new OrderSummary { OrderId = id1, CustomerName = "Alice", TotalAmount = 50m, Status = "Active" }, CancellationToken.None).ConfigureAwait(true);
		await store.UpsertAsync(id2, new OrderSummary { OrderId = id2, CustomerName = "Bob", TotalAmount = 75m, Status = "Shipped" }, CancellationToken.None).ConfigureAwait(true);
		await store.UpsertAsync(id3, new OrderSummary { OrderId = id3, CustomerName = "Carol", TotalAmount = 25m, Status = "Active" }, CancellationToken.None).ConfigureAwait(true);

		var filters = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["Status"] = "Active"
		};

		var results = await store.QueryAsync(filters, null, CancellationToken.None).ConfigureAwait(true);

		results.Count.ShouldBe(2);
		results.ShouldAllBe(r => r.Status == "Active");
	}

	/// <summary>
	/// Verifies that counting with a filter returns the correct count.
	/// </summary>
	[Fact]
	public async Task CountWithFilter()
	{
		if (!_dockerAvailable)
		{
			return;
		}

		await ClearAllProjectionsAsync().ConfigureAwait(true);

		var store = CreateProjectionStore();

		var id1 = Guid.NewGuid().ToString();
		var id2 = Guid.NewGuid().ToString();
		var id3 = Guid.NewGuid().ToString();

		await store.UpsertAsync(id1, new OrderSummary { OrderId = id1, CustomerName = "Alice", TotalAmount = 50m, Status = "Active" }, CancellationToken.None).ConfigureAwait(true);
		await store.UpsertAsync(id2, new OrderSummary { OrderId = id2, CustomerName = "Bob", TotalAmount = 75m, Status = "Shipped" }, CancellationToken.None).ConfigureAwait(true);
		await store.UpsertAsync(id3, new OrderSummary { OrderId = id3, CustomerName = "Carol", TotalAmount = 25m, Status = "Active" }, CancellationToken.None).ConfigureAwait(true);

		var filters = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["Status"] = "Shipped"
		};

		var count = await store.CountAsync(filters, CancellationToken.None).ConfigureAwait(true);

		count.ShouldBe(1);
	}

	private SqlServerProjectionStore<OrderSummary> CreateProjectionStore()
	{
		var logger = NullLogger<SqlServerProjectionStore<OrderSummary>>.Instance;
		return new SqlServerProjectionStore<OrderSummary>(_connectionString!, logger, "OrderSummary");
	}

	private async Task ClearAllProjectionsAsync()
	{
		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync().ConfigureAwait(false);
		await using var command = new SqlCommand("DELETE FROM [OrderSummary]", connection);
		_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
	}

	private async Task InitializeDatabaseAsync()
	{
		const string createTableSql = """
			IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='OrderSummary' AND xtype='U')
			CREATE TABLE [OrderSummary] (
				Id NVARCHAR(255) NOT NULL PRIMARY KEY,
				Data NVARCHAR(MAX) NOT NULL,
				CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
				UpdatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET()
			)
			""";

		Console.WriteLine($"Connection string: {_connectionString}");

		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync().ConfigureAwait(false);
		Console.WriteLine("Database connection opened successfully");

		await using var command = new SqlCommand(createTableSql, connection);
		_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
		Console.WriteLine("OrderSummary table created successfully");
	}

	/// <summary>
	/// Test projection type representing an order summary.
	/// </summary>
	private sealed class OrderSummary
	{
		public string OrderId { get; set; } = string.Empty;
		public string CustomerName { get; set; } = string.Empty;
		public decimal TotalAmount { get; set; }
		public string Status { get; set; } = string.Empty;
	}
}
