// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Caching;

namespace Excalibur.Dispatch.Tests.Messaging.Caching;

/// <summary>
/// Unit tests for <see cref="CacheableInfo"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Caching")]
[Trait("Priority", "0")]
public sealed class CacheableInfoShould
{
	#region Default Value Tests

	[Fact]
	public void Default_TypeName_IsEmpty()
	{
		// Arrange & Act
		var info = new CacheableInfo();

		// Assert
		info.TypeName.ShouldBe(string.Empty);
	}

	[Fact]
	public void Default_IsCacheable_IsFalse()
	{
		// Arrange & Act
		var info = new CacheableInfo();

		// Assert
		info.IsCacheable.ShouldBeFalse();
	}

	[Fact]
	public void Default_Attributes_IsEmptyArray()
	{
		// Arrange & Act
		var info = new CacheableInfo();

		// Assert
		info.Attributes.ShouldBeEmpty();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void TypeName_CanBeSet()
	{
		// Arrange
		var info = new CacheableInfo();

		// Act
		info.TypeName = "MyNamespace.MyClass";

		// Assert
		info.TypeName.ShouldBe("MyNamespace.MyClass");
	}

	[Fact]
	public void IsCacheable_CanBeSet()
	{
		// Arrange
		var info = new CacheableInfo();

		// Act
		info.IsCacheable = true;

		// Assert
		info.IsCacheable.ShouldBeTrue();
	}

	[Fact]
	public void Attributes_CanBeSet()
	{
		// Arrange
		var info = new CacheableInfo();
		var attributes = new[]
		{
			new CacheAttributeInfo { Name = "Attr1", TtlSeconds = 60 },
			new CacheAttributeInfo { Name = "Attr2", TtlSeconds = 120 },
		};

		// Act
		info.Attributes = attributes;

		// Assert
		info.Attributes.ShouldBe(attributes);
		info.Attributes.Length.ShouldBe(2);
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Arrange
		var attributes = new[]
		{
			new CacheAttributeInfo
			{
				Name = "TestCache",
				TtlSeconds = 300,
				KeyPrefix = "test_",
				SlidingExpiration = true,
			},
		};

		// Act
		var info = new CacheableInfo
		{
			TypeName = "MyApp.Domain.Product",
			IsCacheable = true,
			Attributes = attributes,
		};

		// Assert
		info.TypeName.ShouldBe("MyApp.Domain.Product");
		info.IsCacheable.ShouldBeTrue();
		info.Attributes.Length.ShouldBe(1);
		info.Attributes[0].Name.ShouldBe("TestCache");
	}

	#endregion

	#region Attributes Array Tests

	[Fact]
	public void Attributes_CanContainMultipleItems()
	{
		// Arrange
		var info = new CacheableInfo
		{
			Attributes = new[]
			{
				new CacheAttributeInfo { Name = "Cache1" },
				new CacheAttributeInfo { Name = "Cache2" },
				new CacheAttributeInfo { Name = "Cache3" },
			},
		};

		// Assert
		info.Attributes.Length.ShouldBe(3);
		info.Attributes[0].Name.ShouldBe("Cache1");
		info.Attributes[1].Name.ShouldBe("Cache2");
		info.Attributes[2].Name.ShouldBe("Cache3");
	}

	[Fact]
	public void Attributes_CanBeReassigned()
	{
		// Arrange
		var info = new CacheableInfo
		{
			Attributes = new[] { new CacheAttributeInfo { Name = "Original" } },
		};

		// Act
		info.Attributes = new[] { new CacheAttributeInfo { Name = "Replacement" } };

		// Assert
		info.Attributes.Length.ShouldBe(1);
		info.Attributes[0].Name.ShouldBe("Replacement");
	}

	#endregion

	#region Validation Tests

	[Fact]
	public void TypeName_RejectsNull()
	{
		// Arrange
		var info = new CacheableInfo();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => info.TypeName = null!);
	}

	[Fact]
	public void Attributes_RejectsNull()
	{
		// Arrange
		var info = new CacheableInfo();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => info.Attributes = null!);
	}

	#endregion
}
