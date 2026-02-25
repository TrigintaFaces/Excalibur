// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Pooling;

namespace Excalibur.Dispatch.Tests.Messaging.Pooling;

/// <summary>
/// Unit tests for <see cref="PooledMap{TKey, TValue}"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Pooling")]
[Trait("Priority", "0")]
public sealed class PooledMapShould
{
	#region Constructor Tests

	[Fact]
	public void Constructor_CreatesDictionaryFromPool()
	{
		// Arrange
		var pool = new DictionaryPool<string, int>();

		// Act
		using var pooled = pool.CreatePooled();

		// Assert
		_ = pooled.Dictionary.ShouldNotBeNull();
	}

	#endregion

	#region Dictionary Property Tests

	[Fact]
	public void Dictionary_AllowsReadAndWrite()
	{
		// Arrange
		var pool = new DictionaryPool<string, int>();
		using var pooled = pool.CreatePooled();

		// Act
		pooled.Dictionary["key"] = 42;

		// Assert
		pooled.Dictionary["key"].ShouldBe(42);
	}

	[Fact]
	public void Dictionary_SupportsMultipleEntries()
	{
		// Arrange
		var pool = new DictionaryPool<string, string>();
		using var pooled = pool.CreatePooled();

		// Act
		pooled.Dictionary["a"] = "alpha";
		pooled.Dictionary["b"] = "beta";
		pooled.Dictionary["c"] = "gamma";

		// Assert
		pooled.Dictionary.Count.ShouldBe(3);
		pooled.Dictionary["a"].ShouldBe("alpha");
		pooled.Dictionary["b"].ShouldBe("beta");
		pooled.Dictionary["c"].ShouldBe("gamma");
	}

	#endregion

	#region Dispose Tests

	[Fact]
	public void Dispose_ReturnsDictionaryToPool()
	{
		// Arrange
		var pool = new DictionaryPool<string, int>(maxPoolSize: 10);
		Dictionary<string, int>? capturedDictionary;

		// Act
		using (var pooled = pool.CreatePooled())
		{
			capturedDictionary = pooled.Dictionary;
			pooled.Dictionary["test"] = 123;
		}

		// Assert - Same instance should be returned
		var rentedBack = pool.Rent();
		rentedBack.ShouldBeSameAs(capturedDictionary);
	}

	[Fact]
	public void Dispose_ClearsDictionary()
	{
		// Arrange
		var pool = new DictionaryPool<string, int>(maxPoolSize: 10);

		// Act
		using (var pooled = pool.CreatePooled())
		{
			pooled.Dictionary["key1"] = 1;
			pooled.Dictionary["key2"] = 2;
		}

		// Assert - Rented dictionary should be empty
		var rentedBack = pool.Rent();
		rentedBack.ShouldBeEmpty();
	}

	[Fact]
	public void Dispose_CanBeCalledMultipleTimes()
	{
		// Arrange
		var pool = new DictionaryPool<string, int>();
		var pooled = pool.CreatePooled();

		// Act & Assert - Should not throw
		Should.NotThrow(() =>
		{
			pooled.Dispose();
			pooled.Dispose();
			pooled.Dispose();
		});
	}

	#endregion

	#region Equality Tests

	[Fact]
	public void Equals_WithSameDictionaryAndPool_ReturnsTrue()
	{
		// Arrange
		var pool = new DictionaryPool<string, int>();
		var pooled = pool.CreatePooled();

		// Act & Assert
		pooled.Equals(pooled).ShouldBeTrue();
	}

	[Fact]
	public void Equals_WithDifferentInstances_ReturnsFalse()
	{
		// Arrange
		var pool = new DictionaryPool<string, int>();
		using var pooled1 = pool.CreatePooled();
		using var pooled2 = pool.CreatePooled();

		// Act & Assert
		pooled1.Equals(pooled2).ShouldBeFalse();
	}

	[Fact]
	public void Equals_WithObject_WhenSame_ReturnsTrue()
	{
		// Arrange
		var pool = new DictionaryPool<string, int>();
		var pooled = pool.CreatePooled();
		object boxed = pooled;

		// Act & Assert
		pooled.Equals(boxed).ShouldBeTrue();
	}

