// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions.Tests.ContextValues;

/// <summary>
/// Unit tests for <see cref="TenantId"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "ContextValues")]
[Trait("Priority", "0")]
public sealed class TenantIdShould
{
	#region Constructor Tests

	[Fact]
	public void Constructor_WithValue_SetsValue()
	{
		// Act
		var tenantId = new TenantId("tenant-123");

		// Assert
		tenantId.Value.ShouldBe("tenant-123");
	}

	[Fact]
	public void Constructor_WithNullValue_SetsEmptyString()
	{
		// Act
		var tenantId = new TenantId(null);

		// Assert
		tenantId.Value.ShouldBe(string.Empty);
	}

	[Fact]
	public void Constructor_Parameterless_SetsEmptyString()
	{
		// Act
		var tenantId = new TenantId();

		// Assert
		tenantId.Value.ShouldBe(string.Empty);
	}

	[Fact]
	public void Constructor_WithEmptyString_SetsEmptyString()
	{
		// Act
		var tenantId = new TenantId(string.Empty);

		// Assert
		tenantId.Value.ShouldBe(string.Empty);
	}

	#endregion

	#region Value Property Tests

	[Fact]
	public void Value_CanBeSet()
	{
		// Arrange
		var tenantId = new TenantId("original");

		// Act
		tenantId.Value = "modified";

		// Assert
		tenantId.Value.ShouldBe("modified");
	}

	#endregion

	#region ToString Tests

	[Fact]
	public void ToString_ReturnsValue()
	{
		// Arrange
		var tenantId = new TenantId("my-tenant");

		// Act
		var result = tenantId.ToString();

		// Assert
		result.ShouldBe("my-tenant");
	}

	[Fact]
	public void ToString_WhenEmpty_ReturnsEmptyString()
	{
		// Arrange
		var tenantId = new TenantId();

		// Act
		var result = tenantId.ToString();

		// Assert
		result.ShouldBe(string.Empty);
	}

	#endregion

	#region FromString Tests

	[Fact]
	public void FromString_CreatesNewInstance()
	{
		// Act
		var tenantId = TenantId.FromString("from-string-value");

		// Assert
		tenantId.Value.ShouldBe("from-string-value");
	}

	[Fact]
	public void FromString_WithNull_CreatesInstanceWithEmptyString()
	{
		// Act
		var tenantId = TenantId.FromString(null!);

		// Assert
		tenantId.Value.ShouldBe(string.Empty);
	}

	#endregion

	#region Implicit Conversion Tests

	[Fact]
	public void ImplicitConversion_FromString_CreatesInstance()
	{
		// Act
		TenantId tenantId = "implicit-conversion";

		// Assert
		tenantId.Value.ShouldBe("implicit-conversion");
	}

	[Fact]
	public void ImplicitConversion_FromNull_CreatesInstanceWithEmptyString()
	{
		// Act
		TenantId tenantId = (string)null!;

		// Assert
		tenantId.Value.ShouldBe(string.Empty);
	}

	#endregion

	#region Equality Tests

	[Fact]
	public void Equals_WithSameValue_ReturnsTrue()
	{
		// Arrange
		var tenantId1 = new TenantId("same-value");
		var tenantId2 = new TenantId("same-value");

		// Act & Assert
		tenantId1.Equals(tenantId2).ShouldBeTrue();
	}

	[Fact]
	public void Equals_WithDifferentCase_ReturnsTrue()
	{
		// Arrange (case-insensitive comparison)
		var tenantId1 = new TenantId("TENANT");
		var tenantId2 = new TenantId("tenant");

		// Act & Assert
		tenantId1.Equals(tenantId2).ShouldBeTrue();
	}

	[Fact]
	public void Equals_WithDifferentValue_ReturnsFalse()
	{
		// Arrange
		var tenantId1 = new TenantId("value-a");
		var tenantId2 = new TenantId("value-b");

		// Act & Assert
		tenantId1.Equals(tenantId2).ShouldBeFalse();
	}

	[Fact]
	public void Equals_WithNull_ReturnsFalse()
	{
		// Arrange
		var tenantId = new TenantId("test");

		// Act & Assert
		tenantId.Equals(null).ShouldBeFalse();
	}

	[Fact]
	public void Equals_WithSameReference_ReturnsTrue()
	{
		// Arrange
		var tenantId = new TenantId("test");

		// Act & Assert
		tenantId.Equals(tenantId).ShouldBeTrue();
	}

	[Fact]
	public void ObjectEquals_WithSameValue_ReturnsTrue()
	{
		// Arrange
		var tenantId1 = new TenantId("same");
		object tenantId2 = new TenantId("same");

		// Act & Assert
		tenantId1.Equals(tenantId2).ShouldBeTrue();
	}

	[Fact]
	public void ObjectEquals_WithNonTenantId_ReturnsFalse()
	{
		// Arrange
		var tenantId = new TenantId("test");

		// Act - Cast to object to avoid implicit conversion from string to TenantId
		var result = tenantId.Equals((object)123);

		// Assert
		result.ShouldBeFalse();
	}

	#endregion

	#region GetHashCode Tests

	[Fact]
	public void GetHashCode_WithSameValue_ReturnsSameHash()
	{
		// Arrange
		var tenantId1 = new TenantId("same-value");
		var tenantId2 = new TenantId("same-value");

		// Act & Assert
		tenantId1.GetHashCode().ShouldBe(tenantId2.GetHashCode());
	}

	[Fact]
	public void GetHashCode_WithDifferentCase_ReturnsSameHash()
	{
		// Arrange (case-insensitive hash)
		var tenantId1 = new TenantId("TENANT");
		var tenantId2 = new TenantId("tenant");

		// Act & Assert
		tenantId1.GetHashCode().ShouldBe(tenantId2.GetHashCode());
	}

	[Fact]
	public void GetHashCode_WithDifferentValue_ReturnsDifferentHash()
	{
		// Arrange
		var tenantId1 = new TenantId("value-a");
		var tenantId2 = new TenantId("value-b");

		// Act & Assert
		tenantId1.GetHashCode().ShouldNotBe(tenantId2.GetHashCode());
	}

	#endregion

	#region ITenantId Interface Tests

	[Fact]
	public void ImplementsITenantIdInterface()
	{
		// Arrange
		var tenantId = new TenantId("test");

		// Assert
		_ = tenantId.ShouldBeAssignableTo<ITenantId>();
	}

	[Fact]
	public void ITenantId_Value_ReturnsCorrectValue()
	{
		// Arrange
		ITenantId tenantId = new TenantId("interface-test");

		// Act & Assert
		tenantId.Value.ShouldBe("interface-test");
	}

	#endregion
}
