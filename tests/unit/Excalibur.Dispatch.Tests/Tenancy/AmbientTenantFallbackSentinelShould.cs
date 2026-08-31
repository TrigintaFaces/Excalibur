// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Tests.Tenancy;

/// <summary>
/// The ambient-tenant fallback must treat every spelling of <em>untenanted</em> as an absence, including
/// the reserved sentinel.
/// </summary>
/// <remarks>
/// <para>
/// A drain re-establishes each stored row's tenant as ambient before dispatching it, and an untenanted
/// row's term is the reserved sentinel — a concrete, non-empty string, not <see langword="null"/>. The
/// fallback previously tested the ambient for emptiness, so the sentinel passed that test and was stamped
/// onto the message context, where it travelled onto every row the operation went on to write. A
/// deliberate absence was recorded there before.
/// </para>
/// <para>
/// The two spellings resolve to the same partition at the storage boundary, so this is not a
/// cross-tenant fault — but it silently changes what is persisted, and it breaks the reviewed property
/// that an estate-wide operation's redelivery stays untenanted rather than acquiring an owner.
/// </para>
/// <para>
/// The arms fail under different mutations: the sentinel arm is RED for a raw
/// <c>string.IsNullOrEmpty</c> check, and the real-tenant arm is RED for a fold that discards every
/// ambient tenant. Neither alone is sufficient.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Tenancy")]
[Trait("Priority", "1")]
public sealed class AmbientTenantFallbackSentinelShould
{
	[Fact]
	public void LeaveTheContextUntenanted_WhenTheAmbientTermIsTheReservedSentinel()
	{
		var context = new MessageContext();

		using (TenantContextHolder.BeginScope(TenantScope.UntenantedSentinel))
		{
			context.ApplyAmbientTenantFallback();
		}

		context.GetTenantId().ShouldBeNull(
			"the reserved sentinel is how a store spells 'this row has no tenant'. Stamped onto the context "
			+ "it becomes an owner, and it is written onto every row this operation goes on to write, where "
			+ "an absence was recorded before");
	}

	[Fact]
	public void StampTheAmbientTenant_WhenItNamesARealTenant()
	{
		var context = new MessageContext();

		using (TenantContextHolder.BeginScope("tenant-a"))
		{
			context.ApplyAmbientTenantFallback();
		}

		context.GetTenantId().ShouldBe(
			"tenant-a",
			"the fallback must still carry a real ambient tenant onto the context — a fold that discarded "
			+ "every ambient term would satisfy the sentinel arm while making the fallback inert");
	}

	[Fact]
	public void NeverOverwriteATenantAMoreSpecificSourceAlreadySupplied()
	{
		var context = new MessageContext();
		context.GetOrCreateIdentityFeature().TenantId = "tenant-from-header";

		using (TenantContextHolder.BeginScope("tenant-ambient"))
		{
			context.ApplyAmbientTenantFallback();
		}

		context.GetTenantId().ShouldBe(
			"tenant-from-header",
			"the fallback fills an absence; it does not compete with a header or metadata assignment");
	}
}
