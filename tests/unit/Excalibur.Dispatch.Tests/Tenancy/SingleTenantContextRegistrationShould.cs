// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Excalibur.Dispatch.Tests.Tenancy;

// Independent regression lock (author != implementer) for the tenancy FOUNDATION seam:
//
//   ITenantContext is a REQUIRED control. Its ABSENCE is a non-null fail-closed VALUE (SingleTenantContext,
//   TenantId "__default__"), never a null. The multi-tenancy composition REPLACES that default with the
//   ambient, resolver-driven context — and Replace must win regardless of composition order.
//
// The property is asserted through the REAL DI container (a WIRE lock, verify-against-real-infra DI clause),
// and BEHAVIORALLY — SingleTenantContext and AmbientTenantContext are both internal, so this test never names
// them; it distinguishes them by what they DO:
//
//   SingleTenantContext   -> HasTenant == true,  TenantId == "__default__"        (a value, always present)
//   AmbientTenantContext  -> HasTenant == false, TenantId == null  (no ambient scope; delegates to the holder)
//
// SAFETY + LIVENESS (testing-patterns §3), because a fail-closed control is trivially satisfied by one that
// resolves nothing:
//   - liveness: the default resolves to a WORKING non-null sentinel that reads "__default__"
//   - safety:   the sentinel is exactly "__default__" (NOT "default", the keyed-DI service key — a collision),
//               and the multi-tenancy Replace supersedes it in BOTH registration orders.
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SingleTenantContextRegistrationShould
{
	[Fact]
	public void ResolveTheNonNullSingleTenantDefault_FromAddDefaultTenantContext()
	{
		// LIVENESS: the default is a real, resolvable value — GetRequiredService never throws, and it reads its
		// own "__default__" rows. A regression to a null default (the pre-fix optional absence) breaks this.
		using var provider = new ServiceCollection()
			.AddDefaultTenantContext()
			.BuildServiceProvider();

		var context = provider.GetRequiredService<ITenantContext>();

		context.HasTenant.ShouldBeTrue(
			"the single-tenant default must report a tenant is present — its absence is a VALUE, not a null. " +
			"A host with no multi-tenancy wired must still resolve a usable ITenantContext.");
		context.TenantId.ShouldBe(
			"__default__",
			"the sentinel MUST be exactly \"__default__\". \"default\" is the keyed-DI service key and would read " +
			"as a collision; a null would brick every fail-closed (throw-on-null) tenant query. This literal is " +
			"load-bearing — a regression to either value must fail here.");
	}

	[Fact]
	public void NotOverrideAnAlreadyRegisteredContext_BecauseTheDefaultIsTryAdd()
	{
		// SAFETY: AddDefaultTenantContext is idempotent (TryAdd) — it must NOT clobber a context a host already
		// registered. If it used Add/AddSingleton instead, an explicit registration would be silently shadowed.
		var explicitContext = new FixedTenantContext("tenant-explicit");

		using var provider = new ServiceCollection()
			.AddSingleton<ITenantContext>(explicitContext)
			.AddDefaultTenantContext()
			.BuildServiceProvider();

		provider.GetRequiredService<ITenantContext>().ShouldBeSameAs(
			explicitContext,
			"AddDefaultTenantContext must TryAdd, so an already-registered ITenantContext wins. If the default " +
			"overrode it, a host that deliberately wired its own context would silently lose it.");
	}

	[Theory]
	[InlineData(true)]   // AddDefaultTenantContext FIRST, then AddTenantContext
	[InlineData(false)]  // AddTenantContext FIRST, then AddDefaultTenantContext
	public void LetMultiTenancyReplaceTheDefault_RegardlessOfRegistrationOrder(bool defaultFirst)
	{
		// SAFETY + the load-bearing WIRE property: AddTenantContext uses services.Replace(...), NOT TryAdd, so the
		// ambient context supersedes the single-tenant default in EITHER order. If it used TryAdd, whichever
		// registered first would win — a composition-order-dependent tenant context, which is the silent-misconfig
		// class the ruling exists to make inexpressible.
		var services = new ServiceCollection();

		if (defaultFirst)
		{
			_ = services.AddDefaultTenantContext();
			_ = services.AddTenantContext();
		}
		else
		{
			_ = services.AddTenantContext();
			_ = services.AddDefaultTenantContext();
		}

		using var provider = services.BuildServiceProvider();
		var context = provider.GetRequiredService<ITenantContext>();

		// The ambient context (no scope established here) reports NO tenant — behaviorally distinct from the
		// single-tenant default's always-present "__default__". This proves the Replace won, without naming the
		// internal type.
		context.HasTenant.ShouldBeFalse(
			$"With AddTenantContext present (defaultFirst={defaultFirst}), ITenantContext must resolve to the " +
			"ambient context, which reports no tenant outside a scope. If it still reported HasTenant==true / " +
			"\"__default__\", the single-tenant default was NOT replaced and tenancy resolution is " +
			"registration-order-dependent — the exact silent misconfiguration the Replace guards against.");
		context.TenantId.ShouldBeNull(
			"the ambient context has no ambient tenant established in this test, so TenantId must be null. A " +
			"non-null value here means the single-tenant sentinel survived the Replace.");
	}

	/// <summary>
	/// The single-tenant default's value must survive <c>TenantScope.FromContext</c>. This is the arm
	/// that fails if a reserved-prefix rule lands without its coupled change.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The arms above prove the default is REGISTERED and REPLACEABLE. None of them proves it still
	/// WORKS once resolved — the registration and the resolution are different seams, and only the
	/// second one reaches <c>Scoped()</c>.
	/// </para>
	/// <para>
	/// Why that gap matters right now: a reserved-prefix contract on tenant identifiers is under
	/// consideration, and this default's value is prefix-shaped. Implementing the rejection in
	/// <c>TenantScope.Scoped</c> without simultaneously changing this value makes every single-tenant
	/// host throw on every call — and the registration arms above would all stay GREEN, because the
	/// registration is untouched.
	/// </para>
	/// <para>
	/// The assertion is deliberately on the BEHAVIOUR, not the literal: it does not name the sentinel,
	/// so it survives the value being changed and fails only if the value stops being ACCEPTABLE. That
	/// is the whole point — it should not obstruct the coupled change, only an uncoupled one.
	/// </para>
	/// </remarks>
	[Fact]
	public void ProduceAScopeThatIsAcceptedByTenantScope_NotMerelyResolveFromDi()
	{
		var services = new ServiceCollection();
		_ = services.AddDefaultTenantContext();
		using var provider = services.BuildServiceProvider();
		var context = provider.GetRequiredService<ITenantContext>();

		var scope = TenantScope.FromContext(context);

		scope.TenantId.ShouldNotBeNullOrWhiteSpace(
			"the single-tenant default must produce a usable scope, not merely resolve from DI. If this " +
			"throws or reports unscoped, a validation rule has been added to TenantScope.Scoped that " +
			"rejects this context's value WITHOUT the coupled change to that value — which makes every " +
			"single-tenant host fail on every call while every registration test stays green.");
		scope.TenantId.ShouldBe(
			context.TenantId,
			"the scope must carry through the value the context reported, unchanged");
	}

	// A minimal explicit ITenantContext for the idempotency arm — a host's own registration.
	private sealed class FixedTenantContext(string tenantId) : ITenantContext
	{
		public string? TenantId { get; } = tenantId;

		public bool HasTenant => !string.IsNullOrEmpty(TenantId);
	}
}
