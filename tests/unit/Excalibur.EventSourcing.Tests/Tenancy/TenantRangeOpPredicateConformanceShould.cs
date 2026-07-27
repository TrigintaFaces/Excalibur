// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

using EsOra = Excalibur.EventSourcing.Oracle.Requests;
using EsPg = Excalibur.EventSourcing.Postgres.Requests;
using EsSql = Excalibur.EventSourcing.SqlServer.Requests;

namespace Excalibur.EventSourcing.Tests.Tenancy;

/// <summary>
/// Conformance detector for tenant-partitioned RANGE mutations (DELETE / UPDATE that can touch
/// more than one keyed row). Each such statement, when built with the unscoped
/// <see cref="TenantScope.None"/> omission, MUST isolate by tenant — otherwise it reaches another
/// tenant's rows, and on a destructive op that DESTROYS or exposes their data.
/// <para>
/// The request object is where the caller's declared intent becomes SQL, so asserting the emitted
/// <c>Command.CommandText</c> reads the ACTUAL predicate (reachability-aware by construction), not a
/// signature. The emitted tenant fragment must be exactly one of three sanctioned shapes:
/// </para>
/// <list type="number">
/// <item><description>a fail-closed predicate on a tenant-columned table:
/// <c>= @TenantId</c> (scoped), <c>IS NULL</c> (untenanted), <c>= COALESCE(@TenantId, '')</c>
/// (the empty-string-sentinel untenanted partition), or the NULL-safe sentinel-folding form
/// <c>COALESCE(col, @UntenantedSentinel) = @TenantId</c>, which isolates on the scoped AND the
/// unscoped path and therefore needs no omission case at all;</description></item>
/// <item><description>an explicit, declared estate-wide sweep (no tenant fragment, only when the
/// caller opted in — exercised by the saga purge liveness tests);</description></item>
/// <item><description>column-absent single-partition: an empty fragment, sanctioned ONLY when the
/// SAME store's unscoped INSERT path emits no tenant column (so any predicate would be a SQL error).
/// This is a paired structural check, not a trust-the-comment exception. The events-table erase no
/// longer claims this exception — it is column-present on both halves and lives in category 1; the
/// paired check is kept and inverted, so a regression that drops the column from either half is
/// still caught.</description></item>
/// </list>
/// The companion <c>eng/ci/tenant-range-op-coverage-gate.sh</c> guarantees this set is COMPLETE, so a
/// newly added range op cannot escape the check by not being listed here.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
[Trait("Feature", "TenantIsolation")]
public sealed class TenantRangeOpPredicateConformanceShould
{
	private const string AggregateId = "agg-1";
	private const string AggregateType = "OrderAggregate";
	private static readonly TenantScope Scoped = TenantScope.Scoped("tenant-1");

	// A fail-closed tenant fragment on a column-present table is exactly one of these safe shapes.
	// Every alternative that names the column (e.g. `IS NOT NULL`, `<> @t`) leaks and is NOT here:
	// the whitelist enumerates the SAFE forms, because the leaks cannot be enumerated.
	private static bool IsFailClosedFragment(string sql, string column)
	{
		return sql.Contains($"{column} = @TenantId", StringComparison.Ordinal)
			|| sql.Contains($"{column} = :TenantId", StringComparison.Ordinal)
			|| sql.Contains($"{column} IS NULL", StringComparison.Ordinal)
			|| sql.Contains($"{column} = COALESCE(@TenantId, '')", StringComparison.Ordinal)
			// The sentinel-folding form: the column side is NULL-safe (a legacy NULL row folds to the
			// '__untenanted__' sentinel) and the bound term is always concrete, so it isolates on BOTH the
			// scoped and the unscoped path. It is strictly stronger than an omitted predicate, which is why
			// the events-table erase/load moved onto it.
			|| sql.Contains($"COALESCE({column}, @UntenantedSentinel) = @TenantId", StringComparison.Ordinal)
			|| sql.Contains($"COALESCE({column}, :UntenantedSentinel) = :TenantId", StringComparison.Ordinal);
	}

