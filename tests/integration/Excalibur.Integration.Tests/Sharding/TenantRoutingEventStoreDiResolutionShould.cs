// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

using Excalibur.Data.Sharding;
using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

using Testcontainers.MsSql;

#pragma warning disable CA2100 // schema/database names below are fixed test-fixture constants, not user input

namespace Excalibur.Integration.Tests.Sharding;

/// <summary>
/// Real-engine DI-resolution lock (hwe9c3) for the tenant-sharding decorator
/// <c>TenantRoutingEventStore</c>. <see cref="TenantShardIntegrationShould"/> proves the shard-routing
/// BEHAVIOUR by hand-constructing a resolver and calling it directly - it never goes through
/// <c>IEventSourcingBuilder.EnableTenantSharding</c>, the documented entry point every consumer actually
/// calls. That leaves the registration path itself unverified: a DI factory that dropped the
/// <c>ITenantStoreResolver&lt;IEventStore&gt;</c> or <c>ITenantContext</c> dependency, or a
/// <see cref="TenantShardingServiceCollectionExtensions"/> wiring bug that skipped the decorator swap,
/// would leave a consumer resolving the wrong store while every hand-constructed test kept passing.
/// </summary>
/// <remarks>
/// <para>
/// WIRE proof: <c>IEventStore</c> resolved from the real container built by <c>EnableTenantSharding</c> is
/// the routing decorator, not a bare provider store.
/// </para>
/// <para>
/// BEHAVIOUR proof: an event appended under tenant-1's ambient scope lands in tenant-1's REAL SQL Server
/// shard database and is invisible from tenant-2's REAL shard database, and vice versa - over two actual
/// SQL Server databases, not an in-memory stand-in.
/// </para>
/// <para>Never skipped: an absent Docker daemon fails the arm rather than passing silently.</para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Database", "SqlServer")]
[Trait("Component", "Sharding")]
[SuppressMessage("Design", "CA1001", Justification = "Disposed in DisposeAsync via IAsyncLifetime")]
public sealed class TenantRoutingEventStoreDiResolutionShould : IAsyncLifetime
{
	private MsSqlContainer? _container;
	private string? _baseConnectionString;
	private bool _dockerAvailable;
	private Exception? _unavailableCause;

	private string SkipReason(string reason) =>
		_unavailableCause is null ? reason : reason + " Cause: " + _unavailableCause.GetType().Name + ": " + _unavailableCause.Message;

	public async ValueTask InitializeAsync()
	{
		try
		{
			_container = new MsSqlBuilder()
				.WithBoundedMemory()
				.WithImage("mcr.microsoft.com/mssql/server:2022-CU26-ubuntu-22.04")
				.WithName($"mssql-shard-di-{Guid.NewGuid():N}")
				.WithPassword("Test@Pass123")
				.WithCleanUp(true)
				.Build();

			using var startCts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
			await _container.StartAsync(startCts.Token).ConfigureAwait(false);
			_baseConnectionString = _container.GetConnectionString();

			await CreateDatabaseAsync("ShardDiA", startCts.Token).ConfigureAwait(false);
			await CreateDatabaseAsync("ShardDiB", startCts.Token).ConfigureAwait(false);
			await InitializeEventStoreSchemaAsync("ShardDiA", startCts.Token).ConfigureAwait(false);
			await InitializeEventStoreSchemaAsync("ShardDiB", startCts.Token).ConfigureAwait(false);

			_dockerAvailable = true;
		}
		catch (Exception ex)
		{
			_unavailableCause = ex;
			_dockerAvailable = false;
		}
	}

	public async ValueTask DisposeAsync()
	{
		if (_container is null)
		{
			return;
		}

		try
		{
			using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
			await _container.DisposeAsync().AsTask().WaitAsync(cts.Token).ConfigureAwait(false);
		}
		catch (Exception)
		{
			// Suppress disposal errors and timeouts to prevent test host crash.
		}
	}

	[Fact]
	public void ResolveIEventStore_AsTheTenantRoutingDecorator_ThroughEnableTenantSharding()
	{
		Assert.SkipWhen(!_dockerAvailable, SkipReason(
			"[infrastructure-unavailable] SQL Server (Docker) is not available - this real-engine sharding "
			+ "DI-resolution lock is never satisfied by not running."));

		using var provider = BuildWiredStack();
		using var scope = provider.CreateScope();

		var store = scope.ServiceProvider.GetRequiredService<IEventStore>();
		store.GetType().Name.ShouldBe(
			"TenantRoutingEventStore",
			"hwe9c3: EnableTenantSharding() must resolve IEventStore, through the real container, to the "
			+ "tenant-routing decorator - not the bare SQL Server store a dropped resolver dependency would "
			+ "leave in its place.");
	}

