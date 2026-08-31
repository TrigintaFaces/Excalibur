// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using Excalibur.Dispatch;
using Excalibur.Domain.Model;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Oracle;

using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Xunit;

#pragma warning disable CA1812 // Internal class is never instantiated

namespace Excalibur.Integration.Tests.Data.Snapshots;

/// <summary>
/// Executes, on real Oracle, the empty-string-is-NULL question that source-reasoning can raise but
/// cannot settle.
/// </summary>
/// <remarks>
/// <para>
/// Oracle treats the empty string as <c>NULL</c>. If that holds through the converted predicate, then
/// <c>NVL(:TenantId, '')</c> yields <c>NULL</c> on the unscoped path, <c>TENANTID = NULL</c> matches
/// nothing under three-valued logic, and an unscoped read returns no rows — the sentinel conversion
/// that fixes a LEAK on other providers would instead make single-tenant Oracle unable to read its own
/// data. That is a LIVENESS failure, the opposite failure mode from the leak, and a suite that only
/// asserts isolation would report it as a pass.
/// </para>
/// <para>
/// <b>Which is why the liveness arm is first here.</b> Both arms are present because either one alone
/// is satisfiable by a broken store: isolation alone passes when nothing is ever returned, and
/// round-trip alone passes when everything is returned to everyone.
/// </para>
/// <para>
/// Never skipped. Docker availability is asserted rather than used as a skip condition — a suite that
/// passes by not running reports the same green as one that ran.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Oracle")]
public sealed class OracleSnapshotStoreUnscopedReadShould : IClassFixture<OracleSnapshotStoreFixture>
{
	private const string TenantA = "tenant-a";
	private const string TenantB = "tenant-b";
	private const string AggregateType = "TestAggregate";

	private readonly OracleSnapshotStoreFixture _fixture;

	/// <summary>
	/// Initializes a new instance of the <see cref="OracleSnapshotStoreUnscopedReadShould"/> class.
	/// </summary>
	/// <param name="fixture">The Oracle snapshot store fixture.</param>
	public OracleSnapshotStoreUnscopedReadShould(OracleSnapshotStoreFixture fixture)
	{
		_fixture = fixture;
	}

	/// <summary>
	/// LIVENESS, and the arm this file exists for. An unscoped Oracle host must be able to read back
	/// what it just wrote.
	/// </summary>
	[Fact]
	public async Task Still_Read_Back_Its_Own_Row_When_Unscoped()
	{
		await InitializeAsync().ConfigureAwait(false);
		var aggregateId = Guid.NewGuid().ToString();
		var store = CreateStore(tenantScoped: false);

		await store.SaveSnapshotAsync(
			CreateSnapshot(aggregateId, 1, "unscoped-data", tenantId: null),
			CancellationToken.None).ConfigureAwait(false);

		var loaded = await store.GetLatestSnapshotAsync(
			aggregateId,
			AggregateType,
			CancellationToken.None).ConfigureAwait(false);

		_ = loaded.ShouldNotBeNull(
			"an unscoped host must read back its own row. If this is null on Oracle specifically, the " +
			"empty-string sentinel is being stored or compared as NULL, so TENANTID = NVL(:T,'') matches " +
			"nothing and single-tenant Oracle cannot see its own data");
		Encoding.UTF8.GetString(loaded.Data.ToArray()).ShouldBe("unscoped-data");
	}

	/// <summary>
	/// SAFETY. An unscoped reader must not receive a row written under a tenant.
	/// </summary>
	[Fact]
	public async Task Not_Return_A_Tenants_Snapshot_To_An_Unscoped_Reader()
	{
		await InitializeAsync().ConfigureAwait(false);
		var aggregateId = Guid.NewGuid().ToString();

		var scopedStore = CreateStore(tenantScoped: true);
		using (TenantContextHolder.BeginScope(TenantA))
		{
			await scopedStore.SaveSnapshotAsync(
				CreateSnapshot(aggregateId, 1, "tenant-a-data", TenantA),
				CancellationToken.None).ConfigureAwait(false);
		}

		var unscopedStore = CreateStore(tenantScoped: false);
		var leaked = await unscopedStore.GetLatestSnapshotAsync(
			aggregateId,
			AggregateType,
			CancellationToken.None).ConfigureAwait(false);

		leaked.ShouldBeNull("an unscoped reader must not receive a row written under a tenant");
	}