	// ---- Snapshot range deletes: column-present -> MUST emit a fail-closed predicate on None -------

	public static TheoryData<string, string> SnapshotDeletes()
	{
		var data = new TheoryData<string, string>();
		// (emitted-None-sql, tenant-column)
		data.Add(new EsSql.DeleteSnapshotsRequest(AggregateId, AggregateType, TenantScope.None, default).Command.CommandText, "TenantId");
		data.Add(new EsSql.DeleteSnapshotsOlderThanRequest(AggregateId, AggregateType, 5, TenantScope.None, default).Command.CommandText, "TenantId");
		data.Add(new EsPg.DeleteSnapshotsRequest(AggregateId, AggregateType, TenantScope.None, default).Command.CommandText, "tenant_id");
		data.Add(new EsPg.DeleteSnapshotsOlderThanRequest(AggregateId, AggregateType, 5, TenantScope.None, default).Command.CommandText, "tenant_id");
		data.Add(new EsOra.DeleteSnapshotsRequest(AggregateId, AggregateType, TenantScope.None, default).Command.CommandText, "TENANTID");
		data.Add(new EsOra.DeleteSnapshotsOlderThanRequest(AggregateId, AggregateType, 5, TenantScope.None, default).Command.CommandText, "TENANTID");
		return data;
	}

	[Theory]
	[MemberData(nameof(SnapshotDeletes))]
	public void SnapshotDelete_FailsClosed_OnUnscopedOmission(string emittedSql, string tenantColumn)
	{
		// SAFETY: the unscoped snapshot delete must isolate by tenant (fail-closed), never a bare range delete.
		IsFailClosedFragment(emittedSql, tenantColumn).ShouldBeTrue(
			$"unscoped snapshot delete must emit a fail-closed tenant predicate; got: {emittedSql}");
	}

	[Fact]
	public void SnapshotDelete_EmitsScopedPredicate_WhenScoped()
	{
		// LIVENESS: a scoped delete still isolates to the tenant's own rows.
		//
		// REBOUND to the converged contract. This arm previously asserted
		// `TenantId = COALESCE(@TenantId, '')` — the PRE-convergence shape. The snapshot delete now emits an
		// UNCONDITIONAL `AND TenantId = @TenantId` and routes the term through KeyedTenantPartition, which has
		// no empty inhabitant, so the empty-string sentinel the old assertion described is no longer emitted
		// anywhere and the arm was failing against correct code (a stale F-5 sibling, not a product defect).
		//
		// The rebind is STRICTLY STRONGER, not a relaxation to whatever passes: the predicate assertion is kept
		// AND the bound term is now asserted too. `ShouldContain("TenantId = @TenantId")` on its own would be
		// WEAKER than what it replaced, because the isolation of an unscoped delete lives entirely in the VALUE
		// bound to @TenantId — the SQL text is identical on both paths, so text alone can no longer tell a
		// tenant-isolated delete from one that sweeps the estate.
		var sqlServer = new EsSql.DeleteSnapshotsRequest(AggregateId, AggregateType, Scoped, default);
		sqlServer.Command.CommandText.ShouldContain("TenantId = @TenantId");
		BoundTenantTerm(sqlServer.Command, "@TenantId").ShouldBe(
			"tenant-1",
			"a scoped delete must bind the caller's own tenant term, so it reaches that tenant's rows and no others.");

		var oracle = new EsOra.DeleteSnapshotsRequest(AggregateId, AggregateType, Scoped, default);
		oracle.Command.CommandText.ShouldContain("TENANTID = :TenantId");
		BoundTenantTerm(oracle.Command, "TenantId").ShouldBe("tenant-1");
	}

