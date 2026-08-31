// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.EventSourcing.SqlServer.Requests;

using Xunit;

namespace Excalibur.EventSourcing.Tests.SqlServer.Requests;

// SQL SERVER half of the s7yc33 regression lock — INVERTED by the S902 keyed migration (FR-8).
//
// PREMISE CHANGE: the pre-migration lock asserted "TenantScope.Untenanted emits NO tenant reference," on the
// premise that a non-multi-tenant table has no TenantId column. FR-8 makes the event-store Events table a
// KEYED tenant table (TenantId NOT NULL, always). Every tenant-columned request now routes its scope through
// KeyedTenantPartition.FromScope, which has NO empty inhabitant: an unscoped (None) request binds the reserved
// __untenanted__ sentinel and emits a real equality predicate, NEVER an empty one. An un-partitioned
// (all-tenants) read/erase against the keyed table is unconstructable.
//
// All four SqlServer event-store requests now emit the identical fail-closed predicate:
//     AND COALESCE(TenantId, @UntenantedSentinel) = @TenantId
//   * TenantScope.Untenanted            -> the predicate is present; @TenantId binds "__untenanted__" (never empty).
//   * TenantScope.Scoped("t")     -> the predicate is present; @TenantId binds the real tenant "t".
//   * TenantScope.Scoped(null/"") -> THROWS TenantRequiredException at construction (unsafe shape unrepresentable).
//
// SAFETY: an unscoped request can never emit an empty predicate (which would match every tenant's rows) —
// it binds the sentinel, and COALESCE folds legacy-NULL untenanted rows onto that same sentinel, so an
// unscoped operation reaches ONLY untenanted rows.
// LIVENESS: a scoped request still binds the caller's real tenant (A reaches A); the unscoped path still
// reaches the untenanted partition (the sentinel), so single-tenant deployments keep working — a request
// that bound the sentinel (or empty) for EVERYTHING would fail the scoped arm.
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Database", "SqlServer")]
public sealed class TenantColumnedRequestFailsClosedShould
{
    private const string UntenantedSentinel = "__untenanted__";
    private const string TenantPredicate = "COALESCE(TenantId, @UntenantedSentinel) = @TenantId";

    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly Guid ErasureRequestId = new("00000000-0000-0000-0000-000000000042");

    // Each request exposes its emitted SQL and its bound @TenantId term, so the lock asserts both the
    // predicate SHAPE (never empty) and the bound VALUE (real tenant vs sentinel).
    private static readonly (string Name, Func<TenantScope, (string Sql, string? BoundTenant)> Emit)[] TenantColumnedRequests =
    [
        ("LoadEventsRequest", s => Emit(new LoadEventsRequest("agg-1", "OrderAggregate", -1, s, Ct))),
        ("GetCurrentVersionRequest", s => Emit(new GetCurrentVersionRequest("agg-1", "OrderAggregate", (IDbTransaction?)null, s, Ct))),
        ("EraseEventsRequest", s => Emit(new EraseEventsRequest("agg-1", "OrderAggregate", ErasureRequestId, s, Ct))),
        ("IsErasedRequest", s => Emit(new IsErasedRequest("agg-1", "OrderAggregate", s, Ct))),
    ];

    private static (string Sql, string? BoundTenant) Emit<TModel>(DataRequestBase<IDbConnection, TModel> request) =>
        (request.Command.CommandText, request.Parameters.Get<string>("@TenantId"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FailClosed_ThrowingTenantRequired_WhenScopedWithNoTenant(string? tenantId) =>
        Should.Throw<TenantRequiredException>(() => TenantScope.Scoped(tenantId));

    [Fact]
    public void EmitTheUntenantedSentinelPredicate_WhenUnscoped_ForEveryTenantColumnedRequest()
    {
        // SAFETY — an unscoped (None) request against the KEYED Events table must emit a real fail-closed
        // predicate binding the reserved sentinel, NEVER an empty predicate. An empty predicate would match
        // every tenant's rows (fail-open read / cross-tenant GDPR erase). COALESCE folds legacy-NULL untenanted
        // rows onto the same sentinel, so an unscoped operation reaches ONLY untenanted rows.
        var wrong = new List<string>();

        foreach (var (name, emit) in TenantColumnedRequests)
        {
            var (sql, boundTenant) = emit(TenantScope.Untenanted);
            if (!sql.Contains(TenantPredicate, StringComparison.Ordinal) ||
                !string.Equals(boundTenant, UntenantedSentinel, StringComparison.Ordinal))
            {
                wrong.Add($"{name} (sql-has-predicate={sql.Contains(TenantPredicate, StringComparison.Ordinal)}, @TenantId='{boundTenant}')");
            }
        }

        wrong.ShouldBeEmpty(
            "Every unscoped SqlServer tenant-columned request must emit `" + TenantPredicate + "` and bind " +
            "@TenantId to the reserved '" + UntenantedSentinel + "' sentinel — never an empty predicate that " +
            "would match every tenant's rows. Offenders: " + string.Join(", ", wrong));
    }

    [Fact]
    public void EmitTheRealTenantPredicate_WhenScoped_ForEveryTenantColumnedRequest()
    {
        // LIVENESS — proves the unscoped arm is not vacuous (a request binding the sentinel for EVERYTHING would
        // pass it while destroying multi-tenancy): a real tenant still produces the same predicate binding the
        // caller's real tenant term, so a scoped read/erase reaches exactly that tenant's rows (A reaches A).
        var wrong = new List<string>();

        foreach (var (name, emit) in TenantColumnedRequests)
        {
            var (sql, boundTenant) = emit(TenantScope.Scoped("tenant-42"));
            if (!sql.Contains(TenantPredicate, StringComparison.Ordinal) ||
                !string.Equals(boundTenant, "tenant-42", StringComparison.Ordinal))
            {
                wrong.Add($"{name} (sql-has-predicate={sql.Contains(TenantPredicate, StringComparison.Ordinal)}, @TenantId='{boundTenant}')");
            }
        }

        wrong.ShouldBeEmpty(
            "Every scoped SqlServer tenant-columned request must emit `" + TenantPredicate + "` and bind " +
            "@TenantId to the caller's real tenant, so each read/write is scoped to that tenant. Offenders: " +
            string.Join(", ", wrong));
    }
}
