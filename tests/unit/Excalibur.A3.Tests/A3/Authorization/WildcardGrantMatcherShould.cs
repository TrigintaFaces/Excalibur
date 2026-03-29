// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Authorization;

namespace Excalibur.A3.Tests.A3.Authorization;

/// <summary>
/// Unit tests for <see cref="WildcardGrantMatcher"/> (Sprint 725 T.3 pno0zg).
/// Tests zero-allocation wildcard matching across all patterns:
/// exact match, full wildcard (*), suffix wildcard (prefix.*), path wildcard (prefix/*).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Domain")]
public sealed class WildcardGrantMatcherShould
{
	#region Exact Match

	[Fact]
	public void MatchExactScope()
	{
		WildcardGrantMatcher.Matches(
			"tenant1", "role", "admin",
			"tenant1", "role", "admin")
			.ShouldBeTrue();
	}

	[Fact]
	public void NotMatchDifferentTenant()
	{
		WildcardGrantMatcher.Matches(
			"tenant1", "role", "admin",
			"tenant2", "role", "admin")
			.ShouldBeFalse();
	}

	[Fact]
	public void NotMatchDifferentGrantType()
	{
		WildcardGrantMatcher.Matches(
			"tenant1", "role", "admin",
			"tenant1", "permission", "admin")
			.ShouldBeFalse();
	}

	[Fact]
	public void NotMatchDifferentQualifier()
	{
		WildcardGrantMatcher.Matches(
			"tenant1", "role", "admin",
			"tenant1", "role", "user")
			.ShouldBeFalse();
	}

	#endregion

	#region Full Wildcard (*)

	[Fact]
	public void MatchWildcardTenant()
	{
		WildcardGrantMatcher.Matches(
			"*", "role", "admin",
			"any-tenant", "role", "admin")
			.ShouldBeTrue();
	}

	[Fact]
	public void MatchWildcardGrantType()
	{
		WildcardGrantMatcher.Matches(
			"tenant1", "*", "admin",
			"tenant1", "any-type", "admin")
			.ShouldBeTrue();
	}

	[Fact]
	public void MatchWildcardQualifier()
	{
		WildcardGrantMatcher.Matches(
			"tenant1", "role", "*",
			"tenant1", "role", "anything")
			.ShouldBeTrue();
	}

	[Fact]
	public void MatchAllWildcards()
	{
		WildcardGrantMatcher.Matches(
			"*", "*", "*",
			"any-tenant", "any-type", "any-qualifier")
			.ShouldBeTrue();
	}

	#endregion

	#region Suffix Wildcard (prefix.*)

	[Fact]
	public void MatchSuffixWildcard()
	{
		WildcardGrantMatcher.Matches(
			"tenant1", "permission", "Orders.*",
			"tenant1", "permission", "Orders.Create")
			.ShouldBeTrue();
	}

	[Fact]
	public void MatchNestedSuffixWildcard()
	{
		WildcardGrantMatcher.Matches(
			"tenant1", "permission", "Orders.*",
			"tenant1", "permission", "Orders.Items.Create")
			.ShouldBeTrue();
	}

	[Fact]
	public void NotMatchSuffixWildcard_DifferentPrefix()
	{
		WildcardGrantMatcher.Matches(
			"tenant1", "permission", "Orders.*",
			"tenant1", "permission", "Products.Create")
			.ShouldBeFalse();
	}

	#endregion

	#region Path Wildcard (prefix/*)

	[Fact]
	public void MatchPathWildcard()
	{
		WildcardGrantMatcher.Matches(
			"tenant1", "resource", "orders/*",
			"tenant1", "resource", "orders/123")
			.ShouldBeTrue();
	}

	[Fact]
	public void MatchNestedPathWildcard()
	{
		WildcardGrantMatcher.Matches(
			"tenant1", "resource", "orders/*",
			"tenant1", "resource", "orders/123/items")
			.ShouldBeTrue();
	}

	[Fact]
	public void NotMatchPathWildcard_DifferentPrefix()
	{
		WildcardGrantMatcher.Matches(
			"tenant1", "resource", "orders/*",
			"tenant1", "resource", "products/123")
			.ShouldBeFalse();
	}

	#endregion

	#region Edge Cases

	[Fact]
	public void NotMatchWildcardTenant_WhenGrantTypeDiffers()
	{
		WildcardGrantMatcher.Matches(
			"*", "role", "admin",
			"tenant1", "permission", "admin")
			.ShouldBeFalse();
	}

	[Fact]
	public void MatchCaseSensitiveExact()
	{
		// Ordinal comparison -- case matters
		WildcardGrantMatcher.Matches(
			"Tenant1", "Role", "Admin",
			"Tenant1", "Role", "Admin")
			.ShouldBeTrue();
	}

	[Fact]
	public void NotMatchCaseDifference()
	{
		WildcardGrantMatcher.Matches(
			"tenant1", "role", "Admin",
			"tenant1", "role", "admin")
			.ShouldBeFalse();
	}

	[Fact]
	public void MatchEmptyQualifierExactly()
	{
		// Edge case: qualifier is just "*" -- matches anything
		WildcardGrantMatcher.Matches(
			"t", "g", "*",
			"t", "g", "")
			.ShouldBeTrue();
	}

	#endregion
}
