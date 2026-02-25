// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Tests.ContextValues;

/// <summary>
/// Unit tests for <see cref="TenantId"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Dispatch")]
public sealed class TenantIdShould
{
	#region Constructor Tests

	[Fact]
	public void Create_WithValue_SetsValue()
	{
		// Arrange & Act
		var tenantId = new TenantId("tenant-123");

		// Assert
		tenantId.Value.ShouldBe("tenant-123");
	}

	[Fact]
	public void Create_WithNullValue_SetsEmptyString()
	{
		// Arrange & Act
		var tenantId = new TenantId(null);

		// Assert
		tenantId.Value.ShouldBe(string.Empty);
	}

	[Fact]
	public void Create_WithDefaultConstructor_SetsEmptyString()
	{
		// Arrange & Act
		var tenantId = new TenantId();

		// Assert
		tenantId.Value.ShouldBe(string.Empty);
	}

	[Fact]
	public void Create_WithEmptyString_SetsEmptyString()
	{
		// Arrange & Act
		var tenantId = new TenantId("");

		// Assert
		tenantId.Value.ShouldBe(string.Empty);
	}

	#endregion

	#region FromString Tests

	[Fact]
	public void FromString_CreatesNewInstance()
	{
		// Arrange & Act
		var tenantId = TenantId.FromString("my-tenant");

		// Assert
		tenantId.Value.ShouldBe("my-tenant");
	}

	#endregion

	#region Implicit Conversion Tests

	[Fact]
	public void ImplicitConversion_FromString_CreatesTenantId()
	{
		// Arrange & Act
		TenantId tenantId = "implicit-tenant";

		// Assert
		tenantId.Value.ShouldBe("implicit-tenant");
	}

	#endregion

	#region ToString Tests

	[Fact]
	public void ToString_ReturnsValue()
	{
		// Arrange
		var tenantId = new TenantId("test-tenant");

		// Act
		var result = tenantId.ToString();

		// Assert
		result.ShouldBe("test-tenant");
	}

	[Fact]
	public void ToString_WithEmptyValue_ReturnsEmptyString()
	{
		// Arrange
		var tenantId = new TenantId();

		// Act
		var result = tenantId.ToString();

		// Assert
		result.ShouldBe(string.Empty);
	}

	#endregion

	#region Equality Tests

	[Fact]
	public void Equals_SameValue_ReturnsTrue()
	{
		// Arrange
		var tenantId1 = new TenantId("tenant-abc");
		var tenantId2 = new TenantId("tenant-abc");

		// Act & Assert
		tenantId1.Equals(tenantId2).ShouldBeTrue();
	}

	[Fact]
	public void Equals_DifferentCase_ReturnsTrue_BecauseCaseInsensitive()
	{
		// Arrange
		var tenantId1 = new TenantId("Tenant-ABC");
		var tenantId2 = new TenantId("tenant-abc");

		// Act & Assert
		tenantId1.Equals(tenantId2).ShouldBeTrue();
	}

	[Fact]
	public void Equals_DifferentValue_ReturnsFalse()
	{
		// Arrange
		var tenantId1 = new TenantId("tenant-abc");
		var tenantId2 = new TenantId("tenant-xyz");

		// Act & Assert
		tenantId1.Equals(tenantId2).ShouldBeFalse();
	}

	[Fact]
	public void Equals_SameReference_ReturnsTrue()
	{
		// Arrange
		var tenantId = new TenantId("tenant");

		// Act & Assert
		tenantId.Equals(tenantId).ShouldBeTrue();
	}

	[Fact]
	public void Equals_Null_ReturnsFalse()
	{
		// Arrange
		var tenantId = new TenantId("tenant");

		// Act & Assert
		tenantId.Equals(null).ShouldBeFalse();
	}

	[Fact]
	public void Equals_Object_SameValue_ReturnsTrue()
	{
		// Arrange
		var tenantId1 = new TenantId("tenant-abc");
		object tenantId2 = new TenantId("tenant-abc");

		// Act & Assert
		tenantId1.Equals(tenantId2).ShouldBeTrue();
	}

	[Fact]
	public void Equals_Object_DifferentType_ReturnsFalse()
	{
		// Arrange
		var tenantId = new TenantId("tenant");
		object other = "tenant";

		// Act & Assert
		tenantId.Equals(other).ShouldBeFalse();
	}

	#endregion

	#region GetHashCode Tests

	[Fact]
	public void GetHashCode_SameValue_ReturnsSameHash()
	{
		// Arrange
		var tenantId1 = new TenantId("tenant-abc");
		var tenantId2 = new TenantId("tenant-abc");

		// Act & Assert
		tenantId1.GetHashCode().ShouldBe(tenantId2.GetHashCode());
	}

	[Fact]
	public void GetHashCode_SameValueDifferentCase_ReturnsSameHash()
	{
		// Arrange
		var tenantId1 = new TenantId("TENANT-ABC");
		var tenantId2 = new TenantId("tenant-abc");

		// Act & Assert
		tenantId1.GetHashCode().ShouldBe(tenantId2.GetHashCode());
	}

	#endregion

	#region Interface Implementation

	[Fact]
	public void ImplementsITenantId()
	{
		// Arrange
		var tenantId = new TenantId();

		// Assert
		tenantId.ShouldBeAssignableTo<ITenantId>();
	}

	[Fact]
	public void ImplementsIEquatable()
	{
		// Arrange
		var tenantId = new TenantId();

		// Assert
		tenantId.ShouldBeAssignableTo<IEquatable<TenantId>>();
	}

	#endregion

	#region Common Use Cases

	[Theory]
	[InlineData("tenant-123")]
	[InlineData("00000000-0000-0000-0000-000000000001")]
	[InlineData("company-name")]
	[InlineData("org_abc123")]
	public void Create_WithVariousTenantFormats_Succeeds(string tenantValue)
	{
		// Act
		var tenantId = new TenantId(tenantValue);

		// Assert
		tenantId.Value.ShouldBe(tenantValue);
	}

	#endregion
}
