// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Caching;

namespace Excalibur.Dispatch.Middleware.Tests.Caching;

/// <summary>
/// Unit tests for <see cref="CacheResultAttribute"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Caching")]
public sealed class CacheResultAttributeShould
{
	private static readonly string[] ProductCatalogTags = ["products", "catalog"];

	[Fact]
	public void HaveZeroExpirationSeconds_ByDefault()
	{
		// Arrange & Act
		var attribute = new CacheResultAttribute();

		// Assert
		attribute.ExpirationSeconds.ShouldBe(0);
	}

	[Fact]
	public void HaveEmptyTags_ByDefault()
	{
		// Arrange & Act
		var attribute = new CacheResultAttribute();

		// Assert
		attribute.Tags.ShouldNotBeNull();
		attribute.Tags.ShouldBeEmpty();
	}

	[Fact]
	public void HaveOnlyIfSuccessTrue_ByDefault()
	{
		// Arrange & Act
		var attribute = new CacheResultAttribute();

		// Assert
		attribute.OnlyIfSuccess.ShouldBeTrue();
	}

	[Fact]
	public void HaveIgnoreNullResultTrue_ByDefault()
	{
		// Arrange & Act
		var attribute = new CacheResultAttribute();

		// Assert
		attribute.IgnoreNullResult.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingExpirationSeconds()
	{
		// Arrange
		var attribute = new CacheResultAttribute();

		// Act
		attribute.ExpirationSeconds = 300;

		// Assert
		attribute.ExpirationSeconds.ShouldBe(300);
	}

	[Fact]
	public void AllowSettingTags()
	{
		// Arrange
		var attribute = new CacheResultAttribute();
		var tags = new[] { "user", "orders" };

		// Act
		attribute.Tags = tags;

		// Assert
		attribute.Tags.ShouldBe(tags);
	}

	[Fact]
	public void AllowSettingOnlyIfSuccessToFalse()
	{
		// Arrange
		var attribute = new CacheResultAttribute();

		// Act
		attribute.OnlyIfSuccess = false;

		// Assert
		attribute.OnlyIfSuccess.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingIgnoreNullResultToFalse()
	{
		// Arrange
		var attribute = new CacheResultAttribute();

		// Act
		attribute.IgnoreNullResult = false;

		// Assert
		attribute.IgnoreNullResult.ShouldBeFalse();
	}

	[Fact]
	public void BeApplicableToClasses()
	{
		// Arrange
		var attributeUsage = typeof(CacheResultAttribute)
			.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
			.Cast<AttributeUsageAttribute>()
			.Single();

		// Assert
		attributeUsage.ValidOn.ShouldBe(AttributeTargets.Class);
	}

	[Fact]
	public void AllowCreatingWithAllProperties()
	{
		// Arrange & Act
		var attribute = new CacheResultAttribute
		{
			ExpirationSeconds = 600,
			Tags = ["products", "catalog"],
			OnlyIfSuccess = false,
			IgnoreNullResult = false,
		};

		// Assert
		attribute.ExpirationSeconds.ShouldBe(600);
		attribute.Tags.ShouldBe(ProductCatalogTags);
		attribute.OnlyIfSuccess.ShouldBeFalse();
		attribute.IgnoreNullResult.ShouldBeFalse();
	}

	[Fact]
	public void AllowLargeExpirationSeconds()
	{
		// Arrange
		var attribute = new CacheResultAttribute();

		// Act
		attribute.ExpirationSeconds = 86400; // 24 hours

		// Assert
		attribute.ExpirationSeconds.ShouldBe(86400);
	}

	[Fact]
	public void AllowMultipleTags()
	{
		// Arrange
		var attribute = new CacheResultAttribute();

		// Act
		attribute.Tags = ["tag1", "tag2", "tag3", "tag4", "tag5"];

		// Assert
		attribute.Tags.Length.ShouldBe(5);
	}

	[Fact]
	public void InheritFromAttribute()
	{
		// Assert
		typeof(CacheResultAttribute).BaseType.ShouldBe(typeof(Attribute));
	}

	[Fact]
	public void BeSealed()
	{
		// Assert
		typeof(CacheResultAttribute).IsSealed.ShouldBeTrue();
	}
}
