// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing.Postgres;
using Excalibur.EventSourcing.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

using Npgsql;

namespace Excalibur.EventSourcing.Tests.Tenancy;

// Independent regression lock (author != implementer, TestsDeveloper) for the safety-critical ocsbwb fix.
//
// THE FIX (ocsbwb): SqlServerProjectionStore and PostgresProjectionStore are always-tenant-scoped stores — every
// write stamps a TenantId discriminator, so a read with NO tenant predicate would return EVERY tenant's rows
// (false isolation / cross-tenant leak). The stores now resolve the row-level tenant via
// `TenantScope.Scoped(_tenantContext?.TenantId).TenantId`, which FAILS CLOSED: a null context OR a null/blank
// ambient tenant throws TenantRequiredException at the tenant-id line — BEFORE any connection is opened — so an
// unscoped (predicate-less) query is inexpressible. The prior code used TenantScope.FromContext (null context =>
// None => predicate-less => fail-OPEN) / threw the wrong exception type.
//
// SEAM: unit-testable with NO database. The throw fires at `TenantScope.Scoped(...)` before `_connectionFactory()`
// (SqlServer) / before the NpgsqlDataSource opens a connection (Postgres), so a fake ITenantContext drives the
// whole safety property with no real infra. The SqlServer arm uses a THROWING connection factory as a fail-closed
// SENTINEL: if the guard ever regressed to fail-open, the op would reach the factory and throw the sentinel
// instead of TenantRequiredException — turning the safety arm RED.
//
// SAFETY + LIVENESS (testing-patterns §3): a fail-closed guard asserted only on its safety half is satisfied by a
// store that throws for EVERY tenant (and never serves anyone). Each safety arm is paired with a liveness arm
// proving a RESOLVED tenant is admitted past the guard, so the fix cannot be "resolved" by always-throwing.
//
// NON-VACUITY: the pre-fix store threw ArgumentException (not TenantRequiredException) for a missing tenant, so the
// SAFETY arms are RED on committed HEAD before the fix; reverting to FromContext (null => None => no throw) makes
// the null-context safety arm RED (fail-open). The liveness arms are RED against an always-throwing guard.
//
// The real-infra tenant-B-cannot-see-tenant-A row-isolation liveness (SQL predicate correctness) is the province of
// the integration suite (per verify-against-real-infra-not-mock); THIS lock binds the fail-closed guard — a pure
// in-process property whose correct tool is a unit test that proves the throw precedes any I/O.
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
public sealed class ProjectionStoreTenantFailClosedShould
{
	private static readonly string[] MissingTenants = [null!, "", "   "];

	// ---------- SqlServer ----------

	[Fact]
	public async Task SqlServer_FailClosed_OnEveryOp_WhenAmbientTenantIsMissing()
	{
		// SAFETY. For a null, empty, or whitespace ambient tenant (and a null context), every tenant-scoped op must
		// throw TenantRequiredException at the tenant-id line, BEFORE reaching the (throwing sentinel) factory.
		foreach (var missing in MissingTenants)
		{
			var store = NewSqlStore(FakeTenant(missing));
			await AssertAllOpsThrow<TenantRequiredException>(
				store.GetByIdAsync, store.UpsertAsync, store.DeleteAsync, store.QueryAsync, store.CountAsync,
				$"SqlServer projection store must fail closed (TenantRequiredException) for a missing ambient tenant " +
				$"('{missing ?? "<null>"}'); a predicate-less query would leak every tenant's rows.");
		}
	}

	[Fact]
	public async Task SqlServer_FailClosed_WhenTenantContextIsNull()
	{
		// SAFETY (null context => Scoped(null) => throw). The store uses Scoped(_tenantContext?.TenantId), NOT
		// FromContext, so a null context fails closed rather than degrading to an unscoped (None) query. Reverting to
		// FromContext(null)=None makes this RED (fail-open).
		var store = NewSqlStore(tenantContext: null);
		await AssertAllOpsThrow<TenantRequiredException>(
			store.GetByIdAsync, store.UpsertAsync, store.DeleteAsync, store.QueryAsync, store.CountAsync,
			"SqlServer projection store must fail closed even when NO tenant context is registered — an " +
			"always-tenant-scoped store has no legitimate unscoped mode.");
	}

	[Fact]
	public async Task SqlServer_AdmitsAResolvedTenant_PastTheGuard()
	{
		// LIVENESS. A resolved tenant must pass the guard and reach the (throwing sentinel) factory — proving the
		// guard is keyed on tenant PRESENCE, not always-throwing. RED against a guard that always throws
		// TenantRequiredException.
		var store = NewSqlStore(FakeTenant("tenant-a"));

		var ex = await Should.ThrowAsync<Exception>(
			() => store.GetByIdAsync("id-1", CancellationToken.None));
		ex.ShouldBeOfType<ConnectionFactoryReachedException>(
			"A resolved tenant must be admitted past the fail-closed guard (reaching the connection factory). If a " +
			"TenantRequiredException surfaces here, the guard rejects a legitimate tenant and the store serves no one.");
	}

	// ---------- Postgres ----------

