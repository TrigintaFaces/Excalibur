// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Authorization;
using Excalibur.Data.MongoDB.Authorization;

namespace Excalibur.Data.Tests.MongoDB;

/// <summary>
/// Binds the tenant term a grant document carries: stored verbatim, no reserved value substituted for any
/// input, and unchanged across a round trip. Each safety assertion is paired with a liveness assertion,
/// because "no foreign tenant is returned" is also satisfied by a mapper that returns nothing at all.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "MongoDB")]
[Trait("Feature", "Authorization")]
public sealed class GrantTenantTermShould
{
	private static readonly DateTimeOffset GrantedOn = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

	private static Grant GrantFor(string tenantId) =>
		new("user-1", null, tenantId, "role", "admin", null, "system", GrantedOn);

	[Theory]
	[InlineData("tenant-a")]
	[InlineData("__null__")]        // was the reserved sentinel; must now be an ordinary tenant
	[InlineData("__untenanted__")]  // the framework's reserved value; not special to this store either
	[InlineData("null")]            // was the literal spelled into the document id
	public void StoreTheTenantVerbatim(string tenantId) =>
		GrantDocument.FromGrant(GrantFor(tenantId)).TenantId.ShouldBe(tenantId);

	[Theory]
	[InlineData("tenant-a")]
	[InlineData("__null__")]
	[InlineData("__untenanted__")]
	[InlineData("null")]
	public void RoundTripTheTenantUnchanged(string tenantId) =>
		GrantDocument.FromGrant(GrantFor(tenantId)).ToGrant().TenantId.ShouldBe(tenantId);

	[Theory]
	[InlineData("tenant-a")]
	[InlineData("__null__")]
	public void SpellTheTenantVerbatimIntoTheDocumentId(string tenantId) =>
		GrantDocument.CreateId("user-1", tenantId, "role", "admin")
			.ShouldBe($"user-1:{tenantId}:role:admin");

	[Fact]
	public void NotCollideATenantNamedLikeTheFormerSentinelWithAnyOtherTenant()
	{
		var formerSentinel = GrantDocument.FromGrant(GrantFor("__null__"));
		var reserved = GrantDocument.FromGrant(GrantFor("__untenanted__"));
		var ordinary = GrantDocument.FromGrant(GrantFor("tenant-a"));

		// Liveness: none of the three is dropped; each returns its own tenant.
		formerSentinel.ToGrant().TenantId.ShouldBe("__null__");
		reserved.ToGrant().TenantId.ShouldBe("__untenanted__");
		ordinary.ToGrant().TenantId.ShouldBe("tenant-a");

		// Safety: three distinct documents, none mistakable for another.
		new[] { formerSentinel.Id, reserved.Id, ordinary.Id }.Distinct().Count().ShouldBe(3);
	}
}
