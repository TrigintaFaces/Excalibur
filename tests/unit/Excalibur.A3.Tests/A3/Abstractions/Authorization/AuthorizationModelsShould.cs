// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Abstractions.Authorization;
using Excalibur.A3.Abstractions.Authorization;

namespace Excalibur.Tests.A3.Abstractions.Authorization;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class AuthorizationModelsShould
{
	// AuthorizationEffect enum tests
	[Fact]
	public void DefinePermitEffect()
	{
		AuthorizationEffect.Permit.ShouldBe((AuthorizationEffect)0);
	}

	[Fact]
	public void DefineDenyEffect()
	{
		AuthorizationEffect.Deny.ShouldBe((AuthorizationEffect)1);
	}

	[Fact]
	public void DefineIndeterminateEffect()
	{
		AuthorizationEffect.Indeterminate.ShouldBe((AuthorizationEffect)2);
	}

	[Fact]
	public void HaveThreeEffectValues()
	{
		Enum.GetValues<AuthorizationEffect>().Length.ShouldBe(3);
	}

	// AuthorizationDecision tests
	[Fact]
	public void CreateDecisionWithEffectAndReason()
	{
		var decision = new AuthorizationDecision(AuthorizationEffect.Deny, "Insufficient permissions");

		decision.Effect.ShouldBe(AuthorizationEffect.Deny);
		decision.Reason.ShouldBe("Insufficient permissions");
	}

	[Fact]
	public void DefaultDecisionReasonToNull()
	{
		var decision = new AuthorizationDecision(AuthorizationEffect.Permit);
		decision.Reason.ShouldBeNull();
	}

	[Fact]
	public void SupportDecisionRecordEquality()
	{
		var d1 = new AuthorizationDecision(AuthorizationEffect.Permit);
		var d2 = new AuthorizationDecision(AuthorizationEffect.Permit);
		d1.ShouldBe(d2);
	}

	// AuthorizationAction tests
	[Fact]
	public void CreateActionWithNameAndAttributes()
	{
		var attrs = new Dictionary<string, string> { ["scope"] = "global" };
		var action = new AuthorizationAction("Read", attrs);

		action.Name.ShouldBe("Read");
		action.Attributes.ShouldNotBeNull();
		action.Attributes.ShouldContainKeyAndValue("scope", "global");
	}

	[Fact]
	public void AllowNullActionAttributes()
	{
		var action = new AuthorizationAction("Write", null);

		action.Name.ShouldBe("Write");
		action.Attributes.ShouldBeNull();
	}

	// AuthorizationSubject tests
	[Fact]
	public void CreateSubjectWithAllProperties()
	{
		var attrs = new Dictionary<string, string> { ["role"] = "admin" };
		var subject = new AuthorizationSubject("actor-1", "tenant-abc", attrs);

		subject.ActorId.ShouldBe("actor-1");
		subject.TenantId.ShouldBe("tenant-abc");
		subject.Attributes.ShouldNotBeNull();
		subject.Attributes.ShouldContainKeyAndValue("role", "admin");
	}

	[Fact]
	public void AllowNullSubjectTenantAndAttributes()
	{
		var subject = new AuthorizationSubject("actor-1", null, null);

		subject.TenantId.ShouldBeNull();
		subject.Attributes.ShouldBeNull();
	}

	// AuthorizationResource tests
	[Fact]
	public void CreateResourceWithTypeAndId()
	{
		var resource = new AuthorizationResource("Order", "order-123", null);

		resource.Type.ShouldBe("Order");
		resource.Id.ShouldBe("order-123");
		resource.Attributes.ShouldBeNull();
	}

	[Fact]
	public void CreateResourceWithAttributes()
	{
		var attrs = new Dictionary<string, string> { ["owner"] = "user-1" };
		var resource = new AuthorizationResource("Document", "doc-456", attrs);

		resource.Type.ShouldBe("Document");
		resource.Id.ShouldBe("doc-456");
		resource.Attributes.ShouldNotBeNull();
		resource.Attributes.ShouldContainKeyAndValue("owner", "user-1");
	}

	// Grant tests
	[Fact]
	public void CreateGrantWithAllProperties()
	{
		var grantedOn = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
		var expiresOn = new DateTimeOffset(2026, 12, 31, 23, 59, 59, TimeSpan.Zero);

		var grant = new Grant(
			"user-1", "John Doe", "tenant-1", "role", "admin",
			expiresOn, "system", grantedOn);

		grant.UserId.ShouldBe("user-1");
		grant.FullName.ShouldBe("John Doe");
		grant.TenantId.ShouldBe("tenant-1");
		grant.GrantType.ShouldBe("role");
		grant.Qualifier.ShouldBe("admin");
		grant.ExpiresOn.ShouldBe(expiresOn);
		grant.GrantedBy.ShouldBe("system");
		grant.GrantedOn.ShouldBe(grantedOn);
	}

	[Fact]
	public void AllowNullGrantOptionalFields()
	{
		var grantedOn = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
		var grant = new Grant(
			"user-1", null, null, "role", "viewer",
			null, "admin", grantedOn);

		grant.FullName.ShouldBeNull();
		grant.TenantId.ShouldBeNull();
		grant.ExpiresOn.ShouldBeNull();
	}

	[Fact]
	public void SupportGrantRecordEquality()
	{
		var ts = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
		var g1 = new Grant("user-1", null, null, "role", "admin", null, "system", ts);
		var g2 = new Grant("user-1", null, null, "role", "admin", null, "system", ts);

		g1.ShouldBe(g2);
	}
}
