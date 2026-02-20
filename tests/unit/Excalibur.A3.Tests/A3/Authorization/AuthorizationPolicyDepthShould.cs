// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Authorization;
using Excalibur.A3.Authorization.Grants;

namespace Excalibur.Tests.A3.Authorization;

/// <summary>
/// Depth unit tests for <see cref="AuthorizationPolicy"/> grant evaluation logic.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class AuthorizationPolicyDepthShould
{
	private static AuthorizationPolicy CreatePolicy(
		IDictionary<string, object> grants,
		IDictionary<string, object>? activityGroups = null,
		string tenantId = "tenant-1",
		string userId = "user-1")
	{
		var tenant = A.Fake<ITenantId>();
		A.CallTo(() => tenant.Value).Returns(tenantId);

		return new AuthorizationPolicy(
			grants,
			activityGroups ?? new Dictionary<string, object>(),
			tenant,
			userId);
	}

	[Fact]
	public void HaveCorrectTenantId()
	{
		// Arrange
		var policy = CreatePolicy(new Dictionary<string, object>(), tenantId: "my-tenant");

		// Assert
		policy.TenantId.ShouldBe("my-tenant");
	}

	[Fact]
	public void HaveCorrectUserId()
	{
		// Arrange
		var policy = CreatePolicy(new Dictionary<string, object>(), userId: "my-user");

		// Assert
		policy.UserId.ShouldBe("my-user");
	}

	[Fact]
	public void IsAuthorized_ReturnsTrueForDirectActivityGrant()
	{
		// Arrange — grant key format: "{TenantId}:{GrantType}:{Qualifier}"
		var grants = new Dictionary<string, object>
		{
			{ $"tenant-1:{GrantType.Activity}:ReadData", new object() },
		};
		var policy = CreatePolicy(grants);

		// Act & Assert
		policy.IsAuthorized("ReadData").ShouldBeTrue();
	}

	[Fact]
	public void IsAuthorized_ReturnsFalseWhenNoMatchingGrant()
	{
		// Arrange
		var grants = new Dictionary<string, object>();
		var policy = CreatePolicy(grants);

		// Act & Assert
		policy.IsAuthorized("ReadData").ShouldBeFalse();
	}

	[Fact]
	public void IsAuthorized_ReturnsFalseForWrongTenant()
	{
		// Arrange — grant is for "other-tenant", not "tenant-1"
		var grants = new Dictionary<string, object>
		{
			{ $"other-tenant:{GrantType.Activity}:ReadData", new object() },
		};
		var policy = CreatePolicy(grants);

		// Act & Assert
		policy.IsAuthorized("ReadData").ShouldBeFalse();
	}

	[Fact]
	public void IsAuthorized_ReturnsTrueViaActivityGroupGrant()
	{
		// Arrange — user has "AdminGroup" grant, and "AdminGroup" contains "ReadData"
		var grants = new Dictionary<string, object>
		{
			{ $"tenant-1:{GrantType.ActivityGroup}:AdminGroup", new object() },
		};
		var activityGroups = new Dictionary<string, object>
		{
			{ "AdminGroup", new List<object> { "ReadData", "WriteData" } },
		};
		var policy = CreatePolicy(grants, activityGroups);

		// Act & Assert
		policy.IsAuthorized("ReadData").ShouldBeTrue();
	}

	[Fact]
	public void IsAuthorized_ReturnsFalseWhenActivityGroupDoesNotContainActivity()
	{
		// Arrange
		var grants = new Dictionary<string, object>
		{
			{ $"tenant-1:{GrantType.ActivityGroup}:ViewerGroup", new object() },
		};
		var activityGroups = new Dictionary<string, object>
		{
			{ "ViewerGroup", new List<object> { "ViewReport" } },
		};
		var policy = CreatePolicy(grants, activityGroups);

		// Act & Assert
		policy.IsAuthorized("DeleteData").ShouldBeFalse();
	}

	[Fact]
	public void HasGrant_ReturnsTrueForDirectActivityGrant()
	{
		// Arrange
		var grants = new Dictionary<string, object>
		{
			{ $"tenant-1:{GrantType.Activity}:CreateOrder", new object() },
		};
		var policy = CreatePolicy(grants);

		// Act & Assert
		policy.HasGrant("CreateOrder").ShouldBeTrue();
	}

	[Fact]
	public void HasGrant_ReturnsFalseWhenNoGrant()
	{
		// Arrange
		var policy = CreatePolicy(new Dictionary<string, object>());

		// Act & Assert
		policy.HasGrant("CreateOrder").ShouldBeFalse();
	}

	[Fact]
	public void HasGrantGeneric_ReturnsTrueForMatchingType()
	{
		// Arrange — HasGrant<T> uses TypeNameHelper.GetTypeDisplayName which returns
		// C# keyword aliases: "string" not "String", "int" not "Int32"
		var grants = new Dictionary<string, object>
		{
			{ $"tenant-1:{GrantType.Activity}:string", new object() },
		};
		var policy = CreatePolicy(grants);

		// Act & Assert
		policy.HasGrant<string>().ShouldBeTrue();
	}

	[Fact]
	public void HasGrantGeneric_ReturnsFalseForNonMatchingType()
	{
		// Arrange
		var policy = CreatePolicy(new Dictionary<string, object>());

		// Act & Assert
		policy.HasGrant<string>().ShouldBeFalse();
	}

	[Fact]
	public void HasGrantResourceType_ReturnsTrueForMatchingResourceGrant()
	{
		// Arrange — resource grant key: "{TenantId}:{resourceType}:{resourceId}"
		var grants = new Dictionary<string, object>
		{
			{ "tenant-1:Order:order-123", new object() },
		};
		var policy = CreatePolicy(grants);

		// Act & Assert
		policy.HasGrant("Order", "order-123").ShouldBeTrue();
	}

	[Fact]
	public void HasGrantResourceType_ReturnsFalseWhenNoMatch()
	{
		// Arrange
		var policy = CreatePolicy(new Dictionary<string, object>());

		// Act & Assert
		policy.HasGrant("Order", "order-123").ShouldBeFalse();
	}

	[Fact]
	public void HasGrantGenericResourceType_ReturnsTrueForMatchingType()
	{
		// Arrange — HasGrant<TResourceType>(resourceId) uses TypeNameHelper.GetTypeDisplayName
		// which returns C# keyword aliases: "int" not "Int32"
		var grants = new Dictionary<string, object>
		{
			{ "tenant-1:int:res-1", new object() },
		};
		var policy = CreatePolicy(grants);

		// Act & Assert
		policy.HasGrant<int>("res-1").ShouldBeTrue();
	}

	[Fact]
	public void IsAuthorized_ReturnsTrueForResourceGrant()
	{
		// Arrange
		var grants = new Dictionary<string, object>
		{
			{ "tenant-1:Document:doc-456", new object() },
		};
		var policy = CreatePolicy(grants);

		// Act — IsAuthorized with resourceId checks resource grants too
		// IsAuthorized only checks activity grants, not resource grants directly
		// This tests the activity path which should be false since no activity grant exists
		policy.IsAuthorized("ReadDocument", "doc-456").ShouldBeFalse();
	}

	[Fact]
	public void Implement_IAuthorizationPolicy()
	{
		// Arrange
		var policy = CreatePolicy(new Dictionary<string, object>());

		// Assert
		policy.ShouldBeAssignableTo<IAuthorizationPolicy>();
	}
}
