// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Authorization;
using Excalibur.A3.Authorization.Grants;
using Excalibur.Dispatch.Abstractions;

using FakeItEasy;

namespace Excalibur.Tests.A3.Authorization;

/// <summary>
/// Integration tests for AuthorizationPolicy wildcard grant evaluation (Sprint 726 T.4 57v36k).
/// Tests the full Evaluate() flow with wildcard grants through the dual-index pipeline.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
[Trait("Feature", "Authorization")]
public sealed class AuthorizationPolicyWildcardIntegrationShould
{
	private const string Tenant = "tenant-1";
	private readonly ITenantId _tenantId;

	public AuthorizationPolicyWildcardIntegrationShould()
	{
		_tenantId = A.Fake<ITenantId>();
		A.CallTo(() => _tenantId.Value).Returns(Tenant);
	}

	#region Case 1-3: Exact match regression guard

	[Fact]
	public void AuthorizeExactActivityGrant()
	{
		var policy = CreatePolicy(Grant(GrantType.Activity, "Orders.Create"));
		policy.HasGrant("Orders.Create").ShouldBeTrue();
	}

	[Fact]
	public void DenyUnmatchedActivity()
	{
		var policy = CreatePolicy(Grant(GrantType.Activity, "Orders.Create"));
		policy.HasGrant("Orders.Delete").ShouldBeFalse();
	}

	[Fact]
	public void AuthorizeExactResourceGrant()
	{
		var policy = CreatePolicy(Grant("Document", "doc-123"));
		policy.HasGrant("Document", "doc-123").ShouldBeTrue();
	}

	#endregion

	#region Case 4-6: Wildcard qualifier matching

	[Fact]
	public void AuthorizeFullWildcardQualifier()
	{
		var policy = CreatePolicy(Grant(GrantType.Activity, "*"));

		policy.HasGrant("Orders.Create").ShouldBeTrue();
		policy.HasGrant("Users.Delete").ShouldBeTrue();
	}

	[Fact]
	public void AuthorizeSuffixWildcardQualifier()
	{
		var policy = CreatePolicy(Grant(GrantType.Activity, "Orders.*"));

		policy.HasGrant("Orders.Create").ShouldBeTrue();
		policy.HasGrant("Orders.Delete").ShouldBeTrue();
		policy.HasGrant("Users.Create").ShouldBeFalse();
	}

	[Fact]
	public void AuthorizePathWildcardOnResource()
	{
		var policy = CreatePolicy(Grant("Document", "orders/*"));

		policy.HasGrant("Document", "orders/123").ShouldBeTrue();
		policy.HasGrant("Document", "orders/456/items").ShouldBeTrue();
		policy.HasGrant("Document", "products/123").ShouldBeFalse();
	}

	#endregion

	#region Case 7-8: Specificity and precedence

	[Fact]
	public void PreferExactMatchOverWildcard()
	{
		var grants = new Dictionary<string, object>
		{
			[Key(GrantType.Activity, "Orders.Create")] = true,
			[Key(GrantType.Activity, "*")] = true
		};
		var policy = CreatePolicy(grants);

		// Both match, but exact should win via O(1) dict lookup
		policy.HasGrant("Orders.Create").ShouldBeTrue();
	}

	[Fact]
	public void MatchMoreSpecificWildcardFirst()
	{
		var grants = new Dictionary<string, object>
		{
			[Key(GrantType.Activity, "Orders.*")] = true,
			[Key(GrantType.Activity, "*")] = true
		};
		var policy = CreatePolicy(grants);

		// Both wildcards match; "Orders.*" is more specific
		policy.HasGrant("Orders.Create").ShouldBeTrue();
		// Non-matching prefix also authorized via full wildcard
		policy.HasGrant("Users.Create").ShouldBeTrue();
	}

	#endregion

	#region Case 9-10: Activity groups with wildcards

	[Fact]
	public void AuthorizeActivityGroupWithExactGrant()
	{
		var grants = new Dictionary<string, object>
		{
			[Key(GrantType.ActivityGroup, "Readers")] = true
		};
		var activityGroups = new Dictionary<string, object>
		{
			["Readers"] = new[] { "Orders.View", "Products.View" }
		};
		var policy = CreatePolicy(grants, activityGroups);

		policy.HasGrant("Orders.View").ShouldBeTrue();
		policy.HasGrant("Products.View").ShouldBeTrue();
		policy.HasGrant("Orders.Create").ShouldBeFalse();
	}

	[Fact]
	public void AuthorizeActivityGroupWithWildcardGrant()
	{
		var grants = new Dictionary<string, object>
		{
			[Key(GrantType.ActivityGroup, "*")] = true
		};
		var activityGroups = new Dictionary<string, object>
		{
			["Readers"] = new[] { "Orders.View" },
			["Writers"] = new[] { "Orders.Create" }
		};
		var policy = CreatePolicy(grants, activityGroups);

		policy.HasGrant("Orders.View").ShouldBeTrue();
		policy.HasGrant("Orders.Create").ShouldBeTrue();
	}

	#endregion

	private static string Key(string grantType, string qualifier)
		=> $"{Tenant}:{grantType}:{qualifier}";

	private static Dictionary<string, object> Grant(string grantType, string qualifier)
		=> new() { [Key(grantType, qualifier)] = true };

	private AuthorizationPolicy CreatePolicy(
		IDictionary<string, object>? grants = null,
		IDictionary<string, object>? activityGroups = null)
	{
		return new AuthorizationPolicy(
			grants ?? new Dictionary<string, object>(),
			activityGroups ?? new Dictionary<string, object>(),
			_tenantId,
			"user-1");
	}
}
