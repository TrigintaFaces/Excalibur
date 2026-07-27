// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Serialization;
using Excalibur.Saga.SqlServer.Requests;

namespace Excalibur.Data.Tests.Saga;

// Independent regression lock (author != implementer) for the SqlServer saga request tenant-discriminator
// fail-closed rule, now enforced STRUCTURALLY through the TenantScope seam — symmetry with the event-store
// TenantColumnedRequestFailsClosedShould lock. The saga request DTOs migrated from the fail-OPEN
// `string? tenantId = null` pattern (a null tenant silently dropped the predicate) to the fail-CLOSED
// TenantScope struct: a scoped request with no tenant is now UNREPRESENTABLE by construction.
//
//   * TenantScope.None            -> the explicit non-multi-tenant path. Emits NO tenant gate: the Load
//                                    WHERE / Summary WHERE has no "AND TenantId = @TenantId", and the Save
//                                    MERGE ON clause has no "AND target.TenantId = @TenantId".
//   * TenantScope.Scoped("t")     -> emits the tenant gate UNCONDITIONALLY and binds @TenantId.
//   * TenantScope.Scoped(null/"")  -> THROWS TenantRequiredException at scope construction, so a predicate-
//                                    less-yet-tenant-active request can never be built (the widening bug is
//                                    inexpressible). TenantScope.FromContext(activeCtx-with-null-tenant),
//                                    the store's actual call pattern, fails closed the same way.
//
// This is the UNIT tier — it exercises the DTOs' generated SQL + the TenantScope seam directly, no database.
// The real cross-tenant isolation arm (a real SQL Server engine evaluates the predicate) lives in the
// integration tier (SqlServerSagaStoreTenantIsolationShould).
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class SagaTenantScopedRequestFailsClosedShould
{
	private static readonly CancellationToken Ct = CancellationToken.None;
	private const string QualifiedTableName = "[dispatch].[sagas]";

	// Each tenant-columned saga request DTO as a factory returning its generated SQL, paired with the exact
	// scoped-only predicate marker that MUST appear when scoped and MUST be absent when unscoped. (Save always
	// writes the TenantId column in its SET/INSERT list, so the gate marker is the MERGE ON clause, not a bare
	// "TenantId = @TenantId".)
	private static readonly (string Name, Func<TenantScope, string> CommandTextFor, string ScopedMarker)[] TenantColumnedRequests =
	[
		("LoadSagaRequest",
			s => new LoadSagaRequest<TestSagaState>(Guid.NewGuid(), new DispatchJsonSerializer(), QualifiedTableName, s, Ct).Command.CommandText,
			"AND TenantId = @TenantId"),
		("SaveSagaRequest",
			s => new SaveSagaRequest<TestSagaState>(new TestSagaState { SagaId = Guid.NewGuid() }, new DispatchJsonSerializer(), QualifiedTableName, s, Ct).Command.CommandText,
			"AND target.TenantId = @TenantId"),
		("GetSagaSummaryRequest",
			s => new GetSagaSummaryRequest(Guid.NewGuid(), QualifiedTableName, s, Ct).Command.CommandText,
			"AND TenantId = @TenantId"),
	];

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void FailClosed_ThrowingTenantRequired_WhenScopedWithNoTenant(string? tenantId)
	{
		// SAFETY — the fail-closed-by-construction invariant. A scoped request with no tenant is
		// UNREPRESENTABLE: TenantScope.Scoped rejects a null/empty/whitespace tenant, so the pre-migration
		// widening bug (a null tenantId silently drops the predicate while a tenant is active) can never reach
		// a saga request DTO. RED against the old fail-open surface that let a null tenant drop the predicate.
		Should.Throw<TenantRequiredException>(() => TenantScope.Scoped(tenantId));

		// The store's actual call pattern — TenantScope.FromContext over an active-but-unresolved tenant
		// context (HasTenant true, TenantId null) — fails closed identically rather than emitting a
		// predicate-less query.
		Should.Throw<TenantRequiredException>(
			() => TenantScope.FromContext(new NullTenantContext(tenantId)));
	}

	[Fact]
	public void EmitTheTenantGate_EvenWhenUnscoped_ForEverySagaRequest()
	{
		// SAFETY — the gate is UNCONDITIONAL. Every saga request must carry the tenant predicate on the
		// unscoped path too, bound to the reserved untenanted sentinel.
		//
		// This arm asserted the OPPOSITE — that TenantScope.None emits NO gate — which described a live
		// cross-tenant defect as a requirement. The predicate used to be built as
		//     scope.IsScoped ? " AND TenantId = @TenantId" : string.Empty
		// so an UNSCOPED load matched on SagaId + SagaType alone and returned whichever tenant's saga
		// carried that id.
		//
		// The old arm's stated fear — "an impl that emits the gate unconditionally breaks every non-MT
		// deployment" — is answered by the sentinel, not by dropping the predicate: an untenanted
		// deployment writes and reads the reserved value, so the gate always matches its own partition and
		// never spans tenants. Inverted rather than deleted, so the ungated path stays unreachable.
		var ungated = new List<string>();

		foreach (var (name, commandTextFor, scopedMarker) in TenantColumnedRequests)
		{
			var sql = commandTextFor(TenantScope.None);
			if (!sql.Contains(scopedMarker, StringComparison.Ordinal))
			{
				ungated.Add(name);
			}
		}

		ungated.ShouldBeEmpty(
			"These saga request DTOs emitted NO tenant gate on the unscoped (TenantScope.None) path: " +
			string.Join(", ", ungated) + ". A predicate-less saga query matches on SagaId + SagaType alone " +
			"and returns whichever tenant's row carries that id — the cross-tenant read the unconditional " +
			"predicate closes.");
	}

	[Fact]
	public void EmitTheTenantGate_WhenScoped_ForEverySagaRequest()
	{
		// LIVENESS (scoped) — a real tenant produces a genuinely tenant-gated query. Without this arm the
		// SAFETY theory is satisfied by a DTO that throws / drops the gate on EVERYTHING; this proves a valid
		// tenant still builds a scoped query gating on @TenantId.
		var missing = new List<string>();

		foreach (var (name, commandTextFor, scopedMarker) in TenantColumnedRequests)
		{
			var sql = commandTextFor(TenantScope.Scoped("tenant-42"));
			if (!sql.Contains(scopedMarker, StringComparison.Ordinal))
			{
				missing.Add(name);
			}
		}

		missing.ShouldBeEmpty(
			"These saga request DTOs did not emit the tenant gate for a real tenant: " +
			string.Join(", ", missing) + ". When scoped, the gate must be present so every read/write is " +
			"scoped to the caller's tenant.");
	}

	private sealed class TestSagaState : SagaState
	{
	}

	// An ambient tenant context that is "active" but resolves no tenant — the fail-closed trigger for
	// TenantScope.FromContext (multi-tenancy registered, tenant unresolved).
	private sealed class NullTenantContext(string? tenantId) : ITenantContext
	{
		public string? TenantId { get; } = tenantId;

		public bool HasTenant => false;
	}
}