	[Fact]
	public void SnapshotDelete_BindsTheUntenantedSentinel_OnTheUnscopedPath()
	{
		// The half the SQL text cannot express. Post-convergence the predicate is emitted UNCONDITIONALLY, so
		// `SnapshotDelete_FailsClosed_OnUnscopedOmission` — which reads CommandText only — now sees an
		// identical statement whether the term is isolated or not. It would pass against a delete that binds
		// an empty string and matches every row whose tenant column is '' across the estate.
		//
		// What actually fails this delete closed is the VALUE: KeyedTenantPartition has no empty inhabitant, so
		// the unscoped path binds the reserved '__untenanted__' sentinel. Asserting the literal (not the
		// internal constant) is deliberate — this term is written into and matched against persisted rows, so
		// it is a wire value, and a change to it must redden a lock rather than pass silently.
		var sqlServer = new EsSql.DeleteSnapshotsRequest(AggregateId, AggregateType, TenantScope.None, default);
		BoundTenantTerm(sqlServer.Command, "@TenantId").ShouldBe(
			"__untenanted__",
			"an unscoped delete must bind the reserved sentinel — never an empty term, which is how a destructive statement ends up matching every tenant's rows.");

		var oracle = new EsOra.DeleteSnapshotsRequest(AggregateId, AggregateType, TenantScope.None, default);
		BoundTenantTerm(oracle.Command, "TenantId").ShouldBe("__untenanted__");

		// LIVENESS pair: the two paths must bind DIFFERENT terms. An implementation that bound the sentinel
		// unconditionally would satisfy the assertion above while isolating nothing.
		BoundTenantTerm(
			new EsSql.DeleteSnapshotsRequest(AggregateId, AggregateType, Scoped, default).Command,
			"@TenantId")
			.ShouldNotBe("__untenanted__", "a scoped delete must not collapse onto the untenanted partition.");
	}

	/// <summary>
	/// Reads the term actually bound to the tenant parameter. The emitted SQL is identical on the scoped and
	/// unscoped paths, so this value — not the statement text — is where tenant isolation is decided.
	/// </summary>
	private static string? BoundTenantTerm(Dapper.CommandDefinition command, string parameterName)
	{
		var parameters = command.Parameters.ShouldBeOfType<Dapper.DynamicParameters>(
			"the request must bind its tenant term as a parameter, never inline it into the statement.");

		return parameters.Get<string>(parameterName);
	}

	// ---- EraseEvents: column-absent single-partition (category 3) -> paired structural check ------

	[Theory]
	[InlineData("SqlServer")]
	[InlineData("Postgres")]
	[InlineData("Oracle")]
	public void EraseEvents_FailsClosed_OnUnscopedOmission_PairedWithAColumnPresentInsert(string provider)
	{
		// The events table is now COLUMN-PRESENT on both halves, so this op moved from the category-3
		// (column-absent, empty-predicate) exception into category 1 (fail-closed predicate). The paired
		// structural rule still binds, just inverted: BOTH halves must name the tenant column on the None
		// path. An empty predicate here would tombstone every tenant's rows for this aggregate — a
		// destructive cross-tenant GDPR erase — so unscoped MUST bind the '__untenanted__' sentinel, not
		// omit the predicate.
		var (eraseNone, insertNone, column) = EmittedErasePair(provider);

		insertNone.ShouldContain(column, Case.Insensitive,
			$"{provider} unscoped INSERT must emit the tenant column — the erase predicate below references " +
			"it, so an absent column would make the erase an `Invalid column name` failure at runtime");
		IsFailClosedFragment(eraseNone, column).ShouldBeTrue(
			$"{provider} unscoped erase must isolate by the untenanted sentinel, never emit an empty " +
			$"predicate (that would erase across tenants); got: {eraseNone}");
	}

	[Theory]
	[InlineData("SqlServer", "TenantId")]
	[InlineData("Postgres", "tenant_id")]
	[InlineData("Oracle", "TENANTID")]
	public void EraseEvents_EmitsScopedPredicate_WhenScoped(string provider, string column)
	{
		// LIVENESS: when a tenant IS resolved, the erase isolates to that tenant's own rows. Paired with
		// the None arm above, this proves the predicate is UNCONDITIONAL — present and fail-closed on both
		// paths — rather than a predicate that only appears when someone remembered to scope the call.
		var scopedSql = EmittedEraseScoped(provider);

		IsFailClosedFragment(scopedSql, column).ShouldBeTrue(
			$"{provider} scoped erase must emit a fail-closed tenant predicate; got: {scopedSql}");
		scopedSql.ShouldContain(column, Case.Sensitive,
			$"{provider} scoped erase must name the tenant column exactly as the table declares it");
	}

