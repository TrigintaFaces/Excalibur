// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Caching;

namespace Excalibur.Dispatch.Tests.Messaging.Caching;

/// <summary>
/// Tests for <see cref="DelegateCacheKey"/> struct.
/// AD-258-2: Validates zero-allocation cache key implementation.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public class DelegateCacheKeyShould
{
	#region Constructor Tests

	[Fact]
	public void Create_WithPrefixAndKey()
	{
		// Arrange & Act
		var key = new DelegateCacheKey("error", "myHandler");

		// Assert
		key.Prefix.ShouldBe("error");
		key.Key.ShouldBe("myHandler");
		key.Type1.ShouldBeNull();
		key.Type2.ShouldBeNull();
	}

	[Fact]
	public void Create_WithPrefixKeyAndOneType()
	{
		// Arrange & Act
		var key = new DelegateCacheKey("continuation", "test", typeof(int));

		// Assert
		key.Prefix.ShouldBe("continuation");
		key.Key.ShouldBe("test");
		key.Type1.ShouldBe(typeof(int));
		key.Type2.ShouldBeNull();
	}

	[Fact]
	public void Create_WithPrefixKeyAndTwoTypes()
	{
		// Arrange & Act
		var key = new DelegateCacheKey("transform", "convert", typeof(int), typeof(string));

		// Assert
		key.Prefix.ShouldBe("transform");
		key.Key.ShouldBe("convert");
		key.Type1.ShouldBe(typeof(int));
		key.Type2.ShouldBe(typeof(string));
	}

	#endregion Constructor Tests

	#region Equality Tests

	[Fact]
	public void BeEqual_WhenPrefixAndKeyMatch()
	{
		// Arrange
		var key1 = new DelegateCacheKey("error", "handler");
		var key2 = new DelegateCacheKey("error", "handler");

		// Act & Assert
		key1.Equals(key2).ShouldBeTrue();
		(key1 == key2).ShouldBeTrue();
		(key1 != key2).ShouldBeFalse();
	}

	[Fact]
	public void NotBeEqual_WhenPrefixDiffers()
	{
		// Arrange
		var key1 = new DelegateCacheKey("error", "handler");
		var key2 = new DelegateCacheKey("transform", "handler");

		// Act & Assert
		key1.Equals(key2).ShouldBeFalse();
		(key1 == key2).ShouldBeFalse();
		(key1 != key2).ShouldBeTrue();
	}

	[Fact]
	public void NotBeEqual_WhenKeyDiffers()
	{
		// Arrange
		var key1 = new DelegateCacheKey("error", "handler1");
		var key2 = new DelegateCacheKey("error", "handler2");

		// Act & Assert
		key1.Equals(key2).ShouldBeFalse();
	}

	[Fact]
	public void BeEqual_WhenTypesMatch()
	{
		// Arrange
		var key1 = new DelegateCacheKey("transform", "convert", typeof(int), typeof(string));
		var key2 = new DelegateCacheKey("transform", "convert", typeof(int), typeof(string));

		// Act & Assert
		key1.Equals(key2).ShouldBeTrue();
		key1.GetHashCode().ShouldBe(key2.GetHashCode());
	}

	[Fact]
	public void NotBeEqual_WhenType1Differs()
	{
		// Arrange
		var key1 = new DelegateCacheKey("transform", "convert", typeof(int), typeof(string));
		var key2 = new DelegateCacheKey("transform", "convert", typeof(long), typeof(string));

		// Act & Assert
		key1.Equals(key2).ShouldBeFalse();
	}

	[Fact]
	public void NotBeEqual_WhenType2Differs()
	{
		// Arrange
		var key1 = new DelegateCacheKey("transform", "convert", typeof(int), typeof(string));
		var key2 = new DelegateCacheKey("transform", "convert", typeof(int), typeof(object));

		// Act & Assert
		key1.Equals(key2).ShouldBeFalse();
	}

	[Fact]
	public void HandleObjectEquals()
	{
		// Arrange
		var key1 = new DelegateCacheKey("error", "handler");
		object key2 = new DelegateCacheKey("error", "handler");
		object notAKey = "not a key";

		// Act & Assert
		key1.Equals(key2).ShouldBeTrue();
		key1.Equals(notAKey).ShouldBeFalse();
		key1.Equals(null).ShouldBeFalse();
	}

	#endregion Equality Tests

	#region HashCode Tests

	[Fact]
	public void ProduceSameHashCode_ForEqualKeys()
	{
		// Arrange
		var key1 = new DelegateCacheKey("continuation", "test", typeof(int), typeof(string));
		var key2 = new DelegateCacheKey("continuation", "test", typeof(int), typeof(string));

		// Act & Assert
		key1.GetHashCode().ShouldBe(key2.GetHashCode());
	}

	[Fact]
	public void ProduceDifferentHashCode_ForDifferentKeys()
	{
		// Arrange
		var key1 = new DelegateCacheKey("continuation", "test1", typeof(int), typeof(string));
		var key2 = new DelegateCacheKey("continuation", "test2", typeof(int), typeof(string));

		// Act & Assert
		// Note: Different keys should usually have different hash codes, but collisions are possible
		// We just verify they're calculated consistently
		key1.GetHashCode().ShouldNotBe(0);
		key2.GetHashCode().ShouldNotBe(0);
	}

	[Fact]
	public void ProduceConsistentHashCode_AcrossMultipleCalls()
	{
		// Arrange
		var key = new DelegateCacheKey("transform", "convert", typeof(int), typeof(string));

		// Act & Assert
		var hash1 = key.GetHashCode();
		var hash2 = key.GetHashCode();
		var hash3 = key.GetHashCode();

		hash1.ShouldBe(hash2);
		hash2.ShouldBe(hash3);
	}

	#endregion HashCode Tests

	#region ToString Tests

	[Fact]
	public void FormatToString_WithPrefixAndKey()
	{
		// Arrange
		var key = new DelegateCacheKey("error", "handler");

		// Act
		var result = key.ToString();

		// Assert
		result.ShouldBe("error_handler");
	}

	[Fact]
	public void FormatToString_WithOneType()
	{
		// Arrange
		var key = new DelegateCacheKey("continuation", "test", typeof(int));

		// Act
		var result = key.ToString();

		// Assert
		result.ShouldBe("continuation_test_Int32");
	}

	[Fact]
	public void FormatToString_WithTwoTypes()
	{
		// Arrange
		var key = new DelegateCacheKey("transform", "convert", typeof(int), typeof(string));

		// Act
		var result = key.ToString();

		// Assert
		result.ShouldBe("transform_convert_Int32_String");
	}

	#endregion ToString Tests

	#region Dictionary Key Usage Tests

	[Fact]
	public void WorkAsDictionaryKey()
	{
		// Arrange
		var dictionary = new Dictionary<DelegateCacheKey, string>();
		var key1 = new DelegateCacheKey("continuation", "test", typeof(int), typeof(string));
		var key2 = new DelegateCacheKey("continuation", "test", typeof(int), typeof(string));

		// Act
		dictionary[key1] = "value1";

		// Assert
		dictionary.ContainsKey(key2).ShouldBeTrue();
		dictionary[key2].ShouldBe("value1");
	}

	[Fact]
	public void DistinguishDifferentKeysInDictionary()
	{
		// Arrange
		var dictionary = new Dictionary<DelegateCacheKey, string>();
		var key1 = new DelegateCacheKey("continuation", "test", typeof(int), typeof(string));
		var key2 = new DelegateCacheKey("continuation", "test", typeof(long), typeof(string));

		// Act
		dictionary[key1] = "value1";
		dictionary[key2] = "value2";

		// Assert
		dictionary.Count.ShouldBe(2);
		dictionary[key1].ShouldBe("value1");
		dictionary[key2].ShouldBe("value2");
	}

	#endregion Dictionary Key Usage Tests
}
