// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Authorization.Grants;

namespace Excalibur.Tests.A3.Grants;

/// <summary>
/// Unit tests for <see cref="GrantKey"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class GrantKeyShould : UnitTestBase
{
	#region Constructor Tests (Four Parameters)

	[Fact]
	public void Create_WithFourParameters_SetsValues()
	{
		// Arrange & Act
		var key = new GrantKey("user-123", "tenant-abc", "role", "admin");

		// Assert
		key.UserId.ShouldBe("user-123");
		key.Scope.ShouldNotBeNull();
		key.Scope.TenantId.ShouldBe("tenant-abc");
		key.Scope.GrantType.ShouldBe("role");
		key.Scope.Qualifier.ShouldBe("admin");
	}

	[Fact]
	public void Create_WithNullUserId_ThrowsArgumentException()
	{
		// Arrange & Act & Assert
		Should.Throw<ArgumentException>(() => new GrantKey(null, "tenant-abc", "role", "admin"));
	}

	[Fact]
	public void Create_WithEmptyUserId_ThrowsArgumentException()
	{
		// Arrange & Act & Assert
		Should.Throw<ArgumentException>(() => new GrantKey("", "tenant-abc", "role", "admin"));
	}

	[Fact]
	public void Create_WithNullTenantId_ThrowsArgumentException()
	{
		// Arrange & Act & Assert
		Should.Throw<ArgumentException>(() => new GrantKey("user-123", null, "role", "admin"));
	}

	[Fact]
	public void Create_WithEmptyTenantId_ThrowsArgumentException()
	{
		// Arrange & Act & Assert
		Should.Throw<ArgumentException>(() => new GrantKey("user-123", "", "role", "admin"));
	}

	[Fact]
	public void Create_WithNullGrantType_ThrowsArgumentException()
	{
		// Arrange & Act & Assert
		Should.Throw<ArgumentException>(() => new GrantKey("user-123", "tenant-abc", null, "admin"));
	}

	[Fact]
	public void Create_WithEmptyGrantType_ThrowsArgumentException()
	{
		// Arrange & Act & Assert
		Should.Throw<ArgumentException>(() => new GrantKey("user-123", "tenant-abc", "", "admin"));
	}

	[Fact]
	public void Create_WithNullQualifier_ThrowsArgumentException()
	{
		// Arrange & Act & Assert
		Should.Throw<ArgumentException>(() => new GrantKey("user-123", "tenant-abc", "role", null));
	}

	[Fact]
	public void Create_WithEmptyQualifier_ThrowsArgumentException()
	{
		// Arrange & Act & Assert
		Should.Throw<ArgumentException>(() => new GrantKey("user-123", "tenant-abc", "role", ""));
	}

	#endregion

	#region Constructor Tests (String Key)

	[Fact]
	public void Create_FromKeyString_ParsesCorrectly()
	{
		// Arrange
		var keyString = "user-456:tenant-xyz:permission:write";

		// Act
		var key = new GrantKey(keyString);

		// Assert
		key.UserId.ShouldBe("user-456");
		key.Scope.TenantId.ShouldBe("tenant-xyz");
		key.Scope.GrantType.ShouldBe("permission");
		key.Scope.Qualifier.ShouldBe("write");
	}

	[Fact]
	public void Create_FromKeyString_WithNullKey_ThrowsArgumentException()
	{
		// Arrange & Act & Assert
		Should.Throw<ArgumentException>(() => new GrantKey(null!));
	}

	[Fact]
	public void Create_FromKeyString_WithEmptyKey_ThrowsArgumentException()
	{
		// Arrange & Act & Assert
		Should.Throw<ArgumentException>(() => new GrantKey(""));
	}

	[Fact]
	public void Create_FromKeyString_WithTooFewParts_ThrowsArgumentException()
	{
		// Arrange
		var invalidKey = "user:tenant:type";

		// Act & Assert
		var ex = Should.Throw<ArgumentException>(() => new GrantKey(invalidKey));
		ex.Message.ShouldContain("expected format");
	}

	[Fact]
	public void Create_FromKeyString_WithOnePart_ThrowsArgumentException()
	{
		// Arrange
		var invalidKey = "user-only";

		// Act & Assert
		Should.Throw<ArgumentException>(() => new GrantKey(invalidKey));
	}

	#endregion

	#region ToString Tests

	[Fact]
	public void ToString_ReturnsFormattedString()
	{
		// Arrange
		var key = new GrantKey("user-123", "tenant-abc", "role", "admin");

		// Act
		var result = key.ToString();

		// Assert
		result.ShouldBe("user-123:tenant-abc:role:admin");
	}

	[Fact]
	public void ToString_RoundTrips_WithStringConstructor()
	{
		// Arrange
		var original = new GrantKey("user-123", "tenant-abc", "activity-group", "orders");
		var serialized = original.ToString();

		// Act
		var deserialized = new GrantKey(serialized);

		// Assert
		deserialized.UserId.ShouldBe(original.UserId);
		deserialized.Scope.TenantId.ShouldBe(original.Scope.TenantId);
		deserialized.Scope.GrantType.ShouldBe(original.Scope.GrantType);
		deserialized.Scope.Qualifier.ShouldBe(original.Scope.Qualifier);
	}

	#endregion

	#region Scope Property Tests

	[Fact]
	public void Scope_ReturnsValidGrantScope()
	{
		// Arrange
		var key = new GrantKey("user-123", "tenant-abc", "role", "admin");

		// Act
		var scope = key.Scope;

		// Assert
		scope.ShouldNotBeNull();
		scope.ShouldBeOfType<GrantScope>();
		scope.ToString().ShouldBe("tenant-abc:role:admin");
	}

	#endregion

	#region Common Use Cases

	[Theory]
	[InlineData("service-account", "default", "Activity", "CreateOrder")]
	[InlineData("user-guid-123", "company-xyz", "ActivityGroup", "orders-management")]
	[InlineData("admin@example.com", "global", "permission", "admin")]
	public void Create_WithVariousFormats_Succeeds(string userId, string tenantId, string grantType, string qualifier)
	{
		// Act
		var key = new GrantKey(userId, tenantId, grantType, qualifier);

		// Assert
		key.UserId.ShouldBe(userId);
		key.Scope.TenantId.ShouldBe(tenantId);
		key.Scope.GrantType.ShouldBe(grantType);
		key.Scope.Qualifier.ShouldBe(qualifier);
	}

	#endregion
}
