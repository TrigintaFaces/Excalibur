// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Security.Claims;

using Excalibur.A3;
using Excalibur.A3.Authentication;
using Excalibur.A3.Authorization;

namespace Excalibur.Tests.A3;

/// <summary>
/// Depth unit tests for <see cref="AccessToken"/> — delegation paths and full interface coverage.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class AccessTokenDepthShould
{
	[Fact]
	public void ThrowDomainException_WhenUserIdsMismatch()
	{
		// Arrange
		var authToken = A.Fake<IAuthenticationToken>();
		var authPolicy = A.Fake<IAuthorizationPolicy>();
		A.CallTo(() => authToken.UserId).Returns("user-A");
		A.CallTo(() => authPolicy.UserId).Returns("user-B");

		// Act & Assert
		Should.Throw<Excalibur.Domain.Exceptions.DomainException>(
			() => new AccessToken(authToken, authPolicy));
	}

	[Fact]
	public void DelegateIsAuthorized_ToPolicy()
	{
		// Arrange
		var authToken = A.Fake<IAuthenticationToken>();
		var authPolicy = A.Fake<IAuthorizationPolicy>();
		A.CallTo(() => authToken.UserId).Returns("user-1");
		A.CallTo(() => authPolicy.UserId).Returns("user-1");
		A.CallTo(() => authPolicy.TenantId).Returns("tenant-1");
		A.CallTo(() => authPolicy.IsAuthorized("ReadData", null)).Returns(true);

		var token = new AccessToken(authToken, authPolicy);

		// Act & Assert
		token.IsAuthorized("ReadData").ShouldBeTrue();
	}

	[Fact]
	public void DelegateHasGrant_ToPolicy()
	{
		// Arrange
		var authToken = A.Fake<IAuthenticationToken>();
		var authPolicy = A.Fake<IAuthorizationPolicy>();
		A.CallTo(() => authToken.UserId).Returns("user-1");
		A.CallTo(() => authPolicy.UserId).Returns("user-1");
		A.CallTo(() => authPolicy.TenantId).Returns("tenant-1");
		A.CallTo(() => authPolicy.HasGrant("WriteData")).Returns(true);

		var token = new AccessToken(authToken, authPolicy);

		// Act & Assert
		token.HasGrant("WriteData").ShouldBeTrue();
	}

	[Fact]
	public void DelegateHasGrantGeneric_ToPolicy()
	{
		// Arrange
		var authToken = A.Fake<IAuthenticationToken>();
		var authPolicy = A.Fake<IAuthorizationPolicy>();
		A.CallTo(() => authToken.UserId).Returns("user-1");
		A.CallTo(() => authPolicy.UserId).Returns("user-1");
		A.CallTo(() => authPolicy.TenantId).Returns("tenant-1");
		A.CallTo(() => authPolicy.HasGrant<string>()).Returns(true);

		var token = new AccessToken(authToken, authPolicy);

		// Act & Assert
		token.HasGrant<string>().ShouldBeTrue();
	}

	[Fact]
	public void DelegateHasGrantResourceType_ToPolicy()
	{
		// Arrange
		var authToken = A.Fake<IAuthenticationToken>();
		var authPolicy = A.Fake<IAuthorizationPolicy>();
		A.CallTo(() => authToken.UserId).Returns("user-1");
		A.CallTo(() => authPolicy.UserId).Returns("user-1");
		A.CallTo(() => authPolicy.TenantId).Returns("tenant-1");
		A.CallTo(() => authPolicy.HasGrant("Order", "order-123")).Returns(true);

		var token = new AccessToken(authToken, authPolicy);

		// Act & Assert
		token.HasGrant("Order", "order-123").ShouldBeTrue();
	}

	[Fact]
	public void DelegateHasGrantGenericResourceType_ToPolicy()
	{
		// Arrange
		var authToken = A.Fake<IAuthenticationToken>();
		var authPolicy = A.Fake<IAuthorizationPolicy>();
		A.CallTo(() => authToken.UserId).Returns("user-1");
		A.CallTo(() => authPolicy.UserId).Returns("user-1");
		A.CallTo(() => authPolicy.TenantId).Returns("tenant-1");
		A.CallTo(() => authPolicy.HasGrant<int>("res-1")).Returns(true);

		var token = new AccessToken(authToken, authPolicy);

		// Act & Assert
		token.HasGrant<int>("res-1").ShouldBeTrue();
	}

	[Fact]
	public void DelegateClaims_ToAuthenticationToken()
	{
		// Arrange
		var claims = new[] { new Claim(ClaimTypes.Name, "John") };
		var authToken = A.Fake<IAuthenticationToken>();
		var authPolicy = A.Fake<IAuthorizationPolicy>();
		A.CallTo(() => authToken.UserId).Returns("user-1");
		A.CallTo(() => authPolicy.UserId).Returns("user-1");
		A.CallTo(() => authPolicy.TenantId).Returns("tenant-1");
		A.CallTo(() => authToken.Claims).Returns(claims);

		var token = new AccessToken(authToken, authPolicy);

		// Act & Assert
		token.Claims.ShouldBe(claims);
	}

	[Fact]
	public void DelegateFirstName_ToAuthenticationToken()
	{
		// Arrange
		var authToken = A.Fake<IAuthenticationToken>();
		var authPolicy = A.Fake<IAuthorizationPolicy>();
		A.CallTo(() => authToken.UserId).Returns("user-1");
		A.CallTo(() => authPolicy.UserId).Returns("user-1");
		A.CallTo(() => authPolicy.TenantId).Returns("tenant-1");
		A.CallTo(() => authToken.FirstName).Returns("Jane");

		var token = new AccessToken(authToken, authPolicy);

		// Act & Assert
		token.FirstName.ShouldBe("Jane");
	}

	[Fact]
	public void DelegateLastName_ToAuthenticationToken()
	{
		// Arrange
		var authToken = A.Fake<IAuthenticationToken>();
		var authPolicy = A.Fake<IAuthorizationPolicy>();
		A.CallTo(() => authToken.UserId).Returns("user-1");
		A.CallTo(() => authPolicy.UserId).Returns("user-1");
		A.CallTo(() => authPolicy.TenantId).Returns("tenant-1");
		A.CallTo(() => authToken.LastName).Returns("Doe");

		var token = new AccessToken(authToken, authPolicy);

		// Act & Assert
		token.LastName.ShouldBe("Doe");
	}

	[Fact]
	public void DelegateFullName_ToAuthenticationToken()
	{
		// Arrange
		var authToken = A.Fake<IAuthenticationToken>();
		var authPolicy = A.Fake<IAuthorizationPolicy>();
		A.CallTo(() => authToken.UserId).Returns("user-1");
		A.CallTo(() => authPolicy.UserId).Returns("user-1");
		A.CallTo(() => authPolicy.TenantId).Returns("tenant-1");
		A.CallTo(() => authToken.FullName).Returns("Jane Doe");

		var token = new AccessToken(authToken, authPolicy);

		// Act & Assert
		token.FullName.ShouldBe("Jane Doe");
	}

	[Fact]
	public void DelegateLogin_ToAuthenticationToken()
	{
		// Arrange
		var authToken = A.Fake<IAuthenticationToken>();
		var authPolicy = A.Fake<IAuthorizationPolicy>();
		A.CallTo(() => authToken.UserId).Returns("user-1");
		A.CallTo(() => authPolicy.UserId).Returns("user-1");
		A.CallTo(() => authPolicy.TenantId).Returns("tenant-1");
		A.CallTo(() => authToken.Login).Returns("jdoe");

		var token = new AccessToken(authToken, authPolicy);

		// Act & Assert
		token.Login.ShouldBe("jdoe");
	}

	[Fact]
	public void DelegateTenantId_ToPolicy()
	{
		// Arrange
		var authToken = A.Fake<IAuthenticationToken>();
		var authPolicy = A.Fake<IAuthorizationPolicy>();
		A.CallTo(() => authToken.UserId).Returns("user-1");
		A.CallTo(() => authPolicy.UserId).Returns("user-1");
		A.CallTo(() => authPolicy.TenantId).Returns("tenant-xyz");

		var token = new AccessToken(authToken, authPolicy);

		// Act & Assert
		token.TenantId.ShouldBe("tenant-xyz");
	}

	[Fact]
	public void IAccessTokenUserId_ReturnsEmptyForNullUserId()
	{
		// Arrange
		var authToken = A.Fake<IAuthenticationToken>();
		var authPolicy = A.Fake<IAuthorizationPolicy>();
		A.CallTo(() => authToken.UserId).Returns(null);
		A.CallTo(() => authPolicy.UserId).Returns(null);
		A.CallTo(() => authPolicy.TenantId).Returns("tenant-1");

		var token = new AccessToken(authToken, authPolicy);

		// Act — explicit IAccessToken.UserId returns empty instead of null
		((IAccessToken)token).UserId.ShouldBe(string.Empty);
	}

	[Fact]
	public void IAuthenticationTokenUserId_ReturnsNullForNullUserId()
	{
		// Arrange
		var authToken = A.Fake<IAuthenticationToken>();
		var authPolicy = A.Fake<IAuthorizationPolicy>();
		A.CallTo(() => authToken.UserId).Returns(null);
		A.CallTo(() => authPolicy.UserId).Returns(null);
		A.CallTo(() => authPolicy.TenantId).Returns("tenant-1");

		var token = new AccessToken(authToken, authPolicy);

		// Act — IAuthenticationToken.UserId returns null
		((IAuthenticationToken)token).UserId.ShouldBeNull();
	}

	[Fact]
	public void DelegateAuthenticationState_ToAuthenticationToken()
	{
		// Arrange
		var authToken = A.Fake<IAuthenticationToken>();
		var authPolicy = A.Fake<IAuthorizationPolicy>();
		A.CallTo(() => authToken.UserId).Returns("user-1");
		A.CallTo(() => authPolicy.UserId).Returns("user-1");
		A.CallTo(() => authPolicy.TenantId).Returns("tenant-1");
		A.CallTo(() => authToken.AuthenticationState).Returns(AuthenticationState.Authenticated);

		var token = new AccessToken(authToken, authPolicy);

		// Act & Assert
		token.AuthenticationState.ShouldBe(AuthenticationState.Authenticated);
		token.IsAuthenticated().ShouldBeTrue();
		token.IsAnonymous().ShouldBeFalse();
	}

	[Fact]
	public void DelegateAnonymousState_ToAuthenticationToken()
	{
		// Arrange
		var authToken = A.Fake<IAuthenticationToken>();
		var authPolicy = A.Fake<IAuthorizationPolicy>();
		A.CallTo(() => authToken.UserId).Returns("user-1");
		A.CallTo(() => authPolicy.UserId).Returns("user-1");
		A.CallTo(() => authPolicy.TenantId).Returns("tenant-1");
		A.CallTo(() => authToken.AuthenticationState).Returns(AuthenticationState.Anonymous);

		var token = new AccessToken(authToken, authPolicy);

		// Act & Assert
		token.IsAuthenticated().ShouldBeFalse();
		token.IsAnonymous().ShouldBeTrue();
	}
}