	/// <summary>
	/// LIVENESS for the scoped path, so the safety arm cannot be satisfied by a store that returns
	/// nothing to anyone.
	/// </summary>
	[Fact]
	public async Task Still_Serve_A_Tenant_Its_Own_Row()
	{
		await InitializeAsync().ConfigureAwait(false);
		var aggregateId = Guid.NewGuid().ToString();
		var store = CreateStore(tenantScoped: true);

		using (TenantContextHolder.BeginScope(TenantA))
		{
			await store.SaveSnapshotAsync(
				CreateSnapshot(aggregateId, 1, "tenant-a-data", TenantA),
				CancellationToken.None).ConfigureAwait(false);

			var loaded = await store.GetLatestSnapshotAsync(
				aggregateId,
				AggregateType,
				CancellationToken.None).ConfigureAwait(false);

			_ = loaded.ShouldNotBeNull("a tenant must read back its own row");
			Encoding.UTF8.GetString(loaded.Data.ToArray()).ShouldBe("tenant-a-data");
		}
	}

	/// <summary>
	/// CARDINALITY. An unscoped save must UPSERT, not accumulate — the half no read arm can see.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The read arms in this file assert what comes back; none of them can count what is underneath.
	/// A MERGE whose match key compares <c>NULL = NULL</c> never matches, so every "upsert" inserts a
	/// new row — and the read still returns a correct-looking latest snapshot while the table grows
	/// without bound. Both failure and fix are invisible to an assertion about the returned value.
	/// </para>
	/// <para>
	/// This is the direction principle applied to storage rather than retrieval: a read arm answers
	/// <i>did the right thing come back</i>, and only a count answers <i>is there exactly one of it</i>.
	/// Saving the same aggregate three times must leave one row.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task Upsert_Rather_Than_Accumulate_Rows_When_Unscoped()
	{
		await InitializeAsync().ConfigureAwait(false);
		var aggregateId = Guid.NewGuid().ToString();
		var store = CreateStore(tenantScoped: false);

		for (var version = 1; version <= 3; version++)
		{
			await store.SaveSnapshotAsync(
				CreateSnapshot(aggregateId, version, $"v{version}", tenantId: null),
				CancellationToken.None).ConfigureAwait(false);
		}

		var rows = await CountRowsAsync(aggregateId).ConfigureAwait(false);

		rows.ShouldBe(
			1,
			$"three saves of one aggregate must leave ONE row, found {rows}. More than one means the " +
			"merge match key never matched — on the unscoped path that is NULL = NULL, which is never " +
			"true — so every save inserted instead of updating and the table grows without bound");

		// LIVENESS: the surviving row must be the LATEST, not merely the only one. A store that keeps
		// exactly one row by discarding every update would satisfy the count and lose the data.
		var loaded = await store.GetLatestSnapshotAsync(
			aggregateId,
			AggregateType,
			CancellationToken.None).ConfigureAwait(false);

		_ = loaded.ShouldNotBeNull();
		Encoding.UTF8.GetString(loaded.Data.ToArray()).ShouldBe(
			"v3",
			"the single surviving row must carry the newest write, or the upsert is discarding updates");
	}

