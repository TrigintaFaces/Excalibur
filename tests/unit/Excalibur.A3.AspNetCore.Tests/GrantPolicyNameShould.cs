// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.A3.AspNetCore.Tests;

/// <summary>
/// The grant policy-name grammar.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
[Trait("Feature", "Authorization")]
public sealed class GrantPolicyNameShould
{
	[Fact]
	public void BuildAnUnscopedName() =>
		GrantPolicyName.For("Read", "Order").ShouldBe("grant:Read:Order");

	[Fact]
	public void BuildARouteScopedName() =>
		GrantPolicyName.ForRouteValue("Read", "Order", "id").ShouldBe("grant:Read:Order:{id}");

	[Fact]
	public void BuildAFixedResourceName() =>
		GrantPolicyName.ForResource("Read", "Order", "order-42").ShouldBe("grant:Read:Order:order-42");

	[Theory]
	[InlineData("grant:Read:Order", "Read", "Order", null, null)]
	[InlineData("grant:Read:Order:{id}", "Read", "Order", null, "id")]
	[InlineData("grant:Read:Order:order-42", "Read", "Order", "order-42", null)]
	// A literal identifier may contain the separator: it is the trailing segment and is taken verbatim.
	[InlineData("grant:Read:Order:urn:orders:42", "Read", "Order", "urn:orders:42", null)]
	public void ParseAWellFormedName(
		string policyName,
		string activity,
		string resourceType,
		string? resourceId,
		string? routeValueName)
	{
		GrantPolicyName.TryParse(policyName, out var parsed).ShouldBeTrue();

		parsed.ActivityName.ShouldBe(activity);
		parsed.ResourceType.ShouldBe(resourceType);
		parsed.ResourceId.ShouldBe(resourceId);
		parsed.RouteValueName.ShouldBe(routeValueName);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("AdminOnly")]          // an ordinary policy name is left alone
	[InlineData("grant:")]
	[InlineData("grant:Read")]         // no resource type
	[InlineData("grant::Order")]       // empty activity
	[InlineData("grant:Read:")]        // empty resource type
	[InlineData("grant:Read:Order: ")] // whitespace scope
	[InlineData("grant:Read:Order:{}")]// empty route parameter
	public void RejectANameItDoesNotOwn(string? policyName) =>
		GrantPolicyName.TryParse(policyName, out _).ShouldBeFalse();

	[Theory]
	[InlineData("Read:Extra", "Order")]
	[InlineData("Read", "Order:Extra")]
	public void RejectASegmentContainingTheSeparator(string activity, string resourceType) =>
		Should.Throw<ArgumentException>(() => GrantPolicyName.For(activity, resourceType));

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void RejectAnEmptySegment(string? activity) =>
		Should.Throw<ArgumentException>(() => GrantPolicyName.For(activity!, "Order"));

	[Fact]
	public void RejectABraceWrappedResourceIdBecauseItWouldReadAsARouteParameter() =>
		Should.Throw<ArgumentException>(() => GrantPolicyName.ForResource("Read", "Order", "{id}"));
}
