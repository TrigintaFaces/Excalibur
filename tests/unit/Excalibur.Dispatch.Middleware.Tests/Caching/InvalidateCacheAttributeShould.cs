// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Caching;
using Tests.Shared;

namespace Excalibur.Dispatch.Middleware.Tests.Caching;

/// <summary>
/// Unit tests for InvalidateCacheAttribute.
/// </summary>
[Trait("Category", "Unit")]
public sealed class InvalidateCacheAttributeShould : UnitTestBase
{
	[Fact]
	public void Create_WithDefaults_HasEmptyTags()
	{
		// Arrange & Act
		var attribute = new InvalidateCacheAttribute();

		// Assert
		attribute.Tags.ShouldBeEmpty();
	}

	[Fact]
	public void Create_WithTags_StoresTags()
	{
		// Arrange & Act
		var attribute = new InvalidateCacheAttribute
		{
			Tags = new[] { "user", "product" }
		};

		// Assert
		attribute.Tags.ShouldNotBeEmpty();
		attribute.Tags.Length.ShouldBe(2);
		attribute.Tags.ShouldContain("user");
		attribute.Tags.ShouldContain("product");
	}

	[Fact]
	public void Create_WithSingleTag_StoresSingleTag()
	{
		// Arrange & Act
		var attribute = new InvalidateCacheAttribute
		{
			Tags = new[] { "order" }
		};

		// Assert
		attribute.Tags.Length.ShouldBe(1);
		attribute.Tags[0].ShouldBe("order");
	}

	[Fact]
	public void Create_WithMultipleTags_StoresAllTags()
	{
		// Arrange & Act
		var attribute = new InvalidateCacheAttribute
		{
			Tags = new[] { "user", "product", "order", "inventory" }
		};

		// Assert
		attribute.Tags.Length.ShouldBe(4);
		attribute.Tags.ShouldBe(["user", "product", "order", "inventory"]);
	}

	[Fact]
	public void AttributeUsage_IsClassLevel()
	{
		// Arrange
		var attributeUsage = typeof(InvalidateCacheAttribute)
			.GetCustomAttributes(typeof(AttributeUsageAttribute), inherit: false)
			.Cast<AttributeUsageAttribute>()
			.FirstOrDefault();

		// Assert
		attributeUsage.ShouldNotBeNull();
		attributeUsage.ValidOn.ShouldBe(AttributeTargets.Class);
	}
}