	/// <summary>
	/// The Oracle empty-string constraint, as an executable fact rather than a comment.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Two facts are in play here and only ONE of them is about Oracle.</b> Conflating them is how a
	/// team learns to file NULL problems under "Oracle weirdness" and then ships the same defect on
	/// SQL Server:
	/// </para>
	/// <list type="bullet">
	///   <item>
	///   <description>
	///   <b>Oracle folds <c>''</c> to <c>NULL</c></b> — genuinely dialect-specific, and what this arm
	///   asserts. It is why <c>''</c> cannot serve as a stored sentinel on this provider.
	///   </description>
	///   </item>
	///   <item>
	///   <description>
	///   <b><c>NULL = NULL</c> evaluates to UNKNOWN</b> — <b>ANSI three-valued logic, true in EVERY
	///   dialect</b>: SQL Server, Postgres and SQLite included. A predicate comparing a NULL tenant
	///   never matches anywhere. <b>Nothing about that hazard is an Oracle quirk</b>, and this arm does
	///   NOT cover it — it lives in this file only because Oracle is where the folding made it visible
	///   first.
	///   </description>
	///   </item>
	/// </list>
	/// <para>
	/// The three designs invalidated in one night — <c>NVL(:T, '')</c> in a predicate, <c>''</c> as the
	/// canonical sentinel, <c>NOT NULL DEFAULT ('')</c> in a schema — were killed by the FIRST fact.
	/// The ordering hazard that makes a write-before-read change fatal is the SECOND, and it applies to
	/// every provider we ship. Do not read this arm as evidence that the other providers are safe.
	/// </para>
	/// <para>
	/// This arm makes the constraint fail a run instead of a review. It is deliberately independent of
	/// which sentinel value is ultimately chosen: it asserts a property of the database, not of our
	/// design, so it stays true and stays useful no matter how the tenancy question is settled.
	/// </para>
	/// <para>
	/// If this ever goes green — if Oracle stops folding — the three designs above become viable and
	/// somebody should revisit them. That is the other reason to encode it: a comment cannot tell you
	/// when it has stopped being true.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task Fold_The_Empty_String_To_Null_So_No_Sentinel_May_Use_It()
	{
		await InitializeAsync().ConfigureAwait(false);

		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		await using var command = connection.CreateCommand();
		command.CommandText =
			"SELECT CASE WHEN '' IS NULL THEN 1 ELSE 0 END AS FOLDED FROM DUAL";

		var result = await command.ExecuteScalarAsync().ConfigureAwait(false);
		var folded = Convert.ToInt32(result, System.Globalization.CultureInfo.InvariantCulture);

		folded.ShouldBe(
			1,
			"Oracle must fold '' to NULL — this is the constraint that invalidated three tenancy " +
			"designs in one night. If this assertion FAILS, Oracle's behaviour has changed and the " +
			"empty-string sentinel becomes viable again; if it PASSES, no sentinel, predicate, or " +
			"DDL default may use '' on this provider");
	}

