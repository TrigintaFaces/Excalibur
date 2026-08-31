// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Amazon.DynamoDBv2.Model;

using Excalibur.A3.Authorization;
using Excalibur.Data.DynamoDb.Authorization;

namespace Excalibur.Data.Tests.DynamoDb;

/// <summary>
/// Binds the tenant term a grant item carries: stored verbatim as the partition key, no reserved value
/// substituted for any input, and unchanged across a round trip. Safety assertions are paired with
/// liveness assertions, because "no foreign tenant is returned" is also satisfied by a mapper that
/// returns nothing at all.
/// </summary>
/// <remarks>GrantItem is internal, so it is reached by reflection as its sibling tests do.</remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "DynamoDb")]
[Trait("Feature", "Authorization")]
public sealed class GrantTenantTermShould
{
	private static readonly DateTimeOffset GrantedOn = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

	private readonly Type _itemType = typeof(DynamoDbAuthorizationOptions).Assembly
		.GetType("Excalibur.Data.DynamoDb.Authorization.GrantItem")!;

	private static Grant GrantFor(string tenantId) =>
		new("user-1", null, tenantId, "role", "admin", null, "system", GrantedOn);

	private Dictionary<string, AttributeValue> ToItem(Grant grant) =>
		(Dictionary<string, AttributeValue>)_itemType
			.GetMethod("ToItem", BindingFlags.Public | BindingFlags.Static)!
			.Invoke(null, [grant])!;

	private Grant? FromItem(Dictionary<string, AttributeValue> item) =>
		(Grant?)_itemType
			.GetMethod("FromItem", BindingFlags.Public | BindingFlags.Static)!
			.Invoke(null, [item]);

	[Theory]
	[InlineData("tenant-a")]
	[InlineData("__null__")]        // was the reserved sentinel; must now be an ordinary tenant
	[InlineData("__untenanted__")]  // the framework's reserved value; not special to this store either
	[InlineData("null")]            // was the literal spelled into the index sort key
	public void StoreTheTenantVerbatimAsThePartitionKey(string tenantId) =>
		ToItem(GrantFor(tenantId))["tenant_id"].S.ShouldBe(tenantId);

	[Theory]
	[InlineData("tenant-a")]
	[InlineData("__null__")]
	[InlineData("__untenanted__")]
	[InlineData("null")]
	public void RoundTripTheTenantUnchanged(string tenantId)
	{
		var restored = FromItem(ToItem(GrantFor(tenantId)));

		restored.ShouldNotBeNull();
		restored.TenantId.ShouldBe(tenantId);
	}

	[Fact]
	public void NotCollideATenantNamedLikeTheFormerSentinelWithAnyOtherTenant()
	{
		var formerSentinel = ToItem(GrantFor("__null__"));
		var reserved = ToItem(GrantFor("__untenanted__"));
		var ordinary = ToItem(GrantFor("tenant-a"));

		// Liveness: each round-trips to its own tenant, so none of the three is being dropped.
		FromItem(formerSentinel)!.TenantId.ShouldBe("__null__");
		FromItem(reserved)!.TenantId.ShouldBe("__untenanted__");
		FromItem(ordinary)!.TenantId.ShouldBe("tenant-a");

		// Safety: three distinct partitions, none mistakable for another.
		new[] { formerSentinel["tenant_id"].S, reserved["tenant_id"].S, ordinary["tenant_id"].S }
			.Distinct().Count().ShouldBe(3);
	}
}