	[Fact]
	public void Equals_WithNull_ReturnsFalse()
	{
		// Arrange
		var pool = new DictionaryPool<string, int>();
		using var pooled = pool.CreatePooled();

		// Act & Assert
		pooled.Equals(null).ShouldBeFalse();
	}

	[Fact]
	public void Equals_WithDifferentType_ReturnsFalse()
	{
		// Arrange
		var pool = new DictionaryPool<string, int>();
		using var pooled = pool.CreatePooled();

		// Act & Assert
		pooled.Equals("not a pooled map").ShouldBeFalse();
	}

	#endregion

	#region Operator Tests

	[Fact]
	public void EqualityOperator_WithSameInstance_ReturnsTrue()
	{
		// Arrange
		var pool = new DictionaryPool<string, int>();
		var pooled = pool.CreatePooled();

		// Act & Assert
#pragma warning disable CS1718 // Comparison made to same variable
		(pooled == pooled).ShouldBeTrue();
#pragma warning restore CS1718
	}

	[Fact]
	public void EqualityOperator_WithDifferentInstances_ReturnsFalse()
	{
		// Arrange
		var pool = new DictionaryPool<string, int>();
		using var pooled1 = pool.CreatePooled();
		using var pooled2 = pool.CreatePooled();

		// Act & Assert
		(pooled1 == pooled2).ShouldBeFalse();
	}

	[Fact]
	public void InequalityOperator_WithDifferentInstances_ReturnsTrue()
	{
		// Arrange
		var pool = new DictionaryPool<string, int>();
		using var pooled1 = pool.CreatePooled();
		using var pooled2 = pool.CreatePooled();

		// Act & Assert
		(pooled1 != pooled2).ShouldBeTrue();
	}

	[Fact]
	public void InequalityOperator_WithSameInstance_ReturnsFalse()
	{
		// Arrange
		var pool = new DictionaryPool<string, int>();
		var pooled = pool.CreatePooled();

		// Act & Assert
#pragma warning disable CS1718 // Comparison made to same variable
		(pooled != pooled).ShouldBeFalse();
#pragma warning restore CS1718
	}

	#endregion

	#region GetHashCode Tests

	[Fact]
	public void GetHashCode_ReturnsSameValueForSameInstance()
	{
		// Arrange
		var pool = new DictionaryPool<string, int>();
		using var pooled = pool.CreatePooled();

		// Act
		var hash1 = pooled.GetHashCode();
		var hash2 = pooled.GetHashCode();

		// Assert
		hash1.ShouldBe(hash2);
	}

	[Fact]
	public void GetHashCode_ReturnsDifferentValueForDifferentInstances()
	{
		// Arrange
		var pool = new DictionaryPool<string, int>();
		using var pooled1 = pool.CreatePooled();
		using var pooled2 = pool.CreatePooled();

		// Act & Assert - Different instances should generally have different hashes
		// (though hash collisions are technically possible)
		var hash1 = pooled1.GetHashCode();
		var hash2 = pooled2.GetHashCode();

		// They should be different since they have different dictionary references
		(hash1 == hash2).ShouldBeFalse();
	}

	#endregion

	#region Usage Pattern Tests

	[Fact]
	public void UsingStatement_ProperlyDisposes()
	{
		// Arrange
		var pool = new DictionaryPool<int, string>(maxPoolSize: 10);
		Dictionary<int, string>? capturedDict;

		// Act
		using (var map = pool.CreatePooled())
		{
			capturedDict = map.Dictionary;
			map.Dictionary[1] = "one";
			map.Dictionary[2] = "two";
		}

		// Assert - Dictionary should be returned to pool
		var rentedBack = pool.Rent();
		rentedBack.ShouldBeSameAs(capturedDict);
		rentedBack.ShouldBeEmpty();
	}

	[Fact]
	public void NestedUsing_WorksCorrectly()
	{
		// Arrange
		var pool = new DictionaryPool<string, int>(maxPoolSize: 10);

		// Act & Assert - Nested using should work without issues
		using (var outer = pool.CreatePooled())
		{
			outer.Dictionary["outer"] = 1;

			using (var inner = pool.CreatePooled())
			{
				inner.Dictionary["inner"] = 2;

				// Both should be independent
				outer.Dictionary.ShouldNotBeSameAs(inner.Dictionary);
			}
		}
	}

	#endregion
}