	/// <summary>
	/// SAFETY, WRITE AXIS. A second tenant saving at the same aggregate id must not overwrite the
	/// first tenant's row.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Every other tenant-isolation arm in this repository asks what a READER receives. None asks
	/// what a WRITER destroys, and they are not the same question: a foreign write satisfies the
	/// concurrency check on its way past, replaces the victim's payload, and leaves nothing a
	/// read-side assertion could notice. The victim's data is gone rather than exposed.
	/// </para>
	/// <para>
	/// It asserts the victim's ROW, deliberately, and not that the second save threw. "The save
	/// threw" is satisfied by a store that throws on everything, so it cannot tell a working guard
	/// from a broken store; only "A's payload is byte-identical afterwards" can. Its liveness partner
	/// is the arm below, on the same path and the same row-class.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task Not_Let_One_Tenants_Save_Overwrite_Another_Tenants_Snapshot()
	{
		await InitializeAsync().ConfigureAwait(false);
		var sharedAggregateId = Guid.NewGuid().ToString();
		var store = CreateStore(tenantScoped: true);

		using (TenantContextHolder.BeginScope(TenantA))
		{
			await store.SaveSnapshotAsync(
				CreateSnapshot(sharedAggregateId, 1, "tenant-a-original", TenantA),
				CancellationToken.None).ConfigureAwait(false);
		}

		// A second tenant saves at the SAME aggregate id — the shape that destroys data.
		//
		// B's version MUST exceed A's, and this is load-bearing rather than incidental. The upsert
		// updates under `WHERE :VersionCmp > target.VERSION`, so with equal versions the update does
		// not fire and NO store can overwrite — a tenant-blind one included. An arm written with both
		// tenants at version 1 therefore passes because of the version guard, proving nothing about
		// tenancy: it inhabits a world where the defect cannot occur. Verified by mutation — with
		// equal versions, dropping the tenant from the MERGE match key leaves this arm GREEN.
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
			"reached it — an overwrite, not a disclosure: A's data is gone, the concurrency check was " +
			"satisfied on the way past, and nothing recorded that it happened");
	}

	/// <summary>
	/// LIVENESS for the arm above. Without it, a store that refused the second tenant's save outright
	/// would satisfy the safety assertion while having broken multi-tenant writes entirely.
	/// </summary>
	/// <remarks>
	/// The pairing is on the SAME path and the SAME row-class as its safety partner. A liveness arm
	/// exercising a different path is not a pair — it is an unrelated test sitting next to one, and
	/// the combination passes for a store that does nothing.
	/// </remarks>
	[Fact]
	public async Task Still_Let_The_Second_Tenant_Save_Its_Own_Row()
	{
		await InitializeAsync().ConfigureAwait(false);
		var sharedAggregateId = Guid.NewGuid().ToString();
		var store = CreateStore(tenantScoped: true);

		using (TenantContextHolder.BeginScope(TenantA))
		{
			await store.SaveSnapshotAsync(
				CreateSnapshot(sharedAggregateId, 1, "tenant-a-original", TenantA),
				CancellationToken.None).ConfigureAwait(false);
		}

		using (TenantContextHolder.BeginScope(TenantB))
		{
			await store.SaveSnapshotAsync(
				CreateSnapshot(sharedAggregateId, 1, "tenant-b-write", TenantB),
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
				"predicate; if it is null, B's write was silently dropped");
		}
	}

	private async Task<int> CountRowsAsync(string aggregateId)
	{
		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync().ConfigureAwait(false);

		await using var command = connection.CreateCommand();

		// CA2100 suppressed deliberately, not routinely. The only interpolated values are the fixture's
		// own schema and table IDENTIFIERS, which SQL cannot parameterise in any provider — a bind
		// variable may stand for a value, never for an object name. The one input that could carry
		// attacker-controlled text, the aggregate id, IS bound below. Rewriting this to satisfy the
		// analyzer would mean hard-coding the fixture's names, which silently breaks the moment the
		// fixture changes them.
#pragma warning disable CA2100
		command.CommandText =
			$"SELECT COUNT(*) FROM {_fixture.Schema}.{_fixture.TableName} WHERE AGGREGATEID = :AggregateId";
#pragma warning restore CA2100
		var parameter = command.CreateParameter();
		parameter.ParameterName = "AggregateId";
		parameter.Value = aggregateId;
		_ = command.Parameters.Add(parameter);

		var result = await command.ExecuteScalarAsync().ConfigureAwait(false);
		return Convert.ToInt32(result, System.Globalization.CultureInfo.InvariantCulture);
	}

	private async Task InitializeAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"this runs against real Oracle and is never skipped: the empty-string-is-NULL behaviour it " +
			"tests cannot be reproduced by any other engine, so a skip here proves nothing anywhere else.");

		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
	}

	// ── 18c3el: an UNSCOPED destructive delete must NOT span tenants (Oracle) ───────────────────────
	// GREEN PARITY REGRESSION GUARD — Oracle's DeleteSnapshots/DeleteSnapshotsOlderThan predicate is ALREADY
	// unconditional (the unscoped branch is `AND TENANTID IS NULL`), so this provider never had the
	// empty-branch defect; only the Postgres snapshot-delete did (that one is the genuine RED-first lock).
	// These arms assert the owning tenant's snapshot SURVIVES an unscoped delete — GREEN today; they go RED if
	// the predicate is ever regressed to the empty-branch form (18c3el). Mirrors the 1dsy9j "Expected GREEN"
	// parity arms for already-correct providers.

	/// <summary>SAFETY. An unscoped DeleteSnapshots must not remove a tenant's snapshot.</summary>
	[Fact]
	public async Task Not_Delete_A_Tenants_Snapshot_On_An_Unscoped_DeleteSnapshots()
	{
		await InitializeAsync().ConfigureAwait(false);
		var aggregateId = Guid.NewGuid().ToString();

		var scopedStore = CreateStore(tenantScoped: true);
		using (TenantContextHolder.BeginScope(TenantB))
		{
			await scopedStore.SaveSnapshotAsync(
				CreateSnapshot(aggregateId, 1, "tenant-b-data", TenantB), CancellationToken.None).ConfigureAwait(false);
		}

		var unscopedStore = CreateStore(tenantScoped: false);
		await unscopedStore.DeleteSnapshotsAsync(aggregateId, AggregateType, CancellationToken.None).ConfigureAwait(false);

		ISnapshot? survivor;
		using (TenantContextHolder.BeginScope(TenantB))
		{
			survivor = await scopedStore.GetLatestSnapshotAsync(
				aggregateId, AggregateType, CancellationToken.None).ConfigureAwait(false);
		}

		_ = survivor.ShouldNotBeNull(
			"Regression guard (GREEN): Oracle's unscoped DeleteSnapshots predicate is already `TENANTID IS NULL`, "
			+ "so an unscoped delete never removed a tenant's snapshot. This arm goes RED if the predicate is "
			+ "ever regressed to the empty-branch defect (18c3el) — the guarantee: an unscoped delete must NOT "
			+ "remove a tenant's snapshot");
	}

	/// <summary>LIVENESS. The owning tenant's own scoped DeleteSnapshots still removes its snapshot.</summary>
	[Fact]
	public async Task Still_Delete_The_Owning_Tenants_Snapshot_On_A_Scoped_DeleteSnapshots()
	{
		await InitializeAsync().ConfigureAwait(false);
		var aggregateId = Guid.NewGuid().ToString();

		var scopedStore = CreateStore(tenantScoped: true);
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

	/// <summary>SAFETY. An unscoped DeleteSnapshotsOlderThan must not remove a tenant's snapshot.</summary>
	[Fact]
	public async Task Not_Delete_A_Tenants_Snapshot_On_An_Unscoped_DeleteSnapshotsOlderThan()
	{
		await InitializeAsync().ConfigureAwait(false);
		var aggregateId = Guid.NewGuid().ToString();

		var scopedStore = CreateStore(tenantScoped: true);
		using (TenantContextHolder.BeginScope(TenantB))
		{
			await scopedStore.SaveSnapshotAsync(
				CreateSnapshot(aggregateId, 3, "tenant-b-data", TenantB), CancellationToken.None).ConfigureAwait(false);
		}

		var unscopedStore = CreateStore(tenantScoped: false);
		await unscopedStore.DeleteSnapshotsOlderThanAsync(
			aggregateId, AggregateType, 10, CancellationToken.None).ConfigureAwait(false);

		ISnapshot? survivor;
		using (TenantContextHolder.BeginScope(TenantB))
		{
			survivor = await scopedStore.GetLatestSnapshotAsync(
				aggregateId, AggregateType, CancellationToken.None).ConfigureAwait(false);
		}

		_ = survivor.ShouldNotBeNull(
			"Regression guard (GREEN): Oracle's unscoped DeleteSnapshotsOlderThan predicate is already "
			+ "`TENANTID IS NULL`; this arm goes RED if regressed to the empty-branch defect (18c3el) — the "
			+ "guarantee: an unscoped prune must NOT remove a tenant's snapshot");
	}

	/// <summary>LIVENESS. The owning tenant's own scoped DeleteSnapshotsOlderThan still prunes its snapshot.</summary>
	[Fact]
	public async Task Still_Prune_The_Owning_Tenants_Snapshot_On_A_Scoped_DeleteSnapshotsOlderThan()
	{
		await InitializeAsync().ConfigureAwait(false);
		var aggregateId = Guid.NewGuid().ToString();

		var scopedStore = CreateStore(tenantScoped: true);
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

	private ISnapshotStore CreateStore(bool tenantScoped) =>
		new OracleSnapshotStore(
			_fixture.CreateConnection,
			NullLogger<OracleSnapshotStore>.Instance,
			schema: _fixture.Schema,
			table: _fixture.TableName,
			tenantContext: tenantScoped
				? new AmbientHolderTenantContext()
				: UntenantedTestTenantContext.Instance);

	private static ISnapshot CreateSnapshot(string aggregateId, long version, string data, string? tenantId) =>
		new OracleUnscopedReadSnapshot(
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

	private sealed record OracleUnscopedReadSnapshot(
		string SnapshotId,
		string AggregateId,
		string AggregateType,
		long Version,
		DateTimeOffset CreatedAt,
		ReadOnlyMemory<byte> Data,
		IDictionary<string, object>? Metadata,
		string? TenantId) : ISnapshot;
}
