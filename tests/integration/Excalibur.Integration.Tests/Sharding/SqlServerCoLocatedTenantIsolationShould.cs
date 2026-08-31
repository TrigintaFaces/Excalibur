// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using Tests.Shared.Fixtures;

using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

using Testcontainers.MsSql;

namespace Excalibur.Integration.Tests.Sharding;

// Independent real-infra regression lock (author != implementer) for mw6g91 ruling (b) — the same-shard
// cross-tenant isolation guarantee. THIS IS THE END-TO-END PROOF of the mandatory defense-in-depth fix.
//
// THE LEAK IT LOCKS. Tenant event-store resolvers cache the store PER ShardId, and ITenantShardMap is a
// many-tenants -> one-shard map. So two tenants co-located on the SAME shard share ONE store instance and ONE
// database connection. Isolation "by connection/shard" therefore does NOT separate them — only a per-ROW
// tenant predicate does. If the store dropped the `AND TenantId = @TenantId` predicate (the pre-fix widening
// ternary), tenant A's scoped read would return tenant B's rows from the same shard: a cross-tenant leak.
//
// The store reads the tenant from an AMBIENT ITenantContext PER-QUERY (never snapshot at ctor, per SA's
// resolver-threading ruling), so this test drives it exactly as production does: one shared store, the ambient
// tenant switched via TenantContextHolder.BeginScope around each operation. Same aggregate id under two tenants
// proves the tenant_id column disambiguates them.
//
// SAFETY + LIVENESS (testing-patterns §3): a store that returned NOTHING would pass a safety-only isolation
// assertion. So:
//   - SAFETY:   tenant A's scoped read does NOT return tenant B's rows (from the same shard).
//   - LIVENESS: tenant A's scoped read DOES return tenant A's own rows (isolation must not mean "sees nothing").
//
// NON-SKIPPED real infra (verify-against-real-infra-not-mock): Docker availability is asserted, not silently
// skipped — a lock that never runs cannot catch the leak it exists to catch.
[Trait("Category", "Integration")]
[Trait("Database", "SqlServer")]
[Trait("Component", "Sharding")]
[SuppressMessage("Design", "CA1506", Justification = "Integration test requires multiple dependencies")]
public sealed class SqlServerCoLocatedTenantIsolationShould : IAsyncLifetime
{
	private const string TenantA = "tenant-a";
	private const string TenantB = "tenant-b";
	private const string SharedAggregateId = "shared-order-1";
	private const string AggregateType = "Order";

	private MsSqlContainer? _container;
	private string? _connectionString;
	private bool _dockerAvailable;

	public async ValueTask InitializeAsync()
	{
		try
		{
			_container = new MsSqlBuilder()
				.WithBoundedMemory()
				.WithImage("mcr.microsoft.com/mssql/server:2022-CU26-ubuntu-22.04")
				.Build();

			await _container.StartAsync().ConfigureAwait(false);
			_connectionString = _container.GetConnectionString();
			_dockerAvailable = true;

			await InitializeTenantColumnedSchemaAsync().ConfigureAwait(false);
		}
		catch (Exception)
		{
			_dockerAvailable = false;
		}
	}

	public async ValueTask DisposeAsync()
	{
		try
		{
			if (_container is not null)
			{
				using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
				await _container.DisposeAsync().AsTask().WaitAsync(cts.Token).ConfigureAwait(false);
			}
		}
		catch (Exception)
		{
			// Suppress disposal errors and timeouts to prevent test host crash.
		}
	}

	[Fact]
	public async Task NotLeakRowsBetweenTwoTenantsCoLocatedOnOneShard()
	{
		_dockerAvailable.ShouldBeTrue(
			"Docker/SQL Server must be available — this real-infra isolation lock is NEVER skipped. A skipped " +
			"cross-tenant-leak test provides no protection at all.");

		// ONE store = ONE shard = ONE connection, shared by both tenants. The ambient tenant, switched per
		// operation, is the ONLY thing that separates them — exactly the production shared-shard scenario.
		var ambient = new AmbientHolderTenantContext();
		var store = new SqlServerEventStore(
			() => new SqlConnection(_connectionString),
			NullLogger<SqlServerEventStore>.Instance,
			tenantContext: ambient);

		// Tenant A writes the aggregate.
		using (TenantContextHolder.BeginScope(TenantA))
		{
			await store.AppendAsync(SharedAggregateId, AggregateType, [Event("A-evt")], -1, CancellationToken.None)
				.ConfigureAwait(false);
		}

		// Tenant B writes the SAME aggregate id — a distinct tenant_id row on the SAME shard. (If isolation were
		// broken, B's tenant-scoped version check would see A's row and this would concurrency-conflict.)
		using (TenantContextHolder.BeginScope(TenantB))
		{
			await store.AppendAsync(SharedAggregateId, AggregateType, [Event("B-evt")], -1, CancellationToken.None)
				.ConfigureAwait(false);
		}

		// SAFETY + LIVENESS for tenant A: sees exactly its own one event, never tenant B's.
		using (TenantContextHolder.BeginScope(TenantA))
		{
			var loadedForA = await store.LoadAsync(SharedAggregateId, AggregateType, CancellationToken.None)
				.ConfigureAwait(false);

			loadedForA.Count.ShouldBe(
				1,
				"Tenant A's scoped read must return EXACTLY its own one event on the shared shard — not two. If it " +
				"returns 2, the store dropped the tenant_id row predicate and A is reading B's rows: the same-shard " +
				"cross-tenant leak mw6g91 (b) exists to prevent. If it returns 0, isolation collapsed into 'sees " +
				"nothing' (a safety-only lock would pass that — this liveness half catches it).");
		}

		// Symmetric SAFETY + LIVENESS for tenant B.
		using (TenantContextHolder.BeginScope(TenantB))
		{
			var loadedForB = await store.LoadAsync(SharedAggregateId, AggregateType, CancellationToken.None)
				.ConfigureAwait(false);

			loadedForB.Count.ShouldBe(
				1,
				"Tenant B's scoped read must return EXACTLY its own one event on the shared shard.");
		}
	}

	private static IDomainEvent Event(string marker) =>
		new CoLocatedTenantEvent
		{
			AggregateId = SharedAggregateId,
			Version = 0,
			EventId = Guid.NewGuid().ToString(),
			OccurredAt = DateTimeOffset.UtcNow,
			EventType = marker,
		};

	private async Task InitializeTenantColumnedSchemaAsync()
	{
		await using var connection = new SqlConnection(_connectionString);
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
			""";
		await command.ExecuteNonQueryAsync().ConfigureAwait(false);
	}

	// An ambient ITenantContext built only on the PUBLIC TenantContextHolder — mirrors production's
	// AmbientTenantContext (internal) without needing InternalsVisibleTo. Reads the tenant PER access, so the
	// shared store scopes each query to whichever tenant's BeginScope is active.
	private sealed class AmbientHolderTenantContext : ITenantContext
	{
		public string? TenantId => TenantContextHolder.Current;

		public bool HasTenant => !string.IsNullOrEmpty(TenantContextHolder.Current);
	}

	private sealed class CoLocatedTenantEvent : IDomainEvent
	{
		public string EventId { get; init; } = string.Empty;
		public string AggregateId { get; init; } = string.Empty;
		public long Version { get; init; }
		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
		public string EventType { get; init; } = nameof(CoLocatedTenantEvent);
		public IDictionary<string, object>? Metadata { get; init; }
	}
}
