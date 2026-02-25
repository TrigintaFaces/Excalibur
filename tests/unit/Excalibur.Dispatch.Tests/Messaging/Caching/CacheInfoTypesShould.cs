// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Caching;

namespace Excalibur.Dispatch.Tests.Messaging.Caching;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CacheInfoTypesShould
{
	// --- CacheAttributeInfo ---

	[Fact]
	public void CacheAttributeInfo_DefaultValues_AreCorrect()
	{
		// Act
		var info = new CacheAttributeInfo();

		// Assert
		info.Name.ShouldBe(string.Empty);
		info.TtlSeconds.ShouldBe(0);
		info.KeyPrefix.ShouldBeNull();
		info.SlidingExpiration.ShouldBeFalse();
	}

	[Fact]
	public void CacheAttributeInfo_AllProperties_AreSettable()
	{
		// Act
		var info = new CacheAttributeInfo
		{
			Name = "test-cache",
			TtlSeconds = 300,
			KeyPrefix = "prefix:",
			SlidingExpiration = true,
		};

		// Assert
		info.Name.ShouldBe("test-cache");
		info.TtlSeconds.ShouldBe(300);
		info.KeyPrefix.ShouldBe("prefix:");
		info.SlidingExpiration.ShouldBeTrue();
	}

	[Fact]
	public void CacheAttributeInfo_NullName_Throws()
	{
		// Arrange
		var info = new CacheAttributeInfo();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => info.Name = null!);
	}

	[Fact]
	public void CacheAttributeInfo_NegativeTtl_Throws()
	{
		// Arrange
		var info = new CacheAttributeInfo();

		// Act & Assert
		Should.Throw<ArgumentOutOfRangeException>(() => info.TtlSeconds = -1);
	}

	[Fact]
	public void CacheAttributeInfo_ZeroTtl_IsAllowed()
	{
		// Arrange
		var info = new CacheAttributeInfo();

		// Act
		info.TtlSeconds = 0;

		// Assert
		info.TtlSeconds.ShouldBe(0);
	}

	// --- CacheableInfo ---

	[Fact]
	public void CacheableInfo_DefaultValues_AreCorrect()
	{
		// Act
		var info = new CacheableInfo();

		// Assert
		info.TypeName.ShouldBe(string.Empty);
		info.IsCacheable.ShouldBeFalse();
		info.Attributes.ShouldNotBeNull();
		info.Attributes.ShouldBeEmpty();
	}

	[Fact]
	public void CacheableInfo_AllProperties_AreSettable()
	{
		// Arrange
		var attributes = new[] { new CacheAttributeInfo { Name = "attr1" } };

		// Act
		var info = new CacheableInfo
		{
			TypeName = "MyType",
			IsCacheable = true,
			Attributes = attributes,
		};

		// Assert
		info.TypeName.ShouldBe("MyType");
		info.IsCacheable.ShouldBeTrue();
		info.Attributes.Length.ShouldBe(1);
		info.Attributes[0].Name.ShouldBe("attr1");
	}

	[Fact]
	public void CacheableInfo_NullTypeName_Throws()
	{
		// Arrange
		var info = new CacheableInfo();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => info.TypeName = null!);
	}

	[Fact]
	public void CacheableInfo_NullAttributes_Throws()
	{
		// Arrange
		var info = new CacheableInfo();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => info.Attributes = null!);
	}

	// --- MessageTypeMetadata ---

	[Fact]
	public void MessageTypeMetadata_Constructor_WithNullType_Throws()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new MessageTypeMetadata(null!));
	}

	[Fact]
	public void MessageTypeMetadata_Constructor_SetsBasicProperties()
	{
		// Act
		var metadata = new MessageTypeMetadata(typeof(string));

		// Assert
		metadata.Type.ShouldBe(typeof(string));
		metadata.FullName.ShouldBe("System.String");
		metadata.SimpleName.ShouldBe("String");
		metadata.AssemblyQualifiedName.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public void MessageTypeMetadata_PlainType_IsNotEventOrCommand()
	{
		// Act
		var metadata = new MessageTypeMetadata(typeof(string));

		// Assert
		metadata.IsEvent.ShouldBeFalse();
		metadata.IsCommand.ShouldBeFalse();
		metadata.IsDocument.ShouldBeFalse();
		metadata.IsProjection.ShouldBeFalse();
		metadata.RoutingHint.ShouldBe("default");
	}

	[Fact]
	public void MessageTypeMetadata_TypeHashCode_MatchesTypeGetHashCode()
	{
		// Arrange
		var type = typeof(int);

		// Act
		var metadata = new MessageTypeMetadata(type);

		// Assert
		metadata.TypeHashCode.ShouldBe(type.GetHashCode());
	}
}
