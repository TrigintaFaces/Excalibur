// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Serialization;

namespace Excalibur.Dispatch.Abstractions.Tests.Serialization;

/// <summary>
/// Unit tests for <see cref="PropertyDictionary"/>.
/// </summary>
/// <remarks>
/// Tests the dictionary adapter for nullable value support.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Serialization")]
[Trait("Priority", "0")]
public sealed class PropertyDictionaryShould : UnitTestBase
{
	#region Constructor Tests

	[Fact]
	public void Constructor_WithEmptyDictionary_CreatesEmptyPropertyDictionary()
	{
		// Arrange
		var inner = new Dictionary<string, object>();

		// Act
		var propertyDict = new PropertyDictionary(inner);

		// Assert
		propertyDict.Count.ShouldBe(0);
	}

	[Fact]
	public void Constructor_WithPopulatedDictionary_PreservesEntries()
	{
		// Arrange
		var inner = new Dictionary<string, object>
		{
			["key1"] = "value1",
			["key2"] = 42,
		};

		// Act
		var propertyDict = new PropertyDictionary(inner);

		// Assert
		propertyDict.Count.ShouldBe(2);
	}

	#endregion

	#region Indexer Tests

	[Fact]
	public void Indexer_Get_ReturnsValueForExistingKey()
	{
		// Arrange
		var inner = new Dictionary<string, object> { ["key"] = "value" };
		var propertyDict = new PropertyDictionary(inner);

		// Act
		var result = propertyDict["key"];

		// Assert
		result.ShouldBe("value");
	}

