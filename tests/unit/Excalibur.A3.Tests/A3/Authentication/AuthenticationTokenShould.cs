// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Authentication;

namespace Excalibur.Tests.A3.Authentication;

/// <summary>
/// Unit tests for <see cref="AuthenticationToken"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
[Trait("Feature", "Authentication")]
public sealed class AuthenticationTokenShould : UnitTestBase
{
	[Fact]
	public void HaveDefaultConstructor_CreatingEmptyToken()
	{
		// Act
		var token = new AuthenticationToken();

		// Assert
		token.ShouldNotBeNull();
		token.Jwt.ShouldNotBeNull();
	}

	[Fact]
	public void HaveStaticAnonymousToken()
	{
		// Act
		var anonymous = AuthenticationToken.Anonymous;

		// Assert
		anonymous.ShouldNotBeNull();
		anonymous.IsAnonymous().ShouldBeTrue();
		anonymous.IsAuthenticated().ShouldBeFalse();
	}

	[Fact]
	public void ReturnAnonymousFullName_WhenNoNameClaim()
	{
		// Arrange
		var token = new AuthenticationToken();

		// Assert
		token.FullName.ShouldBe("Anonymous");
	}

	[Fact]
	public void HaveNullUserId_WhenNoUpnClaim()
	{
		// Arrange
		var token = new AuthenticationToken();

		// Assert
		token.UserId.ShouldBeNull();
	}

	[Fact]
	public void HaveNullLogin_WhenNoEmailClaim()
	{
		// Arrange
		var token = new AuthenticationToken();

		// Assert
		token.Login.ShouldBeNull();
	}

	[Fact]
	public void HaveNullFirstName_WhenNoGivenNameClaim()
	{
		// Arrange
		var token = new AuthenticationToken();

		// Assert
		token.FirstName.ShouldBeNull();
	}

	[Fact]
	public void HaveNullLastName_WhenNoFamilyNameClaim()
	{
		// Arrange
		var token = new AuthenticationToken();

		// Assert
		token.LastName.ShouldBeNull();
	}

	[Fact]
	public void HaveNullClaims_WhenDefaultConstructed()
	{
		// Arrange
		var token = new AuthenticationToken();

		// Assert
		token.Claims.ShouldBeNull();
	}

	[Fact]
	public void BeAnonymous_WhenNotAuthenticated()
	{
		// Arrange
		var token = new AuthenticationToken
		{
			AuthenticationState = AuthenticationState.Anonymous
		};

		// Act & Assert
		token.IsAnonymous().ShouldBeTrue();
		token.IsAuthenticated().ShouldBeFalse();
	}

	[Fact]
	public void NotBeAnonymous_WhenAuthenticated()
	{
		// Arrange
		var token = new AuthenticationToken
		{
			AuthenticationState = AuthenticationState.Authenticated
		};

		// Act & Assert
		token.IsAnonymous().ShouldBeFalse();
		token.IsAuthenticated().ShouldBeTrue();
	}

	[Fact]
	public void BeAnonymous_WhenIdentifiedButNotAuthenticated()
	{
		// Arrange
		var token = new AuthenticationToken
		{
			AuthenticationState = AuthenticationState.Identified
		};

		// Act & Assert
		token.IsAnonymous().ShouldBeTrue();
		token.IsAuthenticated().ShouldBeFalse();
	}

	[Fact]
	public void WillExpireBy_ReturnsTrue_WhenJwtIsNull()
	{
		// Arrange
		var token = new AuthenticationToken();
		var futureDate = DateTime.UtcNow.AddYears(1);

		// Act
		var result = token.WillExpireBy(futureDate);

		// Assert - empty JWT has no validity period so should return true
		result.ShouldBeTrue();
	}

	[Fact]
	public void SupportAuthenticationStateProperty()
	{
		// Arrange
		var token = new AuthenticationToken();

		// Act
		token.AuthenticationState = AuthenticationState.Authenticated;

		// Assert
		token.AuthenticationState.ShouldBe(AuthenticationState.Authenticated);
	}

	[Fact]
	public void DefaultAuthenticationState_IsAnonymous()
	{
		// Arrange
		var token = new AuthenticationToken();

		// Assert
		token.AuthenticationState.ShouldBe(AuthenticationState.Anonymous);
	}

	[Fact]
	public void IAuthenticationToken_Jwt_CanBeSet()
	{
		// Arrange
		var token = new AuthenticationToken();
		IAuthenticationToken authToken = token;

		// Act
		authToken.Jwt = null;

		// Assert - setting to null creates a new empty JWT
		token.Jwt.ShouldNotBeNull();
	}

	[Fact]
	public void IAuthenticationToken_Jwt_CanBeSetToEmpty()
	{
		// Arrange
		var token = new AuthenticationToken();
		IAuthenticationToken authToken = token;

		// Act
		authToken.Jwt = string.Empty;

		// Assert
		token.Jwt.ShouldNotBeNull();
	}
}
