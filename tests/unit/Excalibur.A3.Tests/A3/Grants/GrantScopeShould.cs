// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Authorization.Grants;

namespace Excalibur.Tests.A3.Grants;

/// <summary>
/// Unit tests for <see cref="GrantScope"/> record.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class GrantScopeShould : UnitTestBase
{
	#region Constructor Tests

	[Fact]
	public void Create_WithValidParameters_SetsValues()
	{
		// Arrange & Act
		var scope = new GrantScope("tenant-123", "role", "admin");

		// Assert
		scope.TenantId.ShouldBe("tenant-123");
		scope.GrantType.ShouldBe("role");
		scope.Qualifier.ShouldBe("admin");
	}

	[Fact]
	public void Create_WithNullTenantId_ThrowsArgumentNullException()
	{
		// Arrange & Act & Assert
		Should.Throw<ArgumentNullException>(() => new GrantScope(null!, "role", "admin"));
	}

	[Fact]
	public void Create_WithEmptyTenantId_ThrowsArgumentNullException()
	{
		// Arrange & Act & Assert
		Should.Throw<ArgumentNullException>(() => new GrantScope("", "role", "admin"));
	}

	[Fact]
	public void Create_WithNullGrantType_ThrowsArgumentNullException()
	{
		// Arrange & Act & Assert
		Should.Throw<ArgumentNullException>(() => new GrantScope("tenant-123", null!, "admin"));
	}

	[Fact]
	public void Create_WithEmptyGrantType_ThrowsArgumentNullException()
	{
		// Arrange & Act & Assert
		Should.Throw<ArgumentNullException>(() => new GrantScope("tenant-123", "", "admin"));
	}

	[Fact]
	public void Create_WithNullQualifier_ThrowsArgumentNullException()
	{
		// Arrange & Act & Assert
		Should.Throw<ArgumentNullException>(() => new GrantScope("tenant-123", "role", null!));
	}

	[Fact]
	public void Create_WithEmptyQualifier_ThrowsArgumentNullException()
	{
		// Arrange & Act & Assert
		Should.Throw<ArgumentNullException>(() => new GrantScope("tenant-123", "role", ""));
	}

	#endregion

	#region FromString Tests

	[Fact]
	public void FromString_WithValidScope_ParsesCorrectly()
	{
		// Arrange
		var scopeString = "tenant-abc:activity-group:orders";

		// Act
		var scope = GrantScope.FromString(scopeString);

		// Assert
		scope.TenantId.ShouldBe("tenant-abc");
		scope.GrantType.ShouldBe("activity-group");
		scope.Qualifier.ShouldBe("orders");
	}

	[Fact]
	public void FromString_WithNullScope_ThrowsArgumentNullException()
	{
		// Arrange & Act & Assert
		Should.Throw<ArgumentNullException>(() => GrantScope.FromString(null!));
	}

	[Fact]
	public void FromString_WithTooFewParts_ThrowsArgumentException()
	{
		// Arrange
		var invalidScope = "tenant:type";

		// Act & Assert
		var ex = Should.Throw<ArgumentException>(() => GrantScope.FromString(invalidScope));
		ex.Message.ShouldContain("expected format");
	}

	[Fact]
	public void FromString_WithEmptyString_ThrowsArgumentException()
	{
		// Arrange & Act & Assert
		Should.Throw<ArgumentException>(() => GrantScope.FromString(""));
	}

	[Fact]
	public void FromString_WithOnlyColons_ThrowsArgumentException()
	{
		// Arrange
		var invalidScope = "::";

		// Act & Assert
		Should.Throw<ArgumentException>(() => GrantScope.FromString(invalidScope));
	}

	#endregion

	#region ToString Tests

	[Fact]
	public void ToString_ReturnsFormattedString()
	{
		// Arrange
		var scope = new GrantScope("tenant-xyz", "permission", "read");

		// Act
		var result = scope.ToString();

		// Assert
		result.ShouldBe("tenant-xyz:permission:read");
	}

	[Fact]
	public void ToString_RoundTrips_WithFromString()
	{
		// Arrange
		var original = new GrantScope("tenant-123", "role", "admin");
		var serialized = original.ToString();

		// Act
		var deserialized = GrantScope.FromString(serialized);

		// Assert
		deserialized.TenantId.ShouldBe(original.TenantId);
		deserialized.GrantType.ShouldBe(original.GrantType);
		deserialized.Qualifier.ShouldBe(original.Qualifier);
	}

	#endregion

	#region Equality Tests

	[Fact]
	public void Equality_SameValues_AreEqual()
	{
		// Arrange
		var scope1 = new GrantScope("tenant-123", "role", "admin");
		var scope2 = new GrantScope("tenant-123", "role", "admin");

		// Act & Assert
		scope1.ShouldBe(scope2);
	}

	[Fact]
	public void Equality_DifferentTenantId_AreNotEqual()
	{
		// Arrange
		var scope1 = new GrantScope("tenant-123", "role", "admin");
		var scope2 = new GrantScope("tenant-456", "role", "admin");

		// Act & Assert
		scope1.ShouldNotBe(scope2);
	}

	[Fact]
	public void Equality_DifferentGrantType_AreNotEqual()
	{
		// Arrange
		var scope1 = new GrantScope("tenant-123", "role", "admin");
		var scope2 = new GrantScope("tenant-123", "permission", "admin");

		// Act & Assert
		scope1.ShouldNotBe(scope2);
	}

	[Fact]
	public void Equality_DifferentQualifier_AreNotEqual()
	{
		// Arrange
		var scope1 = new GrantScope("tenant-123", "role", "admin");
		var scope2 = new GrantScope("tenant-123", "role", "viewer");

		// Act & Assert
		scope1.ShouldNotBe(scope2);
	}

	#endregion

	#region With Expression Tests

	[Fact]
	public void With_CreatesModifiedCopy_TenantId()
	{
		// Arrange
		var original = new GrantScope("tenant-123", "role", "admin");

		// Act
		var modified = original with { TenantId = "tenant-456" };

		// Assert
		modified.TenantId.ShouldBe("tenant-456");
		modified.GrantType.ShouldBe("role");
		modified.Qualifier.ShouldBe("admin");
	}

	[Fact]
	public void With_CreatesModifiedCopy_GrantType()
	{
		// Arrange
		var original = new GrantScope("tenant-123", "role", "admin");

		// Act
		var modified = original with { GrantType = "permission" };

		// Assert
		modified.TenantId.ShouldBe("tenant-123");
		modified.GrantType.ShouldBe("permission");
		modified.Qualifier.ShouldBe("admin");
	}

	[Fact]
	public void With_CreatesModifiedCopy_Qualifier()
	{
		// Arrange
		var original = new GrantScope("tenant-123", "role", "admin");

		// Act
		var modified = original with { Qualifier = "viewer" };

		// Assert
		modified.TenantId.ShouldBe("tenant-123");
		modified.GrantType.ShouldBe("role");
		modified.Qualifier.ShouldBe("viewer");
	}

	#endregion
}