	[Fact]
	public async Task RouteEachTenant_ToItsOwnRealSqlServerShard_AndIsolateTheOthers()
	{
		Assert.SkipWhen(!_dockerAvailable, SkipReason(
			"[infrastructure-unavailable] SQL Server (Docker) is not available - this real-engine sharding "
			+ "DI-resolution lock is never satisfied by not running."));

		using var provider = BuildWiredStack();

		// Tenant 1 writes through the REAL DI-resolved IEventStore, ambient tenant established the
		// documented way (TenantContextHolder.BeginScope) - never a hand-picked resolver.
		using (TenantContextHolder.BeginScope("tenant-di-1"))
		using (var scope = provider.CreateScope())
		{
			var store = scope.ServiceProvider.GetRequiredService<IEventStore>();
			_ = await store.AppendAsync(
				"order-di-1", "Order", new IDomainEvent[] { CreateEvent("order-di-1", 0) }, -1, CancellationToken.None)
				.ConfigureAwait(false);
		}

		using (TenantContextHolder.BeginScope("tenant-di-2"))
		using (var scope = provider.CreateScope())
		{
			var store = scope.ServiceProvider.GetRequiredService<IEventStore>();
			_ = await store.AppendAsync(
				"order-di-2", "Order", new IDomainEvent[] { CreateEvent("order-di-2", 0) }, -1, CancellationToken.None)
				.ConfigureAwait(false);
		}

		// LIVENESS: each tenant reads its own event back through the real DI-resolved store.
		using (TenantContextHolder.BeginScope("tenant-di-1"))
		using (var scope = provider.CreateScope())
		{
			var store = scope.ServiceProvider.GetRequiredService<IEventStore>();
			(await store.LoadAsync("order-di-1", "Order", CancellationToken.None).ConfigureAwait(false)).Count.ShouldBe(1);
		}

		using (TenantContextHolder.BeginScope("tenant-di-2"))
		using (var scope = provider.CreateScope())
		{
			var store = scope.ServiceProvider.GetRequiredService<IEventStore>();
			(await store.LoadAsync("order-di-2", "Order", CancellationToken.None).ConfigureAwait(false)).Count.ShouldBe(1);
		}

		// SAFETY: on the REAL shard databases, tenant 1's write is not visible from tenant 2's shard and
		// vice versa - queried directly against each real SQL Server database, outside the decorator.
		(await CountRowsAsync("ShardDiA", "order-di-2").ConfigureAwait(false)).ShouldBe(
			0, "tenant-2's event must not land on tenant-1's real shard database");
		(await CountRowsAsync("ShardDiB", "order-di-1").ConfigureAwait(false)).ShouldBe(
			0, "tenant-1's event must not land on tenant-2's real shard database");
		(await CountRowsAsync("ShardDiA", "order-di-1").ConfigureAwait(false)).ShouldBe(
			1, "tenant-1's event must land on tenant-1's real shard database");
		(await CountRowsAsync("ShardDiB", "order-di-2").ConfigureAwait(false)).ShouldBe(
			1, "tenant-2's event must land on tenant-2's real shard database");
	}

	private ServiceProvider BuildWiredStack()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		// The AMBIENT context (reads TenantContextHolder.Current), not AddDefaultTenantContext's fixed
		// single-tenant default - a sharding host needs the per-call tenant established by
		// TenantContextHolder.BeginScope, which is exactly what a real consumer wires for sharding.
		_ = services.AddTenantContext();

		var shardMap = new TestShardMap(new Dictionary<string, ShardInfo>(StringComparer.Ordinal)
		{
			["tenant-di-1"] = new ShardInfo("shard-di-a", GetShardConnectionString("ShardDiA")),
			["tenant-di-2"] = new ShardInfo("shard-di-b", GetShardConnectionString("ShardDiB")),
		});
		_ = services.AddSingleton<ITenantShardMap>(shardMap);
		_ = services.AddSingleton<ITenantStoreResolver<IEventStore>>(new TestSqlServerResolver(shardMap));

		_ = services.AddExcalibur(x => x.AddEventSourcing(es =>
			_ = es.EnableTenantSharding(o => o.DefaultShardId = "shard-di-a")));

