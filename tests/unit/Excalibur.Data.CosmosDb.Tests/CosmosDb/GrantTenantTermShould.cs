// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Authorization;
using Excalibur.Data.CosmosDb.Authorization;

namespace Excalibur.Data.Tests.CosmosDb;

/// <summary>
/// Binds the tenant term a grant document carries: the tenant identifier is stored verbatim, with no
/// reserved value substituted for any input, and it survives a round trip unchanged.
/// </summary>
/// <remarks>
/// Each pair below is a safety arm and a liveness arm. The safety arm alone is satisfied by a mapper that
/// stores nothing for anybody, so it is never asserted on its own.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "CosmosDb")]
[Trait("Feature", "Authorization")]
public sealed class GrantTenantTermShould
{
	private static readonly DateTimeOffset GrantedOn = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

	private static Grant GrantFor(string tenantId) =>
		new("user-1", null, tenantId, "role", "admin", null, "system", GrantedOn);

	[Theory]
	[InlineData("tenant-a")]
	[InlineData("__null__")]        // was the reserved sentinel; must now be an ordinary tenant
	[InlineData("__untenanted__")]  // the framework's reserved value; must not be special here either
	[InlineData("null")]            // was the literal spelled into the document id
	public void StoreTheTenantVerbatimAsThePartitionKey(string tenantId)
	{
		var document = GrantDocument.FromGrant(GrantFor(tenantId));

		document.TenantId.ShouldBe(tenantId);
	}

	[Theory]
	[InlineData("tenant-a")]
	[InlineData("__null__")]
	[InlineData("__untenanted__")]
	[InlineData("null")]
	public void RoundTripTheTenantUnchanged(string tenantId)
	{
		var restored = GrantDocument.FromGrant(GrantFor(tenantId)).ToGrant();

		restored.TenantId.ShouldBe(tenantId);
	}

	[Fact]
	public void GiveTwoTenantsDistinctPartitionKeysAndIds()
	{
		var a = GrantDocument.FromGrant(GrantFor("tenant-a"));
		var b = GrantDocument.FromGrant(GrantFor("tenant-b"));

		// Liveness: each names its own tenant.
		a.TenantId.ShouldBe("tenant-a");
		b.TenantId.ShouldBe("tenant-b");

		// Safety: neither can be mistaken for the other.
		a.TenantId.ShouldNotBe(b.TenantId);
		a.Id.ShouldNotBe(b.Id);
	}

	[Fact]
	public void NotCollideATenantNamedLikeTheFormerSentinelWithAnyOtherTenant()
	{
		// The defect this binds: a tenant registered under the literal name of the reserved value shared a
		// partition with grants that named no tenant, and could be returned them.
		var formerSentinel = GrantDocument.FromGrant(GrantFor("__null__"));
		var reserved = GrantDocument.FromGrant(GrantFor("__untenanted__"));
		var ordinary = GrantDocument.FromGrant(GrantFor("tenant-a"));

		// Liveness: each round-trips to its own tenant, so none of the three is being dropped.
		formerSentinel.ToGrant().TenantId.ShouldBe("__null__");
		reserved.ToGrant().TenantId.ShouldBe("__untenanted__");
		ordinary.ToGrant().TenantId.ShouldBe("tenant-a");

		// Safety: three distinct partitions, three distinct documents.
		new[] { formerSentinel.TenantId, reserved.TenantId, ordinary.TenantId }.Distinct().Count().ShouldBe(3);
		new[] { formerSentinel.Id, reserved.Id, ordinary.Id }.Distinct().Count().ShouldBe(3);
	}

	[Theory]
	[InlineData("tenant-a")]
	[InlineData("__null__")]
	public void SpellTheTenantVerbatimIntoTheDocumentId(string tenantId)
	{
		GrantDocument.CreateId("user-1", tenantId, "role", "admin")
			.ShouldBe($"user-1:{tenantId}:role:admin");
	}
}