	// ---- Non-vacuity: the whitelist rejects a leaky fragment --------------------------------------

	[Fact]
	public void Whitelist_Rejects_LeakyTenantFragments()
	{
		// A fragment that references the column but does not isolate (the classic leaks) is NOT whitelisted.
		IsFailClosedFragment("DELETE FROM t WHERE x = 1 AND TenantId IS NOT NULL", "TenantId").ShouldBeFalse();
		IsFailClosedFragment("DELETE FROM t WHERE x = 1 AND TenantId <> @TenantId", "TenantId").ShouldBeFalse();
		IsFailClosedFragment("DELETE FROM t WHERE x = 1", "TenantId").ShouldBeFalse();
		// And it accepts each sanctioned fail-closed shape.
		IsFailClosedFragment("... AND TenantId = @TenantId", "TenantId").ShouldBeTrue();
		IsFailClosedFragment("... AND TENANTID IS NULL", "TENANTID").ShouldBeTrue();
		IsFailClosedFragment("... AND tenant_id = COALESCE(@TenantId, '')", "tenant_id").ShouldBeTrue();
	}

	// ---- fixtures --------------------------------------------------------------------------------

	private static readonly Guid ErasureRequestId = new("00000000-0000-0000-0000-000000000042");

	private static (string erase, string insert, string column) EmittedErasePair(string provider) => provider switch
	{
		"SqlServer" => (
			new EsSql.EraseEventsRequest(AggregateId, AggregateType, ErasureRequestId, TenantScope.None, default).Command.CommandText,
			SqlServerInsertNone(), "TenantId"),
		"Postgres" => (
			new EsPg.EraseEventsRequest(AggregateId, AggregateType, ErasureRequestId, TenantScope.None, default).Command.CommandText,
			PostgresInsertNone(), "tenant_id"),
		"Oracle" => (
			new EsOra.EraseEventsRequest(AggregateId, AggregateType, ErasureRequestId, TenantScope.None, default).Command.CommandText,
			OracleInsertNone(), "TENANTID"),
		_ => throw new ArgumentOutOfRangeException(nameof(provider)),
	};

	private static string EmittedEraseScoped(string provider) => provider switch
	{
		"SqlServer" => new EsSql.EraseEventsRequest(AggregateId, AggregateType, ErasureRequestId, Scoped, default).Command.CommandText,
		"Postgres" => new EsPg.EraseEventsRequest(AggregateId, AggregateType, ErasureRequestId, Scoped, default).Command.CommandText,
		"Oracle" => new EsOra.EraseEventsRequest(AggregateId, AggregateType, ErasureRequestId, Scoped, default).Command.CommandText,
		_ => throw new ArgumentOutOfRangeException(nameof(provider)),
	};

	private static EsSql.EventInsertRow SqlRow() =>
		new("e1", AggregateId, AggregateType, "Created", [1], null, 0, DateTimeOffset.UnixEpoch);
	private static EsPg.EventInsertRow PgRow() =>
		new("e1", AggregateId, AggregateType, "Created", [1], null, 0, DateTimeOffset.UnixEpoch);
	private static EsOra.EventInsertRow OraRow() =>
		new("e1", AggregateId, AggregateType, "Created", [1], null, 0, DateTimeOffset.UnixEpoch);

	private static string SqlServerInsertNone() =>
		new EsSql.InsertEventsBatchRequest([SqlRow()], null, TenantScope.None, default).Command.CommandText;
	private static string PostgresInsertNone() =>
		new EsPg.InsertEventsBatchRequest([PgRow()], null, TenantScope.None, default).Command.CommandText;
	private static string OracleInsertNone() =>
		new EsOra.InsertEventsBatchRequest([OraRow()], null, TenantScope.None, default).Command.CommandText;
}
