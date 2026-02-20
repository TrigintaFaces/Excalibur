// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Caching;

namespace Excalibur.Dispatch.Tests.Messaging.Caching;

/// <summary>
/// Unit tests for <see cref="CacheAttributeInfo"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Caching")]
[Trait("Priority", "0")]
public sealed class CacheAttributeInfoShould
{
	#region Default Value Tests

	[Fact]
	public void Default_Name_IsEmpty()
	{
		// Arrange & Act
		var info = new CacheAttributeInfo();

		// Assert
		info.Name.ShouldBe(string.Empty);
	}

	[Fact]
	public void Default_TtlSeconds_IsZero()
	{
		// Arrange & Act
		var info = new CacheAttributeInfo();

		// Assert
		info.TtlSeconds.ShouldBe(0);
	}

	[Fact]
	public void Default_KeyPrefix_IsNull()
	{
		// Arrange & Act
		var info = new CacheAttributeInfo();

		// Assert
		info.KeyPrefix.ShouldBeNull();
	}

	[Fact]
	public void Default_SlidingExpiration_IsFalse()
	{
		// Arrange & Act
		var info = new CacheAttributeInfo();

		// Assert
		info.SlidingExpiration.ShouldBeFalse();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void Name_CanBeSet()
	{
		// Arrange
		var info = new CacheAttributeInfo();

		// Act
		info.Name = "TestCache";

		// Assert
		info.Name.ShouldBe("TestCache");
	}

	[Fact]
	public void TtlSeconds_CanBeSet()
	{
		// Arrange
		var info = new CacheAttributeInfo();

		// Act
		info.TtlSeconds = 3600;

		// Assert
		info.TtlSeconds.ShouldBe(3600);
	}

	[Fact]
	public void KeyPrefix_CanBeSet()
	{
		// Arrange
		var info = new CacheAttributeInfo();

		// Act
		info.KeyPrefix = "user_";

		// Assert
		info.KeyPrefix.ShouldBe("user_");
	}

	[Fact]
	public void SlidingExpiration_CanBeSet()
	{
		// Arrange
		var info = new CacheAttributeInfo();

		// Act
		info.SlidingExpiration = true;

		// Assert
		info.SlidingExpiration.ShouldBeTrue();
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var info = new CacheAttributeInfo
		{
			Name = "ProductCache",
			TtlSeconds = 7200,
			KeyPrefix = "product_",
			SlidingExpiration = true,
		};

		// Assert
		info.Name.ShouldBe("ProductCache");
		info.TtlSeconds.ShouldBe(7200);
		info.KeyPrefix.ShouldBe("product_");
		info.SlidingExpiration.ShouldBeTrue();
	}

	#endregion

	#region Edge Cases

	[Fact]
	public void TtlSeconds_RejectsNegativeValue()
	{
		// Arrange
		var info = new CacheAttributeInfo();

		// Act & Assert
		Should.Throw<ArgumentOutOfRangeException>(() => info.TtlSeconds = -1);
	}

	[Fact]
	public void Name_RejectsNull()
	{
		// Arrange
		var info = new CacheAttributeInfo();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => info.Name = null!);
	}

	[Fact]
	public void KeyPrefix_CanBeEmptyString()
	{
		// Arrange
		var info = new CacheAttributeInfo();

		// Act
		info.KeyPrefix = string.Empty;

		// Assert
		info.KeyPrefix.ShouldBe(string.Empty);
	}

	#endregion
}