	[Fact]
	public async Task Postgres_FailClosed_OnEveryOp_WhenAmbientTenantIsMissing()
	{
		// SAFETY. Same fail-closed contract as SqlServer; the throw fires before the NpgsqlDataSource opens a
		// connection, so this is DB-free.
		foreach (var missing in MissingTenants)
		{
			var store = NewPgStore(FakeTenant(missing));
			await AssertAllOpsThrow<TenantRequiredException>(
				store.GetByIdAsync, store.UpsertAsync, store.DeleteAsync, store.QueryAsync, store.CountAsync,
				$"Postgres projection store must fail closed (TenantRequiredException) for a missing ambient tenant " +
				$"('{missing ?? "<null>"}').");
		}
	}

	[Fact]
	public async Task Postgres_FailClosed_WhenTenantContextIsNull()
	{
		// SAFETY (null context).
		var store = NewPgStore(tenantContext: null);
		await AssertAllOpsThrow<TenantRequiredException>(
			store.GetByIdAsync, store.UpsertAsync, store.DeleteAsync, store.QueryAsync, store.CountAsync,
			"Postgres projection store must fail closed even when NO tenant context is registered.");
	}

	[Fact]
	public async Task Postgres_AdmitsAResolvedTenant_PastTheGuard()
	{
		// LIVENESS. A resolved tenant must pass the guard; the op then proceeds to open a connection (which fails
		// with a connection error against the unroutable data source, NOT TenantRequiredException). The point is only
		// that the guard admits the tenant — a TenantRequiredException here would prove it rejects a legitimate one.
		var store = NewPgStore(FakeTenant("tenant-a"));

		var ex = await Should.ThrowAsync<Exception>(
			() => store.GetByIdAsync("id-1", CancellationToken.None));
		ex.ShouldNotBeOfType<TenantRequiredException>(
			"A resolved tenant must be admitted past the fail-closed guard; the op should fail on the connection, not " +
			"on the tenant guard. A TenantRequiredException here means the guard rejects a legitimate tenant.");
	}

	// ---------- helpers ----------

	// Runs all five tenant-scoped ops and asserts each throws TException. UpsertAsync needs a projection value; the
	// others do not. Every op is expected to fail at the same tenant guard, so one helper covers the full surface.
	private static async Task AssertAllOpsThrow<TException>(
		Func<string, CancellationToken, Task<TestProjection?>> getById,
		Func<string, TestProjection, CancellationToken, Task> upsert,
		Func<string, CancellationToken, Task> delete,
		Func<System.Collections.Generic.IDictionary<string, object>?, QueryOptions?, CancellationToken, Task<System.Collections.Generic.IReadOnlyList<TestProjection>>> query,
		Func<System.Collections.Generic.IDictionary<string, object>?, CancellationToken, Task<long>> count,
		string because)
		where TException : Exception
	{
		_ = await Should.ThrowAsync<TException>(() => getById("id-1", CancellationToken.None), because);
		_ = await Should.ThrowAsync<TException>(() => upsert("id-1", new TestProjection(), CancellationToken.None), because);
		_ = await Should.ThrowAsync<TException>(() => delete("id-1", CancellationToken.None), because);
		_ = await Should.ThrowAsync<TException>(() => query(null, null, CancellationToken.None), because);
		_ = await Should.ThrowAsync<TException>(() => count(null, CancellationToken.None), because);
	}

	private static SqlServerProjectionStore<TestProjection> NewSqlStore(ITenantContext? tenantContext) =>
		new(
			// Throwing sentinel: reached ONLY if the fail-closed guard is bypassed (fail-open regression).
			connectionFactory: () => throw new ConnectionFactoryReachedException(),
			logger: NullLogger<SqlServerProjectionStore<TestProjection>>.Instance,
			tableName: "TenantScopedProjection",
			jsonOptions: null,
			tenantContext: tenantContext);

	private static PostgresProjectionStore<TestProjection> NewPgStore(ITenantContext? tenantContext) =>
		new(
			// Unroutable data source (port 1, 1s timeout) — never opened by the safety arms (guard throws first);
			// the liveness arm reaches it and fails fast on the connection, proving the tenant was admitted.
			dataSource: NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Timeout=1;Command Timeout=1;Database=x;Username=x"),
			logger: NullLogger<PostgresProjectionStore<TestProjection>>.Instance,
			tableName: "tenant_scoped_projection",
			jsonOptions: null,
			tenantContext: tenantContext);

	private static ITenantContext FakeTenant(string? tenantId) => new StubTenantContext(tenantId);

	private sealed class StubTenantContext(string? tenantId) : ITenantContext
	{
		public string? TenantId { get; } = tenantId;

		public bool HasTenant => !string.IsNullOrEmpty(TenantId);
	}

	private sealed class ConnectionFactoryReachedException()
		: InvalidOperationException("Fail-closed guard was bypassed: the connection factory was reached for a request that lacked a resolved tenant.");

	private sealed class TestProjection
	{
		public string Id { get; init; } = string.Empty;

		public string Name { get; init; } = string.Empty;
	}
}
