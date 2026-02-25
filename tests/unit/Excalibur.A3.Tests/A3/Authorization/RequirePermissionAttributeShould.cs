// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Authorization;

namespace Excalibur.Tests.A3.Authorization;

/// <summary>
/// Unit tests for <see cref="RequirePermissionAttribute"/>.
/// Tests T405.1 scenarios: construction validation, property behavior, and AttributeUsage settings.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Authorization")]
public sealed class RequirePermissionAttributeShould
{
	#region Constructor Tests

	[Fact]
	public void Constructor_ThrowArgumentException_WhenPermissionIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => new RequirePermissionAttribute(null!));
	}

	[Fact]
	public void Constructor_ThrowArgumentException_WhenPermissionIsEmpty()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => new RequirePermissionAttribute(""));
	}

	[Fact]
	public void Constructor_ThrowArgumentException_WhenPermissionIsWhitespace()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => new RequirePermissionAttribute("   "));
	}

	[Fact]
	public void Constructor_SetPermissionProperty()
	{
		// Arrange & Act
		var attr = new RequirePermissionAttribute("users.delete");

		// Assert
		attr.Permission.ShouldBe("users.delete");
	}

	#endregion

	#region Property Default Tests

	[Fact]
	public void Properties_DefaultToNull()
	{
		// Arrange & Act
		var attr = new RequirePermissionAttribute("test.permission");

		// Assert
		attr.ResourceTypes.ShouldBeNull();
		attr.ResourceIdProperty.ShouldBeNull();
		attr.When.ShouldBeNull();
	}

	#endregion

	#region Property Initialization Tests

	[Fact]
	public void Properties_CanBeInitialized()
	{
		// Arrange & Act
		var attr = new RequirePermissionAttribute("orders.update")
		{
			ResourceTypes = ["Order", "LineItem"],
			ResourceIdProperty = "OrderId",
			When = "Order.Status != 'Completed'"
		};

		// Assert
		attr.Permission.ShouldBe("orders.update");
		_ = attr.ResourceTypes.ShouldNotBeNull();
		attr.ResourceTypes.ShouldContain("Order");
		attr.ResourceTypes.ShouldContain("LineItem");
		attr.ResourceIdProperty.ShouldBe("OrderId");
		attr.When.ShouldBe("Order.Status != 'Completed'");
	}

	[Fact]
	public void ResourceTypes_CanBeEmptyArray()
	{
		// Arrange & Act
		var attr = new RequirePermissionAttribute("test") { ResourceTypes = [] };

		// Assert
		_ = attr.ResourceTypes.ShouldNotBeNull();
		attr.ResourceTypes.ShouldBeEmpty();
	}

	#endregion

	#region AttributeUsage Tests

	[Fact]
	public void Attribute_AllowsMultiple()
	{
		// Arrange
		var usage = typeof(RequirePermissionAttribute)
			.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
			.Cast<AttributeUsageAttribute>()
			.Single();

		// Assert
		usage.AllowMultiple.ShouldBeTrue();
	}

	[Fact]
	public void Attribute_IsInherited()
	{
		// Arrange
		var usage = typeof(RequirePermissionAttribute)
			.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
			.Cast<AttributeUsageAttribute>()
			.Single();

		// Assert
		usage.Inherited.ShouldBeTrue();
	}

	[Fact]
	public void Attribute_TargetsClassOnly()
	{
		// Arrange
		var usage = typeof(RequirePermissionAttribute)
			.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
			.Cast<AttributeUsageAttribute>()
			.Single();

		// Assert
		usage.ValidOn.ShouldBe(AttributeTargets.Class);
	}

	#endregion

	#region Reserved Property Tests

	[Fact]
	public void When_PropertyIsReserved_AcceptsValueButNoEffect()
	{
		// Arrange & Act - When property is reserved for future, can set but no effect currently
		var attr = new RequirePermissionAttribute("test") { When = "condition.expression" };

		// Assert - Value is stored but has no runtime effect in Sprint 404/405
		attr.When.ShouldBe("condition.expression");
	}

	#endregion
}
