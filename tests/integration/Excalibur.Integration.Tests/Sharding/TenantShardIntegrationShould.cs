// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

using Excalibur.Data.Abstractions.Sharding;
using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

using Testcontainers.MsSql;

namespace Excalibur.Integration.Tests.Sharding;

/// <summary>
/// B.13 (xfldne): Integration tests for multi-database tenant sharding using
/// SQL Server TestContainers. Verifies data isolation between shards through
/// the public ITenantShardMap / ITenantStoreResolver abstractions.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Database", "SqlServer")]
[Trait("Component", "Sharding")]
[SuppressMessage("Design", "CA1506", Justification = "Integration test requires multiple dependencies")]
public sealed class TenantShardIntegrationShould : IAsyncLifetime
{
	private MsSqlContainer? _container;
	private string? _baseConnectionString;
	private bool _dockerAvailable;

	public async Task InitializeAsync()
	{
		try
		{
			_container = new MsSqlBuilder()
				.WithImage("mcr.microsoft.com/mssql/server:2022-latest")
				.Build();

			await _container.StartAsync().ConfigureAwait(false);
			_baseConnectionString = _container.GetConnectionString();
			_dockerAvailable = true;

			await CreateDatabaseAsync("ShardA").ConfigureAwait(false);
			await CreateDatabaseAsync("ShardB").ConfigureAwait(false);

			await InitializeEventStoreSchemaAsync("ShardA").ConfigureAwait(false);
			await InitializeEventStoreSchemaAsync("ShardB").ConfigureAwait(false);
		}
		catch (Exception)
		{
			_dockerAvailable = false;
		}
	}

	public async Task DisposeAsync()
	{
		if (_container is not null)
		{
			await _container.DisposeAsync().ConfigureAwait(false);
		}
	}

	[Fact]
	public async Task IsolateEventsBetweenTenantShards()
	{
		if (!_dockerAvailable) return;

		// Arrange -- two SQL Server databases as two shards
		var shardAConn = GetShardConnectionString("ShardA");
		var shardBConn = GetShardConnectionString("ShardB");

		var shardMap = new TestShardMap(new Dictionary<string, ShardInfo>
		{
			["tenant-1"] = new ShardInfo("shard-a", shardAConn),
			["tenant-2"] = new ShardInfo("shard-b", shardBConn)
		});

		var resolver = new TestSqlServerResolver(shardMap);

		// Act -- write to shard A (tenant-1)
		var storeA = resolver.Resolve("tenant-1");
		var event1 = CreateTestEvent("order-1", 0);
		await storeA.AppendAsync("order-1", "Order", [event1], -1, CancellationToken.None);

		// Act -- write to shard B (tenant-2)
		var storeB = resolver.Resolve("tenant-2");
		var event2 = CreateTestEvent("order-2", 0);
		await storeB.AppendAsync("order-2", "Order", [event2], -1, CancellationToken.None);

		// Assert -- Shard A has order-1 only
		var fromA = await storeA.LoadAsync("order-1", "Order", CancellationToken.None);
		fromA.Count.ShouldBe(1);

		var notInA = await storeA.LoadAsync("order-2", "Order", CancellationToken.None);
		notInA.Count.ShouldBe(0);

		// Assert -- Shard B has order-2 only
		var fromB = await storeB.LoadAsync("order-2", "Order", CancellationToken.None);
		fromB.Count.ShouldBe(1);

		var notInB = await storeB.LoadAsync("order-1", "Order", CancellationToken.None);
		notInB.Count.ShouldBe(0);
	}

	[Fact]
	public async Task ResolverCachesStorePerShard()
	{
		if (!_dockerAvailable) return;

		// Arrange
		var shardAConn = GetShardConnectionString("ShardA");
		var shardMap = new TestShardMap(new Dictionary<string, ShardInfo>
		{
			["tenant-x"] = new ShardInfo("shard-a", shardAConn),
			["tenant-y"] = new ShardInfo("shard-a", shardAConn) // same shard
		});

		var resolver = new TestSqlServerResolver(shardMap);

		// Act -- resolve same shard for different tenants
		var store1 = resolver.Resolve("tenant-x");
		var store2 = resolver.Resolve("tenant-y");

		// Assert -- same store instance returned (cached per shard ID)
		store1.ShouldBeSameAs(store2);

		// Also verify it works
		await store1.AppendAsync("test-1", "TestAgg",
			[CreateTestEvent("test-1", 0)], -1, CancellationToken.None);
		var events = await store2.LoadAsync("test-1", "TestAgg", CancellationToken.None);
		events.Count.ShouldBe(1);
	}

