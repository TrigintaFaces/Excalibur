// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Excalibur.Dispatch.Tests.Tenancy;

// Liskov L12 (history constraint) lock for ITenantContext, asserting the CONTRACT, not a mechanism:
//
//   1. STRUCTURAL immutability -- the interface exposes no mutator, so the resolved tenant "cannot be
//      reassigned" is enforced by the type, not by convention. RED if a settable property or a mutator
//      method is ever added (that is precisely how a history constraint would be weakened here).
//
//   2. CONSISTENCY invariant across the model family -- HasTenant is true EXACTLY when TenantId is a
//      non-null, non-empty identifier; and repeated reads within one flow return the same value
//      (the scope is preserved, never downgraded). Verified against BOTH real implementations (resolved
//      behaviorally through DI, never named) AND a hand fixture implementing ITenantContext from scratch
//      (testing-patterns §3 fixture-shape corollary), on both sides of the invariant.
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class TenantContextHistoryConstraintShould
{
	[Fact]
	public void ExposeNoMutator_SoAResolvedTenantCannotBeReassigned()
	{
		// SAFETY (history constraint): the contract is read-only by construction.
		foreach (var property in typeof(ITenantContext).GetProperties())
		{
			property.CanWrite.ShouldBeFalse(
				$"ITenantContext.{property.Name} must be read-only -- a settable member would let a consumer " +
				"reassign the resolved tenant, weakening the history constraint the contract guarantees.");
		}

		// No mutator methods either: every method on the interface must be a property getter.
		var mutators = typeof(ITenantContext)
			.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
			.Where(m => !m.Name.StartsWith("get_", StringComparison.Ordinal))
			.Select(m => m.Name)
			.ToArray();

		mutators.ShouldBeEmpty(
			"ITenantContext must expose no mutator method; the tenant is established only by the resolving scope. " +
			$"Found: {string.Join(", ", mutators)}");
	}

	[Fact]
	public void KeepHasTenantConsistentWithTenantId_ForTheSingleTenantDefault()
	{
		// Real impl (behaviorally): the single-tenant default reports a tenant present with a non-empty id.
		using var provider = new ServiceCollection().AddDefaultTenantContext().BuildServiceProvider();
		AssertConsistencyInvariant(provider.GetRequiredService<ITenantContext>(), expectPresent: true);
	}

	[Fact]
	public void KeepHasTenantConsistentWithTenantId_ForTheAmbientContextWithNoScope()
	{
		// Real impl (behaviorally): the ambient context outside any scope reports no tenant and a null id.
		using var provider = new ServiceCollection().AddTenantContext().BuildServiceProvider();
		AssertConsistencyInvariant(provider.GetRequiredService<ITenantContext>(), expectPresent: false);
	}

	[Theory]
	[InlineData("tenant-a", true)]
	[InlineData("", false)]
	[InlineData(null, false)]
	public void KeepHasTenantConsistentWithTenantId_ForAnyImplementation(string? tenantId, bool expectPresent)
	{
		// A hand fixture implementing the interface directly (not via a first-party base) -- proves the
		// invariant binds the CONTRACT, and would go RED for an implementation that reports the wrong HasTenant.
		AssertConsistencyInvariant(new FixedTenantContext(tenantId), expectPresent);
	}

	private static void AssertConsistencyInvariant(ITenantContext context, bool expectPresent)
	{
		context.HasTenant.ShouldBe(
			!string.IsNullOrEmpty(context.TenantId),
			"HasTenant must be true exactly when TenantId is a non-null, non-empty identifier.");
		context.HasTenant.ShouldBe(expectPresent);

		// History constraint: repeated reads within one flow are stable (the scope is preserved, not downgraded).
		context.TenantId.ShouldBe(context.TenantId);
		context.HasTenant.ShouldBe(context.HasTenant);
	}

	private sealed class FixedTenantContext(string? tenantId) : ITenantContext
	{
		public string? TenantId { get; } = tenantId;

		public bool HasTenant => !string.IsNullOrEmpty(TenantId);
	}
}
