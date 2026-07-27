// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing.SqlServer.Requests;

using Xunit;

namespace Excalibur.EventSourcing.Tests.Sharding;

// Independent structural lock (author != implementer) for AC-K1 — the TenantScope seam that makes the
// unsafe multi-tenancy state UNREPRESENTABLE (row-discriminator isolation invariant, enforce-invariants-
// structurally). The one and only way to obtain a tenant-scoped request is TenantScope.Scoped(...), whose
// precondition rejects a null/whitespace tenant; a non-multi-tenant read is the distinct, explicit
// TenantScope.None construction. There is no third shape:
//
//   K1.3a  a null / empty / whitespace tenant is rejected at Scoped(...) with TenantRequiredException, so a
//          "scoped but no tenant" request cannot exist.
//   K1.3b  FromContext(null) == None (non-MT, no context registered); FromContext(non-null) == Scoped for the
//          resolved tenant; FromContext(non-null, unresolved tenant) fails closed.
//   K1 SQL the emitted request SQL contains the tenant predicate IFF the scope is Scoped (both arms — safety
//          and liveness — asserted through a real request DTO, here the SqlServer provider).
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class TenantScopeShould
{
	private static readonly CancellationToken Ct = CancellationToken.None;

	// ---- K1.3a: a scoped request with no tenant is unconstructable-by-type ----

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void RejectANullOrWhitespaceTenant_WhenScoped(string? tenantId)
	{
		// SAFETY — the fail-closed precondition. A null/empty/whitespace tenant cannot become a Scoped scope,
		// so the widening bug (a tenant-active query with no predicate) is inexpressible.
		Should.Throw<TenantRequiredException>(() => TenantScope.Scoped(tenantId));
	}

	[Fact]
	public void CarryTheValidatedTenant_WhenScoped()
	{
		// LIVENESS — a valid tenant produces a scoped value carrying the identifier.
		var scope = TenantScope.Scoped("tenant-7");

		scope.IsScoped.ShouldBeTrue();
		scope.TenantId.ShouldBe("tenant-7");
	}

	// ---- TenantScope.None: the explicit non-multi-tenant path, and the default(TenantScope) invariant ----

	[Fact]
	public void BeUnscoped_WhenNone()
	{
		TenantScope.None.IsScoped.ShouldBeFalse();
		TenantScope.None.TenantId.ShouldBeNull();
	}

	[Fact]
	public void TreatDefaultAsNone()
	{
		// Load-bearing: the request constructors default `scope = default`, which MUST equal None (the non-MT
		// path). If default ever became a scoped value, every omitted-scope call site would silently change
		// behavior.
		default(TenantScope).ShouldBe(TenantScope.None);
		default(TenantScope).IsScoped.ShouldBeFalse();
	}

	// ---- K1.3b: FromContext derivation ----

	[Fact]
	public void DeriveNone_FromANullContext()
	{
		// A null context is the non-multi-tenant path (multi-tenancy not registered).
		var scope = TenantScope.FromContext(null);

		scope.ShouldBe(TenantScope.None);
		scope.IsScoped.ShouldBeFalse();
	}

	[Fact]
	public void DeriveScoped_FromAResolvedContext()
	{
		var context = new MutableTenantContext { TenantId = "tenant-abc" };

		var scope = TenantScope.FromContext(context);

		scope.IsScoped.ShouldBeTrue();
		scope.TenantId.ShouldBe("tenant-abc");
	}

	[Fact]
	public void FailClosed_FromAContextThatResolvesNoTenant()
	{
		// SAFETY — a context that is present (multi-tenancy active) but resolves NO tenant must fail closed,
		// not silently fall back to an unscoped query.
		var unresolved = new MutableTenantContext { TenantId = null };

		Should.Throw<TenantRequiredException>(() => TenantScope.FromContext(unresolved));
	}

	// ---- Equality ----

	[Fact]
	public void CompareByTenantIdentity()
	{
		TenantScope.Scoped("a").ShouldBe(TenantScope.Scoped("a"));
		(TenantScope.Scoped("a") == TenantScope.Scoped("a")).ShouldBeTrue();
		(TenantScope.Scoped("a") != TenantScope.Scoped("b")).ShouldBeTrue();
		(TenantScope.None != TenantScope.Scoped("a")).ShouldBeTrue();
		TenantScope.None.ShouldBe(TenantScope.None);
	}

	// ---- K1 SQL: the request emits the tenant predicate IFF scoped (SqlServer provider) ----

	[Fact]
	public void EmitTheTenantPredicate_WhenScoped_ThroughARequest()
	{
		// LIVENESS (scoped) — a real request built from a Scoped scope carries the tenant predicate + parameter.
		// The predicate is the NULL-safe sentinel-folding form: the column side folds a legacy NULL row to the
		// '__untenanted__' sentinel, so a bare `TenantId = @TenantId` (which would silently miss those rows)
		// is NOT what this store emits and must not be what this asserts.
		var sql = new LoadEventsRequest("agg-1", "OrderAggregate", -1, TenantScope.Scoped("tenant-1"), Ct)
			.Command.CommandText;

		sql.ShouldContain("COALESCE(TenantId, @UntenantedSentinel) = @TenantId");
	}

	[Fact]
	public void EmitTheSentinelBoundPredicate_WhenUnscoped_ThroughARequest()
	{
		// SAFETY (non-MT) — an unscoped read does NOT drop the discriminator. It binds the '__untenanted__'
		// sentinel through the keyed partition, so the predicate is UNCONDITIONAL: an unscoped caller reads
		// the untenanted partition only, and can never range across every tenant's rows for this aggregate.
		// This supersedes the earlier contract, where None emitted no predicate at all — that shape was
		// indistinguishable from a forgotten scope, and on a tenant-columned table it reads cross-tenant.
		var sql = new LoadEventsRequest("agg-1", "OrderAggregate", -1, TenantScope.None, Ct)
			.Command.CommandText;

		sql.ShouldContain("COALESCE(TenantId, @UntenantedSentinel) = @TenantId");
	}
}
