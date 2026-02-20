// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Authentication;

namespace Excalibur.Tests.A3.Authentication;

/// <summary>
/// Unit tests for <see cref="AuthClaims"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
[Trait("Feature", "Authentication")]
public sealed class AuthClaimsShould : UnitTestBase
{
	[Fact]
	public void HaveEmailClaimAsUppercase()
	{
		// Assert
		AuthClaims.Email.ShouldBe("EMAIL");
	}

	[Fact]
	public void HaveFamilyNameClaimAsUppercase()
	{
		// Assert
		AuthClaims.FamilyName.ShouldBe("FAMILYNAME");
	}

	[Fact]
	public void HaveGivenNameClaimAsUppercase()
	{
		// Assert
		AuthClaims.GivenName.ShouldBe("GIVENNAME");
	}

	[Fact]
	public void HaveNameClaimAsUppercase()
	{
		// Assert
		AuthClaims.Name.ShouldBe("NAME");
	}

	[Fact]
	public void HaveUpnClaimAsUppercase()
	{
		// Assert
		AuthClaims.Upn.ShouldBe("UPN");
	}

	[Fact]
	public void HaveNonNullEmailClaim()
	{
		// Assert
		AuthClaims.Email.ShouldNotBeNull();
		AuthClaims.Email.ShouldNotBeEmpty();
	}

	[Fact]
	public void HaveNonNullFamilyNameClaim()
	{
		// Assert
		AuthClaims.FamilyName.ShouldNotBeNull();
		AuthClaims.FamilyName.ShouldNotBeEmpty();
	}

	[Fact]
	public void HaveNonNullGivenNameClaim()
	{
		// Assert
		AuthClaims.GivenName.ShouldNotBeNull();
		AuthClaims.GivenName.ShouldNotBeEmpty();
	}

	[Fact]
	public void HaveNonNullNameClaim()
	{
		// Assert
		AuthClaims.Name.ShouldNotBeNull();
		AuthClaims.Name.ShouldNotBeEmpty();
	}

	[Fact]
	public void HaveNonNullUpnClaim()
	{
		// Assert
		AuthClaims.Upn.ShouldNotBeNull();
		AuthClaims.Upn.ShouldNotBeEmpty();
	}

	[Fact]
	public void HaveDistinctClaimValues()
	{
		// Arrange
		var claims = new[]
		{
			AuthClaims.Email,
			AuthClaims.FamilyName,
			AuthClaims.GivenName,
			AuthClaims.Name,
			AuthClaims.Upn,
		};

		// Assert
		claims.Distinct().Count().ShouldBe(claims.Length);
	}
}
