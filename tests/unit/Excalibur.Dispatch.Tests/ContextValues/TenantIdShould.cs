// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

namespace Excalibur.Dispatch.Tests.ContextValues;

/// <summary>
/// Unit tests for <see cref="TenantId"/> class.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
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
	public void Create_WithNullValue_Throws()
	{
		// Was: null becomes "". Coercion produces an identifier that names no tenant and looks valid to
		// everything downstream.
		_ = Should.Throw<ArgumentNullException>(() => new TenantId(null!));
	}

	[Fact]
	public void Create_WithMissingValue_IsRejectedRatherThanCoerced()
	{
		// This asserted that a default-constructed identifier held the empty string. An empty tenant is not
		// a tenant: downstream it either matches nothing or, on a store that treats absence as unscoped,
		// matches everything. The constructor now refuses all three missing forms at the point the mistake
		// is made, which is the only place it can still be attributed.
		_ = Should.Throw<ArgumentNullException>(() => new TenantId(null!));
		_ = Should.Throw<ArgumentException>(() => new TenantId(string.Empty));
		_ = Should.Throw<ArgumentException>(() => new TenantId("   "));
	}

	[Fact]
	public void Create_WithEmptyString_Throws()
	{
		// Was: "" round-trips as a valid identifier. It is the input whose old behaviour was the defect.
		_ = Should.Throw<ArgumentException>(() => new TenantId(""));
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
	public void ExplicitConstruction_FromString_CreatesTenantId()
	{
		// The implicit string conversion is gone. It let any string in scope become a tenant identifier
		// without the author choosing to make one, including a null that became the empty tenant.
		// Construction is now a deliberate act; its removal is compiler-enforced, so what is asserted here
		// is the surviving explicit path.
		var tenantId = new TenantId("explicit-tenant");

		tenantId.Value.ShouldBe("explicit-tenant");
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
	public void ToString_NeverReturnsEmpty_BecauseAnEmptyIdentifierCannotExist()
	{
		// Arrange -- no empty identifier can be constructed, so the invariant replaces the old assertion.
		var tenantId = new TenantId("t");

		// Act
		var result = tenantId.ToString();

		// Assert
		result.ShouldNotBeNullOrWhiteSpace();
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
	public void Equals_DifferentCase_ReturnsFalse_BecauseComparisonIsOrdinal()
	{
		// INVERTED, and this one is security-relevant rather than tidy. Case-insensitive equality means
		// "Acme" and "acme" are ONE tenant in .NET. Whether they are one tenant in storage depends on the
		// column's collation, so a mismatch between the two layers decides tenant identity differently on
		// either side of a database call — and it fails open, because the looser side wins wherever it is
		// consulted first. Comparison is Ordinal: these are two tenants, here and everywhere.
		var tenantId1 = new TenantId("Tenant-ABC");
		var tenantId2 = new TenantId("tenant-abc");

		tenantId1.Equals(tenantId2).ShouldBeFalse(
			"Ordinal comparison must treat a case difference as a different tenant; conflating them is a "
			+ "cross-tenant identity collision");
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
	public void GetHashCode_SameValueDifferentCase_ReturnsDifferentHash()
	{
		// Follows the equality change and must not be forgotten alongside it: a hash that ignored case
		// while Equals respected it would collide two distinct tenants in every dictionary and set they are
		// used as keys in — which is where tenant-scoped lookups actually happen.
		var tenantId1 = new TenantId("TENANT-ABC");
		var tenantId2 = new TenantId("tenant-abc");

		tenantId1.GetHashCode().ShouldNotBe(tenantId2.GetHashCode());
	}

	#endregion

	#region Interface Implementation

	[Fact]
	public void ImplementsIEquatable()
	{
		// Arrange
		var tenantId = new TenantId("t");

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
