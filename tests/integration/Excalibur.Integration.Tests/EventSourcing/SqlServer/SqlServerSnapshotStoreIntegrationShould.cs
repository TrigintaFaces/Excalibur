// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text;

using Excalibur.Domain.Model;

using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

using Testcontainers.MsSql;

namespace Excalibur.Integration.Tests.EventSourcing.SqlServer;

/// <summary>
/// Integration tests for <see cref="SqlServerSnapshotStore"/> using real SQL Server via TestContainers.
/// Tests snapshot CRUD operations including save, load, update, and delete.
/// </summary>
/// <remarks>
/// Sprint 175 - Provider Testing Epic Phase 1.
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Provider", "SqlServer")]
[Trait("Component", "EventSourcing")]
[SuppressMessage("Design", "CA1506", Justification = "Integration test requires multiple dependencies for proper setup")]
public sealed class SqlServerSnapshotStoreIntegrationShould : IAsyncLifetime
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
	/// Verifies that a snapshot can be saved and then loaded by aggregate ID.
	/// </summary>
	[Fact]
	public async Task SaveAndLoadSnapshot()
	{
		if (!_dockerAvailable)
		{
			return;
		}

		var snapshotStore = CreateSnapshotStore();
		var aggregateId = Guid.NewGuid().ToString();
		var aggregateType = "TestAggregate";
		var data = Encoding.UTF8.GetBytes("{\"name\": \"test\"}");

		var snapshot = Snapshot.Create(aggregateId, 5, data, aggregateType);

		await snapshotStore.SaveSnapshotAsync(snapshot, CancellationToken.None).ConfigureAwait(true);

		var loaded = await snapshotStore.GetLatestSnapshotAsync(aggregateId, aggregateType, CancellationToken.None).ConfigureAwait(true);

		_ = loaded.ShouldNotBeNull();
		loaded.AggregateId.ShouldBe(aggregateId);
		loaded.AggregateType.ShouldBe(aggregateType);
		loaded.Version.ShouldBe(5);
		loaded.Data.ShouldBe(data);
	}

	/// <summary>
	/// Verifies that loading a snapshot for a non-existent aggregate returns null.
	/// </summary>
	[Fact]
	public async Task ReturnNullForNonExistentAggregate()
	{
		if (!_dockerAvailable)
		{
			return;
		}

		var snapshotStore = CreateSnapshotStore();
		var aggregateId = Guid.NewGuid().ToString();

		var loaded = await snapshotStore.GetLatestSnapshotAsync(aggregateId, "NonExistent", CancellationToken.None).ConfigureAwait(true);

		loaded.ShouldBeNull();
	}

	/// <summary>
	/// Verifies that saving a newer snapshot overwrites the existing one (upsert semantics).
	/// </summary>
	[Fact]
	public async Task UpdateExistingSnapshotWithNewerVersion()
	{
		if (!_dockerAvailable)
		{
			return;
		}

		var snapshotStore = CreateSnapshotStore();
		var aggregateId = Guid.NewGuid().ToString();
		var aggregateType = "TestAggregate";

		var data1 = Encoding.UTF8.GetBytes("{\"version\": 1}");
		var snapshot1 = Snapshot.Create(aggregateId, 5, data1, aggregateType);
		await snapshotStore.SaveSnapshotAsync(snapshot1, CancellationToken.None).ConfigureAwait(true);

		var data2 = Encoding.UTF8.GetBytes("{\"version\": 2}");
		var snapshot2 = Snapshot.Create(aggregateId, 10, data2, aggregateType);
		await snapshotStore.SaveSnapshotAsync(snapshot2, CancellationToken.None).ConfigureAwait(true);

		var loaded = await snapshotStore.GetLatestSnapshotAsync(aggregateId, aggregateType, CancellationToken.None).ConfigureAwait(true);

		_ = loaded.ShouldNotBeNull();
		loaded.Version.ShouldBe(10);
		loaded.Data.ShouldBe(data2);
	}

	/// <summary>
	/// Verifies that snapshots from different aggregates are isolated.
	/// </summary>
	[Fact]
	public async Task IsolateSnapshotsAcrossAggregates()
	{
		if (!_dockerAvailable)
		{
			return;
		}

		var snapshotStore = CreateSnapshotStore();
		var aggregateId1 = Guid.NewGuid().ToString();
		var aggregateId2 = Guid.NewGuid().ToString();
		var aggregateType = "TestAggregate";

		var data1 = Encoding.UTF8.GetBytes("{\"agg\": 1}");
		var data2 = Encoding.UTF8.GetBytes("{\"agg\": 2}");

		await snapshotStore.SaveSnapshotAsync(
			Snapshot.Create(aggregateId1, 3, data1, aggregateType), CancellationToken.None).ConfigureAwait(true);
		await snapshotStore.SaveSnapshotAsync(
			Snapshot.Create(aggregateId2, 7, data2, aggregateType), CancellationToken.None).ConfigureAwait(true);

		var loaded1 = await snapshotStore.GetLatestSnapshotAsync(aggregateId1, aggregateType, CancellationToken.None).ConfigureAwait(true);
		var loaded2 = await snapshotStore.GetLatestSnapshotAsync(aggregateId2, aggregateType, CancellationToken.None).ConfigureAwait(true);

		_ = loaded1.ShouldNotBeNull();
		_ = loaded2.ShouldNotBeNull();
		loaded1.AggregateId.ShouldBe(aggregateId1);
		loaded1.Version.ShouldBe(3);
		loaded2.AggregateId.ShouldBe(aggregateId2);
		loaded2.Version.ShouldBe(7);
	}

	/// <summary>
	/// Verifies that deleting all snapshots for an aggregate works correctly.
	/// </summary>
	[Fact]
	public async Task DeleteAllSnapshotsForAggregate()
	{
		if (!_dockerAvailable)
		{
			return;
		}

		var snapshotStore = CreateSnapshotStore();
		var aggregateId = Guid.NewGuid().ToString();
		var aggregateType = "TestAggregate";

		var data = Encoding.UTF8.GetBytes("{\"state\": true}");
		await snapshotStore.SaveSnapshotAsync(
			Snapshot.Create(aggregateId, 5, data, aggregateType), CancellationToken.None).ConfigureAwait(true);

		await snapshotStore.DeleteSnapshotsAsync(aggregateId, aggregateType, CancellationToken.None).ConfigureAwait(true);

		var loaded = await snapshotStore.GetLatestSnapshotAsync(aggregateId, aggregateType, CancellationToken.None).ConfigureAwait(true);
		loaded.ShouldBeNull();
	}

	/// <summary>
	/// Verifies that snapshots from different aggregate types are isolated.
	/// </summary>
	[Fact]
	public async Task IsolateSnapshotsAcrossAggregateTypes()
	{
		if (!_dockerAvailable)
		{
			return;
		}

		var snapshotStore = CreateSnapshotStore();
		var aggregateId = Guid.NewGuid().ToString();

		var data1 = Encoding.UTF8.GetBytes("{\"type\": \"Order\"}");
		var data2 = Encoding.UTF8.GetBytes("{\"type\": \"Customer\"}");

		await snapshotStore.SaveSnapshotAsync(
			Snapshot.Create(aggregateId, 2, data1, "OrderAggregate"), CancellationToken.None).ConfigureAwait(true);
		await snapshotStore.SaveSnapshotAsync(
			Snapshot.Create(aggregateId, 4, data2, "CustomerAggregate"), CancellationToken.None).ConfigureAwait(true);

		var loadedOrder = await snapshotStore.GetLatestSnapshotAsync(aggregateId, "OrderAggregate", CancellationToken.None).ConfigureAwait(true);
		var loadedCustomer = await snapshotStore.GetLatestSnapshotAsync(aggregateId, "CustomerAggregate", CancellationToken.None).ConfigureAwait(true);

		_ = loadedOrder.ShouldNotBeNull();
		_ = loadedCustomer.ShouldNotBeNull();
		loadedOrder.Version.ShouldBe(2);
		loadedOrder.Data.ShouldBe(data1);
		loadedCustomer.Version.ShouldBe(4);
		loadedCustomer.Data.ShouldBe(data2);
	}

	/// <summary>
	/// Verifies that deleting snapshots for one aggregate does not affect others.
	/// </summary>
	[Fact]
	public async Task NotAffectOtherAggregatesWhenDeleting()
	{
		if (!_dockerAvailable)
		{
			return;
		}

		var snapshotStore = CreateSnapshotStore();
		var aggregateId1 = Guid.NewGuid().ToString();
		var aggregateId2 = Guid.NewGuid().ToString();
		var aggregateType = "TestAggregate";

		await snapshotStore.SaveSnapshotAsync(
			Snapshot.Create(aggregateId1, 3, Encoding.UTF8.GetBytes("{\"1\": true}"), aggregateType), CancellationToken.None).ConfigureAwait(true);
		await snapshotStore.SaveSnapshotAsync(
			Snapshot.Create(aggregateId2, 5, Encoding.UTF8.GetBytes("{\"2\": true}"), aggregateType), CancellationToken.None).ConfigureAwait(true);

		await snapshotStore.DeleteSnapshotsAsync(aggregateId1, aggregateType, CancellationToken.None).ConfigureAwait(true);

		var loaded1 = await snapshotStore.GetLatestSnapshotAsync(aggregateId1, aggregateType, CancellationToken.None).ConfigureAwait(true);
		var loaded2 = await snapshotStore.GetLatestSnapshotAsync(aggregateId2, aggregateType, CancellationToken.None).ConfigureAwait(true);

		loaded1.ShouldBeNull();
		_ = loaded2.ShouldNotBeNull();
		loaded2.Version.ShouldBe(5);
	}

	/// <summary>
	/// Verifies that the snapshot preserves the SnapshotId through save and load.
	/// </summary>
	[Fact]
	public async Task PreserveSnapshotIdThroughSaveAndLoad()
	{
		if (!_dockerAvailable)
		{
			return;
		}

		var snapshotStore = CreateSnapshotStore();
		var aggregateId = Guid.NewGuid().ToString();
		var aggregateType = "TestAggregate";
		var data = Encoding.UTF8.GetBytes("{\"id\": true}");

		var snapshot = Snapshot.Create(aggregateId, 1, data, aggregateType);
		var originalSnapshotId = snapshot.SnapshotId;

		await snapshotStore.SaveSnapshotAsync(snapshot, CancellationToken.None).ConfigureAwait(true);

		var loaded = await snapshotStore.GetLatestSnapshotAsync(aggregateId, aggregateType, CancellationToken.None).ConfigureAwait(true);

		_ = loaded.ShouldNotBeNull();
		loaded.SnapshotId.ShouldBe(originalSnapshotId);
	}

	private ISnapshotStore CreateSnapshotStore()
	{
		var logger = NullLogger<SqlServerSnapshotStore>.Instance;
		return new SqlServerSnapshotStore(_connectionString!, logger);
	}

	private async Task InitializeDatabaseAsync()
	{
		const string createTableSql = """
			IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='EventStoreSnapshots' AND xtype='U')
			CREATE TABLE EventStoreSnapshots (
				SnapshotId NVARCHAR(255) NOT NULL,
				AggregateId NVARCHAR(255) NOT NULL,
				AggregateType NVARCHAR(255) NOT NULL,
				Version BIGINT NOT NULL,
				Data VARBINARY(MAX) NOT NULL,
				CreatedAt DATETIME2 NOT NULL,
				PRIMARY KEY (AggregateId, AggregateType)
			)
			""";

		Console.WriteLine($"Connection string: {_connectionString}");

		await using var connection = new SqlConnection(_connectionString);
		await connection.OpenAsync().ConfigureAwait(false);
		Console.WriteLine("Database connection opened successfully");

		await using var command = new SqlCommand(createTableSql, connection);
		_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
		Console.WriteLine("EventStoreSnapshots table created successfully");
	}
}
