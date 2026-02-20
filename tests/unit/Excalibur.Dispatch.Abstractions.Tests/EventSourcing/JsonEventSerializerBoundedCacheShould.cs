// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Excalibur.Dispatch.Abstractions.Tests.EventSourcing;

/// <summary>
/// Tests verifying JsonEventSerializer type cache is bounded at 1024 entries (S543.7).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class JsonEventSerializerBoundedCacheShould : UnitTestBase
{
	[Fact]
	[RequiresDynamicCode("Test requires dynamic code")]
	public void HaveMaxTypeCacheSizeConstant()
	{
		// Arrange — verify the constant exists
		var field = typeof(JsonEventSerializer)
			.GetField("MaxTypeCacheSize", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy);

		// Assert
		field.ShouldNotBeNull("JsonEventSerializer should have MaxTypeCacheSize constant");
		var value = field.GetValue(null);
		value.ShouldBe(1024);
	}

	[Fact]
	[RequiresDynamicCode("Test requires dynamic code")]
	public void UseConcurrentDictionaryForTypeCache()
	{
		// Arrange
		var field = typeof(JsonEventSerializer)
			.GetField("_typeCache", BindingFlags.NonPublic | BindingFlags.Instance);

		// Assert
		field.ShouldNotBeNull("JsonEventSerializer should have _typeCache field");
		field.FieldType.ShouldBe(typeof(ConcurrentDictionary<string, Type>));
	}

	[Fact]
	[RequiresDynamicCode("Test requires dynamic code")]
	public void ResolveType_CachesWithinCapacity()
	{
		// Arrange
		var serializer = new JsonEventSerializer();
		var typeName = typeof(string).AssemblyQualifiedName;

		// Act — resolve the same type twice
		var result1 = serializer.ResolveType(typeName);
		var result2 = serializer.ResolveType(typeName);

		// Assert — both should return the same type (cached)
		result1.ShouldBe(typeof(string));
		result2.ShouldBe(typeof(string));
	}

	[Fact]
	[RequiresDynamicCode("Test requires dynamic code")]
	public void ResolveType_StillWorksWhenCacheIsFull()
	{
		// Arrange
		var serializer = new JsonEventSerializer();

		// Fill the cache to capacity by using reflection to pre-populate
		var cacheField = typeof(JsonEventSerializer)
			.GetField("_typeCache", BindingFlags.NonPublic | BindingFlags.Instance)!;
		var cache = (ConcurrentDictionary<string, Type>)cacheField.GetValue(serializer)!;

		// Fill with dummy entries to reach MaxTypeCacheSize
		for (var i = 0; i < 1024; i++)
		{
			cache.TryAdd($"DummyType_{i}", typeof(object));
		}

		cache.Count.ShouldBe(1024);

		// Act — resolve a real type when cache is full (should still work, just not cache)
		var typeName = typeof(int).AssemblyQualifiedName!;
		var result = serializer.ResolveType(typeName);

		// Assert — type resolved correctly even though cache is full
		result.ShouldBe(typeof(int));
		// The new entry should NOT have been added since cache is at capacity
		cache.Count.ShouldBe(1024);
		cache.ContainsKey(typeName).ShouldBeFalse();
	}

	[Fact]
	[RequiresDynamicCode("Test requires dynamic code")]
	public void ResolveType_DoesNotExceedCapacity()
	{
		// Arrange
		var serializer = new JsonEventSerializer();
		var cacheField = typeof(JsonEventSerializer)
			.GetField("_typeCache", BindingFlags.NonPublic | BindingFlags.Instance)!;
		var cache = (ConcurrentDictionary<string, Type>)cacheField.GetValue(serializer)!;

		// Fill cache to 1023 (one below capacity)
		for (var i = 0; i < 1023; i++)
		{
			cache.TryAdd($"DummyType_{i}", typeof(object));
		}

		// Act — resolve one more type (should cache since under limit)
		var typeName = typeof(string).AssemblyQualifiedName!;
		serializer.ResolveType(typeName);

		// Assert — at capacity now
		cache.Count.ShouldBe(1024);
		cache.ContainsKey(typeName).ShouldBeTrue();

		// Act — resolve another type (should NOT cache since at limit)
		var typeName2 = typeof(int).AssemblyQualifiedName!;
		serializer.ResolveType(typeName2);

		// Assert — still at capacity, new type NOT cached
		cache.Count.ShouldBe(1024);
		cache.ContainsKey(typeName2).ShouldBeFalse();
	}
}
