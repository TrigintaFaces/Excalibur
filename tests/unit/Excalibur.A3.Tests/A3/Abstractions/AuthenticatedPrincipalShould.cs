// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Abstractions.Authentication;

namespace Excalibur.Tests.A3.Abstractions;

/// <summary>
/// Unit tests for <see cref="AuthenticatedPrincipal"/> record.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class AuthenticatedPrincipalShould : UnitTestBase
{
	#region Constructor Tests

	[Fact]
	public void Create_WithSubjectIdOnly_HasNullTenantAndClaims()
	{
		// Arrange & Act
		var principal = new AuthenticatedPrincipal("user-123", null, null);

		// Assert
		principal.SubjectId.ShouldBe("user-123");
		principal.TenantId.ShouldBeNull();
		principal.Claims.ShouldBeNull();
	}

	[Fact]
	public void Create_WithSubjectIdAndTenant_SetsValues()
	{
		// Arrange & Act
		var principal = new AuthenticatedPrincipal("user-123", "tenant-abc", null);

		// Assert
		principal.SubjectId.ShouldBe("user-123");
		principal.TenantId.ShouldBe("tenant-abc");
		principal.Claims.ShouldBeNull();
	}

	[Fact]
	public void Create_WithAllParameters_SetsValues()
	{
		// Arrange
		var claims = new Dictionary<string, string>
		{
			["email"] = "user@example.com",
			["name"] = "John Doe",
			["role"] = "admin"
		};

		// Act
		var principal = new AuthenticatedPrincipal("user-456", "tenant-xyz", claims);

		// Assert
		principal.SubjectId.ShouldBe("user-456");
		principal.TenantId.ShouldBe("tenant-xyz");
		principal.Claims.ShouldNotBeNull();
		principal.Claims.Count.ShouldBe(3);
		principal.Claims["email"].ShouldBe("user@example.com");
		principal.Claims["name"].ShouldBe("John Doe");
		principal.Claims["role"].ShouldBe("admin");
	}

	[Fact]
	public void Create_WithEmptyClaims_SetsEmptyDictionary()
	{
		// Arrange
		var claims = new Dictionary<string, string>();

		// Act
		var principal = new AuthenticatedPrincipal("user-123", null, claims);

		// Assert
		principal.Claims.ShouldNotBeNull();
		principal.Claims.Count.ShouldBe(0);
	}

	#endregion

	#region Equality Tests

	[Fact]
	public void Equality_SameSubjectIdAndNulls_AreEqual()
	{
		// Arrange
		var principal1 = new AuthenticatedPrincipal("user-123", null, null);
		var principal2 = new AuthenticatedPrincipal("user-123", null, null);

		// Act & Assert
		principal1.ShouldBe(principal2);
	}

	[Fact]
	public void Equality_DifferentSubjectId_AreNotEqual()
	{
		// Arrange
		var principal1 = new AuthenticatedPrincipal("user-123", null, null);
		var principal2 = new AuthenticatedPrincipal("user-456", null, null);

		// Act & Assert
		principal1.ShouldNotBe(principal2);
	}

	[Fact]
	public void Equality_DifferentTenantId_AreNotEqual()
	{
		// Arrange
		var principal1 = new AuthenticatedPrincipal("user-123", "tenant-a", null);
		var principal2 = new AuthenticatedPrincipal("user-123", "tenant-b", null);

		// Act & Assert
		principal1.ShouldNotBe(principal2);
	}

	[Fact]
	public void Equality_NullVsNonNullTenant_AreNotEqual()
	{
		// Arrange
		var principal1 = new AuthenticatedPrincipal("user-123", null, null);
		var principal2 = new AuthenticatedPrincipal("user-123", "tenant-a", null);

		// Act & Assert
		principal1.ShouldNotBe(principal2);
	}

	#endregion

	#region With Expression Tests

	[Fact]
	public void With_CreatesModifiedCopy_SubjectId()
	{
		// Arrange
		var original = new AuthenticatedPrincipal("user-123", "tenant-a", null);

		// Act
		var modified = original with { SubjectId = "user-456" };

		// Assert
		original.SubjectId.ShouldBe("user-123");
		modified.SubjectId.ShouldBe("user-456");
		modified.TenantId.ShouldBe("tenant-a");
	}

	[Fact]
	public void With_CreatesModifiedCopy_TenantId()
	{
		// Arrange
		var original = new AuthenticatedPrincipal("user-123", "tenant-a", null);

		// Act
		var modified = original with { TenantId = "tenant-b" };

		// Assert
		original.TenantId.ShouldBe("tenant-a");
		modified.TenantId.ShouldBe("tenant-b");
	}

	[Fact]
	public void With_CreatesModifiedCopy_Claims()
	{
		// Arrange
		var original = new AuthenticatedPrincipal("user-123", null, null);
		var newClaims = new Dictionary<string, string> { ["scope"] = "api" };

		// Act
		var modified = original with { Claims = newClaims };

		// Assert
		original.Claims.ShouldBeNull();
		modified.Claims.ShouldNotBeNull();
		modified.Claims["scope"].ShouldBe("api");
	}

	#endregion

	#region Subject ID Formats

	[Theory]
	[InlineData("user-123")]
	[InlineData("00000000-0000-0000-0000-000000000001")]
	[InlineData("auth0|user123")]
	[InlineData("google-oauth2|123456789")]
	[InlineData("service-account@project.iam.gserviceaccount.com")]
	public void Create_WithVariousSubjectIdFormats_Succeeds(string subjectId)
	{
		// Act
		var principal = new AuthenticatedPrincipal(subjectId, null, null);

		// Assert
		principal.SubjectId.ShouldBe(subjectId);
	}

	#endregion
}
