// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;
using System.Text.RegularExpressions;

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.EventSourcing.Postgres.Requests;

using Xunit;

namespace Excalibur.EventSourcing.Tests.Postgres.Requests;

// POSTGRES half of the s7yc33 regression lock — INVERTED by the S902 keyed migration (FR-8).
//
// PREMISE CHANGE: the pre-migration lock asserted "TenantScope.Untenanted emits NO tenant reference," on the
// premise that a non-multi-tenant table has no tenant_id column. FR-8 makes the event-store Events table a
// KEYED tenant table (tenant_id NOT NULL, always). Every tenant-columned request now routes its scope through
// KeyedTenantPartition.FromScope, which has NO empty inhabitant: an unscoped (None) request binds the reserved
// __untenanted__ sentinel and emits a real equality predicate, NEVER an empty one. An un-partitioned
// (all-tenants) read/erase against the keyed table is unconstructable.
//
//   * TenantScope.Untenanted            -> a tenant term is present; @TenantId binds "__untenanted__" (never empty).
//   * TenantScope.Scoped("t")     -> a tenant term is present; @TenantId binds the real tenant "t".
//   * TenantScope.Scoped(null/"") -> THROWS TenantRequiredException at construction (unsafe shape unrepresentable).
//
// SAFETY: an unscoped request can never emit an empty predicate (which would match every tenant's rows) —
// it binds the sentinel, and the NULL-safe fold maps legacy-NULL untenanted rows onto that same sentinel, so an
// unscoped operation reaches ONLY untenanted rows.
// LIVENESS: a scoped request still binds the caller's real tenant (A reaches A); the unscoped path still
// reaches the untenanted partition (the sentinel), so single-tenant deployments keep working — a request
// that bound the sentinel (or empty) for EVERYTHING would fail the scoped arm.
//
// The assertion is deliberately PROPERTY-BASED, not exact-string: it requires "a tenant term that binds
// @TenantId" and pins the BOUND VALUE, so it stays GREEN across the NULL-safe COALESCE form and a bare
// `tenant_id = @TenantId` form, while staying RED on the only shape that matters — no tenant term at all
// (fail-open across every tenant), or the sentinel bound for a real tenant.
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Database", "Postgres")]
public sealed class TenantColumnedRequestFailsClosedShould
{
	private const string UntenantedSentinel = "__untenanted__";

	// A tenant term is any equality between the tenant column and the @TenantId bind, with or without a
	// NULL-safe COALESCE fold on either side. Form-agnostic by design; an EMPTY predicate matches nothing here.
	private static readonly Regex TenantTerm = new(
		@"(COALESCE\s*\(\s*tenant_id\s*,[^)]*\)|tenant_id)\s*=\s*(COALESCE\s*\(\s*)?@TenantId",
		RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	private static readonly CancellationToken Ct = CancellationToken.None;
	private static readonly Guid ErasureRequestId = new("00000000-0000-0000-0000-000000000042");

	// Each request exposes its emitted SQL and its bound @TenantId term, so the lock asserts both the
	// predicate SHAPE (never empty) and the bound VALUE (real tenant vs sentinel).
	private static readonly (string Name, Func<TenantScope, (string Sql, string? BoundTenant)> Emit)[] TenantColumnedRequests =
	[
		("LoadEventsRequest", s => Emit(new LoadEventsRequest("agg-1", "OrderAggregate", -1, s, Ct))),
		("GetCurrentVersionRequest", s => Emit(new GetCurrentVersionRequest("agg-1", "OrderAggregate", null, s, Ct))),
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
		// tenant term binding the reserved sentinel, NEVER an empty predicate. An empty predicate would match
		// every tenant's rows (fail-open read / cross-tenant GDPR erase). The NULL-safe fold maps legacy-NULL
		// untenanted rows onto the same sentinel, so an unscoped operation reaches ONLY untenanted rows.
		var wrong = new List<string>();

		foreach (var (name, emit) in TenantColumnedRequests)
		{
			var (sql, boundTenant) = emit(TenantScope.Untenanted);
			if (!TenantTerm.IsMatch(sql) || !string.Equals(boundTenant, UntenantedSentinel, StringComparison.Ordinal))
			{
				wrong.Add($"{name} (sql-has-tenant-term={TenantTerm.IsMatch(sql)}, @TenantId='{boundTenant}')");
			}
		}

		wrong.ShouldBeEmpty(
			"Every unscoped Postgres tenant-columned request must emit a tenant term binding @TenantId to the " +
			"reserved '" + UntenantedSentinel + "' sentinel — never an empty predicate that would match every " +
			"tenant's rows. Offenders: " + string.Join(", ", wrong));
	}

	[Fact]
	public void EmitTheRealTenantPredicate_WhenScoped_ForEveryTenantColumnedRequest()
	{
		// LIVENESS — proves the unscoped arm is not vacuous (a request binding the sentinel for EVERYTHING would
		// pass it while destroying multi-tenancy): a real tenant still produces a tenant term binding the
		// caller's real tenant, so a scoped read/erase reaches exactly that tenant's rows (A reaches A).
		var wrong = new List<string>();

		foreach (var (name, emit) in TenantColumnedRequests)
		{
			var (sql, boundTenant) = emit(TenantScope.Scoped("tenant-42"));
			if (!TenantTerm.IsMatch(sql) || !string.Equals(boundTenant, "tenant-42", StringComparison.Ordinal))
			{
				wrong.Add($"{name} (sql-has-tenant-term={TenantTerm.IsMatch(sql)}, @TenantId='{boundTenant}')");
			}
		}

		wrong.ShouldBeEmpty(
			"Every scoped Postgres tenant-columned request must emit a tenant term binding @TenantId to the " +
			"caller's real tenant, so each read/write is scoped to that tenant. Offenders: " +
			string.Join(", ", wrong));
	}
}
