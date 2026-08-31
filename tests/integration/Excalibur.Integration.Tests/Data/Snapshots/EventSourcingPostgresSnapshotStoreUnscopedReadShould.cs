// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using Excalibur.Dispatch;
using Excalibur.Domain.Model;
using Excalibur.EventSourcing;

using Microsoft.Extensions.Logging.Abstractions;

using Npgsql;

using Shouldly;

using Xunit;

#pragma warning disable CA1812 // Internal class is never instantiated

namespace Excalibur.Integration.Tests.Data.Snapshots;

/// <summary>
/// Binds the tenancy contract of the canonical <c>Excalibur.EventSourcing.Postgres.PostgresSnapshotStore</c>
/// across the tenant boundary — coverage the general conformance suite does not provide.
/// </summary>
/// <remarks>
/// <para>
/// The general conformance suite writes and reads within a single tenant state; it cannot detect a store
/// that ignores the scope entirely. These arms cross the tenant BOUNDARY (write under one scope, read
/// under another), which is where a missing read predicate does damage. This is the topology in which a
/// live cross-tenant read survived a green suite in a sibling provider.
/// </para>
/// <para>
/// The arms cross the tenancy BOUNDARY rather than merely visiting a state. A test that writes and reads
/// in the same state cannot detect a store that ignores the scope entirely, which is how a live
/// cross-tenant read survived a green suite in a sibling provider. Unscoped is reachable only by
/// constructing the store WITHOUT a context: from a context-wired store an absent tenant throws
/// (fail-closed) rather than becoming <c>TenantScope.Untenanted</c>. So the two stores here are not a
/// contrivance — one database serving a tenant-scoped host and an unscoped host is a supported
/// deployment, and it is the exact topology in which a missing predicate does damage.
/// </para>
/// <para>
/// Expected GREEN: this store writes <c>COALESCE(@TenantId, '')</c> and keys
/// <c>ON CONFLICT (aggregate_id, aggregate_type, tenant_id)</c> — unconditional, tenant in the key. The
/// arms exist so that stays true by test rather than by inspection; the identical assertion went RED
/// against a sibling store whose read predicate was conditional, and returned another tenant's row.
/// </para>
/// </remarks>
[Collection(PostgresSnapshotStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Postgres")]
public sealed class EventSourcingPostgresSnapshotStoreUnscopedReadShould
	: IClassFixture<PostgresSnapshotStoreContainerFixture>
{
	private const string TenantA = "tenant-a";
	private const string TenantB = "tenant-b";
	private const string AggregateType = "TestAggregate";

	private readonly PostgresSnapshotStoreContainerFixture _fixture;

	/// <summary>
	/// Initializes a new instance of the <see cref="EventSourcingPostgresSnapshotStoreUnscopedReadShould"/> class.
	/// </summary>
	/// <param name="fixture">The Postgres container fixture.</param>
	public EventSourcingPostgresSnapshotStoreUnscopedReadShould(PostgresSnapshotStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <summary>
	/// SAFETY. A store constructed without a tenant context must not read a row written under a tenant.
	/// </summary>
	[Fact]
	public async Task Not_Return_A_Tenants_Snapshot_To_An_Unscoped_Reader()
	{
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		var aggregateId = Guid.NewGuid().ToString();

		await using var dataSource = NpgsqlDataSource.Create(_fixture.ConnectionString);
		await EnsureStoreTableAsync(dataSource).ConfigureAwait(false);

		var scopedStore = CreateStore(dataSource, tenantScoped: true);
		using (TenantContextHolder.BeginScope(TenantA))
		{
			await scopedStore.SaveSnapshotAsync(
				CreateSnapshot(aggregateId, 1, "tenant-a-data", TenantA),
				CancellationToken.None).ConfigureAwait(false);
		}

		var unscopedStore = CreateStore(dataSource, tenantScoped: false);
		var leaked = await unscopedStore.GetLatestSnapshotAsync(
			aggregateId,
			AggregateType,
			CancellationToken.None).ConfigureAwait(false);

		leaked.ShouldBeNull(
			"an unscoped reader must not receive a row written under a tenant; the write puts the tenant " +
			"in the key, so the read must filter on it unconditionally or it matches any tenant's row. " +
			"This arm was authored while the leak was live and is GREEN now that the read predicate is " +
			"unconditional; it must NOT be weakened — mutating the predicate back to a conditional form " +
			"returns tenant A's row to the unscoped reader and this arm goes RED, which is the whole point " +
			"of keeping it");
	}

	/// <summary>
	/// LIVENESS. Without this, a store whose unscoped read returned nothing at all would satisfy the
	/// arm above while having removed single-tenant support entirely.
	/// </summary>
	[Fact]
	public async Task Still_Read_Back_Its_Own_Row_When_Unscoped()
	{
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		var aggregateId = Guid.NewGuid().ToString();

		await using var dataSource = NpgsqlDataSource.Create(_fixture.ConnectionString);
		await EnsureStoreTableAsync(dataSource).ConfigureAwait(false);
		var store = CreateStore(dataSource, tenantScoped: false);

		await store.SaveSnapshotAsync(
			CreateSnapshot(aggregateId, 1, "unscoped-data", tenantId: null),
			CancellationToken.None).ConfigureAwait(false);

		var loaded = await store.GetLatestSnapshotAsync(
			aggregateId,
			AggregateType,
			CancellationToken.None).ConfigureAwait(false);

		_ = loaded.ShouldNotBeNull("the single-tenant path must remain fully usable");
		Encoding.UTF8.GetString(loaded.Data.ToArray()).ShouldBe("unscoped-data");
	}

	/// <summary>
	/// SAFETY, mirror direction. The empty-string tenant is a key value, not a wildcard.
	/// </summary>
	[Fact]
	public async Task Not_Return_An_Unscoped_Row_To_A_Tenant_Reader()
	{
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		var aggregateId = Guid.NewGuid().ToString();

		await using var dataSource = NpgsqlDataSource.Create(_fixture.ConnectionString);
		await EnsureStoreTableAsync(dataSource).ConfigureAwait(false);

		var unscopedStore = CreateStore(dataSource, tenantScoped: false);
		await unscopedStore.SaveSnapshotAsync(
			CreateSnapshot(aggregateId, 1, "unscoped-data", tenantId: null),
			CancellationToken.None).ConfigureAwait(false);

		var scopedStore = CreateStore(dataSource, tenantScoped: true);
		ISnapshot? leaked;
		using (TenantContextHolder.BeginScope(TenantA))
		{
			leaked = await scopedStore.GetLatestSnapshotAsync(
				aggregateId,
				AggregateType,
				CancellationToken.None).ConfigureAwait(false);
		}

		leaked.ShouldBeNull(
			"the empty-string tenant is a key value, not a wildcard — a scoped reader must not match it");
	}

	/// <summary>
	/// SAFETY, WRITE AXIS. A second tenant saving at the same aggregate id must not overwrite the
	/// first tenant's row.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Every other tenant-isolation arm in this repository asks what a READER receives; none asks
	/// what a WRITER destroys. They are different questions: a foreign write satisfies the
	/// concurrency check on the way past, replaces the victim's payload, and leaves nothing a
	/// read-side assertion could notice. The victim's data is gone rather than exposed.
	/// </para>
	/// <para>
	/// It asserts the victim's ROW, not that the second save threw — "the save threw" is satisfied by
	/// a store that throws on everything, so it cannot separate a working guard from a broken store.
	/// </para>
	/// <para>
	/// B's version deliberately exceeds A's. This store's <c>DO UPDATE</c> is unconditional, so equal
	/// versions would work HERE — but the Oracle sibling updates under
	/// <c>WHERE :VersionCmp &gt; target.VERSION</c>, where equal versions make the update a no-op and
	/// the arm passes for a reason unrelated to tenancy. Verified by mutation on that provider. The
	/// higher version is correct on every provider and cannot go silently vacuous if a guard is added
	/// here later.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task Not_Let_One_Tenants_Save_Overwrite_Another_Tenants_Snapshot()
	{
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		var sharedAggregateId = Guid.NewGuid().ToString();

		await using var dataSource = NpgsqlDataSource.Create(_fixture.ConnectionString);
		await EnsureStoreTableAsync(dataSource).ConfigureAwait(false);
		var store = CreateStore(dataSource, tenantScoped: true);

		using (TenantContextHolder.BeginScope(TenantA))
		{
			await store.SaveSnapshotAsync(
				CreateSnapshot(sharedAggregateId, 1, "tenant-a-original", TenantA),
				CancellationToken.None).ConfigureAwait(false);
		}

		using (TenantContextHolder.BeginScope(TenantB))
		{
			await store.SaveSnapshotAsync(
				CreateSnapshot(sharedAggregateId, 2, "tenant-b-write", TenantB),
				CancellationToken.None).ConfigureAwait(false);
		}

		ISnapshot? tenantAsRow;
		using (TenantContextHolder.BeginScope(TenantA))
		{
			tenantAsRow = await store.GetLatestSnapshotAsync(
				sharedAggregateId,
				AggregateType,
				CancellationToken.None).ConfigureAwait(false);
		}

		_ = tenantAsRow.ShouldNotBeNull(
			"tenant A's snapshot must still EXIST after tenant B saved at the same aggregate id");
		Encoding.UTF8.GetString(tenantAsRow.Data.ToArray()).ShouldBe(
			"tenant-a-original",
			"tenant A's snapshot must be UNCHANGED. If it carries tenant B's payload, a foreign write " +
			"reached it — an overwrite, not a disclosure: A's data is gone and nothing recorded it");
	}

	/// <summary>
	/// LIVENESS for the arm above — a store that refused the second tenant's save outright would
	/// satisfy the safety assertion while having broken multi-tenant writes entirely.
	/// </summary>
	/// <remarks>
	/// The pairing is on the SAME path and row-class as its safety partner. A liveness arm on a
	/// different path is not a pair; the combination would pass for a store that does nothing.
	/// </remarks>
	[Fact]
	public async Task Still_Let_The_Second_Tenant_Save_Its_Own_Row()
	{
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		var sharedAggregateId = Guid.NewGuid().ToString();

		await using var dataSource = NpgsqlDataSource.Create(_fixture.ConnectionString);
		await EnsureStoreTableAsync(dataSource).ConfigureAwait(false);
		var store = CreateStore(dataSource, tenantScoped: true);

		using (TenantContextHolder.BeginScope(TenantA))
		{
			await store.SaveSnapshotAsync(
				CreateSnapshot(sharedAggregateId, 1, "tenant-a-original", TenantA),
				CancellationToken.None).ConfigureAwait(false);
		}

		using (TenantContextHolder.BeginScope(TenantB))
		{
			await store.SaveSnapshotAsync(
				CreateSnapshot(sharedAggregateId, 2, "tenant-b-write", TenantB),
				CancellationToken.None).ConfigureAwait(false);

			var tenantBsRow = await store.GetLatestSnapshotAsync(
				sharedAggregateId,
				AggregateType,
				CancellationToken.None).ConfigureAwait(false);

			_ = tenantBsRow.ShouldNotBeNull(
				"tenant B's own save must succeed and be readable by B — isolation must never be " +
				"achieved by refusing the write");
			Encoding.UTF8.GetString(tenantBsRow.Data.ToArray()).ShouldBe(
				"tenant-b-write",
				"B must read back what B wrote. If this is A's payload the read carries no tenant " +
				"predicate; if null, B's write was silently dropped");
		}
	}

	/// <summary>
	/// Creates the table this store requires, DERIVED FROM ITS OWN SQL — not from a shipped schema,
	/// because the package ships none.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The store writes <c>VALUES (…, COALESCE(@TenantId, ''))</c> and keys
	/// <c>ON CONFLICT (aggregate_id, aggregate_type, tenant_id)</c>. Postgres rejects that
	/// <c>ON CONFLICT</c> without a matching unique constraint, and the empty-string sentinel only
	/// isolates tenants when the column is <c>NOT NULL</c>. Both are reproduced below because the
	/// store's correctness depends on them.
	/// </para>
	/// <para>
	/// <b>This DDL is not authoritative and must MIRROR the shipped schema exactly.</b> The shipped
	/// schema DOES exist: Excalibur.EventSourcing.Postgres/Scripts/001_CreateSnapshotSchema.sql, and the
	/// Oracle sibling ships one too. An earlier revision of this remark claimed the OPPOSITE ("this one
	/// does not") and used it to justify hand-rolling a shape inferred from the store's SQL. The claim
	/// was FALSE for the very provider this fixture covers, and it was load-bearing: it was the stated
	/// reason the fixture was allowed to diverge. When the shipped schema changes, this block changes
	/// with it. <c>snapshot_id</c> is TEXT rather than UUID because the store
	/// binds it as a string; the neighbouring fixture declares UUID and belongs to a different store
	/// that shares this type's name.
	/// </para>
	/// </remarks>
	// ── 18c3el: an UNSCOPED destructive delete must NOT span tenants ────────────────────────────────
	// DeleteSnapshotsRequest / DeleteSnapshotsOlderThanRequest carry the same empty-branch predicate as the
	// erase core (scope.IsScoped ? " AND tenant_id = @TenantId" : ""). An unscoped delete therefore removes
	// EVERY tenant's snapshots for the aggregate — a cross-tenant destruction, same class as the GDPR erase.
	// Property-based (testing-patterns §3 corollary): the safety arm asserts the owning tenant's snapshot
	// SURVIVES an unscoped delete — true under whichever bounding the fix uses (the shipped fix is the ''
	// sentinel: tenant_id = COALESCE(@TenantId, '')), never the SQL. This was the genuine 18c3el defect and
	// was RED-first; the fix is now committed, so these arms are GREEN at HEAD and RED against the pre-fix
	// commit (unlike the Oracle/SqlServer delete arms, which were already-correct parity guards).

	/// <summary>
	/// SAFETY. An unscoped <c>DeleteSnapshotsAsync</c> must not remove a tenant's snapshot.
	/// </summary>
	[Fact]
	public async Task Not_Delete_A_Tenants_Snapshot_On_An_Unscoped_DeleteSnapshots()
	{
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		var aggregateId = Guid.NewGuid().ToString();

		await using var dataSource = NpgsqlDataSource.Create(_fixture.ConnectionString);
		await EnsureStoreTableAsync(dataSource).ConfigureAwait(false);

		var scopedStore = CreateStore(dataSource, tenantScoped: true);
		using (TenantContextHolder.BeginScope(TenantB))
		{
			await scopedStore.SaveSnapshotAsync(
				CreateSnapshot(aggregateId, 1, "tenant-b-data", TenantB), CancellationToken.None).ConfigureAwait(false);
		}

		// Unscoped delete (a store with no tenant context — its own partition is the untenanted one).
		var unscopedStore = CreateStore(dataSource, tenantScoped: false);
		await unscopedStore.DeleteSnapshotsAsync(aggregateId, AggregateType, CancellationToken.None).ConfigureAwait(false);

		ISnapshot? survivor;
		using (TenantContextHolder.BeginScope(TenantB))
		{
			survivor = await scopedStore.GetLatestSnapshotAsync(
				aggregateId, AggregateType, CancellationToken.None).ConfigureAwait(false);
		}

		_ = survivor.ShouldNotBeNull(
			"An unscoped delete operates on the untenanted partition; it must NOT remove a tenant's snapshot. "
			+ "This was the 18c3el defect (the empty-branch predicate deleted every tenant's snapshot); the fix "
			+ "(tenant_id = COALESCE(@TenantId, '')) is now committed — GREEN at HEAD, RED against the pre-fix commit");
	}

	/// <summary>
	/// LIVENESS. The owning tenant's own <c>DeleteSnapshotsAsync</c> still removes its snapshot.
	/// </summary>
	[Fact]
	public async Task Still_Delete_The_Owning_Tenants_Snapshot_On_A_Scoped_DeleteSnapshots()
	{
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		var aggregateId = Guid.NewGuid().ToString();

		await using var dataSource = NpgsqlDataSource.Create(_fixture.ConnectionString);
		await EnsureStoreTableAsync(dataSource).ConfigureAwait(false);

		var scopedStore = CreateStore(dataSource, tenantScoped: true);
		using (TenantContextHolder.BeginScope(TenantB))
		{
			await scopedStore.SaveSnapshotAsync(
				CreateSnapshot(aggregateId, 1, "tenant-b-data", TenantB), CancellationToken.None).ConfigureAwait(false);
			await scopedStore.DeleteSnapshotsAsync(aggregateId, AggregateType, CancellationToken.None).ConfigureAwait(false);

			var gone = await scopedStore.GetLatestSnapshotAsync(
				aggregateId, AggregateType, CancellationToken.None).ConfigureAwait(false);
			gone.ShouldBeNull("the owning tenant's scoped delete removes its own snapshot (delete is not a no-op)");
		}
	}

	/// <summary>
	/// SAFETY. An unscoped <c>DeleteSnapshotsOlderThanAsync</c> must not remove a tenant's snapshot.
	/// </summary>
	[Fact]
	public async Task Not_Delete_A_Tenants_Snapshot_On_An_Unscoped_DeleteSnapshotsOlderThan()
	{
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		var aggregateId = Guid.NewGuid().ToString();

		await using var dataSource = NpgsqlDataSource.Create(_fixture.ConnectionString);
		await EnsureStoreTableAsync(dataSource).ConfigureAwait(false);

		var scopedStore = CreateStore(dataSource, tenantScoped: true);
		using (TenantContextHolder.BeginScope(TenantB))
		{
			await scopedStore.SaveSnapshotAsync(
				CreateSnapshot(aggregateId, 3, "tenant-b-data", TenantB), CancellationToken.None).ConfigureAwait(false);
		}

		// Unscoped delete-older-than a higher version → prunes the tenant's v3 across all tenants at HEAD.
		var unscopedStore = CreateStore(dataSource, tenantScoped: false);
		await unscopedStore.DeleteSnapshotsOlderThanAsync(
			aggregateId, AggregateType, 10, CancellationToken.None).ConfigureAwait(false);

		ISnapshot? survivor;
		using (TenantContextHolder.BeginScope(TenantB))
		{
			survivor = await scopedStore.GetLatestSnapshotAsync(
				aggregateId, AggregateType, CancellationToken.None).ConfigureAwait(false);
		}

		_ = survivor.ShouldNotBeNull(
			"An unscoped prune must NOT remove a tenant's snapshot. This was the 18c3el defect (the empty-branch "
			+ "predicate pruned every tenant's older snapshots); the fix (COALESCE(@TenantId, '')) is now "
			+ "committed — GREEN at HEAD, RED against the pre-fix commit");
	}

	/// <summary>
	/// LIVENESS. The owning tenant's own <c>DeleteSnapshotsOlderThanAsync</c> still prunes its snapshot.
	/// </summary>
	[Fact]
	public async Task Still_Prune_The_Owning_Tenants_Snapshot_On_A_Scoped_DeleteSnapshotsOlderThan()
	{
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		var aggregateId = Guid.NewGuid().ToString();

		await using var dataSource = NpgsqlDataSource.Create(_fixture.ConnectionString);
		await EnsureStoreTableAsync(dataSource).ConfigureAwait(false);

		var scopedStore = CreateStore(dataSource, tenantScoped: true);
		using (TenantContextHolder.BeginScope(TenantB))
		{
			await scopedStore.SaveSnapshotAsync(
				CreateSnapshot(aggregateId, 3, "tenant-b-data", TenantB), CancellationToken.None).ConfigureAwait(false);
			await scopedStore.DeleteSnapshotsOlderThanAsync(
				aggregateId, AggregateType, 10, CancellationToken.None).ConfigureAwait(false);

			var gone = await scopedStore.GetLatestSnapshotAsync(
				aggregateId, AggregateType, CancellationToken.None).ConfigureAwait(false);
			gone.ShouldBeNull("the owning tenant's scoped prune removes its own older snapshot (prune is not a no-op)");
		}
	}

	/// <summary>
	/// ANSI THREE-VALUED LOGIC, and the universal half of the Oracle fold arm (ueiejh). Asserts as an
	/// executable fact on a non-Oracle provider that <c>NULL = NULL</c> is UNKNOWN — a predicate binding
	/// NULL against a NULL tenant column matches NOTHING — so the write-before-read ordering hazard is
	/// enforced on every provider we ship, not filed as an Oracle quirk.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The Oracle sibling (<c>Fold_The_Empty_String_To_Null_So_No_Sentinel_May_Use_It</c>) asserts a
	/// dialect-specific fact: Oracle folds <c>''</c> to <c>NULL</c>. THIS arm asserts the ANSI fact that
	/// holds identically in SQL Server, Postgres and SQLite — <c>NULL = NULL</c> never evaluates TRUE — so
	/// a tenant predicate that relies on a bare <c>=</c> to read untenanted rows fails on EVERY provider.
	/// That is why the untenanted partition must be a concrete non-null sentinel, never NULL.
	/// </para>
	/// <para>
	/// Both arms are present so the fact is non-vacuous: the bare-equality bind returning zero proves the
	/// hazard, and the NULL-safe <c>IS NOT DISTINCT FROM</c> bind returning one proves the correct operator
	/// is the fix. This RED-detects any predicate that assumes <c>NULL = NULL</c> matches. It asserts a
	/// property of the database, not of our design, so it stays true and useful however tenancy is settled.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task Not_Match_A_Null_Tenant_With_A_Null_Bind_Because_Ansi_Null_Equality_Is_Unknown()
	{
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);

		await using var dataSource = NpgsqlDataSource.Create(_fixture.ConnectionString);

		// The only interpolated identifiers are the fixture's own schema and a hex-only ('N' format) GUID
		// scratch-table name — neither is attacker-controlled and neither can be a bind variable (SQL cannot
		// parameterise an object name in any provider). The one bound value, @p, IS a parameter below.
#pragma warning disable CA2100
		var scratch = $"ansi_null_probe_{Guid.NewGuid():N}";
		await using (var create = dataSource.CreateCommand(
			$"CREATE TABLE {_fixture.SchemaName}.\"{scratch}\" (tenant_id VARCHAR(255) NULL, payload TEXT NOT NULL)"))
		{
			_ = await create.ExecuteNonQueryAsync().ConfigureAwait(false);
		}

		try
		{
			await using (var insert = dataSource.CreateCommand(
				$"INSERT INTO {_fixture.SchemaName}.\"{scratch}\" (tenant_id, payload) VALUES (NULL, 'untenanted-row')"))
			{
				_ = await insert.ExecuteNonQueryAsync().ConfigureAwait(false);
			}

			// SAFETY: a bare equality binding NULL matches NOTHING — NULL = NULL is UNKNOWN, never TRUE.
			await using (var bareEquality = dataSource.CreateCommand(
				$"SELECT COUNT(*) FROM {_fixture.SchemaName}.\"{scratch}\" WHERE tenant_id = @p"))
			{
				var p = bareEquality.CreateParameter();
				p.ParameterName = "p";
				p.Value = DBNull.Value;
				_ = bareEquality.Parameters.Add(p);

				var matched = Convert.ToInt64(
					await bareEquality.ExecuteScalarAsync().ConfigureAwait(false),
					System.Globalization.CultureInfo.InvariantCulture);

				matched.ShouldBe(
					0,
					"ANSI three-valued logic: NULL = NULL is UNKNOWN, so a bare-equality predicate binding NULL " +
					"matches no rows on this non-Oracle provider. A tenant predicate that relies on NULL = NULL to " +
					"read untenanted rows is broken on EVERY provider we ship — this is why the untenanted partition " +
					"must be a concrete non-null sentinel, never NULL. If this returns 1, the provider stopped " +
					"following ANSI null semantics and the sentinel rationale must be revisited");
			}

			// LIVENESS / non-vacuity: the NULL-safe operator DOES match the same row — proving the fix is the
			// operator, not luck, and RED-detecting any predicate that assumes NULL = NULL matches.
			await using (var nullSafe = dataSource.CreateCommand(
				$"SELECT COUNT(*) FROM {_fixture.SchemaName}.\"{scratch}\" WHERE tenant_id IS NOT DISTINCT FROM @p"))
			{
				var p = nullSafe.CreateParameter();
				p.ParameterName = "p";
				p.Value = DBNull.Value;
				_ = nullSafe.Parameters.Add(p);

				var matched = Convert.ToInt64(
					await nullSafe.ExecuteScalarAsync().ConfigureAwait(false),
					System.Globalization.CultureInfo.InvariantCulture);

				matched.ShouldBe(
					1,
					"the NULL-safe operator (IS NOT DISTINCT FROM) matches the untenanted row that a bare = cannot; " +
					"this pairs the safety arm so the fact is non-vacuous rather than trivially satisfiable");
			}
		}
		finally
		{
			await using var drop = dataSource.CreateCommand(
				$"DROP TABLE IF EXISTS {_fixture.SchemaName}.\"{scratch}\"");
			_ = await drop.ExecuteNonQueryAsync().ConfigureAwait(false);
		}
#pragma warning restore CA2100
	}

	// ── r07soo: the fixture must be no more permissive than the SHIPPED schema ──────────────────────
	// The shipped schema declares tenant_id NOT NULL with NO DEFAULT, deliberately: the tenant is a
	// component of IDENTITY, not an optional filter. This fixture previously declared a default, which
	// ACCEPTS writes production REJECTS -- so a forgotten tenant landed silently in the untenanted
	// partition and every arm in this suite ran against a laxer schema than the one we ship.
	//
	// THIS ARM OWNS ITS TABLE, and that is not incidental. The suite's shared table is created with
	// CREATE TABLE IF NOT EXISTS, so a table left in the container by an EARLIER run keeps its ORIGINAL
	// definition -- a DDL edit cannot alter it, and IF NOT EXISTS reports success while changing nothing.
	// An arm bound to that table therefore measures whatever a previous run happened to leave behind:
	// it passed for me on a fresh container and FAILED on a re-run, same source, because the stale table
	// still carried the default. A per-arm table with a unique name makes the result depend only on the
	// DDL under test.
	//
	// SAFETY + LIVENESS are both required. A rejection-only assertion is satisfied by a table that
	// rejects EVERYTHING, which is how a broken fixture looks; the liveness arm proves an INSERT that
	// DOES name the tenant still succeeds, so the rejection is attributable to the missing tenant.
	[Fact]
	public async Task Reject_An_Insert_That_Omits_The_Tenant_While_Still_Accepting_One_That_Names_It()
	{
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);

		await using var dataSource = NpgsqlDataSource.Create(_fixture.ConnectionString);

		// Only the fixture's own schema and a hex-only ('N' format) GUID table name are interpolated --
		// neither is attacker-controlled, and SQL cannot parameterise an object name in any provider.
#pragma warning disable CA2100
		var probe = $"tenant_not_null_probe_{Guid.NewGuid():N}";

		// MIRRORS the shipped schema's tenant_id declaration exactly: NOT NULL, no DEFAULT.
		await using (var create = dataSource.CreateCommand(
			$"""
			CREATE TABLE {_fixture.SchemaName}."{probe}" (
				snapshot_id TEXT NOT NULL,
				tenant_id VARCHAR(255) NOT NULL,
				payload TEXT NOT NULL
			)
			"""))
		{
			_ = await create.ExecuteNonQueryAsync().ConfigureAwait(false);
		}

		try
		{
			// SAFETY: omitting tenant_id must FAIL. With a DEFAULT present this INSERT succeeds and the
			// row silently becomes untenanted -- the defect this arm exists to RED-detect.
			var omitted = await Should.ThrowAsync<PostgresException>(async () =>
			{
				await using var insert = dataSource.CreateCommand(
					$"""INSERT INTO {_fixture.SchemaName}."{probe}" (snapshot_id, payload) VALUES (@id, 'no-tenant')""");
				AddParameter(insert, "id", Guid.NewGuid().ToString("N"));
				_ = await insert.ExecuteNonQueryAsync().ConfigureAwait(false);
			}).ConfigureAwait(false);

			// 23502 = not_null_violation. Asserting the SQLSTATE rather than the message keeps the arm
			// bound to the constraint: any other failure is a different defect and must not green it.
			omitted.SqlState.ShouldBe(
				"23502",
				"omitting tenant_id must fail as a NOT NULL violation -- a different SQLSTATE means the "
				+ "INSERT broke for an unrelated reason and proves nothing about tenancy");

			// LIVENESS: the same INSERT naming the tenant must SUCCEED. Without this, the arm above is
			// satisfied by a table that rejects every write.
			await using (var accepted = dataSource.CreateCommand(
				$"""INSERT INTO {_fixture.SchemaName}."{probe}" (snapshot_id, tenant_id, payload) VALUES (@id, @tenantId, 'tenanted')"""))
			{
				AddParameter(accepted, "id", Guid.NewGuid().ToString("N"));
				AddParameter(accepted, "tenantId", "r07soo-tenant");
				var written = await accepted.ExecuteNonQueryAsync().ConfigureAwait(false);
				written.ShouldBe(1, "an INSERT naming the tenant must still be accepted");
			}
		}
		finally
		{
			await using var drop = dataSource.CreateCommand(
				$"""DROP TABLE IF EXISTS {_fixture.SchemaName}."{probe}" """);
			_ = await drop.ExecuteNonQueryAsync().ConfigureAwait(false);
		}
#pragma warning restore CA2100
	}

	private static void AddParameter(System.Data.Common.DbCommand command, string name, string value)
	{
		var parameter = command.CreateParameter();
		parameter.ParameterName = name;
		parameter.Value = value;
		_ = command.Parameters.Add(parameter);
	}

	private async Task EnsureStoreTableAsync(NpgsqlDataSource dataSource)
	{
		var sql = $"""
			CREATE TABLE IF NOT EXISTS {_fixture.SchemaName}.{TableName} (
				snapshot_id TEXT NOT NULL,
				aggregate_id VARCHAR(255) NOT NULL,
				aggregate_type VARCHAR(255) NOT NULL,
				version BIGINT NOT NULL,
				data BYTEA NOT NULL,
				-- The store's INSERT writes eight columns and this DDL must declare every one of them.
				-- metadata is nullable BYTEA: SaveSnapshotRequest serializes the dictionary with
				-- JsonSerializer.SerializeToUtf8Bytes and stores SQL NULL when the snapshot carries none.
				-- Omitting it does not fail to compile -- it surfaces at runtime as
				-- 42703: column "metadata" ... does not exist, on this shard only.
				metadata BYTEA NULL,
				created_at TIMESTAMPTZ NOT NULL,
				-- NO DEFAULT, mirroring the shipped schema deliberately. A default makes this fixture
				-- ACCEPT writes production REJECTS, so a forgotten tenant lands silently in the
				-- untenanted partition and becomes indistinguishable from a deliberately untenanted
				-- row. With the default present this suite could not detect that class at all.
				tenant_id VARCHAR(255) NOT NULL,
				PRIMARY KEY (aggregate_id, aggregate_type, tenant_id)
			);
			""";

		await using var command = dataSource.CreateCommand(sql);
		_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
	}

	private const string TableName = "event_sourcing_snapshots_tenancy";

	private global::Excalibur.EventSourcing.Postgres.PostgresSnapshotStore CreateStore(
		NpgsqlDataSource dataSource,
		bool tenantScoped) =>
		// `tenantScoped: false` is the UNSCOPED store under test and must keep binding the reserved
		// untenanted term. An absent context never meant "emit no term": it became TenantScope.Untenanted, which
		// every keyed statement routes through KeyedTenantPartition into the untenanted partition.
		// Resolving the sentinel reproduces that term exactly; the default tenant identity would bind an
		// ordinary tenant and stop exercising the branch these arms exist to guard.
		new(
			dataSource,
			NullLogger<global::Excalibur.EventSourcing.Postgres.PostgresSnapshotStore>.Instance,
			tenantContext: tenantScoped
				? new AmbientHolderTenantContext()
				: UntenantedTestTenantContext.Instance,
			schema: _fixture.SchemaName,
			table: TableName);

	private static ISnapshot CreateSnapshot(string aggregateId, long version, string data, string? tenantId) =>
		new PostgresUnscopedReadSnapshot(
			Guid.NewGuid().ToString(),
			aggregateId,
			AggregateType,
			version,
			DateTimeOffset.UtcNow,
			Encoding.UTF8.GetBytes(data),
			null,
			tenantId);

	private sealed class AmbientHolderTenantContext : ITenantContext
	{
		public string? TenantId => TenantContextHolder.Current;

		public bool HasTenant => !string.IsNullOrEmpty(TenantContextHolder.Current);
	}

	private sealed record PostgresUnscopedReadSnapshot(
		string SnapshotId,
		string AggregateId,
		string AggregateType,
		long Version,
		DateTimeOffset CreatedAt,
		ReadOnlyMemory<byte> Data,
		IDictionary<string, object>? Metadata,
		string? TenantId) : ISnapshot;
}