	[Fact]
	public void Indexer_Get_ReturnsNullForMissingKey()
	{
		// Arrange
		var inner = new Dictionary<string, object>();
		var propertyDict = new PropertyDictionary(inner);

		// Act
		var result = propertyDict["nonexistent"];

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void Indexer_Set_AddsNewEntry()
	{
		// Arrange
		var inner = new Dictionary<string, object>();
		var propertyDict = new PropertyDictionary(inner);

		// Act
		propertyDict["key"] = "value";

		// Assert
		propertyDict["key"].ShouldBe("value");
		inner["key"].ShouldBe("value");
	}

	[Fact]
	public void Indexer_Set_UpdatesExistingEntry()
	{
		// Arrange
		var inner = new Dictionary<string, object> { ["key"] = "old" };
		var propertyDict = new PropertyDictionary(inner);

		// Act
		propertyDict["key"] = "new";

		// Assert
		propertyDict["key"].ShouldBe("new");
	}

	[Fact]
	public void Indexer_Set_WithNull_RemovesEntry()
	{
		// Arrange
		var inner = new Dictionary<string, object> { ["key"] = "value" };
		var propertyDict = new PropertyDictionary(inner);

		// Act
		propertyDict["key"] = null;

		// Assert
		propertyDict["key"].ShouldBeNull();
		inner.ContainsKey("key").ShouldBeFalse();
	}

	#endregion

	#region Add Method Tests

	[Fact]
	public void Add_WithKeyValue_AddsEntry()
	{
		// Arrange
		var inner = new Dictionary<string, object>();
		var propertyDict = new PropertyDictionary(inner);

		// Act
		propertyDict.Add("key", "value");

		// Assert
		propertyDict["key"].ShouldBe("value");
	}

	[Fact]
	public void Add_WithKeyValuePair_AddsEntry()
	{
		// Arrange
		var inner = new Dictionary<string, object>();
		var propertyDict = new PropertyDictionary(inner);

		// Act
		propertyDict.Add(new KeyValuePair<string, object?>("key", "value"));

		// Assert
		propertyDict["key"].ShouldBe("value");
	}

	[Fact]
	public void Add_WithNullValue_RemovesExistingKey()
	{
		// Arrange
		var inner = new Dictionary<string, object> { ["key"] = "value" };
		var propertyDict = new PropertyDictionary(inner);

		// Act
		propertyDict.Add("key", null);

		// Assert
		propertyDict.ContainsKey("key").ShouldBeFalse();
		inner.ContainsKey("key").ShouldBeFalse();
	}

	[Fact]
	public void Add_WithNullValue_ForNonExistingKey_DoesNotThrow()
	{
		// Arrange
		var inner = new Dictionary<string, object>();
		var propertyDict = new PropertyDictionary(inner);

		// Act & Assert - should not throw
		propertyDict.Add("nonexistent", null);
		propertyDict.Count.ShouldBe(0);
	}

	[Fact]
	public void Add_WithKeyValuePair_WithNullValue_RemovesExistingKey()
	{
		// Arrange
		var inner = new Dictionary<string, object> { ["key"] = "value" };
		var propertyDict = new PropertyDictionary(inner);

		// Act
		propertyDict.Add(new KeyValuePair<string, object?>("key", null));

		// Assert
		propertyDict.ContainsKey("key").ShouldBeFalse();
		inner.ContainsKey("key").ShouldBeFalse();
	}

	[Fact]
	public void Add_WithKeyValuePair_WithNullValue_ForNonExistingKey_DoesNotThrow()
	{
		// Arrange
		var inner = new Dictionary<string, object>();
		var propertyDict = new PropertyDictionary(inner);

		// Act & Assert - should not throw
		propertyDict.Add(new KeyValuePair<string, object?>("nonexistent", null));
		propertyDict.Count.ShouldBe(0);
	}

	#endregion

	#region Remove Method Tests

	[Fact]
	public void Remove_WithExistingKey_ReturnsTrue()
	{
		// Arrange
		var inner = new Dictionary<string, object> { ["key"] = "value" };
		var propertyDict = new PropertyDictionary(inner);

		// Act
		var result = propertyDict.Remove("key");

		// Assert
		result.ShouldBeTrue();
		propertyDict.ContainsKey("key").ShouldBeFalse();
	}

	[Fact]
	public void Remove_WithNonExistingKey_ReturnsFalse()
	{
		// Arrange
		var inner = new Dictionary<string, object>();
		var propertyDict = new PropertyDictionary(inner);

		// Act
		var result = propertyDict.Remove("nonexistent");

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void Remove_WithKeyValuePair_ReturnsTrue_WhenMatches()
	{
		// Arrange
		var inner = new Dictionary<string, object> { ["key"] = "value" };
		var propertyDict = new PropertyDictionary(inner);

		// Act
		var result = propertyDict.Remove(new KeyValuePair<string, object?>("key", "value"));

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void Remove_WithKeyValuePair_ReturnsFalse_WhenValueDoesNotMatch()
	{
		// Arrange
		var inner = new Dictionary<string, object> { ["key"] = "value" };
		var propertyDict = new PropertyDictionary(inner);

		// Act
		var result = propertyDict.Remove(new KeyValuePair<string, object?>("key", "different"));

		// Assert
		result.ShouldBeFalse();
	}

	#endregion

	#region ContainsKey Tests

	[Fact]
	public void ContainsKey_WithExistingKey_ReturnsTrue()
	{
		// Arrange
		var inner = new Dictionary<string, object> { ["key"] = "value" };
		var propertyDict = new PropertyDictionary(inner);

		// Act & Assert
		propertyDict.ContainsKey("key").ShouldBeTrue();
	}

	[Fact]
	public void ContainsKey_WithNonExistingKey_ReturnsFalse()
	{
		// Arrange
		var inner = new Dictionary<string, object>();
		var propertyDict = new PropertyDictionary(inner);

		// Act & Assert
		propertyDict.ContainsKey("key").ShouldBeFalse();
	}

	#endregion

	#region Contains Tests

	[Fact]
	public void Contains_WithMatchingKeyValuePair_ReturnsTrue()
	{
		// Arrange
		var inner = new Dictionary<string, object> { ["key"] = "value" };
		var propertyDict = new PropertyDictionary(inner);

		// Act & Assert
		propertyDict.Contains(new KeyValuePair<string, object?>("key", "value")).ShouldBeTrue();
	}

	[Fact]
	public void Contains_WithNonMatchingValue_ReturnsFalse()
	{
		// Arrange
		var inner = new Dictionary<string, object> { ["key"] = "value" };
		var propertyDict = new PropertyDictionary(inner);

		// Act & Assert
		propertyDict.Contains(new KeyValuePair<string, object?>("key", "different")).ShouldBeFalse();
	}

	[Fact]
	public void Contains_WithNonExistingKey_ReturnsFalse()
	{
		// Arrange
		var inner = new Dictionary<string, object>();
		var propertyDict = new PropertyDictionary(inner);

		// Act & Assert
		propertyDict.Contains(new KeyValuePair<string, object?>("key", "value")).ShouldBeFalse();
	}

	#endregion

	#region TryGetValue Tests

	[Fact]
	public void TryGetValue_WithExistingKey_ReturnsTrueAndValue()
	{
		// Arrange
		var inner = new Dictionary<string, object> { ["key"] = "value" };
		var propertyDict = new PropertyDictionary(inner);

		// Act
		var result = propertyDict.TryGetValue("key", out var value);

		// Assert
		result.ShouldBeTrue();
		value.ShouldBe("value");
	}

	[Fact]
	public void TryGetValue_WithNonExistingKey_ReturnsFalseAndNull()
	{
		// Arrange
		var inner = new Dictionary<string, object>();
		var propertyDict = new PropertyDictionary(inner);

		// Act
		var result = propertyDict.TryGetValue("key", out var value);

		// Assert
		result.ShouldBeFalse();
		value.ShouldBeNull();
	}

	#endregion

	#region Clear Tests

	[Fact]
	public void Clear_RemovesAllEntries()
	{
		// Arrange
		var inner = new Dictionary<string, object>
		{
			["key1"] = "value1",
			["key2"] = "value2",
		};
		var propertyDict = new PropertyDictionary(inner);

		// Act
		propertyDict.Clear();

		// Assert
		propertyDict.Count.ShouldBe(0);
		inner.Count.ShouldBe(0);
	}

	#endregion

	#region Keys and Values Tests

	[Fact]
	public void Keys_ReturnsAllKeys()
	{
		// Arrange
		var inner = new Dictionary<string, object>
		{
			["key1"] = "value1",
			["key2"] = "value2",
		};
		var propertyDict = new PropertyDictionary(inner);

		// Act
		var keys = propertyDict.Keys;

		// Assert
		keys.Count.ShouldBe(2);
		keys.ShouldContain("key1");
		keys.ShouldContain("key2");
	}

	[Fact]
	public void Values_ReturnsAllValues()
	{
		// Arrange
		var inner = new Dictionary<string, object>
		{
			["key1"] = "value1",
			["key2"] = 42,
		};
		var propertyDict = new PropertyDictionary(inner);

		// Act
		var values = propertyDict.Values;

		// Assert
		values.Count.ShouldBe(2);
		values.ShouldContain("value1");
		values.ShouldContain(42);
	}

	#endregion

	#region IsReadOnly Tests

	[Fact]
	public void IsReadOnly_ReturnsFalse()
	{
		// Arrange
		var inner = new Dictionary<string, object>();
		var propertyDict = new PropertyDictionary(inner);

		// Assert
		propertyDict.IsReadOnly.ShouldBeFalse();
	}

	#endregion

	#region CopyTo Tests

	[Fact]
	public void CopyTo_CopiesAllEntriesToArray()
	{
		// Arrange
		var inner = new Dictionary<string, object>
		{
			["key1"] = "value1",
			["key2"] = "value2",
		};
		var propertyDict = new PropertyDictionary(inner);
		var array = new KeyValuePair<string, object?>[2];

		// Act
		propertyDict.CopyTo(array, 0);

		// Assert
		array.Length.ShouldBe(2);
	}

	[Fact]
	public void CopyTo_WithNullArray_ThrowsArgumentNullException()
	{
		// Arrange
		var inner = new Dictionary<string, object>();
		var propertyDict = new PropertyDictionary(inner);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => propertyDict.CopyTo(null!, 0));
	}

	[Fact]
	public void CopyTo_WithNegativeIndex_ThrowsArgumentOutOfRangeException()
	{
		// Arrange
		var inner = new Dictionary<string, object>();
		var propertyDict = new PropertyDictionary(inner);
		var array = new KeyValuePair<string, object?>[1];

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() => propertyDict.CopyTo(array, -1));
	}

	[Fact]
	public void CopyTo_WithInsufficientArraySize_ThrowsArgumentException()
	{
		// Arrange
		var inner = new Dictionary<string, object>
		{
			["key1"] = "value1",
			["key2"] = "value2",
		};
		var propertyDict = new PropertyDictionary(inner);
		var array = new KeyValuePair<string, object?>[1];

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => propertyDict.CopyTo(array, 0));
	}

	#endregion

	#region Enumeration Tests

	[Fact]
	public void GetEnumerator_IteratesAllEntries()
	{
		// Arrange
		var inner = new Dictionary<string, object>
		{
			["key1"] = "value1",
			["key2"] = "value2",
		};
		var propertyDict = new PropertyDictionary(inner);

		// Act
		var count = 0;
		foreach (var kvp in propertyDict)
		{
			count++;
			_ = kvp.Key.ShouldNotBeNull();
		}

		// Assert
		count.ShouldBe(2);
	}

	#endregion

	#region Interface Implementation Tests

	[Fact]
	public void ImplementsIDictionaryInterface()
	{
		// Arrange
		var inner = new Dictionary<string, object>();

		// Act
		var propertyDict = new PropertyDictionary(inner);

		// Assert
		_ = propertyDict.ShouldBeAssignableTo<IDictionary<string, object?>>();
	}

	#endregion
}