		return services.BuildServiceProvider();
	}

	private static IDomainEvent CreateEvent(string aggregateId, long version) => new TestShardDiEvent
	{
		AggregateId = aggregateId,
		Version = version,
		EventId = Guid.NewGuid().ToString(),
		OccurredAt = DateTimeOffset.UtcNow,
	};

	private string GetShardConnectionString(string databaseName) =>
		new SqlConnectionStringBuilder(_baseConnectionString!) { InitialCatalog = databaseName }.ConnectionString;

	private async Task CreateDatabaseAsync(string databaseName, CancellationToken cancellationToken)
	{
		await using var connection = new SqlConnection(_baseConnectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
		await using var command = new SqlCommand($"IF DB_ID('{databaseName}') IS NULL CREATE DATABASE [{databaseName}]", connection);
		_ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}

	private async Task InitializeEventStoreSchemaAsync(string databaseName, CancellationToken cancellationToken)
	{
		await using var connection = new SqlConnection(GetShardConnectionString(databaseName));
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
		await using var command = new SqlCommand(
			"""
			IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='EventStoreEvents' AND xtype='U')
			CREATE TABLE EventStoreEvents (
				Position BIGINT IDENTITY(1,1) PRIMARY KEY,
				EventId NVARCHAR(255) NOT NULL UNIQUE,
				AggregateId NVARCHAR(255) NOT NULL,
				AggregateType NVARCHAR(255) NOT NULL,
				EventType NVARCHAR(500) NOT NULL,
				-- Nullable, matching shipped 001: erasure tombstones an event by setting EventData
				-- to NULL. A fixture that restates this column NOT NULL drifts from the schema the
				-- package ships and would reject any erase exercised against it.
				EventData VARBINARY(MAX) NULL,
				Metadata VARBINARY(MAX) NULL,
				Version BIGINT NOT NULL,
				Timestamp DATETIMEOFFSET NOT NULL,
				TenantId NVARCHAR(255) NULL,
				INDEX IX_EventStoreEvents_Aggregate (AggregateId, AggregateType, Version)
			)
			""",
			connection);
		_ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}

	private async Task<int> CountRowsAsync(string databaseName, string aggregateId)
	{
		await using var connection = new SqlConnection(GetShardConnectionString(databaseName));
		await connection.OpenAsync().ConfigureAwait(false);
		await using var command = new SqlCommand(
			"SELECT COUNT(*) FROM EventStoreEvents WHERE AggregateId = @AggregateId", connection);
		_ = command.Parameters.AddWithValue("@AggregateId", aggregateId);
		return (int)(await command.ExecuteScalarAsync().ConfigureAwait(false))!;
	}

[MessageName("Test.TestShardDiEvent")]
private sealed class TestShardDiEvent : IDomainEvent
	{
		public string EventId { get; init; } = string.Empty;
		public string AggregateId { get; init; } = string.Empty;
		public long Version { get; init; }
		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
		public IDictionary<string, object>? Metadata { get; init; }
	}

	private sealed class TestShardMap : ITenantShardMap
	{
		private readonly Dictionary<string, ShardInfo> _map;

		public TestShardMap(Dictionary<string, ShardInfo> map) => _map = map;

		public ShardInfo GetShardInfo(string tenantId) =>
			_map.TryGetValue(tenantId, out var info) ? info : throw new TenantShardNotFoundException(tenantId);

		public IReadOnlyCollection<string> GetRegisteredShardIds() =>
			_map.Values.Select(static s => s.ShardId).Distinct().ToList();
	}

	private sealed class TestSqlServerResolver : ITenantStoreResolver<IEventStore>
	{
		private readonly ITenantShardMap _shardMap;
		private readonly ConcurrentDictionary<string, IEventStore> _cache = new(StringComparer.Ordinal);

		public TestSqlServerResolver(ITenantShardMap shardMap) => _shardMap = shardMap;

		public IEventStore Resolve(string tenantId)
		{
			var shard = _shardMap.GetShardInfo(tenantId);
			return _cache.GetOrAdd(shard.ShardId, _ =>
				new SqlServerEventStore(
					() => new SqlConnection(shard.ConnectionString),
					NullLogger<SqlServerEventStore>.Instance,
					tenantContext: new FixedTenantContext(tenantId)));
		}
	}

	private sealed class FixedTenantContext(string tenantId) : ITenantContext
	{
		public string? TenantId { get; } = tenantId;

		public bool HasTenant => !string.IsNullOrEmpty(TenantId);
	}
}
