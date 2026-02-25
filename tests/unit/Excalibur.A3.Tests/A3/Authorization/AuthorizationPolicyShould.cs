// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Authorization;
using Excalibur.A3.Authorization.Grants;
using Excalibur.Dispatch.Abstractions;

using FakeItEasy;

namespace Excalibur.Tests.A3.Authorization;

/// <summary>
/// Unit tests for <see cref="AuthorizationPolicy"/> public class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
[Trait("Feature", "Authorization")]
public sealed class AuthorizationPolicyShould
{
	private readonly ITenantId _tenantId;

	public AuthorizationPolicyShould()
	{
		_tenantId = A.Fake<ITenantId>();
		A.CallTo(() => _tenantId.Value).Returns("tenant-1");
	}

	[Fact]
	public void ImplementIAuthorizationPolicy()
	{
		// Arrange
		var sut = CreatePolicy();

		// Assert
		sut.ShouldBeAssignableTo<IAuthorizationPolicy>();
	}

	[Fact]
	public void NotImplementIDisposable()
	{
		// Arrange
		var sut = CreatePolicy();

		// Assert
		sut.ShouldNotBeAssignableTo<IDisposable>();
	}

	[Fact]
	public void BePublicAndSealed()
	{
		// Assert
		typeof(AuthorizationPolicy).IsPublic.ShouldBeTrue();
		typeof(AuthorizationPolicy).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void StoreTenantId()
	{
		// Arrange
		var sut = CreatePolicy();

		// Assert
		sut.TenantId.ShouldBe("tenant-1");
	}

	[Fact]
	public void StoreUserId()
	{
		// Arrange
		var sut = CreatePolicy();

		// Assert
		sut.UserId.ShouldBe("user-1");
	}

	[Fact]
	public void ReturnFalseWhenNoGrantsExist()
	{
		// Arrange
		var sut = CreatePolicy();

		// Act
		var result = sut.IsAuthorized("TestActivity");

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ReturnTrueWhenDirectActivityGrantExists()
	{
		// Arrange
		var grants = new Dictionary<string, object>
		{
			[$"tenant-1:{GrantType.Activity}:TestActivity"] = true,
		};
		var sut = CreatePolicy(grants: grants);

		// Act
		var result = sut.IsAuthorized("TestActivity");

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ReturnFalseWhenGrantIsForDifferentTenant()
	{
		// Arrange
		var grants = new Dictionary<string, object>
		{
			[$"other-tenant:{GrantType.Activity}:TestActivity"] = true,
		};
		var sut = CreatePolicy(grants: grants);

		// Act
		var result = sut.IsAuthorized("TestActivity");

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ReturnFalseWhenGrantIsForDifferentActivity()
	{
		// Arrange
		var grants = new Dictionary<string, object>
		{
			[$"tenant-1:{GrantType.Activity}:OtherActivity"] = true,
		};
		var sut = CreatePolicy(grants: grants);

		// Act
		var result = sut.IsAuthorized("TestActivity");

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ReturnHasGrantForDirectActivityGrant()
	{
		// Arrange
		var grants = new Dictionary<string, object>
		{
			[$"tenant-1:{GrantType.Activity}:TestActivity"] = true,
		};
		var sut = CreatePolicy(grants: grants);

		// Act
		var result = sut.HasGrant("TestActivity");

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ReturnHasResourceGrantForResourceGrant()
	{
		// Arrange
		var grants = new Dictionary<string, object>
		{
			["tenant-1:Document:doc-123"] = true,
		};
		var sut = CreatePolicy(grants: grants);

		// Act
		var result = sut.HasGrant("Document", "doc-123");

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ReturnFalseForMissingResourceGrant()
	{
		// Arrange
		var sut = CreatePolicy();

		// Act
		var result = sut.HasGrant("Document", "doc-123");

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ReturnTrueWhenActivityGroupGrantContainsActivity()
	{
		// Arrange
		var grants = new Dictionary<string, object>
		{
			[$"tenant-1:{GrantType.ActivityGroup}:AdminGroup"] = true,
		};
		var activityGroups = new Dictionary<string, object>
		{
			["AdminGroup"] = new List<object> { "TestActivity", "OtherActivity" },
		};
		var sut = CreatePolicy(grants: grants, activityGroups: activityGroups);

		// Act
		var result = sut.HasGrant("TestActivity");

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ReturnFalseWhenActivityGroupDoesNotContainActivity()
	{
		// Arrange
		var grants = new Dictionary<string, object>
		{
			[$"tenant-1:{GrantType.ActivityGroup}:ViewerGroup"] = true,
		};
		var activityGroups = new Dictionary<string, object>
		{
			["ViewerGroup"] = new List<object> { "ReadOnly", "ViewReport" },
		};
		var sut = CreatePolicy(grants: grants, activityGroups: activityGroups);

		// Act
		var result = sut.HasGrant("TestActivity");

		// Assert
		result.ShouldBeFalse();
	}

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
