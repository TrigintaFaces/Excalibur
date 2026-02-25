// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Abstractions.Authorization;

namespace Excalibur.Tests.A3.Abstractions;

/// <summary>
/// Unit tests for <see cref="Grant"/> record.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class GrantShould : UnitTestBase
{
	private readonly DateTimeOffset _grantedOn = new(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);
	private readonly DateTimeOffset _expiresOn = new(2025, 1, 15, 10, 30, 0, TimeSpan.Zero);

	#region Constructor Tests

	[Fact]
	public void Create_WithRequiredParameters_SetsValues()
	{
		// Arrange & Act
		var grant = new Grant(
			UserId: "user-123",
			FullName: null,
			TenantId: null,
			GrantType: "role",
			Qualifier: "admin",
			ExpiresOn: null,
			GrantedBy: "system",
			GrantedOn: _grantedOn);

		// Assert
		grant.UserId.ShouldBe("user-123");
		grant.FullName.ShouldBeNull();
		grant.TenantId.ShouldBeNull();
		grant.GrantType.ShouldBe("role");
		grant.Qualifier.ShouldBe("admin");
		grant.ExpiresOn.ShouldBeNull();
		grant.GrantedBy.ShouldBe("system");
		grant.GrantedOn.ShouldBe(_grantedOn);
	}

	[Fact]
	public void Create_WithAllParameters_SetsValues()
	{
		// Arrange & Act
		var grant = new Grant(
			UserId: "user-456",
			FullName: "John Doe",
			TenantId: "tenant-abc",
			GrantType: "activity-group",
			Qualifier: "orders-management",
			ExpiresOn: _expiresOn,
			GrantedBy: "admin-user",
			GrantedOn: _grantedOn);

		// Assert
		grant.UserId.ShouldBe("user-456");
		grant.FullName.ShouldBe("John Doe");
		grant.TenantId.ShouldBe("tenant-abc");
		grant.GrantType.ShouldBe("activity-group");
		grant.Qualifier.ShouldBe("orders-management");
		grant.ExpiresOn.ShouldBe(_expiresOn);
		grant.GrantedBy.ShouldBe("admin-user");
		grant.GrantedOn.ShouldBe(_grantedOn);
	}

	#endregion

	#region Equality Tests

	[Fact]
	public void Equality_SameValues_AreEqual()
	{
		// Arrange
		var grant1 = new Grant("user-123", null, null, "role", "admin", null, "system", _grantedOn);
		var grant2 = new Grant("user-123", null, null, "role", "admin", null, "system", _grantedOn);

		// Act & Assert
		grant1.ShouldBe(grant2);
	}

	[Fact]
	public void Equality_DifferentUserId_AreNotEqual()
	{
		// Arrange
		var grant1 = new Grant("user-123", null, null, "role", "admin", null, "system", _grantedOn);
		var grant2 = new Grant("user-456", null, null, "role", "admin", null, "system", _grantedOn);

		// Act & Assert
		grant1.ShouldNotBe(grant2);
	}

	[Fact]
	public void Equality_DifferentGrantType_AreNotEqual()
	{
		// Arrange
		var grant1 = new Grant("user-123", null, null, "role", "admin", null, "system", _grantedOn);
		var grant2 = new Grant("user-123", null, null, "permission", "admin", null, "system", _grantedOn);

		// Act & Assert
		grant1.ShouldNotBe(grant2);
	}

	[Fact]
	public void Equality_DifferentQualifier_AreNotEqual()
	{
		// Arrange
		var grant1 = new Grant("user-123", null, null, "role", "admin", null, "system", _grantedOn);
		var grant2 = new Grant("user-123", null, null, "role", "viewer", null, "system", _grantedOn);

		// Act & Assert
		grant1.ShouldNotBe(grant2);
	}

	[Fact]
	public void Equality_DifferentExpiresOn_AreNotEqual()
	{
		// Arrange
		var grant1 = new Grant("user-123", null, null, "role", "admin", null, "system", _grantedOn);
		var grant2 = new Grant("user-123", null, null, "role", "admin", _expiresOn, "system", _grantedOn);

		// Act & Assert
		grant1.ShouldNotBe(grant2);
	}

	#endregion

	#region With Expression Tests

	[Fact]
	public void With_CreatesModifiedCopy_UserId()
	{
		// Arrange
		var original = new Grant("user-123", null, null, "role", "admin", null, "system", _grantedOn);

		// Act
		var modified = original with { UserId = "user-456" };

		// Assert
		original.UserId.ShouldBe("user-123");
		modified.UserId.ShouldBe("user-456");
	}

	[Fact]
	public void With_CreatesModifiedCopy_ExpiresOn()
	{
		// Arrange
		var original = new Grant("user-123", null, null, "role", "admin", null, "system", _grantedOn);

		// Act
		var modified = original with { ExpiresOn = _expiresOn };

		// Assert
		original.ExpiresOn.ShouldBeNull();
		modified.ExpiresOn.ShouldBe(_expiresOn);
	}

	[Fact]
	public void With_CreatesModifiedCopy_FullName()
	{
		// Arrange
		var original = new Grant("user-123", null, null, "role", "admin", null, "system", _grantedOn);

		// Act
		var modified = original with { FullName = "Jane Smith" };

		// Assert
		original.FullName.ShouldBeNull();
		modified.FullName.ShouldBe("Jane Smith");
	}

	#endregion

	#region Grant Type Scenarios

	[Theory]
	[InlineData("role")]
	[InlineData("activity-group")]
	[InlineData("permission")]
	[InlineData("scope")]
	public void Create_WithCommonGrantTypes_Succeeds(string grantType)
	{
		// Act
		var grant = new Grant("user-123", null, null, grantType, "qualifier", null, "system", _grantedOn);

		// Assert
		grant.GrantType.ShouldBe(grantType);
	}

	#endregion
}
