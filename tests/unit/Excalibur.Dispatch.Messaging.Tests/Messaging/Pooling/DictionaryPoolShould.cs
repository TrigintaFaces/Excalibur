// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Pooling;

namespace Excalibur.Dispatch.Tests.Messaging.Pooling;

/// <summary>
/// Unit tests for <see cref="DictionaryPool{TKey, TValue}"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Pooling")]
[Trait("Priority", "0")]
public sealed class DictionaryPoolShould
{
	#region Constructor Tests

	[Fact]
	public void Constructor_WithDefaults_CreatesPool()
	{
		// Act
		var pool = new DictionaryPool<string, int>();

		// Assert
		_ = pool.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_WithCustomParameters_CreatesPool()
	{
		// Act
		var pool = new DictionaryPool<string, int>(maxPoolSize: 32, initialCapacity: 8);

		// Assert
		_ = pool.ShouldNotBeNull();
	}

	#endregion

	#region Rent Tests

	[Fact]
	public void Rent_WhenPoolEmpty_ReturnsNewDictionary()
	{
		// Arrange
		var pool = new DictionaryPool<string, int>();

		// Act
		var dictionary = pool.Rent();

		// Assert
		_ = dictionary.ShouldNotBeNull();
		dictionary.ShouldBeEmpty();
	}

	[Fact]
	public void Rent_MultipleTimes_ReturnsDistinctInstances()
	{
		// Arrange
		var pool = new DictionaryPool<string, int>();

		// Act
		var dict1 = pool.Rent();
		var dict2 = pool.Rent();

		// Assert
		dict1.ShouldNotBeSameAs(dict2);
	}

	[Fact]
	public void Rent_AfterReturn_ReturnsSameInstance()
	{
		// Arrange
		var pool = new DictionaryPool<string, int>(maxPoolSize: 10);
		var original = pool.Rent();
		pool.Return(original);

		// Act
		var rented = pool.Rent();

		// Assert
		rented.ShouldBeSameAs(original);
	}

	#endregion

	#region Return Tests

	[Fact]
	public void Return_WithNull_DoesNotThrow()
	{
		// Arrange
		var pool = new DictionaryPool<string, int>();

		// Act & Assert
		Should.NotThrow(() => pool.Return(null));
	}

	[Fact]
	public void Return_ClearsDictionaryContents()
	{
		// Arrange
		var pool = new DictionaryPool<string, int>(maxPoolSize: 10);
		var dictionary = pool.Rent();
		dictionary["key1"] = 1;
		dictionary["key2"] = 2;
		pool.Return(dictionary);

		// Act
		var rented = pool.Rent();

		// Assert
		rented.ShouldBeEmpty();
	}

	[Fact]
	public void Return_WhenPoolFull_DoesNotAddToPool()
	{
		// Arrange - Pool with size 1
		var pool = new DictionaryPool<string, int>(maxPoolSize: 1);
		var dict1 = pool.Rent();
		var dict2 = pool.Rent();

		// Return first dictionary - pool now has 1 item
		pool.Return(dict1);

		// Return second dictionary - pool is full, should be discarded
		pool.Return(dict2);

		// Act - Rent should get dict1 back
		var rented = pool.Rent();

		// Assert
		rented.ShouldBeSameAs(dict1);
	}

	#endregion

	#region CreatePooled Tests

	[Fact]
	public void CreatePooled_ReturnsPooledMap()
	{
		// Arrange
		var pool = new DictionaryPool<string, int>();

		// Act
		using var pooled = pool.CreatePooled();

		// Assert
		_ = pooled.Dictionary.ShouldNotBeNull();
	}

	[Fact]
	public void CreatePooled_ReturnsToDictionaryPoolOnDispose()
	{
		// Arrange
		var pool = new DictionaryPool<string, int>(maxPoolSize: 10);
		Dictionary<string, int>? inner;

		// Act
		using (var pooled = pool.CreatePooled())
		{
			inner = pooled.Dictionary;
			inner["test"] = 42;
		}

		// Assert - The dictionary should be returned to pool and cleared
		var rented = pool.Rent();
		rented.ShouldBeSameAs(inner);
		rented.ShouldBeEmpty();
	}

	#endregion

	#region Type Constraint Tests

	[Fact]
	public void DictionaryPool_WorksWithReferenceTypeKeys()
	{
		// Arrange
		var pool = new DictionaryPool<string, object>();

		// Act
		var dictionary = pool.Rent();
		dictionary["key"] = new object();

		// Assert
		dictionary.ShouldContainKey("key");
		pool.Return(dictionary);
	}

	[Fact]
	public void DictionaryPool_WorksWithValueTypeKeys()
	{
		// Arrange
		var pool = new DictionaryPool<int, string>();

		// Act
		var dictionary = pool.Rent();
		dictionary[42] = "value";

		// Assert
		dictionary.ShouldContainKey(42);
		pool.Return(dictionary);
	}

	[Fact]
	public void DictionaryPool_WorksWithGuidKeys()
	{
		// Arrange
		var pool = new DictionaryPool<Guid, string>();
		var key = Guid.NewGuid();

		// Act
		var dictionary = pool.Rent();
		dictionary[key] = "value";

		// Assert
		dictionary.ShouldContainKey(key);
		pool.Return(dictionary);
	}

	#endregion

	#region Thread Safety Tests

	[Fact]
	public async Task Rent_AndReturn_ThreadSafe()
	{
		// Arrange
		var pool = new DictionaryPool<int, int>(maxPoolSize: 100);
		var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();

		// Act - Concurrent rent/return operations
		var tasks = Enumerable.Range(0, 10).Select(_ => Task.Run(() =>
		{
			try
			{
				for (var i = 0; i < 100; i++)
				{
					var dict = pool.Rent();
					dict[i] = i;
					pool.Return(dict);
				}
			}
			catch (Exception ex)
			{
				exceptions.Add(ex);
			}
		}));

		await Task.WhenAll(tasks);

		// Assert
		exceptions.ShouldBeEmpty();
	}

	#endregion
}
