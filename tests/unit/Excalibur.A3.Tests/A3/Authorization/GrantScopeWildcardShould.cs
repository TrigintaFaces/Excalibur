// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Authorization.Grants;

namespace Excalibur.A3.Tests.A3.Authorization;

/// <summary>
/// Unit tests for GrantScope wildcard properties (Sprint 725 T.2 wy4ybq):
/// IsWildcard, SpecificityScore, Validate().
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Domain")]
public sealed class GrantScopeWildcardShould
{
	#region IsWildcard

	[Fact]
	public void IsWildcard_ReturnsFalse_ForExactScope()
	{
		var scope = new GrantScope("tenant1", "role", "admin");
		scope.IsWildcard.ShouldBeFalse();
	}

	[Fact]
	public void IsWildcard_ReturnsTrue_ForWildcardTenant()
	{
		var scope = new GrantScope("*", "role", "admin");
		scope.IsWildcard.ShouldBeTrue();
	}

	[Fact]
	public void IsWildcard_ReturnsTrue_ForWildcardGrantType()
	{
		var scope = new GrantScope("tenant1", "*", "admin");
		scope.IsWildcard.ShouldBeTrue();
	}

	[Fact]
	public void IsWildcard_ReturnsTrue_ForWildcardQualifier()
	{
		var scope = new GrantScope("tenant1", "role", "*");
		scope.IsWildcard.ShouldBeTrue();
	}

	[Fact]
	public void IsWildcard_ReturnsTrue_ForSuffixWildcard()
	{
		var scope = new GrantScope("tenant1", "permission", "Orders.*");
		scope.IsWildcard.ShouldBeTrue();
	}

	[Fact]
	public void IsWildcard_ReturnsTrue_ForPathWildcard()
	{
		var scope = new GrantScope("tenant1", "resource", "orders/*");
		scope.IsWildcard.ShouldBeTrue();
	}

	#endregion

	#region SpecificityScore

	[Fact]
	public void SpecificityScore_HighestForExactMatch()
	{
		var exact = new GrantScope("tenant1", "role", "admin");
		var wildcard = new GrantScope("*", "*", "*");
		exact.SpecificityScore.ShouldBeGreaterThan(wildcard.SpecificityScore);
	}

	[Fact]
	public void SpecificityScore_MoreSpecificTenantWins()
	{
		var specific = new GrantScope("tenant1", "*", "*");
		var wildcard = new GrantScope("*", "*", "*");
		specific.SpecificityScore.ShouldBeGreaterThan(wildcard.SpecificityScore);
	}

	[Fact]
	public void SpecificityScore_LongerPrefixWins()
	{
		var longPrefix = new GrantScope("tenant1", "permission", "Orders.Items.*");
		var shortPrefix = new GrantScope("tenant1", "permission", "Orders.*");
		longPrefix.SpecificityScore.ShouldBeGreaterThan(shortPrefix.SpecificityScore);
	}

	[Fact]
	public void SpecificityScore_FullWildcardIsZero()
	{
		var wildcard = new GrantScope("*", "*", "*");
		wildcard.SpecificityScore.ShouldBe(0);
	}

	#endregion

	#region Validate

	[Fact]
	public void Validate_ReturnsTrue_ForValidWildcardQualifier()
	{
		GrantScope.Validate("tenant1", "role", "*", out _).ShouldBeTrue();
	}

	[Fact]
	public void Validate_ReturnsTrue_ForValidSuffixWildcard()
	{
		GrantScope.Validate("tenant1", "permission", "Orders.*", out _).ShouldBeTrue();
	}

	[Fact]
	public void Validate_ReturnsTrue_ForValidPathWildcard()
	{
		GrantScope.Validate("tenant1", "resource", "orders/*", out _).ShouldBeTrue();
	}

	[Fact]
	public void Validate_ReturnsFalse_ForMidWildcard()
	{
		GrantScope.Validate("tenant1", "permission", "Orders.*.Create", out var error).ShouldBeFalse();
		error.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public void Validate_ReturnsFalse_ForDoubleWildcard()
	{
		GrantScope.Validate("tenant1", "role", "**", out var error).ShouldBeFalse();
		error.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public void Validate_ReturnsTrue_ForExactQualifier()
	{
		GrantScope.Validate("tenant1", "role", "admin", out _).ShouldBeTrue();
	}

	[Fact]
	public void Validate_ReturnsFalse_ForPartialWildcardTenant()
	{
		GrantScope.Validate("tenant*", "role", "admin", out var error).ShouldBeFalse();
		error.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public void Validate_ReturnsTrue_ForWildcardTenant()
	{
		GrantScope.Validate("*", "role", "admin", out _).ShouldBeTrue();
	}

	#endregion
}
