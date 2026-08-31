// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch;
using Excalibur.EventSourcing.SqlServer.Requests;

using Xunit;

namespace Excalibur.EventSourcing.Tests.Sharding;

// Independent structural lock (author != implementer) for AC-K1 — the TenantScope seam that makes the
// unsafe multi-tenancy state UNREPRESENTABLE (row-discriminator isolation invariant, enforce-invariants-
// structurally). The one and only way to obtain a tenant-scoped request is TenantScope.Scoped(...), whose
// precondition rejects a null/whitespace tenant; the untenanted partition is the distinct, explicit
// TenantScope.Untenanted value, which still binds a term. There is no absent shape, not even `default`:
//
//   K1.3a  a null / empty / whitespace tenant is rejected at Scoped(...) with TenantRequiredException, so a
//          "scoped but no tenant" request cannot exist.
//   K1.3b  FromContext(non-null) == Scoped for the resolved tenant; FromContext(non-null, unresolved
//          tenant) fails closed; there is no conversion that yields an absent tenant term.
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

		scope.TenantId.ShouldBe("tenant-7");
	}

	// ---- bd-5y8vg2 / ADR-140: reject over-length rather than let a store silently truncate ----

	[Fact]
	public void RejectAnIdentifierLongerThanMaxLength_WhenScoped()
	{
		// SAFETY — no shipped provider is guaranteed to store an identifier longer than
		// TenantId.MaxLength whole (the narrowest shipped tenant column is exactly that length), so
		// accepting one here would let a store truncate it later, where a truncated identifier could
		// collide with another tenant's.
		Should.Throw<ArgumentException>(() => TenantScope.Scoped(new string('t', TenantId.MaxLength + 1)));
	}

	[Fact]
	public void CarryAnIdentifierAtExactlyMaxLength_WhenScoped()
	{
		// LIVENESS pair for the arm above — a legal identifier at the boundary still round-trips. A guard
		// that rejected everything would satisfy the safety arm alone.
		var value = new string('t', TenantId.MaxLength);

		TenantScope.Scoped(value).TenantId.ShouldBe(value);
	}

	// ---- The untenanted partition, and the invariant that there is no absent inhabitant ----

	[Fact]
	public void BindTheReservedSentinel_WhenUntenanted()
	{
		// LIVENESS — the untenanted partition is a VALUE with a concrete term, not an absence.
		TenantScope.Untenanted.TenantId.ShouldBe(TenantScope.UntenantedSentinel);
	}

	[Fact]
	public void HaveNoAbsentInhabitant_NotEvenDefault()
	{
		// SAFETY, and the structural invariant this whole type exists for. A value type ALWAYS admits a
		// `default`, so the only way to remove the absent state is to give `default` a meaning rather than
		// to remove a named accessor for it. It IS the untenanted partition.
		//
		// This is the lock on the regression that matters: the request constructors default `scope = default`,
		// so if the tenant term here ever became null again, every omitted-scope call site would silently emit
		// a statement carrying no tenant term — the cross-tenant read this type is built to make inexpressible.
		default(TenantScope).TenantId.ShouldBe(TenantScope.UntenantedSentinel);
		default(TenantScope).ShouldBe(TenantScope.Untenanted);
		new TenantScope().TenantId.ShouldBe(TenantScope.UntenantedSentinel);
	}

	// ---- K1.3b: FromContext derivation ----

	[Fact]
	public void RejectANullContext_RatherThanInventingAnUnscopedQuery()
	{
		// None means "emit no tenant predicate", which is a deliberate query shape -- not something a missing
		// context may decide on the caller's behalf. A caller whose context is optional must name None itself.
		_ = Should.Throw<ArgumentNullException>(() => TenantScope.FromContext(null!));
	}

	[Fact]
	public void ExposeNoNullAcceptingFromContextConversion()
	{
		// The structural half: a store holding an ITenantContext? cannot call this at all, so the unscoped
		// fallback has to be written down at the call site where it can be reviewed.
		var overloads = typeof(TenantScope)
			.GetMethods(BindingFlags.Public | BindingFlags.Static)
			.Where(m => string.Equals(m.Name, nameof(TenantScope.FromContext), StringComparison.Ordinal))
			.ToList();

		overloads.Count.ShouldBe(1);

		var parameter = overloads[0].GetParameters().Single();
		new NullabilityInfoContext().Create(parameter).WriteState.ShouldBe(NullabilityState.NotNull);
	}

	[Fact]
	public void DeriveScoped_FromAResolvedContext()
	{
		var context = new MutableTenantContext { TenantId = "tenant-abc" };

		var scope = TenantScope.FromContext(context);

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
		(TenantScope.Untenanted != TenantScope.Scoped("a")).ShouldBeTrue();
		TenantScope.Untenanted.ShouldBe(TenantScope.Untenanted);
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
		// This supersedes the earlier contract, where an absent scope emitted no predicate at all — that shape
		// was indistinguishable from a forgotten scope, and on a tenant-columned table it reads cross-tenant.
		var sql = new LoadEventsRequest("agg-1", "OrderAggregate", -1, TenantScope.Untenanted, Ct)
			.Command.CommandText;

		sql.ShouldContain("COALESCE(TenantId, @UntenantedSentinel) = @TenantId");
	}
}
