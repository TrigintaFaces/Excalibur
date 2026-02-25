// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Caching;

namespace Excalibur.Dispatch.Caching.Tests;

/// <summary>
/// Unit tests for <see cref="CacheResultAttribute"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Caching")]
public sealed class CacheResultAttributeShould
{
	private static readonly string[] UserProfileTags = ["users", "profiles"];
	private static readonly string[] CacheTagValues = ["cache-tag-1", "cache-tag-2"];

	#region Default Value Tests

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
	public void HaveTrueOnlyIfSuccess_ByDefault()
	{
		// Arrange & Act
		var attribute = new CacheResultAttribute();

		// Assert
		attribute.OnlyIfSuccess.ShouldBeTrue();
	}

	[Fact]
	public void HaveTrueIgnoreNullResult_ByDefault()
	{
		// Arrange & Act
		var attribute = new CacheResultAttribute();

		// Assert
		attribute.IgnoreNullResult.ShouldBeTrue();
	}

	#endregion

	#region Property Setter Tests

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

		// Act
		attribute.Tags = ["users", "profiles"];

		// Assert
		attribute.Tags.ShouldBe(UserProfileTags);
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

	#endregion

	#region Attribute Usage Tests

	[Fact]
	public void BeApplicableToClasses()
	{
		// Arrange & Act
		var usageAttribute = typeof(CacheResultAttribute)
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
		var attribute = new CacheResultAttribute();

		// Assert
		attribute.ShouldBeAssignableTo<Attribute>();
	}

	#endregion

	#region Complete Initialization Tests

	[Fact]
	public void AllowSettingAllPropertiesAtOnce()
	{
		// Arrange & Act
		var attribute = new CacheResultAttribute
		{
			ExpirationSeconds = 600,
			Tags = ["cache-tag-1", "cache-tag-2"],
			OnlyIfSuccess = false,
			IgnoreNullResult = false,
		};

		// Assert
		attribute.ExpirationSeconds.ShouldBe(600);
		attribute.Tags.ShouldBe(CacheTagValues);
		attribute.OnlyIfSuccess.ShouldBeFalse();
		attribute.IgnoreNullResult.ShouldBeFalse();
	}

	#endregion
}
