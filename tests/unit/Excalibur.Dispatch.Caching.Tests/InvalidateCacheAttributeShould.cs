// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Caching;

namespace Excalibur.Dispatch.Caching.Tests;

/// <summary>
/// Unit tests for <see cref="InvalidateCacheAttribute"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Caching")]
public sealed class InvalidateCacheAttributeShould
{
	#region Default Value Tests

	[Fact]
	public void HaveEmptyTags_ByDefault()
	{
		// Arrange & Act
		var attribute = new InvalidateCacheAttribute();

		// Assert
		attribute.Tags.ShouldNotBeNull();
		attribute.Tags.ShouldBeEmpty();
	}

	#endregion

	#region Property Initialization Tests

	[Fact]
	public void AllowSettingTags_ViaInit()
	{
		// Arrange & Act
		var attribute = new InvalidateCacheAttribute { Tags = ["users", "profiles"] };

		// Assert
		attribute.Tags.ShouldBe(new[] { "users", "profiles" });
	}

	[Fact]
	public void AllowSettingSingleTag()
	{
		// Arrange & Act
		var attribute = new InvalidateCacheAttribute { Tags = ["user-cache"] };

		// Assert
		attribute.Tags.Length.ShouldBe(1);
		attribute.Tags[0].ShouldBe("user-cache");
	}

	[Fact]
	public void AllowSettingMultipleTags()
	{
		// Arrange & Act
		var attribute = new InvalidateCacheAttribute
		{
			Tags = ["tag1", "tag2", "tag3", "tag4", "tag5"],
		};

		// Assert
		attribute.Tags.Length.ShouldBe(5);
	}

	#endregion

	#region Attribute Usage Tests

	[Fact]
	public void BeApplicableToClasses()
	{
		// Arrange & Act
		var usageAttribute = typeof(InvalidateCacheAttribute)
			.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
			.Cast<AttributeUsageAttribute>()
			.FirstOrDefault();

		// Assert
		usageAttribute.ShouldNotBeNull();
		usageAttribute.ValidOn.ShouldBe(AttributeTargets.Class);
	}

	[Fact]
	public void InheritFromAttribute()
	{
		// Arrange & Act
		var attribute = new InvalidateCacheAttribute();

		// Assert
		attribute.ShouldBeAssignableTo<Attribute>();
	}

	#endregion
}