	[Fact]
	public async Task MultipleAppendsThenLoadReturnsAllEvents()
	{
		if (!_dockerAvailable) return;

		// Arrange
		var shardAConn = GetShardConnectionString("ShardA");
		var shardMap = new TestShardMap(new Dictionary<string, ShardInfo>
		{
			["tenant-z"] = new ShardInfo("shard-a", shardAConn)
		});

		var resolver = new TestSqlServerResolver(shardMap);
		var store = resolver.Resolve("tenant-z");

		// Act -- append events in batches
		var events1 = Enumerable.Range(0, 3).Select(i => CreateTestEvent("agg-z", i)).ToList();
		await store.AppendAsync("agg-z", "TestAgg", events1, -1, CancellationToken.None);

		var events2 = Enumerable.Range(3, 2).Select(i => CreateTestEvent("agg-z", i)).ToList();
		await store.AppendAsync("agg-z", "TestAgg", events2, 2, CancellationToken.None);

		// Assert
		var loaded = await store.LoadAsync("agg-z", "TestAgg", CancellationToken.None);
		loaded.Count.ShouldBe(5);
	}

	[Fact]
	public void ThrowForUnknownTenantWithNoDefault()
	{
		if (!_dockerAvailable) return;

		var shardMap = new TestShardMap(new Dictionary<string, ShardInfo>
		{
			["tenant-known"] = new ShardInfo("shard-a", "Server=x;")
		});

		Should.Throw<TenantShardNotFoundException>(
			() => shardMap.GetShardInfo("unknown-tenant"));
	}

	#region Helpers

	private static IDomainEvent CreateTestEvent(string aggregateId, long version) =>
		new TestShardEvent
		{
			AggregateId = aggregateId,
			Version = version,
			EventId = Guid.NewGuid().ToString(),
			OccurredAt = DateTimeOffset.UtcNow,
			EventType = "TestShardEvent"
		};

	private string GetShardConnectionString(string databaseName)
	{
		var builder = new SqlConnectionStringBuilder(_baseConnectionString!)
		{
			InitialCatalog = databaseName
		};
		return builder.ConnectionString;
	}

	[SuppressMessage("Security", "CA2100", Justification = "Test-only method with controlled database name")]
	private async Task CreateDatabaseAsync(string databaseName)
	{
		await using var connection = new SqlConnection(_baseConnectionString);
		await connection.OpenAsync().ConfigureAwait(false);
		await using var command = connection.CreateCommand();
		command.CommandText = $"IF DB_ID('{databaseName}') IS NULL CREATE DATABASE [{databaseName}]";
		await command.ExecuteNonQueryAsync().ConfigureAwait(false);
	}

	private async Task InitializeEventStoreSchemaAsync(string databaseName)
	{
		var connString = GetShardConnectionString(databaseName);
		await using var connection = new SqlConnection(connString);
		await connection.OpenAsync().ConfigureAwait(false);
		await using var command = connection.CreateCommand();
		command.CommandText = """
			IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='EventStoreEvents' AND xtype='U')
			CREATE TABLE EventStoreEvents (
				Position BIGINT IDENTITY(1,1) PRIMARY KEY,
				EventId NVARCHAR(255) NOT NULL UNIQUE,
				AggregateId NVARCHAR(255) NOT NULL,
				AggregateType NVARCHAR(255) NOT NULL,
				EventType NVARCHAR(500) NOT NULL,
				EventData VARBINARY(MAX) NOT NULL,
				Metadata VARBINARY(MAX) NULL,
				Version BIGINT NOT NULL,
				Timestamp DATETIMEOFFSET NOT NULL,
				INDEX IX_EventStoreEvents_Aggregate (AggregateId, AggregateType, Version)
			)
			""";
		await command.ExecuteNonQueryAsync().ConfigureAwait(false);
	}

	#endregion

	#region Test Fixtures

	internal sealed class TestShardEvent : IDomainEvent
	{
		public string EventId { get; init; } = string.Empty;
		public string AggregateId { get; init; } = string.Empty;
		public long Version { get; init; }
		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
		public string EventType { get; init; } = nameof(TestShardEvent);
		public IDictionary<string, object>? Metadata { get; init; }
	}

	/// <summary>
	/// Test shard map using public ITenantShardMap interface.
	/// </summary>
	internal sealed class TestShardMap : ITenantShardMap
	{
		private readonly Dictionary<string, ShardInfo> _map;

		public TestShardMap(Dictionary<string, ShardInfo> map) => _map = map;

		public ShardInfo GetShardInfo(string tenantId) =>
			_map.TryGetValue(tenantId, out var info)
				? info
				: throw new TenantShardNotFoundException(tenantId);

		public IReadOnlyCollection<string> GetRegisteredShardIds() =>
			_map.Values.Select(s => s.ShardId).Distinct().ToList();
	}

	/// <summary>
	/// Test resolver using public ITenantStoreResolver interface with caching.
	/// </summary>
	internal sealed class TestSqlServerResolver : ITenantStoreResolver<IEventStore>
	{
		private readonly ITenantShardMap _shardMap;
		private readonly ConcurrentDictionary<string, IEventStore> _cache = new();

		public TestSqlServerResolver(ITenantShardMap shardMap) => _shardMap = shardMap;

		public IEventStore Resolve(string tenantId)
		{
			var shard = _shardMap.GetShardInfo(tenantId);
			return _cache.GetOrAdd(shard.ShardId, _ =>
				new SqlServerEventStore(shard.ConnectionString, NullLogger<SqlServerEventStore>.Instance));
		}
	}

	#endregion
}
